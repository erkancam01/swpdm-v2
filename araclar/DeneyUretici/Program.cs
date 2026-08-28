using System;
using System.Collections.Generic;
using System.IO;
using SwPdm.Cekirdek;

namespace SwPdm.DeneyUretici;

/// <summary>
/// DOSYAYA YAZMA DENEYI - dort deneyi tek turda ureten arac.
///
/// SORU: bir SOLIDWORKS dosyasinin icindeki yazili yolu degistirip
/// dosyayi SOLIDWORKS'e actirabiliyor muyuz? Cevap BURADA OLCULEMEZ.
///
/// DENEY TASARIMI - degiskenler TEK TEK ayriliyor (CLAUDE.md 2):
///
///   0  metin AYNI, yalnizca yeniden sikistirildi + dolgu
///      -> bastaki 4 bayt (sagalama olabilir) ve dolgu TEK BASINA olculur
///   1  Parça1 -> Parca1, yalnizca "Header2"
///      -> tek akis yetiyor mu
///   2  Parça1 -> Parca1, yol yazan BUTUN akislar
///      -> hepsini degistirmek gerekiyor mu
///   3  Parça1 -> GövdeParçası1 (UZUN ad), butun akislar
///      -> dize uzunlugu degisince kiriliyor mu
///
/// 1 ve 2'de ad AYNI HARF SAYISINDA secildi: MFC dizesinin uzunluk bayti ve
/// akisin acilmis boyu degismiyor, yani "uzunluk" degiskeni devre disi.
/// Onu yalnizca 3 olcuyor.
/// </summary>
internal static class Program
{
    private static int Main(string[] argumanlar)
    {
        if (argumanlar.Length != 2)
        {
            Console.Error.WriteLine("Kullanim: DeneyUretici <ornek-veri-klasoru> <cikti-klasoru>");
            return 2;
        }

        string kaynak = argumanlar[0];
        string cikti = argumanlar[1];

        if (!Directory.Exists(kaynak))
        {
            Console.Error.WriteLine("Kaynak klasor yok: " + kaynak);
            return 2;
        }

        if (Directory.Exists(cikti))
        {
            Directory.Delete(cikti, recursive: true);
        }

        bool tamam = true;
        tamam &= Deney0(kaynak, Path.Combine(cikti, "0-yeniden-sikistirma"));
        tamam &= Deney(kaynak, Path.Combine(cikti, "1-yalniz-header2"),
            "Parca1.SLDPRT", yalnizDogrudan: true);
        tamam &= Deney(kaynak, Path.Combine(cikti, "2-butun-akislar"),
            "Parca1.SLDPRT", yalnizDogrudan: false);
        tamam &= Deney(kaynak, Path.Combine(cikti, "3-uzun-ad"),
            "GövdeParçası1.SLDPRT", yalnizDogrudan: false);

        Talimat(cikti);
        Console.WriteLine(tamam ? "== DENEY PAKETI HAZIR ==" : "== EKSIK URETILDI ==");
        return tamam ? 0 : 1;
    }

    /// <summary>
    /// Pakete NE YAPILACAGINI yazar.
    ///
    /// CRLF SART: Windows'ta Not Defteri LF'li dosyayi tek satir gosteriyor
    /// (CLAUDE.md 4'teki .bat dersinin kardesi). Metin okunamazsa deney de
    /// yapilamaz.
    /// </summary>
    private static void Talimat(string cikti)
    {
        string[] satirlar =
        [
            "SOLIDWORKS DOSYASINA YAZMA DENEYI",
            "=================================",
            "",
            "Bir parcanin adi degisince onu kullanan montaj ve teknik resim",
            "ESKI ADI arar ve bulamaz. Tek cozum, dosyanin ICINDEKI yaziyi",
            "degistirmek. Yazdigim dosyayi SOLIDWORKS'un kabul edip etmedigini",
            "BURADA olcemiyorum - SOLIDWORKS yok. Bu paket onun icin.",
            "",
            "YAPILACAK: dort klasorun her birinde Montaj1.SLDASM ve",
            "Parca1.SLDDRW dosyalarini SOLIDWORKS'te AC. Her biri icin",
            "sunlardan hangisi oldugunu yaz:",
            "",
            "   a) sorunsuz acildi, parca yerinde",
            "   b) acildi ama 'dosya bulunamadi' diye sordu",
            "   c) hic acilmadi / hata verdi  (hatanin metnini de yaz)",
            "",
            "KLASORLER",
            "",
            "  0-yeniden-sikistirma",
            "     Hicbir yazi degismedi. Dosyanin icindeki veri yalnizca",
            "     yeniden sikistirilip ayni yere yazildi.",
            "     BU EN ONEMLISI: acilirsa, dosyaya yazma yolunun acik oldugunu",
            "     ogreniriz. Acilmazsa sebep metin degil, yazmanin kendisidir.",
            "",
            "  1-yalniz-header2",
            "     Parca1.SLDPRT -> Parca1.SLDPRT (c yerine c), yalnizca bir",
            "     akista degistirildi. Eski ad bilerek baska akislarda BIRAKILDI.",
            "     Acilirsa: tek yeri degistirmek yetiyor.",
            "",
            "  2-butun-akislar",
            "     Ayni ad degisikligi, yolu yazan BUTUN akislarda.",
            "     Asil kullanacagimiz yol bu.",
            "",
            "  3-uzun-ad",
            "     Parca1.SLDPRT -> GovdeParcasi1.SLDPRT. Ad daha UZUN.",
            "     Acilirsa: ad uzunlugu bir engel degil.",
            "",
            "NOT: bunlarin hepsi KOPYA. Senin asil dosyalarina dokunulmadi ve",
            "bu paketteki hicbir sey senin arsivini etkilemez. Acilmayan bir",
            "dosya olursa kaybedilen bir sey yok - ogrendigimiz sey var.",
        ];

        File.WriteAllText(
            Path.Combine(cikti, "OKU-BENI.txt"), string.Join("\r\n", satirlar) + "\r\n");
    }

