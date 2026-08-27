using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace SwPdm.Cekirdek;

/// <summary>Agacta gorunen bir dosya.</summary>
public sealed record DosyaOgesi(
    string Yol,
    string Ad,
    DosyaTuru Tur,
    long Boyut,
    DateTime Degistirme);

/// <summary>
/// Agacta gorunen bir klasor.
/// <paramref name="DosyaSayisi"/> ve <paramref name="AltKlasorVarMi"/> null ise
/// klasor OKUNAMADI - bilinmiyor demektir, "0" demek DEGILDIR (CLAUDE.md 3).
/// </summary>
public sealed record KlasorOgesi(
    string Yol,
    string Ad,
    int? DosyaSayisi,
    bool? AltKlasorVarMi,
    string? Hata);

/// <summary>Bir klasorun icerigi. <paramref name="Hata"/> doluysa liste EKSIKTIR.</summary>
public sealed record KlasorIcerigi(
    IReadOnlyList<KlasorOgesi> Klasorler,
    IReadOnlyList<DosyaOgesi> Dosyalar,
    string? Hata);

/// <summary>Arama sonucu. Kesilme ve sinir asimi GIZLENMEZ.</summary>
public sealed record AramaSonucu(
    IReadOnlyList<DosyaOgesi> Bulunanlar,
    int TarananKlasor,
    bool SinirAsildi,
    bool Iptal,
    IReadOnlyList<string> OkunamayanKlasorler);

/// <summary>
/// Diski okur. ARAYUZ BILMEZ - CLAUDE.md 7: bir arayuz sinifi hem ekran hem
/// is akisi surucusu olmaz. Hedef net8.0 oldugu icin bu mantik Linux'ta
/// gercek klasorlerle TEST EDILEBILIYOR.
///
/// TUM dosyalar gosterilir - gizli olanlar ve SOLIDWORKS'un ~$ kilit
/// dosyalari dahil. Gizlemek CLAUDE.md 3'e aykiri olurdu: kullanici klasoru
/// bos sanip silebilir. Ustelik CLAUDE.md 4'e gore "dizin bos degil"
/// hatasinin sebebi cogu zaman tam olarak o gorunmeyen dosyalardir.
/// </summary>
public static class KlasorTarayici
{
    /// <summary>Bir klasorun DOGRUDAN icerigini okur. Alt klasorlere INMEZ.</summary>
    public static KlasorIcerigi Tara(string? klasorYolu)
    {
        if (string.IsNullOrWhiteSpace(klasorYolu))
        {
            return new KlasorIcerigi([], [], "Klasör yolu boş.");
        }

        string[] altYollar;
        string[] dosyaYollari;
        try
        {
            altYollar = Directory.GetDirectories(klasorYolu);
            dosyaYollari = Directory.GetFiles(klasorYolu);
        }
        catch (Exception hata) when (OkumaHatasi(hata))
        {
            return new KlasorIcerigi([], [], Sebep(hata));
        }

        var klasorler = new List<KlasorOgesi>(altYollar.Length);
        foreach (string alt in altYollar)
        {
            // TEK AYRILMA: cop klasoru agacta gosterilmez. O klasor bizim,
            // kullanicinin dosyasi degil; icini agacta gormek karisiklik
            // olurdu. GIZLENMIS de olmuyor - yeri Cop Kutusu penceresinde
            // acikca yaziyor (CLAUDE.md 3).
            if (string.Equals(WindowsYolu.DosyaAdi(alt), Cop.KlasorAdi, StringComparison.Ordinal))
            {
                continue;
            }

            klasorler.Add(KlasoruOlc(alt));
        }

        var dosyalar = new List<DosyaOgesi>(dosyaYollari.Length);
        foreach (string dosya in dosyaYollari)
        {
            DosyaOgesi? oge = DosyayiOku(dosya);
            if (oge is not null)
            {
                dosyalar.Add(oge);
            }
        }

        // Gezgin gibi: "1, 2, 33, 222" - "1, 2, 222, 33" degil.
        klasorler.Sort(static (a, b) => DogalKarsilastirici.Ortak.Compare(a.Ad, b.Ad));
        dosyalar.Sort(static (a, b) => DogalKarsilastirici.Ortak.Compare(a.Ad, b.Ad));

        return new KlasorIcerigi(klasorler, dosyalar, null);
    }

