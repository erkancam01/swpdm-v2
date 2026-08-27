using System;
using System.Globalization;

namespace SwPdm.Cekirdek;

/// <summary>
/// Tarih/saati ekrana yazar.
///
/// TEK YER (CLAUDE.md 8): ayni bicim iki yerde durursa bir gun ayrisir ve
/// kullanici ayni dosyayi iki ekranda farkli tarihte gorur. v1'de boyut
/// bicimlendirmesinin basina tam olarak bu geldi.
/// </summary>
public static class Zaman
{
    /// <summary>Dosya listelerinde kullanilan kisa bicim.</summary>
    public static string Yaz(DateTime an)
        => an.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
}
