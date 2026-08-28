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

        // REFERANS ONARIMI. Bir SOLIDWORKS dosyasinin adi degisince onu
        // kullanan montaj/teknik resim ESKI ADI arar; komsuluk kurali da
        // kurtarmiyor (CLAUDE.md 5). Tek cozum ebeveynin ICINE yazmak - ve
        // bunun calistigi Erkan'in makinesinde OLCULDU (28.08.2026).
        if (SwReferans.TasiyabilirMi(yol))
        {
            OnarimPlani plan = ReferansOnarimi.Planla(baglam.Referanslar.Indeks, yol, yeniAd);
            switch (OnarimKutusu.Sor(baglam.Sahip, plan, eskiAd))
            {
                case OnarimKarari.Vazgec:
                    baglam.Bildir("Ad değiştirme iptal edildi.");
                    return;

                case OnarimKarari.Onar:
                    Onar(baglam, plan, eskiAd, yeniAd);
                    return;

                default:
                    break;   // onarmadan devam
            }
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

    /// <summary>
    /// Adi degistirir VE onu kullanan dosyalari onarir - hepsi ya da hicbiri.
    /// Sonuc SAYIYLA yaziliyor; "oldu" demek yetmez (CLAUDE.md 10).
    /// </summary>
    private static void Onar(IslemBaglami baglam, OnarimPlani plan, string eskiAd, string yeniAd)
    {
        OnarimSonucu sonuc = ReferansOnarimi.Uygula(plan);
        if (!sonuc.Oldu)
        {
            MessageBox.Show(
                baglam.Sahip, sonuc.Sebep ?? "Bilinmeyen sebep.",
                "Onarılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            baglam.Bildir("Onarılamadı — " + eskiAd);
            return;
        }

        string yeniYol = WindowsYolu.Birlestir(WindowsYolu.Klasor(plan.EskiYol), yeniAd);

        // INDEKS TAZELENIYOR: yoksa referans paneli artik olmayan bir adi
        // "kullaniyor" diye gosterir (CLAUDE.md 3).
        baglam.Referanslar.Tazele([yeniYol, .. sonuc.Onarilanlar]);

        GeriAlDefteri.Kaydet(OnarimiGeriAl(yeniYol, plan.EskiYol, eskiAd, yeniAd, sonuc.Onarilanlar));

        baglam.Tazele(yeniYol);
        baglam.Bildir(
            $"{eskiAd} → {yeniAd} · onu kullanan {sonuc.Onarilanlar.Count} dosya onarıldı");
    }

    /// <summary>
    /// Onarimi GERI ALIR: adi eskiye dondurur VE ebeveynleri geri onarir.
    ///
    /// EBEVEYN LISTESI BURADA TASINIYOR, indekse yeniden sorulmuyor: indeks
    /// ad degisiminden sonra yeni adi bilmez ve sifir ebeveyn dondururdu -
    /// yani geri alma dosyayi eski adina dondurup ebeveynleri YENI ada bakar
    /// halde birakirdi. Referansi geri alma KIRARDI.
    /// </summary>
    private static GeriAlinabilir OnarimiGeriAl(
        string yeniYol, string eskiYol, string eskiAd, string yeniAd,
        IReadOnlyList<string> ebeveynler)
        => new(
            $"\"{eskiAd}\" → \"{yeniAd}\" adlandırması ve {ebeveynler.Count} onarım",
            baglam =>
            {
                var olmayan = new List<string>();
                OnarimSonucu geri = ReferansOnarimi.Uygula(
                    ReferansOnarimi.PlanlaBilinenlerle(ebeveynler, yeniYol, eskiYol));

                if (!geri.Oldu)
                {
                    olmayan.Add(yeniAd + " — " + (geri.Sebep ?? "bilinmeyen sebep"));
                    return olmayan;
                }

                baglam.Referanslar.Tazele([eskiYol, .. ebeveynler]);
                return olmayan;
            });

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
}

/// <summary>
/// /// Kucuk ad sorma kutusu. WinForms'ta hazir bir "InputBox" yok; buraya
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
