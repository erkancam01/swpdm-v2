using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SwPdm.Cekirdek;

/// <summary>Bir SOLIDWORKS belgesinin icindeki bilgiler.</summary>
/// <param name="Ozel">
/// KULLANICININ girdigi ozellikler (Malzeme, Ağırlık, Çizen...). Sirasi
/// dosyadaki sira.
/// </param>
/// <param name="Sistem">SOLIDWORKS'un kendi ozellikleri (SW- ile baslayanlar).</param>
/// <param name="SonKaydeden">Belgeyi en son kaydeden kullanici; bilinmiyorsa null.</param>
/// <param name="Olusturma">Olusturma zamani; bilinmiyorsa null.</param>
/// <param name="Degistirme">Son kaydetme zamani; bilinmiyorsa null.</param>
/// <param name="Okundu">Bilgiler gercekten okunabildi mi.</param>
/// <param name="Sebep">Okunamadiysa SEBEP - bos sozluk "ozellik yok" DEMEK DEGIL.</param>
public sealed record SwBelgeBilgileri(
    IReadOnlyList<KeyValuePair<string, string>> Ozel,
    IReadOnlyDictionary<string, string> Sistem,
    string? SonKaydeden,
    DateTime? Olusturma,
    DateTime? Degistirme,
    bool Okundu,
    string? Sebep)
{
    /// <summary>Okunamamis sonuc.</summary>
    public static SwBelgeBilgileri Okunamadi(string sebep)
        => new([], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
               null, null, null, Okundu: false, sebep);

    /// <summary>Sistem ozelligini verir; yoksa null.</summary>
    public string? SistemOzelligi(string ad)
        => Sistem.TryGetValue(ad, out string? deger) && !string.IsNullOrEmpty(deger) ? deger : null;

    /// <summary>Yapilandirma adi ("Varsayılan" gibi); bilinmiyorsa null.</summary>
    public string? Yapilandirma => SistemOzelligi("SW-Configuration Name");
}

/// <summary>
/// BELGE BILGILERINI OKUR - SOLIDWORKS KURULU OLMADAN.
///
/// OLCULDU (28.08.2026, SOLIDWORKS 2022): bilgiler DUZ XML akislarinda.
/// Ikili "Contents/CusProps" kabini cozmeye GEREK YOK - once orasi
/// incelendi, sonra ayni verinin XML'de de durdugu goruldu; XML hem basit
/// hem kirilgan degil.
///
///   docProps/custom.xml                 -> kullanicinin ozellikleri
///   docProps/ISolidWorksInformation.xml -> SW- ile baslayan 46 ozellik
///   docProps/core.xml                   -> kim, ne zaman kaydetti
///
/// Olculen ornek:
///   Malzeme = "Pirinç"   ·   Ağırlık = "851.42"   ·   Çizen = "" (bos)
///
/// DEGER ONBELLEGE ALINMIS OLABILIR. "Ağırlık" ornekte bir denkleme bagli
/// ("SW-Kütle@Parça1.SLDPRT") ve dosyada duran sey o denklemin EN SON
/// HESAPLANMIS sonucu. Model degisip yeniden olusturulmadiysa bayat olur.
/// Buradan yeniden hesaplanmaz - uydurma bir sayi gostermektense dosyada
/// yazani gostermek dogru (CLAUDE.md 3).
///
/// BOS DEGER ile OLMAYAN OZELLIK AYRI SEYDIR. "Çizen" ornekte var ama bos;
/// onu listeden dusurmek "boyle bir alan yok" demek olurdu.
/// </summary>
public static class SwBelgeBilgisi
{
    private const string OzelAkis = "docProps/custom.xml";
    private const string SistemAkis = "docProps/ISolidWorksInformation.xml";
    private const string CekirdekAkis = "docProps/core.xml";

