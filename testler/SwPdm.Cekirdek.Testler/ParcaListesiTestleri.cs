using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// PARCA LISTESI (BOM) - gercek SOLIDWORKS dosyalariyla.
///
/// Bu listeye bakip TEKLIF veriliyor: eksik bir satir eksik fiyat, yanlis
/// bir "kac yerde geciyor" yanlis adet demektir. O yuzden olculen sey her
/// zaman DISKTEKI gercek dosyalar - uydurma icerikle degil.
/// </summary>
public sealed class ParcaListesiTestleri : IDisposable
{
    private readonly string _kok;

    public ParcaListesiTestleri()
    {
        _kok = Path.Combine(
            Path.GetTempPath(), "swpdm-bom-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_kok);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_kok, recursive: true);
        }
        catch (IOException)
        {
            // Temizlik tutmazsa sonucu degistirmez.
        }
    }

    private static string Ornek(string altYol)
        => Path.Combine(AppContext.BaseDirectory, "veri", altYol);

    /// <summary>Ornek dosyayi koke (istenirse baska adla) koyar.</summary>
    private string Koy(string kaynakAltYol, string? yeniAd = null)
    {
        string ad = yeniAd ?? Path.GetFileName(kaynakAltYol);
        string yol = WindowsYolu.Birlestir(_kok, ad);
        Directory.CreateDirectory(WindowsYolu.Klasor(yol));
        File.Copy(Ornek(kaynakAltYol), yol);
        return yol;
    }

    /// <summary>
    /// Ornek dosyayi kokun ALTINDAKI bir klasore koyar. ADIM ADIM
    /// birlestiriliyor: Birlestir'in ayiricisi SOLDAKI yola bakiyor, adin
    /// icine ayirici gomulmus bir parca Linux'ta TEK dosya adi olurdu.
    /// </summary>
    private string KoyAlta(string kaynakAltYol, string klasor)
    {
        string hedefKlasor = WindowsYolu.Birlestir(_kok, klasor);
        Directory.CreateDirectory(hedefKlasor);
        string yol = WindowsYolu.Birlestir(hedefKlasor, Path.GetFileName(kaynakAltYol));
        File.Copy(Ornek(kaynakAltYol), yol);
        return yol;
    }

    private static ParcaSatiri Satir(ParcaListesiSonucu sonuc, string ad)
        => sonuc.Satirlar.First(s => s.Ad == ad);

    // ---------------------------------------------------------------------

    [Fact]
    public void MONTAJIN_AGACI_SEVIYE_SEVIYE_cikiyor()
    {
        Koy("tertemiz/Parça1.SLDPRT");
        Koy("tertemiz/Yeni klasör/Parça2.SLDPRT");
        string montaj = Koy("tertemiz/Montaj1.SLDASM");

        ParcaListesiSonucu sonuc = ParcaListesi.Uret(montaj);

        Assert.True(sonuc.Tam);
        Assert.Equal(0, sonuc.Sorunlu);
        Assert.Equal(0, Satir(sonuc, "Montaj1.SLDASM").Seviye);
        Assert.Equal(1, Satir(sonuc, "Parça1.SLDPRT").Seviye);
        Assert.Equal(1, Satir(sonuc, "Parça2.SLDPRT").Seviye);
        Assert.Equal(DosyaTuru.Montaj, Satir(sonuc, "Montaj1.SLDASM").Tur);
        Assert.Equal(DosyaTuru.Parca, Satir(sonuc, "Parça1.SLDPRT").Tur);
    }

