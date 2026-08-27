using System;

namespace SwPdm.Cekirdek;

/// <summary>Uygulamanin tanidigi dosya turleri.</summary>
public enum DosyaTuru
{
    /// <summary>Tanimadigimiz bir dosya. GIZLENMEZ, oldugu gibi gosterilir.</summary>
    Bilinmeyen = 0,
    Parca,
    Montaj,
    TeknikResim,
    Pdf,
}

/// <summary>
/// Uzanti -> tur esleme. TEK yerde (CLAUDE.md 8).
/// </summary>
public static class DosyaTurleri
{
    /// <summary>Dosya adindan turu belirler.</summary>
    public static DosyaTuru Tani(string? dosyaAdi)
    {
        string uzanti = WindowsYolu.Uzanti(dosyaAdi);

        // Ordinal SART: bu bir MAKINE karsilastirmasi, insan metni degil.
        // Kulture bagli karsilastirma Turkce yerelinde noktali/noktasiz I
        // yuzunden sasar.
        if (uzanti.Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase)) { return DosyaTuru.Parca; }
        if (uzanti.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase)) { return DosyaTuru.Montaj; }
        if (uzanti.Equals(".SLDDRW", StringComparison.OrdinalIgnoreCase)) { return DosyaTuru.TeknikResim; }
        if (uzanti.Equals(".PDF", StringComparison.OrdinalIgnoreCase)) { return DosyaTuru.Pdf; }

        return DosyaTuru.Bilinmeyen;
    }

    /// <summary>Turun ekranda gorunen adi.</summary>
    public static string Adi(DosyaTuru tur) => tur switch
    {
        DosyaTuru.Parca => "Parça",
        DosyaTuru.Montaj => "Montaj",
        DosyaTuru.TeknikResim => "Teknik resim",
        DosyaTuru.Pdf => "PDF",
        _ => "Dosya",
    };
}
