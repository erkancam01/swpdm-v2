using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Beklenen degerler ELLE yazilmistir; Path'ten TURETILMEZ. Path'ten turetilse
/// Linux'ta yanlis cevabi dogrulardi - CLAUDE.md 4'un tam olarak uyardigi tuzak.
/// </summary>
public class WindowsYoluTestleri
{
    // ---------------------------------------------------------------
    // CLAUDE.md 4'te BELGELENMIS uc kirilma. Path bunlari Linux'ta yanlis
    // yapiyor; WindowsYolu her iki platformda dogru yapmali.
    // ---------------------------------------------------------------

    [Fact]
    public void Uzanti_ProjeAdindaNoktaVarsaBileDogru()
    {
        // Path Linux'ta ".0\parca" donduruyor.
        Assert.Equal("", WindowsYolu.Uzanti(@"C:\Proje 2.0\parca"));
        Assert.Equal(".SLDPRT", WindowsYolu.Uzanti(@"C:\Proje 2.0\parca.SLDPRT"));
    }

    [Fact]
    public void DosyaAdi_YolunTamaminiDegilSonParcayiDondurur()
    {
        // Path Linux'ta yolun TAMAMINI donduruyor.
        Assert.Equal("b.SLDPRT", WindowsYolu.DosyaAdi(@"C:\a\b.SLDPRT"));
    }

    [Fact]
    public void Klasor_UstKlasoruDondurur()
    {
        // Path Linux'ta "" donduruyor.
        Assert.Equal(@"C:\a", WindowsYolu.Klasor(@"C:\a\b.SLDPRT"));
    }

    // ---------------------------------------------------------------
    // SURUCU KOKU TUZAGI: "C:" donduren bir yardimci, birlestirildiginde
    // surucuye GORELI bir yol uretir ve dosyayi bambaska yere yazar.
    // ---------------------------------------------------------------