    [Fact]
    public void ALT_KLASORDEKI_parca_BAYAT_MUTLAK_yola_ragmen_BULUNUYOR()
    {
        // KAPI YAKALADI (31.08.2026, 22. olcum): Montaj1'in icinde
        // "C:\Users\PC\Desktop\tertemiz\Yeni klasör\Parça2.SLDPRT"
        // yaziyor - baska bir makinenin yolu. Parca gercekte montajin
        // yanindaki "Yeni klasör" altinda duruyor ama komsuluk kurali yalniz
        // dosya ADINA baktigi icin ALT KLASORDEKINI gormuyordu: liste diskte
        // DURAN bir parcayi "bulunamadi" gosterdi. Boyle bir listeye bakan
        // biri o parcayi FIYATLAMAZ (CLAUDE.md 3).
        Koy("tertemiz/Parça1.SLDPRT");
        KoyAlta("tertemiz/Yeni klasör/Parça2.SLDPRT", "Yeni klasör");
        string montaj = Koy("tertemiz/Montaj1.SLDASM");

        ParcaListesiSonucu sonuc = ParcaListesi.Uret(montaj);

        ParcaSatiri parca2 = Satir(sonuc, "Parça2.SLDPRT");
        Assert.True(parca2.Bulundu, parca2.Durum ?? "bulunamadı");
        Assert.Contains("Yeni klasör", parca2.Yol, StringComparison.Ordinal);
        Assert.Equal(0, sonuc.Sorunlu);

        // AYNI DELIK VERSIYON ARSIVINDE DE VARDI: o parca arsive hic
        // girmiyordu ve montajin versiyonu SOLIDWORKS'te acilmazdi.
        CocukKumesi cocuklar = Surumler.Cocuklari(montaj);
        Assert.Equal(0, cocuklar.Cozulemeyen);
        Assert.Contains(cocuklar.Yollar, y => y.EndsWith("Parça2.SLDPRT", StringComparison.Ordinal));
    }

