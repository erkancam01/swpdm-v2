using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

public class DogalKarsilastiriciTestleri
{
    private static string[] Sirala(params string[] girdi)
    {
        string[] kopya = [.. girdi];
        System.Array.Sort(kopya, DogalKarsilastirici.Ortak);
        return kopya;
    }

    [Fact]
    public void Sayilar_SAYI_olarak_siralanir()
    {
        // Erkan'in ekranindaki gercek klasor adlari. Duz metin siralamasi
        // bunu "1, 2, 222, 33" yapiyordu.
        Assert.Equal(["1", "2", "33", "222"], Sirala("222", "33", "1", "2"));
    }

    [Fact]
    public void MetinIcindekiSayilarDA_sayi_olarak_siralanir()
    {
        Assert.Equal(
            ["Parça1.SLDPRT", "Parça2.SLDPRT", "Parça10.SLDPRT"],
            Sirala("Parça10.SLDPRT", "Parça1.SLDPRT", "Parça2.SLDPRT"));
    }

    [Fact]
    public void BastakiSifirlar_degeri_degistirmez()
    {
        Assert.Equal(["007", "8", "0010"], Sirala("0010", "007", "8"));
    }

    [Fact]
    public void BuyukKucukHarf_SIRAYI_belirlemez()
    {
        // Sirayi belirleyen sey buyuk/kucuk harf DEGIL: "MONTAJ10" yine
        // "montaj2"den SONRA gelir.
        Assert.Equal(["montaj2", "MONTAJ10"], Sirala("MONTAJ10", "montaj2"));

        // Ayni adin yalnizca harf buyuklugu farkliysa sira KARARLI olsun diye
        // son bir ordinal olcut var; yani Compare 0 DONMEZ. Bilincli:
        // 0 donen bir karsilastirici, iki farkli dosya adini "ayni" sayar ve
        // siralama koseden koseye ziplayabilir.
        Assert.NotEqual(0, DogalKarsilastirici.Ortak.Compare("montaj2", "MONTAJ2"));
        Assert.Equal(["MONTAJ2", "montaj2"], Sirala("montaj2", "MONTAJ2"));
    }

    [Fact]
    public void HarfVeSayiKarisik()
    {
        Assert.Equal(
            ["alt klasor", "ORJINAL", "Parça2"],
            Sirala("Parça2", "ORJINAL", "alt klasor"));
    }

    [Fact]
    public void BosVeNull_IstisnaATMAZ()
    {
        Assert.Equal(0, DogalKarsilastirici.Ortak.Compare(null, null));
        Assert.True(DogalKarsilastirici.Ortak.Compare(null, "a") < 0);
        Assert.True(DogalKarsilastirici.Ortak.Compare("a", null) > 0);
        Assert.True(DogalKarsilastirici.Ortak.Compare("", "a") < 0);
    }

    [Fact]
    public void CokUzunSayi_TASMAZ()
    {
        // long'a sigmayan sayilar. Uzunluk karsilastirmasi sayesinde
        // ayristirma yapilmadigi icin tasma riski yok.
        Assert.True(DogalKarsilastirici.Ortak.Compare(
            "a99999999999999999999999999", "a100000000000000000000000000") < 0);
    }
}
