using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// SIL - ve KALICI DEGIL: kokun icindeki cop klasorune tasir.
///
/// Neden Windows Cop Kutusu DEGIL: o yalnizca yerel disklerde var. AG
/// SURUCUSUNDEN silinen dosya oraya GITMEZ, kalici gider - ve bu uygulamanin
/// asil calisma yeri ag surucusu. "Geri alinabilir" demek orada yalan olurdu
/// (CLAUDE.md 3). Ayrintisi Cekirdek/Cop.cs'te.
/// </summary>
internal sealed class SilIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Sil";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Delete;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (secim.Ogeler.Count == 0)
        {
            nedenOlmaz = "Önce silinecek öğeleri seçin.";
            return false;
        }

        if (secim.CopKlasoru is null)
        {
            nedenOlmaz = "Önce bir klasör açın.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        IReadOnlyList<object> ogeler = baglam.Secim.Ogeler;
        if (ogeler.Count == 0 || baglam.Secim.CopKlasoru is not string cop)
        {
            return;
        }

        if (!Onayla(baglam.Sahip, ogeler, baglam.Referanslar))
        {
            baglam.Bildir("Silme iptal edildi.");
            return;
        }

        var silinen = new List<string>();
        var silinenYollar = new List<string>();
        var kalan = new List<string>();

        foreach (object oge in ogeler)
        {
            string? yol = SecimBaglami.Yolu(oge);
            if (yol is null)
            {
                continue;
            }

            IslemRaporu rapor = Cop.Sil(cop, yol);
            if (rapor.Oldu)
            {
                silinen.Add(SecimBaglami.Adi(oge));
                silinenYollar.Add(yol);
            }
            else
            {
                kalan.Add(SecimBaglami.Adi(oge) + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
            }
        }

        if (silinenYollar.Count > 0)
        {
            GeriAlDefteri.Kaydet(GeriAlmasi(cop, silinenYollar));
        }

        baglam.Tazele(null);

        if (kalan.Count > 0)
        {
            // CLAUDE.md 3: kismi basarisizlikta NE OLDU NE OLMADI tek tek yazilir.
            // "Bazilari silinemedi" demek kullaniciyi ikinci kez denemeye iter.
            var metin = new StringBuilder();
            metin.AppendLine($"{silinen.Count} öğe çöp kutusuna gitti.");
            metin.AppendLine();
            metin.AppendLine($"{kalan.Count} öğe SİLİNMEDİ:");
            foreach (string satir in kalan)
            {
                metin.AppendLine("  • " + satir);
            }

            MessageBox.Show(
                baglam.Sahip, metin.ToString(), "Bazı öğeler silinemedi",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        baglam.Bildir(kalan.Count == 0
            ? $"{silinen.Count} öğe çöp kutusuna gönderildi."
            : $"{silinen.Count} silindi · {kalan.Count} silinemedi");
    }

    /// <summary>
    /// Geri alma: silinenleri copten geri yukler. Oge, ESKI YOLUNDAN
    /// bulunuyor; ayni yoldan birden fazla varsa en YENI silinen aliniyor
    /// (Cop.Listele en yeniyi basta veriyor).
    /// </summary>
    private static GeriAlinabilir GeriAlmasi(string cop, IReadOnlyList<string> yollar)
        => new(
            $"{yollar.Count} öğenin silinmesi",
            baglam =>
            {
                var olmayan = new List<string>();

                foreach (string yol in yollar)
                {
                    CopOgesi? oge = null;
                    foreach (CopOgesi aday in Cop.Listele(cop))
                    {
                        if (string.Equals(aday.EskiYol, yol, StringComparison.OrdinalIgnoreCase))
                        {
                            oge = aday;
                            break;
                        }
                    }

                    if (oge is null)
                    {
                        olmayan.Add(WindowsYolu.DosyaAdi(yol) + " — çöp kutusunda bulunamadı.");
                        continue;
                    }

                    IslemRaporu rapor = Cop.GeriYukle(cop, oge);
                    if (!rapor.Oldu)
                    {
                        olmayan.Add(oge.Ad + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
                    }
                }

                return olmayan;
            });

    private static bool Onayla(
        IWin32Window sahip, IReadOnlyList<object> ogeler, ReferansSurucusu referanslar)
    {
        var metin = new StringBuilder();
        metin.AppendLine(ogeler.Count == 1
            ? $"\"{SecimBaglami.Adi(ogeler[0])}\" çöp kutusuna gönderilecek."
            : $"{ogeler.Count} öğe çöp kutusuna gönderilecek.");

        // En fazla on ad; gerisi sayiyla. Uzun liste kutuyu tasiriyor.
        int yazilan = 0;
        foreach (object oge in ogeler)
        {
            if (yazilan == 10)
            {
                metin.AppendLine($"  … ve {ogeler.Count - 10} tane daha");
                break;
            }

            metin.AppendLine("  • " + SecimBaglami.Adi(oge));
            yazilan++;
        }

        metin.AppendLine();
        metin.AppendLine("Çöp kutusundan geri yüklenebilir.");

        // REFERANS UYARISI KALIYOR: silinen dosya ONARILAMAZ. Tasima ve ad
        // degisiminde uyari kalkti cunku onlari onariyoruz; burada onarilacak
        // bir hedef yok, yani risk gercek (CLAUDE.md 3).
        string uyari = ReferansUyarisi(ogeler, referanslar);
        if (uyari.Length > 0)
        {
            metin.AppendLine();
            metin.Append(uyari);
        }

        return OnayKutusu.Sor(
            sahip, "Çöp kutusuna gönder", metin.ToString().TrimEnd(), tehlikeli: true);
    }

    /// <summary>
    /// SILINECEK DOSYALARI KIM KULLANIYOR - bu uygulamanin varlik sebebi.
    ///
    /// Uc ayri hal, UCU DE ayri yazilir (CLAUDE.md 3):
    ///   tarama yok      -> "BILMIYORUZ" (sessizce "temiz" DEMEZ)
    ///   tarama var, 0   -> "kullanan bulunamadi"
    ///   tarama var, n   -> hangi dosyalar, adlariyla
    ///
    /// UYARIR AMA ENGELLEMEZ (Erkan'in karari): kullanici ne yaptigini
    /// biliyor olabilir; karari ondan almak degil, ona GERCEGI vermek.
    /// </summary>
    private static string ReferansUyarisi(
        IReadOnlyList<object> ogeler, ReferansSurucusu referanslar)
    {
        if (!referanslar.Hazir)
        {
            return "UYARI: Referans taraması yok ya da eksik — bu dosyaları hangi\n"
                 + "montajların kullandığını BİLMİYORUZ.\n"
                 + "Ctrl+Shift+R ile tarayıp yeniden deneyebilirsiniz.\n";
        }

        var kullananlar = new List<string>();
        foreach (object oge in ogeler)
        {
            if (SecimBaglami.Yolu(oge) is not string yol)
            {
                continue;
            }

            foreach (string kullanan in referanslar.Kullananlarin(yol))
            {
                string ad = WindowsYolu.DosyaAdi(kullanan);
                if (!kullananlar.Exists(v => string.Equals(v, ad, StringComparison.OrdinalIgnoreCase)))
                {
                    kullananlar.Add(ad);
                }
            }
        }

        if (kullananlar.Count == 0)
        {
            return "Referans taraması tamam: bu dosyaları kullanan bulunamadı.\n";
        }

        var yazi = new StringBuilder();
        yazi.AppendLine($"DİKKAT: bunları {kullananlar.Count} dosya KULLANIYOR:");
        int yazilan = 0;
        foreach (string ad in kullananlar)
        {
            if (yazilan == 8)
            {
                yazi.AppendLine($"  … ve {kullananlar.Count - 8} tane daha");
                break;
            }

            yazi.AppendLine("  ! " + ad);
            yazilan++;
        }

        yazi.AppendLine("Silerseniz o dosyalar bu parçayı bulamaz.");
        return yazi.ToString();
    }
}