    /// <summary>Montajin ve teknik resmin degismeyen komsulari.</summary>
    private static void Komsulari(string kaynak, string klasor, string parcaAdi)
    {
        Directory.CreateDirectory(Path.Combine(klasor, "Yeni klasör"));
        File.Copy(
            Path.Combine(kaynak, "Parça1.SLDPRT"), Path.Combine(klasor, parcaAdi), true);
        File.Copy(
            Path.Combine(kaynak, "Yeni klasör", "Parça2.SLDPRT"),
            Path.Combine(klasor, "Yeni klasör", "Parça2.SLDPRT"), true);
    }

    /// <summary>0 numara: hicbir metin degismiyor, yalnizca yeniden sikistiriliyor.</summary>
    private static bool Deney0(string kaynak, string klasor)
    {
        Directory.CreateDirectory(klasor);
        Komsulari(kaynak, klasor, "Parça1.SLDPRT");

        bool tamam = true;
        foreach (string ad in new[] { "Montaj1.SLDASM", "Parça1.SLDDRW" })
        {
            // ASIL YAMANIN DOKUNDUGU AKISLARIN AYNISI - kiyas ancak boyle adil.
            YamaSonucu s = SwYazici.YenidenSikistir(
                Path.Combine(kaynak, ad), Path.Combine(klasor, ad), "Parça1.SLDPRT");
            tamam &= Yaz("0", ad, s);
        }

        return tamam;
    }

    private static bool Deney(string kaynak, string klasor, string yeniAd, bool yalnizDogrudan)
    {
        Directory.CreateDirectory(klasor);
        Komsulari(kaynak, klasor, yeniAd);

        string numara = Path.GetFileName(klasor)[..1];
        bool tamam = true;

        foreach (string ad in new[] { "Montaj1.SLDASM", "Parça1.SLDDRW" })
        {
            YamaSonucu s = SwYazici.AdiDegistir(
                Path.Combine(kaynak, ad), Path.Combine(klasor, ad),
                "Parça1.SLDPRT", yeniAd, yalnizDogrudan);
            tamam &= Yaz(numara, ad, s);
        }

        return tamam;
    }

    /// <summary>
    /// Sonucu OLCULMUS haliyle yazar. "Oldu" demek yetmez: kac akis, kac
    /// dize, ve eski adin HALA yazili kaldigi akislar da sayilir (CLAUDE.md 3).
    /// </summary>
    private static bool Yaz(string numara, string ad, YamaSonucu s)
    {
        if (!s.Oldu)
        {
            Console.WriteLine($"  [{numara}] {ad,-16} OLMADI — {s.Sebep}");
            return false;
        }

        string kalan = s.KalanAkislar.Count == 0
            ? "kalan yok"
            : $"ESKI AD HALA {s.KalanAkislar.Count} akista: {string.Join(", ", s.KalanAkislar)}";

        Console.WriteLine($"  [{numara}] {ad,-16} {s.DegisenAkis} akis · {s.DegisenDize} yol · {kalan}");
        return true;
    }
}