    [Fact]
    public void Klasor_SurucuKokundeTersBoluyuKORUR()
    {
        Assert.Equal(@"C:\", WindowsYolu.Klasor(@"C:\a.SLDPRT"));
        Assert.NotEqual("C:", WindowsYolu.Klasor(@"C:\a.SLDPRT"));
    }

    [Fact]
    public void Birlestir_SurucuKokuyleGoreliYolURETMEZ()
    {
        string kok = WindowsYolu.Klasor(@"C:\a.SLDPRT");
        Assert.Equal(@"C:\x", WindowsYolu.Birlestir(kok, "x"));
        Assert.NotEqual("C:x", WindowsYolu.Birlestir(kok, "x"));
    }

    // ---------------------------------------------------------------
    // v1'de ayni mantik DOKUZ yerde elle yazilmisti ve UC ayri bicimde
    // ayrismisti. Ucu de burada birer test.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BosGirdi_IstisnaATMAZ(string? yol)
    {
        // v1'de uc kopya burada NullReferenceException atiyordu.
        Assert.Equal("", WindowsYolu.DosyaAdi(yol));
        Assert.Equal("", WindowsYolu.Klasor(yol));
        Assert.Equal("", WindowsYolu.Uzanti(yol));
        Assert.Equal("", WindowsYolu.DosyaAdiUzantisiz(yol));
    }

    [Fact]
    public void SondakiAyiriciKIRPILIR()
    {
        // v1'de bir kismi kirpmiyordu.
        Assert.Equal("b", WindowsYolu.DosyaAdi(@"C:\a\b\"));
        Assert.Equal("b", WindowsYolu.DosyaAdi(@"C:\a\b\\\"));
        Assert.Equal(@"C:\a", WindowsYolu.Klasor(@"C:\a\b\"));
    }

    [Fact]
    public void EgikBoluDA_AyiriciSayilir()
    {
        // v1'de iki kopya '/' tanimiyordu.
        Assert.Equal("b.SLDPRT", WindowsYolu.DosyaAdi("C:/a/b.SLDPRT"));
        Assert.Equal("C:/a", WindowsYolu.Klasor("C:/a/b.SLDPRT"));
        Assert.Equal("b.SLDPRT", WindowsYolu.DosyaAdi(@"C:\a/b.SLDPRT"));
    }

    // ---------------------------------------------------------------
    // Kokler ve UNC. Atolyede dosyalar ag surucusunde duruyor.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\", "", "")]
    [InlineData(@"C:", "", "")]
    [InlineData(@"\", "", "")]
    [InlineData(@"\\sunucu\pay", "", "")]
    public void Kokun_UstuVeAdiYoktur(string yol, string beklenenAd, string beklenenKlasor)
    {
        Assert.Equal(beklenenAd, WindowsYolu.DosyaAdi(yol));
        Assert.Equal(beklenenKlasor, WindowsYolu.Klasor(yol));
    }

    [Fact]
    public void Unc_DogruAyristirilir()
    {
        Assert.Equal("montaj.SLDASM", WindowsYolu.DosyaAdi(@"\\10.34.1.250\ortak\montaj.SLDASM"));
        Assert.Equal(@"\\10.34.1.250\ortak", WindowsYolu.Klasor(@"\\10.34.1.250\ortak\montaj.SLDASM"));
        Assert.Equal(@"\\10.34.1.250\ortak", WindowsYolu.Klasor(@"\\10.34.1.250\ortak\alt\"));
    }

    // ---------------------------------------------------------------
    // Uzanti kurallari .NET'in kendi davranisindan OLCULMUSTUR.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("b.SLDPRT", ".SLDPRT", "b")]
    [InlineData("b", "", "b")]
    [InlineData("b.", "", "b")]
    [InlineData(".gitignore", ".gitignore", "")]
    [InlineData("a.b.c", ".c", "a.b")]
    [InlineData("a..b", ".b", "a.")]
    public void UzantiKurallari(string ad, string beklenenUzanti, string beklenenGovde)
    {
        Assert.Equal(beklenenUzanti, WindowsYolu.Uzanti(ad));
        Assert.Equal(beklenenGovde, WindowsYolu.DosyaAdiUzantisiz(ad));
    }

    // ---------------------------------------------------------------
    // Gecersiz adlar. CLAUDE.md 4: Path.GetInvalidFileNameChars() Linux'ta
    // yalnizca '/' ve '\0' donduruyor, yani testler gecersiz adi KABUL EDER.
    // Liste elle yazildi; asagisi onun kosulan karsiligi.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("Parça1.SLDPRT")]
    [InlineData("Montaj 2 (kopya).SLDASM")]
    [InlineData("ölçü-şablonu.SLDDRW")]
    public void GecerliAdlarKabulEdilir(string ad)
    {
        Assert.True(WindowsYolu.AdGecerliMi(ad, out string sebep), sebep);
    }

    [Theory]
    [InlineData("a<b.SLDPRT")]
    [InlineData("a>b.SLDPRT")]
    [InlineData("a:b.SLDPRT")]
    [InlineData("a\"b.SLDPRT")]
    [InlineData("a|b.SLDPRT")]
    [InlineData("a?b.SLDPRT")]
    [InlineData("a*b.SLDPRT")]
    [InlineData(@"a\b.SLDPRT")]
    [InlineData("a/b.SLDPRT")]
    [InlineData("bitiyor.")]
    [InlineData("bitiyor ")]
    [InlineData("")]
    public void GecersizAdlarSebebiyleReddedilir(string ad)
    {
        Assert.False(WindowsYolu.AdGecerliMi(ad, out string sebep));
        Assert.NotEqual("", sebep);   // sebep EKRANDA gosterilecek, bos olamaz
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("NUL.SLDPRT")]
    [InlineData("COM1")]
    [InlineData("lpt9.SLDDRW")]
    public void AyrilmisAygitAdlariReddedilir(string ad)
    {
        Assert.False(WindowsYolu.AdGecerliMi(ad, out string sebep));
        Assert.Contains("ayrılmış", sebep);
    }

    // ---------------------------------------------------------------------
    // GORELI YOL - referans onariminda ebeveynin icine yazilan deger.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\a\b", @"C:\a\b\p.SLDPRT", @".\p.SLDPRT")]
    [InlineData(@"C:\a\b", @"C:\a\b\alt\p.SLDPRT", @".\alt\p.SLDPRT")]
    [InlineData(@"C:\a\b", @"C:\a\c\p.SLDPRT", @"..\c\p.SLDPRT")]
    [InlineData(@"C:\a\b\c", @"C:\a\p.SLDPRT", @"..\..\p.SLDPRT")]
    public void Goreli_YOLU_EBEVEYNE_GORE_yaziyor(string temel, string hedef, string beklenen)
        => Assert.Equal(beklenen, WindowsYolu.Goreli(temel, hedef));

    [Fact]
    public void Goreli_BASKA_SURUCUDE_null_doner()
    {
        // Farkli kokte goreli yol YAZILAMAZ; uydurmak yanlis dosyaya
        // isaret ederdi (CLAUDE.md 3).
        Assert.Null(WindowsYolu.Goreli(@"C:\a", @"D:\a\p.SLDPRT"));
        Assert.Null(WindowsYolu.Goreli(null, @"C:\a\p.SLDPRT"));
    }
}
