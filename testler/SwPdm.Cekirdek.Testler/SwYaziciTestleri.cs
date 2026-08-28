using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// DOSYANIN ICINE YAZMA - gercek SOLIDWORKS 2022 dosyalarina karsi.
///
/// EN ONEMLI TEST <see cref="Yamalanan_dosya_YENI_ADI_REFERANS_OLARAK_VERIYOR"/>:
/// yaziciyi kendi okuyucumuzla dogruluyor. Yazma isleminin "oldum" demesi
/// hicbir sey ifade etmez (CLAUDE.md 2 - v1'de ReplaceViewModel true dondu
/// ve hicbir sey yapmadi); onemli olan sonucun DISKTEN yeniden okununca
/// degismis olmasi.
///
/// BURADA OLCULEMEYEN: SOLIDWORKS'un bu dosyayi ACIP ACMADIGI. Dosyanin ilk
/// 4 bayti her kayitta degisiyor ve sagalama toplami OLABILIR (sekiz standart
/// varyant denendi, 0/8 tuttu - ne oldugu bilinmiyor). Bu ancak Erkan'in
/// makinesinde olculur.
/// </summary>
public sealed class SwYaziciTestleri : IDisposable
{
    private static string Kok => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private readonly string _gecici;

    public SwYaziciTestleri()
    {
        _gecici = Path.Combine(Path.GetTempPath(), "swpdm-yazici-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_gecici);
    }

    public void Dispose()
    {
        try { Directory.Delete(_gecici, recursive: true); } catch (IOException) { }
    }

    private string Kaynak(string ad) => Path.Combine(Kok, ad);

    private string Hedef(string ad) => Path.Combine(_gecici, ad);

    [Fact]
    public void Yamalanan_dosya_YENI_ADI_REFERANS_OLARAK_VERIYOR()
    {
        string hedef = Hedef("Montaj1.SLDASM");
        YamaSonucu s = SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), hedef, "Parça1.SLDPRT", "Parca1.SLDPRT");

        Assert.True(s.Oldu, s.Sebep);
        Assert.True(s.DegisenDize > 0);

        // ASIL OLCUM: sonuc DISKTEN yeniden okunuyor.
        SwReferanslar r = SwReferans.Oku(hedef);
        Assert.True(r.Okundu, r.Sebep);

        var adlar = r.Dogrudan.Select(WindowsYolu.DosyaAdi).ToList();
        Assert.Contains("Parca1.SLDPRT", adlar);
        Assert.DoesNotContain("Parça1.SLDPRT", adlar);

