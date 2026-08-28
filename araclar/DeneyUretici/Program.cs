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

        // IKINCI TUR. Birinci turun sonucu (Erkan, 28.08.2026):
        //   0 yeniden sikistirma -> ACILDI    (yazma mekanizmasi kabul ediliyor)
        //   1 yalniz Header2     -> acildi ama ICI BOS (bir akis yetmiyor)
        //   2 butun akislar      -> ACILDI, parcalar yerinde (ASIL YOL BU)
        //   3 uzun ad            -> HATA (dize uzayinca kiriliyor)
        // Bu turda uzunluk KLASOR KISMINDAN karsilaniyor; iki yon de sinaniyor.
        bool tamam = true;
        tamam &= Deney(kaynak, Path.Combine(cikti, "4-kisa-ad-dolgulu"),
            "P1.SLDPRT", yalnizDogrudan: false);
        tamam &= Deney(kaynak, Path.Combine(cikti, "5-uzun-ad-goreli"),
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
            "SOLIDWORKS YAZMA DENEYI - IKINCI TUR",
            "====================================",
            "",
            "BIRINCI TURDA OGRENDIKLERIMIZ (senin olcumun):",
            "   0 yeniden sikistirma -> ACILDI. Yazma mekanizmasi kabul ediliyor.",
            "   1 yalniz bir akis    -> acildi ama ici bos. Bir akis yetmiyor.",
            "   2 butun akislar      -> ACILDI, parcalar yerinde. ASIL YOL BU.",
            "   3 uzun ad            -> HATA. Yazilan yol UZAYINCA kiriliyor.",
            "",
            "Demek ki sart su: dosyanin icine yazilan yolun KARAKTER SAYISI",
            "degismemeli. Bu turda farki KLASOR kismindan karsiliyorum -",
            "SOLIDWORKS zaten once ebeveynin yanina bakiyor, yazili klasor",
            "bir ipucu.",
            "",
            "YAPILACAK: iki klasorde de Montaj1.SLDASM ve Parca1.SLDDRW ac.",
            "Her biri icin: acildi mi, parcalar yerinde mi?",
            "",
            "  4-kisa-ad-dolgulu",
            "     Parca1.SLDPRT -> P1.SLDPRT  (ad KISALDI)",
            "     Yolun icine \".\\\" eklenerek uzunluk sabit tutuldu; yol hala",
            "     ayni yeri gosteriyor.",
            "",
            "  5-uzun-ad-goreli",
            "     Parca1.SLDPRT -> GovdeParcasi1.SLDPRT  (ad UZADI)",
            "     Uzunluk sabit kalsin diye yolun soldaki klasorleri atildi;",
            "     yol GORELI hale geldi.",
            "",
            "Ikisi de acilirsa: ad degistirme onarimi HER AD icin calisiyor",
            "demektir ve is biter. Yalniz 4 acilirsa: kisaltmak serbest,",
            "uzatmak degil. Ikisi de acilmazsa: yalnizca ayni uzunluktaki",
            "adlar onarilabilir - o da az sey degil.",
            "",
            "NOT: hepsi KOPYA. Asil dosyalarina dokunulmadi.",
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
