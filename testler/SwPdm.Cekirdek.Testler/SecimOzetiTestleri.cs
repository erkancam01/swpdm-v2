using System;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Coklu secim ozetinin cumlesi. Buradaki asil sinav CLAUDE.md 3:
/// klasor secildiginde toplam boyut TAM DEGIL ve bu SOYLENMEK zorunda -
/// eksik bir toplami tam gibi gostermek kullaniciya yanlis karar verdirir.
/// </summary>
public class SecimOzetiTestleri
{
    private static DosyaOgesi Dosya(string ad, long boyut)
        => new($@"C:\k\{ad}", ad, DosyaTurleri.Tani(ad), boyut, new DateTime(2026, 8, 27));

    private static KlasorOgesi Klasor(string ad)
        => new($@"C:\k\{ad}", ad, null, null, null);

    [Fact]
    public void Hesapla_BosSecim_SifirVeCumleSecimYok()
    {
        SecimOzeti ozet = SecimOzeti.Hesapla([]);

        Assert.Equal(0, ozet.Toplam);
        Assert.Equal("Seçim yok", ozet.Yaz());
    }

    [Fact]
    public void Hesapla_TanimadigimizEtiketleriSaymaz()
    {
        // Agacta "henuz taranmadi" yer tutucu dugumler de var; onlar oge degil.
        SecimOzeti ozet = SecimOzeti.Hesapla([new object(), null, Dosya("a.SLDPRT", 10)]);

        Assert.Equal(1, ozet.DosyaSayisi);
        Assert.Equal(0, ozet.KlasorSayisi);
    }

    [Fact]
    public void Hesapla_YalnizDosyalar_ToplamBoyutTAM()
    {
        SecimOzeti ozet = SecimOzeti.Hesapla(
            [Dosya("a.SLDPRT", 1000), Dosya("b.SLDASM", 24)]);

        Assert.Equal(2, ozet.DosyaSayisi);
        Assert.Equal(1024, ozet.ToplamBoyut);
        Assert.True(ozet.BoyutTam);
        Assert.Equal("2 dosya seçildi  ·  " + Boyut.Yaz(1024), ozet.Yaz());
    }

    [Fact]
    public void Hesapla_KlasorDeVarsa_BoyutTAM_DEGIL_VE_BUNU_SOYLER()
    {
        SecimOzeti ozet = SecimOzeti.Hesapla([Dosya("a.SLDPRT", 500), Klasor("alt")]);

        Assert.False(ozet.BoyutTam);
        string cumle = ozet.Yaz();

        Assert.Contains("1 dosya", cumle, StringComparison.Ordinal);
        Assert.Contains("1 klasör", cumle, StringComparison.Ordinal);

        // CLAUDE.md 3: bilinmeyen SOYLENIR. Bu cumle olmazsa kullanici
        // 500 bayti secimin TAMAMI sanir.
        Assert.Contains("klasörlerin içi taranmadı", cumle, StringComparison.Ordinal);
    }

    [Fact]
    public void Hesapla_YalnizKlasor_HicBOYUT_YAZMAZ()
    {
        SecimOzeti ozet = SecimOzeti.Hesapla([Klasor("a"), Klasor("b")]);

        // Sifir bayt yazmak "bu klasorler bos" demektir - bilmiyoruz.
        Assert.Equal("2 klasör seçildi", ozet.Yaz());
    }

    [Fact]
    public void Hesapla_NullListe_Patlar()
        => Assert.Throws<ArgumentNullException>(() => SecimOzeti.Hesapla(null!));
}
