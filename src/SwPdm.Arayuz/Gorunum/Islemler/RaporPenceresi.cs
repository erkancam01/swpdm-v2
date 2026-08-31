using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// RAPOR PENCERESI: kirik referanslar, yetim parcalar, teknik resmi
/// olmayanlar, tasinmis dosyalar ve okunamayanlar.
///
/// SEKMELER TEK LISTEDEN URETILIYOR (<see cref="RaporListesi.Tumu"/>) -
/// burada elle yazilmis bir rapor adi YOK (CLAUDE.md 1b). Yeni bir rapor
/// eklendiginde sekmesi kendiliginden cikar, kaldirildiginda kendiliginden
/// gider; bu dosya degismez.
///
/// HER SEKMEDE GUVENILIRLIK YAZIYOR. Bos bir liste tek basina "sorun yok"
/// demek DEGILDIR: tarama yapilmadiysa ya da yarim kaldiysa liste bos
/// gorunur. O yuzden bos liste bile bir cumleyle aciklaniyor (CLAUDE.md 3).
/// </summary>
internal static class RaporPenceresi
{
    /// <summary>Raporlari gosterir.</summary>
    /// <param name="git">
    /// Bir rapor satirina gidilir: pencere kapanir ve o dosya agacta secilir.
    /// </param>
    internal static void Ac(
        IWin32Window sahip, ReferansIndeksi? indeks, Action<string> bildir, Action<string> git)
    {
        ArgumentNullException.ThrowIfNull(bildir);
        ArgumentNullException.ThrowIfNull(git);

        if (indeks is null)
        {
            bildir("Önce bir klasör açın.");
            return;
        }

        using var pencere = new Form
        {
            Text = "Referans raporları",
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(760, 460),
            MinimumSize = new Size(520, 320),
            ShowInTaskbar = false,
            MinimizeBox = false,
        };

        var sekmeler = new TabControl { Dock = DockStyle.Fill };

        foreach (IRapor rapor in RaporListesi.Tumu)
        {
            sekmeler.TabPages.Add(Sekme(pencere, rapor, indeks, bildir, git));
        }

        var kapat = new Button
        {
            Text = "Kapat",
            Dock = DockStyle.Bottom,
            Height = 32,
            DialogResult = DialogResult.OK,
        };

        pencere.Controls.Add(sekmeler);
        pencere.Controls.Add(kapat);
        pencere.AcceptButton = kapat;
        pencere.CancelButton = kapat;

        pencere.ShowDialog(sahip);
    }

    private static TabPage Sekme(
        Form pencere, IRapor rapor, ReferansIndeksi indeks, Action<string> bildir,
        Action<string> git)
    {
        RaporSonucu sonuc = rapor.Uret(indeks);
        // Sekme basliginda SAYI var ama yalnizca guvenilirse. Guvenilir
        // olmayan bir "0", "sorun yok" diye okunurdu.
        var sayfa = new TabPage(
            sonuc.Guvenilir ? $"{rapor.Ad} ({sonuc.Satirlar.Count})" : $"{rapor.Ad} (?)");

        var liste = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.None,
        };
        liste.Columns.Add("Dosya", 260);
        liste.Columns.Add("Klasör", 200);
        liste.Columns.Add("Bulgu", 260);

        foreach (RaporSatiri satir in sonuc.Satirlar)
        {
            var oge = new ListViewItem(WindowsYolu.DosyaAdi(satir.Yol)) { Tag = satir.Yol };
            oge.SubItems.Add(WindowsYolu.Klasor(satir.Yol));
            oge.SubItems.Add(satir.Aciklama);
            liste.Items.Add(oge);
        }

        // SATIRA GIDILEBILIYOR (29.08.2026). Once liste yalnizca BAKILAN bir
        // seydi: "Dosya" ve "Klasör" yaziyordu ama oraya gitmenin yolu yoktu,
        // kullanici dosyayi agacta elle ariyordu. Referans panelinde bu
        // yetenek zaten vardi (cift tik / Enter = oraya git); rapor
        // penceresinde yoktu.
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

