using System;
using System.Globalization;

namespace SwPdm.Cekirdek;

/// <summary>
/// Dosya boyutunu ekrana yazar.
///
/// TEK YER (CLAUDE.md 8): v1'de boyut bicimlendirmesi UC yerdeydi ve biri
/// FARKLI sayi gosteriyordu - ayni dosya iki ekranda farkli boyutta
/// gorunuyordu. Ikinci kopyasi yazilmayacak.
/// </summary>
public static class Boyut
{
    private static readonly string[] Birimler = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Bayti okunabilir metne cevirir. Negatif deger "?" doner.</summary>
    public static string Yaz(long bayt)
    {
        if (bayt < 0)
        {
            return "?";
        }

        if (bayt < 1024)
        {
            return bayt.ToString(CultureInfo.CurrentCulture) + " B";
        }

        double deger = bayt;
        int birim = 0;
        while (deger >= 1024 && birim < Birimler.Length - 1)
        {
            deger /= 1024;
            birim++;
        }

        // 10'un altinda tek ondalik, ustunde tam sayi: "1,4 MB" ama "812 KB".
        string sayi = deger < 10
            ? deger.ToString("0.0", CultureInfo.CurrentCulture)
            : Math.Round(deger).ToString("0", CultureInfo.CurrentCulture);

        return sayi + " " + Birimler[birim];
    }
}
