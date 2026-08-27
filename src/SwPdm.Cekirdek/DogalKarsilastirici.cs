using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// Gezgin gibi siralar: sayilar SAYI olarak karsilastirilir.
///
///   duz metin siralamasi : 1, 2, 222, 33     (YANLIS gorunuyor)
///   dogal siralama       : 1, 2, 33, 222     (kullanicinin bekledigi)
///
/// Windows'un kendi islevi (StrCmpLogicalW) yalnizca Windows'ta var; ona
/// dayansaydik davranis Linux'ta OLCULEMEZDI ve fark kullanicinin makinesine
/// kalirdi (CLAUDE.md 4'un tam olarak uyardigi kalip). Bu yuzden kendimiz
/// yaziyoruz ve testleri her yerde kosuyor.
/// </summary>
public sealed class DogalKarsilastirici : IComparer<string>
{
    /// <summary>Paylasilan ornek; her yerde AYNI siralama (CLAUDE.md 8).</summary>
    public static readonly DogalKarsilastirici Ortak = new();

    /// <inheritdoc/>
    public int Compare(string? sol, string? sag)
    {
        if (ReferenceEquals(sol, sag)) { return 0; }
        if (sol is null) { return -1; }
        if (sag is null) { return 1; }

        int i = 0;
        int j = 0;

        while (i < sol.Length && j < sag.Length)
        {
            bool solRakam = char.IsDigit(sol[i]);
            bool sagRakam = char.IsDigit(sag[j]);

            if (solRakam && sagRakam)
            {
                int fark = SayilariKarsilastir(sol, ref i, sag, ref j);
                if (fark != 0) { return fark; }
            }
            else
            {
                int fark = MetinleriKarsilastir(sol, ref i, sag, ref j);
                if (fark != 0) { return fark; }
            }
        }

        if (i < sol.Length) { return 1; }
        if (j < sag.Length) { return -1; }

        // Tamamen esitse siralamanin kararli olmasi icin son bir olcut.
        return string.CompareOrdinal(sol, sag);
    }

    private static int SayilariKarsilastir(string sol, ref int i, string sag, ref int j)
    {
        int solBas = i;
        while (i < sol.Length && char.IsDigit(sol[i])) { i++; }

        int sagBas = j;
        while (j < sag.Length && char.IsDigit(sag[j])) { j++; }

        ReadOnlySpan<char> solSayi = BasSifirlariAt(sol.AsSpan(solBas, i - solBas));
        ReadOnlySpan<char> sagSayi = BasSifirlariAt(sag.AsSpan(sagBas, j - sagBas));

        // Bas sifirlar atilinca uzun olan buyuktur - tasma riski olmadan.
        if (solSayi.Length != sagSayi.Length)
        {
            return solSayi.Length - sagSayi.Length;
        }

        return solSayi.SequenceCompareTo(sagSayi);
    }

    private static int MetinleriKarsilastir(string sol, ref int i, string sag, ref int j)
    {
        int solBas = i;
        while (i < sol.Length && !char.IsDigit(sol[i])) { i++; }

        int sagBas = j;
        while (j < sag.Length && !char.IsDigit(sag[j])) { j++; }

        // Kulture bagli ve buyuk/kucuk harf duyarsiz: bu INSAN metni.
        // (Makine karsilastirmalari - uzanti, ayrilmis ad - Ordinal kalir.)
        return string.Compare(
            sol[solBas..i], sag[sagBas..j], StringComparison.CurrentCultureIgnoreCase);
    }

    private static ReadOnlySpan<char> BasSifirlariAt(ReadOnlySpan<char> sayi)
    {
        int k = 0;
        while (k < sayi.Length - 1 && sayi[k] == '0') { k++; }
        return sayi[k..];
    }
}