    [Fact]
    public void COZULEMEYEN_COCUK_SATIRDA_KALIR_ve_SEBEBI_yazar()
    {
        // BOS LISTE "COCUK YOK" DEMEK DEGIL (CLAUDE.md 3). Bulunamayan bir
        // parcayi listeden dusurmek, teklifte o parcanin hic olmadigini
        // gostermek olurdu - kullanici eksik fiyat verir ve BUNU HIC GORMEZ.
        string montaj = Koy("tertemiz/Montaj1.SLDASM");   // parcalari KOYULMADI

        ParcaListesiSonucu sonuc = ParcaListesi.Uret(montaj);

        Assert.Equal(3, sonuc.Satirlar.Count);       // montaj + iki bulunamayan
        Assert.Equal(2, sonuc.Sorunlu);

        ParcaSatiri eksik = sonuc.Satirlar.First(s => !s.Bulundu);
        Assert.NotNull(eksik.Durum);
        Assert.Contains("bulunamadı", eksik.Durum!, StringComparison.Ordinal);

        // Yolu DOSYADA YAZAN yol olmali: kullanici neyin arandigini gormeli.
        Assert.Contains("SLD", eksik.Yol, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OZEL_OZELLIKLER_SATIRA_ve_SUTUNA_giriyor()
    {
        // veri/ozellikli/Parça1.SLDPRT: Erkan elle girdi - Malzeme "Pirinç".
        string parca = Koy("ozellikli/Parça1.SLDPRT");

        ParcaListesiSonucu sonuc = ParcaListesi.Uret(parca);

        Assert.Single(sonuc.Satirlar);
        Assert.Equal("Pirinç", Satir(sonuc, "Parça1.SLDPRT").Ozel["Malzeme"]);
        Assert.Contains("Malzeme", sonuc.OzelSutunlar);
        Assert.Contains("Ağırlık", sonuc.OzelSutunlar);
    }

    [Fact]
    public void SUTUNLAR_TURETILIYOR_dosyada_olmayan_sutun_ACILMIYOR()
    {
        // CLAUDE.md 1b: sabit bir sutun listesi YOK. Ozelligi olmayan bir
        // dosyada hic sutun acilmamali - bos "Malzeme" sutunu, o parcanin
        // malzemesi girilmemis gibi degil, GIRILEMEZ gibi okunurdu.
        string sade = Koy("tertemiz/Parça1.SLDPRT");

        ParcaListesiSonucu sonuc = ParcaListesi.Uret(sade);

        Assert.Empty(sonuc.OzelSutunlar);
    }

    [Fact]
    public void KAC_YERDE_GECIYOR_iki_ebeveynli_parcada_IKI()
    {
        // IKI EBEVEYN KURULUYOR: agac yuruyusu dosyanin ICINI okuyor, adina
        // bakmiyor - o yuzden Montaj1'in bir kopyasi "Parça2.SLDPRT" adiyla
        // konuyor. Montaj1'in ikinci referansi ("Yeni klasör\Parça2.SLDPRT")
        // KOMSULUK kuraliyla (CLAUDE.md 5) bu kopyaya cozulur; kopya da
        // Parça1'i referans verir. Sonuc: Parça1'in IKI ayri ebeveyni.
        Koy("tertemiz/Parça1.SLDPRT");
        Koy("tertemiz/Montaj1.SLDASM", "Parça2.SLDPRT");
        string montaj = Koy("tertemiz/Montaj1.SLDASM");

        ParcaListesiSonucu sonuc = ParcaListesi.Uret(montaj);

        Assert.Equal(2, Satir(sonuc, "Parça1.SLDPRT").KacYerde);

        // Kok satirinda sayi YOK: secilen belge bir yerde "geciyor" degil.
        Assert.Equal(0, Satir(sonuc, "Montaj1.SLDASM").KacYerde);

        // Ikinci gorunus SESSIZ GECILMEZ: alt agacinin bir kez acildigi
        // yaziyor (CLAUDE.md 3).
        Assert.Equal(2, sonuc.Satirlar.Count(s => s.Ad == "Parça1.SLDPRT"));
        Assert.Contains(
            sonuc.Satirlar,
            s => s.Ad == "Parça1.SLDPRT" && s.Durum is not null
                 && s.Durum.Contains("Yukarıda", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // CSV
    // ---------------------------------------------------------------------

    private static ParcaListesiSonucu Uydur(params ParcaSatiri[] satirlar)
        => new(satirlar, ["Malzeme"], 0, Tam: true, Sebep: null);

    private static ParcaSatiri Duz(string ad, string malzeme)
        => new(
            0, ad, @"C:\a\" + ad, Bulundu: true, DosyaTuru.Parca, 0,
            null, null, null,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Malzeme"] = malzeme },
            null);

    [Fact]
    public void CSV_AYRACI_NOKTALI_VIRGUL_ve_ilk_satir_UYARI()
    {
        string metin = ParcaListesiCsv.Metin(Uydur(Duz("Parça1.SLDPRT", "Pirinç")));
        string[] satirlar = metin.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("NOT:", satirlar[0], StringComparison.Ordinal);
        Assert.StartsWith("Seviye;Ad;Tür;", satirlar[1], StringComparison.Ordinal);
        Assert.Contains("Malzeme", satirlar[1], StringComparison.Ordinal);
        Assert.Contains("Pirinç", satirlar[2], StringComparison.Ordinal);
    }

    [Fact]
    public void CSV_AYRAC_ICEREN_DEGER_TEK_HUCREDE_kalir()
    {
        // Kacislama olmazsa bir malzeme adindaki ";" satiri ikiye boler ve
        // BUTUN sutunlar kayar - hicbir hata vermeden.
        string metin = ParcaListesiCsv.Metin(Uydur(Duz("Parça1.SLDPRT", "Pirinç; kaplama \"A\"")));
        string satir = metin.Split("\r\n")[2];

        Assert.Contains("\"Pirinç; kaplama \"\"A\"\"\"", satir, StringComparison.Ordinal);
        Assert.Single(metin.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(2).ToList());
    }

    [Fact]
    public void CSV_DOSYASI_BOM_ILE_yaziliyor()
    {
        // BOM'suz UTF-8'i Excel eski kod sayfasiyla okuyor ve "Parça"
        // bozuluyor. Uc bayt, butun Turkce metni kurtariyor.
        string yol = WindowsYolu.Birlestir(_kok, "liste.csv");
        IslemRaporu rapor = ParcaListesiCsv.Yaz(yol, Uydur(Duz("Parça1.SLDPRT", "Pirinç")));

        Assert.True(rapor.Oldu, rapor.Sebebi);

        byte[] bayt = File.ReadAllBytes(yol);
        Assert.True(bayt.Length > 3);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bayt[..3]);
        Assert.Contains("Pirinç", Encoding.UTF8.GetString(bayt), StringComparison.Ordinal);
    }
}
