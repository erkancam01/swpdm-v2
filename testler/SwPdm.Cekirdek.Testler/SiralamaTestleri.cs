using System;
using System.Collections.Generic;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>Siralama. Klasorler HER ZAMAN once, olcut ne olursa olsun.</summary>
public class SiralamaTestleri
{
    private static DosyaOgesi D(string ad, long boyut, int gun)
        => new($@"C:\k\{ad}", ad, DosyaTurleri.Tani(ad), boyut, new DateTime(2026, 1, gun));

    private static List<DosyaOgesi> Ornek() =>
    [
        D("b.SLDPRT", 300, 3),
        D("a.SLDASM", 100, 1),
        D("c.pdf", 200, 2),
    ];

    [Fact]
    public void Ad_DOGAL_SIRA()
    {
        var dosyalar = new List<DosyaOgesi> { D("33.txt", 1, 1), D("2.txt", 1, 1), D("222.txt", 1, 1) };

        Siralama.Varsayilan.Uygula(dosyalar);

        Assert.Equal(["2.txt", "33.txt", "222.txt"], Adlar(dosyalar));
    }

    [Fact]
    public void Ad_AZALAN_TERSINE_DIZER()
    {
        // BU TEST BIR HATAYI YAKALADI: "Ad" olcutunde karsilastirma her zaman
        // 0 donuyordu ve yon HIC uygulanmiyordu - dugmede "Ad ↓" yaziyor,
        // agac artan duruyordu. Hicbir istisna yok, yalnizca yanlis sira.
        var dosyalar = new List<DosyaOgesi> { D("33.txt", 1, 1), D("2.txt", 1, 1), D("222.txt", 1, 1) };

        new Siralama(SiralamaOlcutu.Ad, Azalan: true).Uygula(dosyalar);

        Assert.Equal(["222.txt", "33.txt", "2.txt"], Adlar(dosyalar));
    }

    [Fact]
    public void Esitlik_ADLA_COZULUR_VE_ARTAN_KALIR()
    {
        // Boyutlar esit: yon AZALAN olsa bile esitlik bozucu ADA gore ARTAN
        // kalmali, yoksa ayni boyuttaki dosyalar her taramada yer degistirir.
        var dosyalar = new List<DosyaOgesi> { D("c.txt", 5, 1), D("a.txt", 5, 1), D("b.txt", 5, 1) };

        new Siralama(SiralamaOlcutu.Boyut, Azalan: true).Uygula(dosyalar);

        Assert.Equal(["a.txt", "b.txt", "c.txt"], Adlar(dosyalar));
    }

    [Fact]
    public void Boyut_Artan()
    {
        List<DosyaOgesi> dosyalar = Ornek();

        new Siralama(SiralamaOlcutu.Boyut, Azalan: false).Uygula(dosyalar);

        Assert.Equal(["a.SLDASM", "c.pdf", "b.SLDPRT"], Adlar(dosyalar));
    }

    [Fact]
    public void Boyut_Azalan()
    {
        List<DosyaOgesi> dosyalar = Ornek();

        new Siralama(SiralamaOlcutu.Boyut, Azalan: true).Uygula(dosyalar);

        Assert.Equal(["b.SLDPRT", "c.pdf", "a.SLDASM"], Adlar(dosyalar));
    }

    [Fact]
    public void Tarih_Azalan_EnYeniOnce()
    {
        List<DosyaOgesi> dosyalar = Ornek();

        new Siralama(SiralamaOlcutu.Tarih, Azalan: true).Uygula(dosyalar);

        Assert.Equal(["b.SLDPRT", "c.pdf", "a.SLDASM"], Adlar(dosyalar));
    }

    [Fact]
    public void ESIT_DEGERLER_ADLA_COZULUR()
    {
        // Ayni boyuttaki dosyalarin sirasi her taramada degisirse agac
        // gozun onunde oynar. Esitlik ADLA cozuluyor: sira KARARLI.
        var dosyalar = new List<DosyaOgesi> { D("z.txt", 5, 1), D("a.txt", 5, 1), D("m.txt", 5, 1) };

        new Siralama(SiralamaOlcutu.Boyut, Azalan: false).Uygula(dosyalar);

        Assert.Equal(["a.txt", "m.txt", "z.txt"], Adlar(dosyalar));
    }

    [Fact]
    public void KLASORLER_BOYUT_OLCUTUNDE_ADA_GORE_KALIR()
    {
        // Klasorun boyutu TARANMADAN bilinmiyor; uydurma bir siraya sokmak
        // yalan olurdu (CLAUDE.md 3).
        var klasorler = new List<KlasorOgesi>
        {
            new(@"C:\k\z", "z", null, null, null),
            new(@"C:\k\a", "a", null, null, null),
        };

        new Siralama(SiralamaOlcutu.Boyut, Azalan: true).Uygula(klasorler);

        Assert.Equal(["a", "z"], klasorler.ConvertAll(k => k.Ad));
    }

    [Fact]
    public void CozYaz_GIDIS_DONUS()
    {
        var sira = new Siralama(SiralamaOlcutu.Tarih, Azalan: true);

        Assert.Equal(sira, Siralama.Coz(sira.Yaz()));
    }

    [Fact]
    public void Coz_BOZUK_DEGERDE_VARSAYILANA_DONER()
    {
        Assert.Equal(Siralama.Varsayilan, Siralama.Coz("bu bozuk"));
        Assert.Equal(Siralama.Varsayilan, Siralama.Coz(null));
        Assert.Equal(Siralama.Varsayilan, Siralama.Coz(""));
    }

    private static List<string> Adlar(List<DosyaOgesi> dosyalar)
        => dosyalar.ConvertAll(d => d.Ad);
}