    /// <summary>
    /// Kok altinda adinda <paramref name="metin"/> gecen dosyalari arar.
    ///
    /// Kulture bagli karsilastirma BILINCLI: bu INSAN metni. Turkce yerelinde
    /// kullanici "IGDIR" yazip "igdir" bulmayi bekler. (Makine karsilastirmalari
    /// - uzanti, ayrilmis ad - Ordinal kalir; ikisi ayni sey degil.)
    ///
    /// Uzun surebilir: iptal edilebilir, ilerleme bildirir ve sinir asilirsa
    /// bunu SOYLER. Sessiz kirpma yok (CLAUDE.md 9).
    /// </summary>
    public static AramaSonucu Ara(
        string? kok,
        string? metin,
        int enFazla,
        CancellationToken iptal = default,
        Action<int, int>? ilerleme = null)
    {
        var bulunanlar = new List<DosyaOgesi>();
        var okunamayanlar = new List<string>();
        int taranan = 0;

        if (string.IsNullOrWhiteSpace(kok) || string.IsNullOrWhiteSpace(metin))
        {
            return new AramaSonucu(bulunanlar, 0, false, false, okunamayanlar);
        }

        CompareInfo karsilastirici = CultureInfo.CurrentCulture.CompareInfo;
        var siradakiler = new Stack<string>();
        siradakiler.Push(kok);
        bool sinirAsildi = false;

        while (siradakiler.Count > 0)
        {
            if (iptal.IsCancellationRequested)
            {
                return new AramaSonucu(bulunanlar, taranan, sinirAsildi, true, okunamayanlar);
            }

            string simdiki = siradakiler.Pop();
            taranan++;
            ilerleme?.Invoke(taranan, bulunanlar.Count);

            string[] altlar;
            string[] dosyalar;
            try
            {
                altlar = Directory.GetDirectories(simdiki);
                dosyalar = Directory.GetFiles(simdiki);
            }
            catch (Exception hata) when (OkumaHatasi(hata))
            {
                okunamayanlar.Add(simdiki + " — " + Sebep(hata));
                continue;
            }

            foreach (string alt in altlar)
            {
                siradakiler.Push(alt);
            }

            foreach (string dosya in dosyalar)
            {
                string ad = WindowsYolu.DosyaAdi(dosya);
                if (karsilastirici.IndexOf(ad, metin, CompareOptions.IgnoreCase) < 0)
                {
                    continue;
                }

                if (bulunanlar.Count >= enFazla)
                {
                    sinirAsildi = true;
                    return new AramaSonucu(bulunanlar, taranan, true, false, okunamayanlar);
                }

                DosyaOgesi? oge = DosyayiOku(dosya);
                if (oge is not null)
                {
                    bulunanlar.Add(oge);
                }
            }
        }

        bulunanlar.Sort(static (a, b) => DogalKarsilastirici.Ortak.Compare(a.Ad, b.Ad));
        return new AramaSonucu(bulunanlar, taranan, sinirAsildi, false, okunamayanlar);
    }

    private static KlasorOgesi KlasoruOlc(string yol)
    {
        string ad = WindowsYolu.DosyaAdi(yol);
        try
        {
            // Sayim icin ENUMERATE: buyuk klasorde tum diziyi bellege almiyoruz.
            int dosyaSayisi = 0;
            foreach (string _ in Directory.EnumerateFiles(yol))
            {
                dosyaSayisi++;
            }

            bool altVar = false;
            foreach (string _ in Directory.EnumerateDirectories(yol))
            {
                altVar = true;
                break;
            }

            return new KlasorOgesi(yol, ad, dosyaSayisi, altVar, null);
        }
        catch (Exception hata) when (OkumaHatasi(hata))
        {
            // Sayilar null: BILINMIYOR. "0" yazmak "ici bos" demek olurdu.
            return new KlasorOgesi(yol, ad, null, null, Sebep(hata));
        }
    }

    private static DosyaOgesi? DosyayiOku(string yol)
    {
        try
        {
            var bilgi = new FileInfo(yol);
            return new DosyaOgesi(
                yol,
                WindowsYolu.DosyaAdi(yol),
                DosyaTurleri.Tani(yol),
                bilgi.Length,
                bilgi.LastWriteTime);
        }
        catch (Exception hata) when (OkumaHatasi(hata))
        {
            return null;   // tarama sirasinda silinmis olabilir
        }
    }

    private static bool OkumaHatasi(Exception hata)
        => hata is UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or PathTooLongException
            or IOException
            or ArgumentException
            or NotSupportedException;

    /// <summary>
    /// Hata sebebini EKRANDA gosterilebilecek bir cumleye cevirir (CLAUDE.md 3).
    /// Yalnizca gunluge yazip ekranda susmak yasak.
    /// </summary>
    private static string Sebep(Exception hata) => hata switch
    {
        UnauthorizedAccessException => "Erişim izni yok.",
        DirectoryNotFoundException => "Klasör bulunamadı (silinmiş ya da ağ bağlantısı kopmuş olabilir).",
        FileNotFoundException => "Dosya bulunamadı.",
        PathTooLongException => "Yol Windows'un izin verdiğinden uzun.",
        IOException => "Okunamadı: " + hata.Message,
        _ => hata.Message,
    };
}
