using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YENIDEN ADLANDIR. Kutu, dogrulama, uzanti uyarisi ve referans uyarisi -
/// hepsi burada (CLAUDE.md 1b).
/// </summary>
internal sealed class YenidenAdlandirIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Yeniden adlandır";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.F2;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (secim.Ogeler.Count == 0)
        {
            nedenOlmaz = "Önce bir öğe seçin.";
            return false;
        }

        if (secim.Ogeler.Count > 1)
        {
            nedenOlmaz = "Aynı anda tek bir öğenin adı değiştirilebilir.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        object? oge = baglam.Secim.TekOge;
        string? yol = SecimBaglami.Yolu(oge);
        if (oge is null || yol is null)
        {
            return;
        }

        string eskiAd = SecimBaglami.Adi(oge);

        if (oge is DosyaOgesi dosya && SolidworksMu(dosya.Tur) && !ReferansUyarisi(baglam.Sahip))
        {
            baglam.Bildir("Ad değiştirme iptal edildi.");
            return;
        }

        string? yeniAd = AdKutusu.Sor(baglam.Sahip, eskiAd);
        if (yeniAd is null || yeniAd == eskiAd)
        {
            return;
        }

        // Uzantiyi degistirmek dosyayi tanimsiz hale getirir; Gezgin de sorar.
        if (oge is DosyaOgesi
            && !string.Equals(WindowsYolu.Uzanti(eskiAd), WindowsYolu.Uzanti(yeniAd),
                StringComparison.OrdinalIgnoreCase)
            && MessageBox.Show(
                baglam.Sahip,
                "Dosya uzantısını değiştiriyorsunuz.\n\n"
                + $"{WindowsYolu.Uzanti(eskiAd)}  →  {WindowsYolu.Uzanti(yeniAd)}\n\n"
                + "Dosya kullanılamaz hale gelebilir. Devam edilsin mi?",
                "Uzantı değişiyor",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.OK)
        {
            baglam.Bildir("Ad değiştirme iptal edildi.");
            return;
        }

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(yol, yeniAd);

        if (!rapor.Oldu)
        {
            MessageBox.Show(
                baglam.Sahip, rapor.Sebep ?? "Bilinmeyen sebep.",
                "Adı değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            baglam.Bildir("Adı değiştirilemedi — " + eskiAd);
            return;
        }

        if (rapor.YeniYol is string yeniYol)
        {
            GeriAlDefteri.Kaydet(GeriAlmasi(yeniYol, eskiAd, yeniAd));
        }

        baglam.Tazele(rapor.YeniYol);
        baglam.Bildir($"{eskiAd} → {yeniAd}");
    }

    /// <summary>Geri alma: eski adi geri koyar.</summary>
    private static GeriAlinabilir GeriAlmasi(string yeniYol, string eskiAd, string yeniAd)
        => new(
            $"\"{eskiAd}\" → \"{yeniAd}\" adlandırması",
            baglam =>
            {
                var olmayan = new List<string>();
                IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(yeniYol, eskiAd);
                if (!rapor.Oldu)
                {
                    olmayan.Add(yeniAd + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
                }

                return olmayan;
            });

    private static bool SolidworksMu(DosyaTuru tur)
        => tur is DosyaTuru.Parca or DosyaTuru.Montaj or DosyaTuru.TeknikResim;

    /// <summary>
    /// CLAUDE.md 3: BILMEDIGIMIZI SOYLE. Bu bir "emin misiniz?" degil; eksik
    /// olanin ADI. Referans indeksi gelince bu uyari kalkar ve yerine gercek
    /// onarim gelir.
    /// </summary>
    private static bool ReferansUyarisi(IWin32Window sahip)
        => MessageBox.Show(
            sahip,
            "Bu bir SOLIDWORKS dosyası.\n\n"
            + "Adını değiştirirseniz onu kullanan montaj ve teknik resimler "
            + "parçayı bulamayabilir.\n\n"
            + "Referans taraması ve onarımı HENÜZ YAPILMIYOR — hangi dosyaların "
            + "bunu kullandığını bilmiyoruz ve bağı onaramıyoruz.\n\n"
            + "Yine de devam edilsin mi?",
            "Referans bağı kırılabilir",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK;
}

/// <summary>
/// Kucuk ad sorma kutusu. WinForms'ta hazir bir "InputBox" yok; buraya
/// yaziliyor cunku yalnizca bu islem kullaniyor (CLAUDE.md 1b).
/// </summary>
internal static class AdKutusu
{
    /// <summary>Yeni adi sorar. Vazgecilirse null.</summary>
    internal static string? Sor(IWin32Window sahip, string eskiAd)
    {
        // CLAUDE.md 6: alanlar boyut degistiren her seyden ONCE atanir.
        var kutu = new TextBox { Text = eskiAd };
        var uyari = new Label { ForeColor = Color.FromArgb(0xB0, 0x30, 0x30), AutoSize = false };
        var tamam = new Button { Text = "Tamam", DialogResult = DialogResult.OK };
        var vazgec = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel };

        using var pencere = new Form
        {
            Text = "Yeniden adlandır",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(380, 120),
            Font = new Font("Segoe UI", 9f),
        };

        kutu.SetBounds(12, 12, 356, 24);
        uyari.SetBounds(12, 42, 356, 32);
        tamam.SetBounds(196, 82, 80, 26);
        vazgec.SetBounds(284, 82, 80, 26);

        pencere.Controls.Add(kutu);
        pencere.Controls.Add(uyari);
        pencere.Controls.Add(tamam);
        pencere.Controls.Add(vazgec);
        pencere.AcceptButton = tamam;
        pencere.CancelButton = vazgec;

        // Uzantisiz kismi secili gelir - Gezgin de oyle yapar.
        string uzanti = WindowsYolu.Uzanti(eskiAd);
        kutu.SelectionStart = 0;
        kutu.SelectionLength = eskiAd.Length - uzanti.Length;

        void Denetle()
        {
            bool olur = WindowsYolu.AdGecerliMi(kutu.Text, out string sebep);
            uyari.Text = olur ? string.Empty : sebep;   // sebep EKRANDA, aninda
            tamam.Enabled = olur;
        }

        kutu.TextChanged += (_, _) => Denetle();
        Denetle();

        return pencere.ShowDialog(sahip) == DialogResult.OK ? kutu.Text : null;
    }
}
