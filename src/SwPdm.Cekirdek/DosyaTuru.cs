using System;
using System.Collections.Generic;

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

/// <summary>Bir dosya turunun tanimi: uzantisi ve ekranda gorunen adi.</summary>
/// <param name="Tur">Turun kendisi.</param>
/// <param name="Uzanti">Nokta dahil, buyuk harfle (".SLDPRT").</param>
/// <param name="Ad">Kullaniciya gosterilen ad.</param>
public readonly record struct TurTanimi(DosyaTuru Tur, string Uzanti, string Ad);

/// <summary>
/// TUR KAYDI - uygulamanin tanidigi dosya turlerinin TEK KAYNAGI.
///
/// CLAUDE.md 1b: yeni bir tur eklemek ya da bir turu kaldirmak
/// <see cref="Tumu"/> listesine BIR SATIR eklemek/silmektir. Simge listesi,
/// simge siralari ve suzgec seridi bu listeden TURETILIYOR; hicbirine ayrica
/// dokunulmaz.
///
/// Once boyle degildi ve olculdu (27.08.2026): yeni bir tur 4 dosyada 5 yere
/// satir ekletiyordu ve iki listenin ayni sirada olmasini yalnizca bir YORUM
/// SATIRI sagliyordu - kaysa hata sessizdi, yanlis simge cizilirdi.
/// </summary>
public static class DosyaTurleri
{
    /// <summary>
    /// Tanidigimiz turler. SIRA ONEMLI: suzgec seridindeki dugmeler ve
    /// simge listesi bu sirada uretiliyor, yani kullanicinin gordugu sira budur.
    ///
    /// Ayni tur birden fazla uzantiyla listelenebilir; ad ilk satirdan alinir.
    /// </summary>
    public static readonly IReadOnlyList<TurTanimi> Tumu =
    [
        new(DosyaTuru.Montaj, ".SLDASM", "Montaj"),
        new(DosyaTuru.Parca, ".SLDPRT", "Parça"),
        new(DosyaTuru.TeknikResim, ".SLDDRW", "Teknik resim"),
        new(DosyaTuru.Pdf, ".PDF", "PDF"),
    ];

    /// <summary>Dosya adindan turu belirler.</summary>
    public static DosyaTuru Tani(string? dosyaAdi)
    {
        string uzanti = WindowsYolu.Uzanti(dosyaAdi);
        if (uzanti.Length == 0)
        {
            return DosyaTuru.Bilinmeyen;
        }

        foreach (TurTanimi tanim in Tumu)
        {
            // Ordinal SART: bu bir MAKINE karsilastirmasi, insan metni degil.
            // Kulture bagli karsilastirma Turkce yerelinde noktali/noktasiz I
            // yuzunden sasar.
            if (uzanti.Equals(tanim.Uzanti, StringComparison.OrdinalIgnoreCase))
            {
                return tanim.Tur;
            }
        }

        return DosyaTuru.Bilinmeyen;
    }

    /// <summary>Turun ekranda gorunen adi.</summary>
    public static string Adi(DosyaTuru tur)
    {
        foreach (TurTanimi tanim in Tumu)
        {
            if (tanim.Tur == tur)
            {
                return tanim.Ad;
            }
        }

        return "Dosya";
    }

    /// <summary>
    /// Turun uzantisi (ilk satirdan). Kayitli degilse null - "kabuktan simge
    /// isteyecek bir uzantim yok" demek.
    /// </summary>
    public static string? Uzantisi(DosyaTuru tur)
    {
        foreach (TurTanimi tanim in Tumu)
        {
            if (tanim.Tur == tur)
            {
                return tanim.Uzanti;
            }
        }

        return null;
    }

    /// <summary>Kayitli turler, listedeki sirayla, her tur bir kez.</summary>
    public static IReadOnlyList<DosyaTuru> Turler()
    {
        var sonuc = new List<DosyaTuru>(Tumu.Count);
        foreach (TurTanimi tanim in Tumu)
        {
            if (!sonuc.Contains(tanim.Tur))
            {
                sonuc.Add(tanim.Tur);
            }
        }

        return sonuc;
    }
}
