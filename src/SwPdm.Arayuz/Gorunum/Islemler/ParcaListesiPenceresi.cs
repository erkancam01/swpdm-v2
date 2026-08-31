using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// PARCA LISTESI PENCERESI - montajin butun agaci tek tabloda, CSV'ye
/// aktarilabilir.
///
/// SUTUNLAR TURETILIYOR (CLAUDE.md 1b): sabit sutunlardan sonra, dosyalarda
/// GERCEKTEN gorulen ozel ozellikler sutun oluyor. Kodda "Malzeme" diye bir
/// satir YOK; yeni bir ozellik eklenince bu dosya degismez.
///
/// UST BILGI YALAN SOYLEMEZ (CLAUDE.md 3): kac satir, kac tanesi sorunlu ve
/// ozel deger okumanin BAYAT olabilecegi orada yaziyor. Bos ya da eksik bir
/// liste "sorun yok" diye okunamasin diye.
/// </summary>
internal static class ParcaListesiPenceresi
{
    /// <summary>Pencereyi acar.</summary>
    /// <param name="git">Bir satira gidilir: pencere kapanir, dosya agacta secilir.</param>
    internal static void Ac(
        IWin32Window sahip, string belgeAdi, ParcaListesiSonucu sonuc,
        Action<string> bildir, Action<string> git)
    {
        ArgumentNullException.ThrowIfNull(sonuc);
        ArgumentNullException.ThrowIfNull(bildir);
        ArgumentNullException.ThrowIfNull(git);

        using var pencere = new Form
        {
            Text = "Parça listesi — " + belgeAdi,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(880, 520),
            MinimumSize = new Size(560, 360),
            ShowInTaskbar = false,
            MinimizeBox = false,
        };

        var liste = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
        };

        Sutunlar(liste, sonuc);
        Satirlar(liste, sonuc);

        var baslik = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 46,
            Padding = new Padding(8, 6, 8, 6),
            Text = Ozet(sonuc) + Environment.NewLine + ParcaListesiCsv.Uyari,
            ForeColor = sonuc.Tam && sonuc.Sorunlu == 0 ? Renkler.UstBilgiYazi : Renkler.UyariYazi,
        };

        var alt = new Panel { Dock = DockStyle.Bottom, Height = 40 };
        var aktar = new Button
        {
            Text = "CSV'ye aktar",
            Width = 130,
            Height = 30,
            Left = 8,
            Top = 5,
        };
        var kapat = new Button
        {
            Text = "Kapat",
            Width = 100,
            Height = 30,
            Top = 5,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            DialogResult = DialogResult.OK,
        };
        kapat.Left = alt.ClientSize.Width - kapat.Width - 8;

        aktar.Click += (_, _) => Aktar(pencere, belgeAdi, sonuc, bildir);

        void Git()
        {
            if (liste.SelectedItems.Count == 0 || liste.SelectedItems[0].Tag is not string yol)
            {
                return;
            }

            // Pencere KAPANIR: modal pencerenin arkasindaki secim gorunmez.
            pencere.DialogResult = DialogResult.OK;
            pencere.Close();
            git(yol);
        }

        liste.MouseDoubleClick += (_, _) => Git();
        liste.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Git();
            }
        };

        alt.Controls.Add(aktar);
        alt.Controls.Add(kapat);
        pencere.Controls.Add(liste);
        pencere.Controls.Add(alt);
        pencere.Controls.Add(baslik);
        pencere.AcceptButton = kapat;
        pencere.CancelButton = kapat;

        pencere.ShowDialog(sahip);
    }

    private static void Sutunlar(ListView liste, ParcaListesiSonucu sonuc)
    {
        liste.Columns.Add("Ad", 240);
        liste.Columns.Add("Tür", 90);
        liste.Columns.Add("Kaç yerde", 70, HorizontalAlignment.Right);
        liste.Columns.Add("Yapılandırma", 110);

        foreach (string sutun in sonuc.OzelSutunlar)
        {
            liste.Columns.Add(sutun, 100);
        }

        liste.Columns.Add("Durum", 220);
        liste.Columns.Add("Yol", 320);
    }

    private static void Satirlar(ListView liste, ParcaListesiSonucu sonuc)
    {
        foreach (ParcaSatiri satir in sonuc.Satirlar)
        {
            // GIRINTI METINDE: ListView'in Details gorunumu agac cizmiyor,
            // seviyeyi gosteren tek sey bu bosluk. Bosluk yerine bir isaret
            // koymak dar sutunda kirpildiginda seviyeyi tamamen yok ederdi.
            var oge = new ListViewItem(new string(' ', satir.Seviye * 4) + satir.Ad)
            {
                Tag = satir.Bulundu ? satir.Yol : null,
                ForeColor = satir.Durum is null ? liste.ForeColor : Renkler.YolBayatYazi,
            };

            oge.SubItems.Add(DosyaTurleri.Adi(satir.Tur));
            oge.SubItems.Add(satir.Seviye == 0
                ? string.Empty
                : satir.KacYerde.ToString(CultureInfo.InvariantCulture));
            oge.SubItems.Add(satir.Yapilandirma ?? string.Empty);

            foreach (string sutun in sonuc.OzelSutunlar)
            {
                oge.SubItems.Add(satir.Ozel.TryGetValue(sutun, out string? deger) ? deger : string.Empty);
            }

            oge.SubItems.Add(satir.Durum ?? string.Empty);
            oge.SubItems.Add(satir.Yol);
            liste.Items.Add(oge);
        }
    }

    /// <summary>
    /// Listenin ne kadar guvenilir oldugunu SOYLEYEN satir. Yarim ya da
    /// sorunlu bir liste, tam bir liste gibi gorunmemeli (CLAUDE.md 3).
    /// </summary>
    private static string Ozet(ParcaListesiSonucu sonuc)
    {
        string sayi = $"{sonuc.Satirlar.Count} satır";
        if (!sonuc.Tam)
        {
            return $"{sayi} — LİSTE YARIM: {sonuc.Sebep ?? "sebep bilinmiyor"}";
        }

        return sonuc.Sorunlu == 0
            ? sayi + " — ağacın tamamı okundu."
            : $"{sayi} — {sonuc.Sorunlu} satır eksik ya da okunamadı (Durum sütununa bakın).";
    }

    /// <summary>
    /// CSV'ye yazar. Kabuk kutusu CALISMA KLASORUNU kaydiriyor (CLAUDE.md 4);
    /// KabukKutusu bunu tek yerde onaruyor, burada elle bir sey yapilmiyor.
    /// </summary>
    private static void Aktar(
        Form pencere, string belgeAdi, ParcaListesiSonucu sonuc, Action<string> bildir)
    {
        using var kutu = new SaveFileDialog
        {
            Title = "Parça listesini CSV olarak kaydet",
            Filter = "CSV dosyası (*.csv)|*.csv",
            FileName = WindowsYolu.DosyaAdiUzantisiz(belgeAdi) + " parça listesi.csv",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (KabukKutusu.Goster(kutu, pencere) != DialogResult.OK)
        {
            return;
        }

        IslemRaporu rapor = ParcaListesiCsv.Yaz(kutu.FileName, sonuc);
        if (!rapor.Oldu)
        {
            // SEBEP EKRANDA (CLAUDE.md 3): kullanici dosyayi Excel'de arayip
            // bulamayacak, neden yazilmadigini bilmesi lazim.
            MessageBox.Show(
                pencere, "CSV yazılamadı: " + rapor.Sebebi, "Parça listesi",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bildir($"Parça listesi yazıldı: {kutu.FileName} ({sonuc.Satirlar.Count} satır)");
    }
}
