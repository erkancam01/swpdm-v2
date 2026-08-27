using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// GERCEK klasorlerle kosuyor. Cekirdek net8.0 oldugu icin bu olcum Linux'ta
/// da yapilabiliyor - v1'de olculemeyen alanin bedeli CLAUDE.md 7'de duruyor.
/// </summary>
public sealed class KlasorTarayiciTestleri : IDisposable
{
    private readonly string _kok;

    public KlasorTarayiciTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_kok);
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, recursive: true); } catch (IOException) { }
    }

    private string Klasor(params string[] parcalar)
    {
        string yol = Path.Combine([_kok, .. parcalar]);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private string Dosya(string goreceli, int bayt = 3)
    {
        string yol = Path.Combine(_kok, goreceli);
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        File.WriteAllBytes(yol, new byte[bayt]);
        return yol;
    }

    // ---------------------------------------------------------------- Tara

    [Fact]
    public void Tara_KlasorleriVeDosyalariAyriDondurur()
    {
        Klasor("alt1");
        Klasor("alt2");
        Dosya("Parca1.SLDPRT");
        Dosya("Montaj1.SLDASM");

        KlasorIcerigi icerik = KlasorTarayici.Tara(_kok);

        Assert.Null(icerik.Hata);
        Assert.Equal(2, icerik.Klasorler.Count);
        Assert.Equal(2, icerik.Dosyalar.Count);
    }

    [Fact]
    public void Tara_TUM_dosyalari_gosterir_tanimadiklarini_da()
    {
        Dosya("Parca1.SLDPRT");
        Dosya("notlar.txt");
        Dosya("olcum.step");
        Dosya("uzantisiz");

        KlasorIcerigi icerik = KlasorTarayici.Tara(_kok);

        Assert.Equal(4, icerik.Dosyalar.Count);
        Assert.Contains(icerik.Dosyalar, d => d.Ad == "notlar.txt" && d.Tur == DosyaTuru.Bilinmeyen);
        Assert.Contains(icerik.Dosyalar, d => d.Ad == "uzantisiz");
    }

    [Fact]
    public void Tara_SOLIDWORKS_kilit_dosyasini_GIZLEMEZ()
    {
        // CLAUDE.md 4/5: "~$" kilit dosyalari gorunmez oldugu icin Windows
        // "dizin bos degil" diyor ve kullanici sebebini goremiyor. Gizlemek
        // tam olarak o karanligi uretirdi.
        Dosya("Parca1.SLDPRT");
        Dosya("~$Parca1.SLDPRT");

        KlasorIcerigi icerik = KlasorTarayici.Tara(_kok);

        Assert.Equal(2, icerik.Dosyalar.Count);
        Assert.Contains(icerik.Dosyalar, d => d.Ad == "~$Parca1.SLDPRT");
    }

    [Fact]
    public void Tara_KlasorDosyaSayisiniVeAltKlasorVarligiDogruSayar()
    {
        Klasor("dolu");
        Dosya("dolu/a.SLDPRT");
        Dosya("dolu/b.SLDPRT");
        Klasor("dolu/derin");
        Klasor("bos");

        KlasorIcerigi icerik = KlasorTarayici.Tara(_kok);
        KlasorOgesi dolu = icerik.Klasorler.Single(k => k.Ad == "dolu");
        KlasorOgesi bos = icerik.Klasorler.Single(k => k.Ad == "bos");

        Assert.Equal(2, dolu.DosyaSayisi);
        Assert.True(dolu.AltKlasorVarMi);
        Assert.Equal(0, bos.DosyaSayisi);
        Assert.False(bos.AltKlasorVarMi);
    }

    [Fact]
    public void Tara_OlmayanKlasor_SEBEBIYLE_bildirir()
    {
        KlasorIcerigi icerik = KlasorTarayici.Tara(Path.Combine(_kok, "boyle-bir-yer-yok"));

        // CLAUDE.md 3: bos liste "yok" demek DEGILDIR. Sebep dolu olmali.
        Assert.NotNull(icerik.Hata);
        Assert.NotEqual("", icerik.Hata);
        Assert.Empty(icerik.Dosyalar);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Tara_BosGirdi_IstisnaATMAZ(string? yol)
    {
        KlasorIcerigi icerik = KlasorTarayici.Tara(yol);
        Assert.NotNull(icerik.Hata);
        Assert.Empty(icerik.Klasorler);
    }

    [Fact]
    public void Tara_SiraliDondurur()
    {
        Dosya("ccc.SLDPRT");
        Dosya("aaa.SLDPRT");
        Dosya("bbb.SLDPRT");

        KlasorIcerigi icerik = KlasorTarayici.Tara(_kok);

        Assert.Equal(["aaa.SLDPRT", "bbb.SLDPRT", "ccc.SLDPRT"], icerik.Dosyalar.Select(d => d.Ad));
    }

    // ---------------------------------------------------------------- Ara

    [Fact]
    public void Ara_AltKlasorlereDE_iner()
    {
        Dosya("a/b/c/DerinParca.SLDPRT");
        Dosya("baska.SLDPRT");

        AramaSonucu sonuc = KlasorTarayici.Ara(_kok, "derin", 100);

        Assert.Single(sonuc.Bulunanlar);
        Assert.Equal("DerinParca.SLDPRT", sonuc.Bulunanlar[0].Ad);
        Assert.False(sonuc.Iptal);
        Assert.False(sonuc.SinirAsildi);
    }

    [Fact]
    public void Ara_BuyukKucukHarfAyirmaz()
    {
        Dosya("PARÇA1.SLDPRT");

        Assert.Single(KlasorTarayici.Ara(_kok, "parça", 100).Bulunanlar);
        Assert.Single(KlasorTarayici.Ara(_kok, "PARÇA", 100).Bulunanlar);
    }

    [Fact]
    public void Ara_SinirAsilirsa_SOYLER_sessizce_kirpmaz()
    {
        for (int i = 0; i < 10; i++)
        {
            Dosya($"parca{i}.SLDPRT");
        }

        AramaSonucu sonuc = KlasorTarayici.Ara(_kok, "parca", enFazla: 3);

        // CLAUDE.md 9: sessiz kirpma "hepsini kapsadim" gibi okunur.
        Assert.True(sonuc.SinirAsildi);
        Assert.Equal(3, sonuc.Bulunanlar.Count);
    }

    [Fact]
    public void Ara_IptalEdilirse_IPTAL_bayragiyla_doner()
    {
        Dosya("a/b/parca.SLDPRT");
        using var kaynak = new CancellationTokenSource();
        kaynak.Cancel();

        AramaSonucu sonuc = KlasorTarayici.Ara(_kok, "parca", 100, kaynak.Token);

        Assert.True(sonuc.Iptal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ara_BosMetin_hicbirseyDondurmez(string? metin)
    {
        Dosya("parca.SLDPRT");
        AramaSonucu sonuc = KlasorTarayici.Ara(_kok, metin, 100);
        Assert.Empty(sonuc.Bulunanlar);
    }

    [Fact]
    public void Ara_OkunamayanKlasoru_SESSIZCE_atlamaz()
    {
        Dosya("parca.SLDPRT");
        AramaSonucu sonuc = KlasorTarayici.Ara(_kok, "parca", 100);

        // Bu ortamda okunamayan klasor uretemiyoruz (testler root olarak
        // kosuyor, chmod 000 bile engellemiyor). Olculen: alanin VAR oldugu ve
        // saglikli durumda BOS kaldigi. Gercek engel Windows'ta olcuecek.
        Assert.Empty(sonuc.OkunamayanKlasorler);
        Assert.True(sonuc.TarananKlasor >= 1);
    }

    // ---------------------------------------------------------------- tur / boyut

    [Theory]
    [InlineData("a.SLDPRT", DosyaTuru.Parca)]
    [InlineData("a.sldprt", DosyaTuru.Parca)]
    [InlineData("a.SLDASM", DosyaTuru.Montaj)]
    [InlineData("a.SLDDRW", DosyaTuru.TeknikResim)]
    [InlineData("a.pdf", DosyaTuru.Pdf)]
    [InlineData("a.txt", DosyaTuru.Bilinmeyen)]
    [InlineData("a", DosyaTuru.Bilinmeyen)]
    [InlineData(null, DosyaTuru.Bilinmeyen)]
    public void DosyaTuru_UzantidanTanir(string? ad, DosyaTuru beklenen)
        => Assert.Equal(beklenen, DosyaTurleri.Tani(ad));

    [Fact]
    public void Boyut_TekYerdenYazilir()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Assert.Equal("0 B", Boyut.Yaz(0));
        Assert.Equal("512 B", Boyut.Yaz(512));
        Assert.Equal("1.0 KB", Boyut.Yaz(1024));
        Assert.Equal("81 KB", Boyut.Yaz(83000));
        Assert.Equal("1.0 MB", Boyut.Yaz(1024 * 1024));
        Assert.Equal("?", Boyut.Yaz(-1));
    }
}