    /// <summary>Belgenin bilgilerini okur.</summary>
    public static SwBelgeBilgileri Oku(string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu))
        {
            return SwBelgeBilgileri.Okunamadi("Yol boş.");
        }

        try
        {
            using SwPaket? paket = SwPaket.Ac(dosyaYolu);
            if (paket is null)
            {
                return SwBelgeBilgileri.Okunamadi(
                    "Dosya SOLIDWORKS paketi gibi görünmüyor (biçim tanınmadı).");
            }

            List<KeyValuePair<string, string>> ozel = Ozellikler(Belge(paket, OzelAkis));
            Dictionary<string, string> sistem = Sozluk(Ozellikler(Belge(paket, SistemAkis)));
            (string? kim, DateTime? olusturma, DateTime? degistirme) = Cekirdek(Belge(paket, CekirdekAkis));

            return new SwBelgeBilgileri(
                ozel, sistem, kim, olusturma, degistirme, Okundu: true, Sebep: null);
        }
        catch (FileNotFoundException)
        {
            return SwBelgeBilgileri.Okunamadi("Dosya bulunamadı.");
        }
        catch (DirectoryNotFoundException)
        {
            return SwBelgeBilgileri.Okunamadi("Klasör bulunamadı.");
        }
        catch (UnauthorizedAccessException)
        {
            return SwBelgeBilgileri.Okunamadi("Dosya okunamadı: erişim reddedildi.");
        }
        catch (IOException hata)
        {
            return SwBelgeBilgileri.Okunamadi("Dosya okunamadı: " + hata.Message);
        }
    }

    /// <summary>Akisi XML olarak okur; yoksa ya da bozuksa null.</summary>
    private static XDocument? Belge(SwPaket paket, string akis)
    {
        byte[]? veri = paket.AkisiOku(akis);
        if (veri is null || veri.Length == 0)
        {
            return null;
        }

        try
        {
            return XDocument.Parse(Encoding.UTF8.GetString(veri));
        }
        catch (System.Xml.XmlException)
        {
            // Bozuk XML uygulamayi dusurmez; o akis yokmus gibi davranilir.
            return null;
        }
    }

    /// <summary>
    /// &lt;property name="X"&gt;&lt;vt:...&gt;deger&lt;/vt:...&gt;&lt;/property&gt; ciftlerini cikarir.
    ///
    /// Ad alani BOS olanlar atlanir: onlar denklem kaynagi ve kod sayfasi
    /// gibi ic kayitlar (ornekte pid=16777220 -> "SW-Kütle@Parça1.SLDPRT").
    /// Ad alani DOLU ama degeri bos olanlar KALIR - bos deger bir bilgidir.
    ///
    /// Ad alanlari (namespace) YOK SAYILIYOR: dosyada ust ogede bir ad alani
    /// var, alt ogelerde xmlns="" ile kaldiriliyor. Yerel ada bakmak ikisini
    /// de dogru okur ve SOLIDWORKS bir gun ad alanini degistirse bile tutar.
    /// </summary>
    private static List<KeyValuePair<string, string>> Ozellikler(XDocument? belge)
    {
        var sonuc = new List<KeyValuePair<string, string>>();
        if (belge is null)
        {
            return sonuc;
        }

        var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (XElement oge in belge.Descendants()
                     .Where(e => string.Equals(e.Name.LocalName, "property", StringComparison.Ordinal)))
        {
            string? ad = oge.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(ad) || !gorulen.Add(ad))
            {
                continue;
            }

            // Deger, ilk cocuk ogede duruyor (vt:lpstr, vt:i2, vt:bool...).
            // FPVals bir alt kap: denklemin HAM hali; kullaniciya gosterilecek
            // olan gorunen deger, o yuzden atlaniyor.
            XElement? deger = oge.Elements()
                .FirstOrDefault(e => !string.Equals(e.Name.LocalName, "FPVals", StringComparison.Ordinal));

            sonuc.Add(new KeyValuePair<string, string>(ad, deger?.Value ?? string.Empty));
        }

        return sonuc;
    }

    private static Dictionary<string, string> Sozluk(List<KeyValuePair<string, string>> ciftler)
    {
        var sozluk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> c in ciftler)
        {
            sozluk[c.Key] = c.Value;
        }

        return sozluk;
    }

    private static (string? Kim, DateTime? Olusturma, DateTime? Degistirme) Cekirdek(XDocument? belge)
    {
        if (belge is null)
        {
            return (null, null, null);
        }

        string? Al(string yerelAd) => belge.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, yerelAd, StringComparison.Ordinal))?.Value;

        return (Bos(Al("lastModifiedBy")), Zamani(Al("created")), Zamani(Al("modified")));
    }

    private static string? Bos(string? deger)
        => string.IsNullOrWhiteSpace(deger) ? null : deger;

    private static DateTime? Zamani(string? metin)
        => DateTime.TryParse(
               metin, CultureInfo.InvariantCulture,
               DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime zaman)
           ? zaman.ToLocalTime()
           : null;
}
