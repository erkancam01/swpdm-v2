using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Ortak ImageList icindeki simge siralari. Sayi TEK yerde durur;
/// CLAUDE.md 8: ayni bilginin ikinci kopyasi yazilmaz.
/// </summary>
internal static class SimgeSirasi
{
    internal const int Klasor = 0;
    internal const int Parca = 1;
    internal const int Montaj = 2;
    internal const int TeknikResim = 3;
    internal const int Pdf = 4;
    internal const int Dosya = 5;

    /// <summary>Cekirdegin tur bilgisini simge sirasina cevirir.</summary>
    internal static int Turden(DosyaTuru tur) => tur switch
    {
        DosyaTuru.Parca => Parca,
        DosyaTuru.Montaj => Montaj,
        DosyaTuru.TeknikResim => TeknikResim,
        DosyaTuru.Pdf => Pdf,
        _ => Dosya,
    };
}