        var baslik = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 46,
            Padding = new Padding(8, 6, 8, 6),
            Text = rapor.Aciklama + Environment.NewLine + Durum(sonuc),
            ForeColor = sonuc.Guvenilir ? Renkler.UstBilgiYazi : Color.FromArgb(0xB0, 0x30, 0x30),
        };

        sayfa.Controls.Add(liste);
        sayfa.Controls.Add(baslik);

        // DUZELT DUGMESI YALNIZCA DUZELTILEBILIR RAPORDA. Karar raporun
        // KENDISINDE (IRapor.Duzelt); pencere hangi raporun duzeltilebilir
        // oldugunu BILMEZ - yoksa yeni bir duzeltilebilir rapor eklemek
        // burayi da degistirtirdi (CLAUDE.md 1b).
        if (sonuc.Satirlar.Count > 0 && Duzeltilebilir(rapor, indeks))
        {
            var duzelt = new Button
            {
                Text = $"Bulunanları düzelt ({sonuc.Satirlar.Count})",
                Dock = DockStyle.Bottom,
                Height = 34,
            };

            duzelt.Click += (_, _) => Duzelt(pencere, rapor, indeks, bildir, duzelt);
            sayfa.Controls.Add(duzelt);
        }

        return sayfa;
    }

    /// <summary>
    /// Rapor duzeltmeyi DESTEKLIYOR mu. Sormanin bedeli yok: duzeltmeyen
    /// raporlar null donuyor ve hicbir sey yapmiyor.
    /// </summary>
    private static bool Duzeltilebilir(IRapor rapor, ReferansIndeksi indeks)
    {
        // Sinif adina bakmiyoruz (CLAUDE.md 9: kapsam ADLARA baglanmaz).
        // Bos bir indeks uzerinde soruluyor olsaydi is yapardi; o yuzden
        // yalnizca "bu tur destekliyor mu" diye bakan ucuz bir yol lazim.
        return rapor.Duzelt(BosIndeks) is not null;
    }

    /// <summary>Duzeltme destegini sormak icin BOS indeks - is yapmaz.</summary>
    private static readonly ReferansIndeksi BosIndeks = new(string.Empty);

    /// <summary>
    /// Duzeltmeyi kosturur ve SONUCU SAYIYLA yazar. "Duzeltildi" demek
    /// yetmez; kac tanesi ve tutmayanlarin sebebi de yazilir (CLAUDE.md 3).
    /// </summary>
    private static void Duzelt(
        Form pencere, IRapor rapor, ReferansIndeksi indeks, Action<string> bildir, Button dugme)
    {
        if (!OnayKutusu.Sor(
                pencere, "Bulunanları düzelt",
                rapor.Aciklama + "\n\n"
                + "Bu dosyaların İÇİNE yazılacak: bayat yollar gerçek konuma\n"
                + "çevrilecek. Özgün hâli doğrulanana kadar korunur; bir dosya\n"
                + "onarılamazsa ona DOKUNULMAZ ve sebebi yazılır.\n\n"
                + "SOLIDWORKS'te açık dosyalar atlanır."))
        {
            return;
        }

        dugme.Enabled = false;
        OnarimOzeti? ozet = rapor.Duzelt(indeks);
        if (ozet is null)
        {
            // DUGME OLU KALMASIN: burasi eskiden duz "return" ediyordu ve
            // dugme kalici olarak sonuk kaliyordu - hicbir sey olmuyor,
            // sebep de yok (CLAUDE.md 3).
            dugme.Enabled = true;
            bildir("Düzeltilecek bir şey bulunamadı.");
            return;
        }

        foreach (string dosya in ozet.Dokunulan)
        {
            IndeksTarama.Tazele(indeks, dosya);
        }

        if (ozet.Hatalar.Count > 0)
        {
            MaddeKutusu.Goster(
                pencere, "Bazıları düzeltilemedi",
                $"{ozet.Onarilan} yol düzeltildi.\n\n{ozet.Hatalar.Count} tanesi düzeltilemedi:",
                ozet.Hatalar);
        }

        bildir($"{ozet.Onarilan} bayat yol düzeltildi"
            + (ozet.Hatalar.Count > 0 ? $" · {ozet.Hatalar.Count} olmadı" : string.Empty));

        // Pencere KAPANIR: listeler artik bayat. Yeniden acildiginda
        // guncel hali gorunur - yarim guncellenmis bir pencere gostermek
        // kullaniciyi yanlis sayiya baktirirdi.
        pencere.DialogResult = DialogResult.OK;
        pencere.Close();
    }

    /// <summary>
    /// Listenin ne kadar guvenilir oldugunu SOYLEYEN satir. Bos liste burada
    /// aciklanir: "sorun bulunmadi" ile "bakilmadi" ayri seylerdir.
    /// </summary>
    private static string Durum(RaporSonucu sonuc)
    {
        if (!sonuc.Guvenilir)
        {
            return "DİKKAT — " + (sonuc.Sebep ?? "Bu liste eksik olabilir.")
                 + "  Ctrl+Shift+R ile tarayın.";
        }

        return sonuc.Satirlar.Count == 0
            ? "Tarama tamam: bu raporda bulgu yok."
            : $"Tarama tamam: {sonuc.Satirlar.Count} bulgu.";
    }
}

/// <summary>Rapor penceresini acan islem.</summary>
internal sealed class RaporIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Referans raporları";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.D;

    /// <inheritdoc/>
    public bool Yazar => false;   // yalnizca gosterir

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (string.IsNullOrWhiteSpace(secim.Kok))
        {
            nedenOlmaz = "Önce bir klasör açın.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
        => RaporPenceresi.Ac(
            baglam.Sahip, baglam.Referanslar.Indeks, baglam.Bildir, baglam.Tazele);
}