        // Oteki referans BOZULMAMALI.
        Assert.Contains("Parça2.SLDPRT", adlar);
    }

    [Fact]
    public void Dosya_BOYUTU_DEGISMIYOR_hicbir_sey_kaymiyor()
    {
        // Yerinde yamanin butun guvenligi bu: yuva boyutu ayni kaliyor, yani
        // dosyanin bilmedigimiz %13'u de, her ofset de yerinde kaliyor.
        string kaynak = Kaynak("Montaj1.SLDASM");
        string hedef = Hedef("Montaj1.SLDASM");

        Assert.True(SwYazici.AdiDegistir(kaynak, hedef, "Parça1.SLDPRT", "Parca1.SLDPRT").Oldu);

        Assert.Equal(new FileInfo(kaynak).Length, new FileInfo(hedef).Length);
    }

    [Fact]
    public void KAYNAGA_DOKUNULMUYOR()
    {
        string kaynak = Kaynak("Montaj1.SLDASM");
        byte[] once = File.ReadAllBytes(kaynak);

        SwYazici.AdiDegistir(kaynak, Hedef("k.SLDASM"), "Parça1.SLDPRT", "Parca1.SLDPRT");

        Assert.Equal(once, File.ReadAllBytes(kaynak));
    }

    [Fact]
    public void Teknik_resmin_MODELI_de_degistirilebiliyor()
    {
        string hedef = Hedef("Parça1.SLDDRW");
        YamaSonucu s = SwYazici.AdiDegistir(
            Kaynak("Parça1.SLDDRW"), hedef, "Parça1.SLDPRT", "Parca1.SLDPRT");

        Assert.True(s.Oldu, s.Sebep);
        Assert.Contains(
            "Parca1.SLDPRT", SwReferans.Oku(hedef).Dogrudan.Select(WindowsYolu.DosyaAdi));
    }

    [Fact]
    public void YALNIZ_HEADER2_ESKISINI_GERIDE_BIRAKIYOR_ve_bunu_SOYLUYOR()
    {
        // OLCULDU (28.08.2026): ayni yol "Header2" ile
        // "Contents/Config-0-ModelHeader" akislarinda BIREBIR AYNI iceriklerle
        // duruyor. Yani "yalniz Header2" yamasi eskisini geride birakir.
        // Bu bir hata degil ama SESSIZ KALMAK yalan olurdu (CLAUDE.md 3):
        // KalanAkislar bunu adiyla sayiyor.
        YamaSonucu hepsi = SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), Hedef("hepsi.SLDASM"), "Parça1.SLDPRT", "Parca1.SLDPRT");
        YamaSonucu tek = SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), Hedef("tek.SLDASM"), "Parça1.SLDPRT", "Parca1.SLDPRT",
            yalnizDogrudan: true);

        Assert.True(hepsi.Oldu, hepsi.Sebep);
        Assert.Empty(hepsi.KalanAkislar);

        Assert.True(tek.Oldu, tek.Sebep);
        Assert.Equal(1, tek.DegisenAkis);
        Assert.NotEmpty(tek.KalanAkislar);
        Assert.Contains("Contents/Config-0-ModelHeader", tek.KalanAkislar);
        Assert.True(hepsi.DegisenAkis > tek.DegisenAkis);
    }

    [Fact]
    public void OLMAYAN_AD_icin_SEBEP_yaziliyor_sessiz_basari_yok()
    {
        YamaSonucu s = SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), Hedef("y.SLDASM"), "YokBoyleBirSey.SLDPRT", "X.SLDPRT");

        Assert.False(s.Oldu);
        Assert.False(string.IsNullOrWhiteSpace(s.Sebep));
    }

    [Fact]
    public void SIGMAYAN_AD_REDDEDILIYOR_ve_SEBEBI_yaziliyor()
    {
        // Yazili yolun uzunlugu korunamiyorsa yazmiyoruz. Uydurma bir yol
        // yazmaktansa "yapamadim" demek dogru (CLAUDE.md 3).
        string uzun = new string('u', 250) + ".SLDPRT";
        YamaSonucu s = SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), Hedef("u.SLDASM"), "Parça1.SLDPRT", uzun);

        Assert.False(s.Oldu);
        Assert.Contains("uzunluğu korunamıyor", s.Sebep);
    }

    [Fact]
    public void MFC_254_karakter_sinirini_asan_dize_YAZILMIYOR()
    {
        // Kacis bicimi GORULMEDI; tahminle yazmak dosya bozardi (CLAUDE.md 2).
        Assert.Null(MfcDize.Yaz(new string('u', 255)));
        Assert.NotNull(MfcDize.Yaz(new string('u', 254)));
    }

    [Fact]
    public void AYNI_AD_ve_BOS_GIRDI_reddediliyor()
    {
        Assert.False(SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), Hedef("a.SLDASM"), "Parça1.SLDPRT", "Parça1.SLDPRT").Oldu);
        Assert.False(SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), Hedef("b.SLDASM"), "", "X.SLDPRT").Oldu);
    }

    [Fact]
    public void Yamalanan_dosya_hala_PAKET_olarak_okunuyor()
    {
        string hedef = Hedef("Montaj1.SLDASM");
        Assert.True(SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), hedef, "Parça1.SLDPRT", "Parca1.SLDPRT").Oldu);

        using SwPaket? once = SwPaket.Ac(Kaynak("Montaj1.SLDASM"));
        using SwPaket? sonra = SwPaket.Ac(hedef);

        Assert.NotNull(once);
        Assert.NotNull(sonra);

        // Akis SAYISI ve ADLARI birebir ayni kalmali: yalnizca bir yuvanin
        // ICI degisti, kabin yapisi degil.
        Assert.Equal(once!.Akislar.Count, sonra!.Akislar.Count);
        Assert.Equal(
            once.Akislar.Select(a => a.Ad).ToList(),
            sonra.Akislar.Select(a => a.Ad).ToList());
        Assert.Equal(
            once.Akislar.Select(a => a.VeriBaslangici).ToList(),
            sonra.Akislar.Select(a => a.VeriBaslangici).ToList());
    }

    [Fact]
    public void Onizleme_ve_ozellikler_BOZULMUYOR()
    {
        string hedef = Hedef("Montaj1.SLDASM");
        Assert.True(SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), hedef, "Parça1.SLDPRT", "Parca1.SLDPRT").Oldu);

        Assert.NotNull(SwOnizleme.Oku(hedef));
        Assert.True(SwBelgeBilgisi.Oku(hedef).Okundu);
    }

    // ---------------------------------------------------------------------
    // UZUNLUK KORUNAN YOL
    //
    // OLCULDU (Erkan, 28.08.2026): ayni uzunluktaki ad degisimi ACILIYOR,
    // daha uzun ad HATA VERIYOR. O yuzden yazilan dizenin karakter sayisi
    // hic degismemeli; fark klasor kismindan karsilaniyor.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\a\Parça1.SLDPRT", "Parca1.SLDPRT")]           // ayni uzunluk
    [InlineData(@"C:\Users\PC\Desktop\t\Parça1.SLDPRT", "P1.SLDPRT")]   // kisaldi
    [InlineData(@"C:\Users\PC\Desktop\t\Parça1.SLDPRT", "GövdeParçası1.SLDPRT")]  // uzadi
    [InlineData(@"C:\Users\PC\Desktop\t\Parça1.SLDPRT", "Ab.SLDPRT")]   // tek karakter fark
    public void AdDegisimi_UZUNLUGU_AYNEN_KORUYOR(string eski, string yeniAd)
    {
        string? yeni = YazilacakYol.AdDegisimi(eski, yeniAd);

        Assert.NotNull(yeni);
        Assert.Equal(eski.Length, yeni!.Length);
        Assert.Equal(yeniAd, WindowsYolu.DosyaAdi(yeni));
    }

    [Fact]
    public void AdDegisimi_ad_tek_basina_uzunsa_NULL_doner()
    {
        // Uydurma bir yol yazmaktansa "yapamadim" demek dogru (CLAUDE.md 3).
        Assert.Null(YazilacakYol.AdDegisimi(@"C:\a\b.SLDPRT", new string('u', 40) + ".SLDPRT"));
    }

    [Fact]
    public void Uzun_ad_icin_yama_ARTIK_YAPILABILIYOR()
    {
        string hedef = Hedef("Montaj1.SLDASM");
        YamaSonucu s = SwYazici.AdiDegistir(
            Kaynak("Montaj1.SLDASM"), hedef, "Parça1.SLDPRT", "GövdeParçası1.SLDPRT");

        Assert.True(s.Oldu, s.Sebep);
        Assert.Contains(
            "GövdeParçası1.SLDPRT",
            SwReferans.Oku(hedef).Dogrudan.Select(WindowsYolu.DosyaAdi));

        // ASIL SART: yazilan dizenin uzunlugu degismemis olmali.
        using SwPaket? once = SwPaket.Ac(Kaynak("Montaj1.SLDASM"));
        using SwPaket? sonra = SwPaket.Ac(hedef);
        Assert.Equal(
            once!.Akislar.Select(a => a.AcilmisBoyut).ToList(),
            sonra!.Akislar.Select(a => a.AcilmisBoyut).ToList());
    }
}
