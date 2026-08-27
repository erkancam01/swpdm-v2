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
    internal static void Ac(IWin32Window sahip, ReferansIndeksi? indeks, Action<string> bildir)
    {
        ArgumentNullException.ThrowIfNull(bildir);

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
            RaporSonucu sonuc = rapor.Uret(indeks);
            sekmeler.TabPages.Add(Sekme(rapor, sonuc));
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

    private static TabPage Sekme(IRapor rapor, RaporSonucu sonuc)
    {
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
            var oge = new ListViewItem(WindowsYolu.DosyaAdi(satir.Yol));
            oge.SubItems.Add(WindowsYolu.Klasor(satir.Yol));
            oge.SubItems.Add(satir.Aciklama);
            liste.Items.Add(oge);
        }

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
        return sayfa;
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
        => RaporPenceresi.Ac(baglam.Sahip, baglam.Referanslar.Indeks, baglam.Bildir);
}
