using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// AD DEGISINCE EBEVEYNLERI ONARMA - gercek SOLIDWORKS 2022 dosyalariyla,
/// gercek bir klasorde, gercek bir indeksle.
///
/// Ornek kume: Montaj1.SLDASM -> Parça1.SLDPRT + Yeni klasör\Parça2.SLDPRT
///             Parça1.SLDDRW  -> Parça1.SLDPRT
/// Yani Parça1'in IKI ebeveyni var; onarim ikisini birden yapmali.
///
/// BURADA OLCULEMEYEN: SOLIDWORKS onarilmis dosyayi aciyor mu. O olcum
/// Erkan'in makinesinde yapildi (28.08.2026): ayni harf sayisindaki ad ile
/// ACILDI, parcalar yerinde.
/// </summary>
public sealed class ReferansOnarimiTestleri : IDisposable
{
    private static string Ornek => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private readonly string _kok;

    public ReferansOnarimiTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-onarim-" + Guid.NewGuid().ToString("N"));
        Kopyala(Ornek, _kok);
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, recursive: true); } catch (IOException) { }
    }

    private static void Kopyala(string kaynak, string hedef)
    {
        Directory.CreateDirectory(hedef);
        foreach (string d in Directory.GetFiles(kaynak))
        {
            File.Copy(d, Path.Combine(hedef, Path.GetFileName(d)));
        }

        foreach (string k in Directory.GetDirectories(kaynak))
        {
            Kopyala(k, Path.Combine(hedef, Path.GetFileName(k)));
        }
    }

    private ReferansIndeksi Indeks()
    {
        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);
        return indeks;
    }

    private string Yol(params string[] p) => Path.Combine([_kok, .. p]);

    private static string[] Referanslari(string yol)
        => SwReferans.Oku(yol).Dogrudan.Select(WindowsYolu.DosyaAdi).ToArray();

    [Fact]
    public void IKI_EBEVEYN_de_onariliyor_ve_yeni_adi_gosteriyor()
    {
        OnarimPlani plan = ReferansOnarimi.Planla(Indeks(), Yol("Parça1.SLDPRT"), "Gövde1.SLDPRT");

        Assert.Empty(plan.Engeller);
        Assert.Equal(2, plan.Ebeveynler.Count);

        OnarimSonucu s = ReferansOnarimi.Uygula(plan);
        Assert.True(s.Oldu, s.Sebep);

        Assert.True(File.Exists(Yol("Gövde1.SLDPRT")));
        Assert.False(File.Exists(Yol("Parça1.SLDPRT")));

        Assert.Contains("Gövde1.SLDPRT", Referanslari(Yol("Montaj1.SLDASM")));
        Assert.Contains("Gövde1.SLDPRT", Referanslari(Yol("Parça1.SLDDRW")));

        // Oteki referans BOZULMAMALI.
        Assert.Contains("Parça2.SLDPRT", Referanslari(Yol("Montaj1.SLDASM")));
    }

    [Fact]
    public void Onarim_sonrasi_GECICI_DOSYA_KALMIYOR()
    {
        ReferansOnarimi.Uygula(
            ReferansOnarimi.Planla(Indeks(), Yol("Parça1.SLDPRT"), "Gövde1.SLDPRT"));

        string[] artik = Directory
            .GetFiles(_kok, "*.swpdm-*", SearchOption.AllDirectories);
        Assert.Empty(artik);
    }

    [Theory]
    [InlineData("G1.SLDPRT")]                  // ad KISALDI
    [InlineData("GövdeParçası1.SLDPRT")]       // ad UZADI
    public void FARKLI_UZUNLUKTAKI_ad_da_onariliyor(string yeniAd)
    {
        // OLCULDU (Erkan, 28.08.2026, ikinci tur): uzunluk farki klasor
        // kismindan karsilaninca SOLIDWORKS dosyayi ACIYOR - kisa da uzun da.
        OnarimSonucu s = ReferansOnarimi.Uygula(
            ReferansOnarimi.Planla(Indeks(), Yol("Parça1.SLDPRT"), yeniAd));

        Assert.True(s.Oldu, s.Sebep);
        Assert.Contains(yeniAd, Referanslari(Yol("Montaj1.SLDASM")));
        Assert.Contains(yeniAd, Referanslari(Yol("Parça1.SLDDRW")));
    }

    [Fact]
    public void ACIK_dosya_ENGEL_sayiliyor_ve_hicbir_sey_degismiyor()
    {
        // SOLIDWORKS'te acik bir belgenin yaninda "~$" kilidi durur.
        File.WriteAllBytes(Yol("~$Montaj1.SLDASM"), new byte[4]);

        OnarimPlani plan = ReferansOnarimi.Planla(Indeks(), Yol("Parça1.SLDPRT"), "Gövde1.SLDPRT");
        Assert.NotEmpty(plan.Engeller);
        Assert.Contains(plan.Engeller, e => e.Contains("Montaj1.SLDASM"));

        OnarimSonucu s = ReferansOnarimi.Uygula(plan);
        Assert.False(s.Oldu);
        Assert.True(File.Exists(Yol("Parça1.SLDPRT")));
        Assert.Contains("Parça1.SLDPRT", Referanslari(Yol("Montaj1.SLDASM")));
    }

    [Fact]
    public void TARANMAMIS_indekste_cevap_GUVENILIR_DEGIL()
    {
        // Bos indeks: ebeveyn listesi bos doner ama bu "kimse kullanmiyor"
        // DEMEK DEGILDIR (CLAUDE.md 3).
        var bos = new ReferansIndeksi(_kok);
        OnarimPlani plan = ReferansOnarimi.Planla(bos, Yol("Parça1.SLDPRT"), "Gövde1.SLDPRT");

        Assert.False(plan.Guvenilir);
        Assert.Empty(plan.Ebeveynler);
    }

    [Fact]
    public void INDEKS_YOKSA_engel_yaziliyor()
    {
        OnarimPlani plan = ReferansOnarimi.Planla(null, Yol("Parça1.SLDPRT"), "Gövde1.SLDPRT");

        Assert.NotEmpty(plan.Engeller);
        Assert.False(ReferansOnarimi.Uygula(plan).Oldu);
    }

    [Fact]
    public void AYNI_ADDA_dosya_varsa_YAPILMIYOR()
    {
        File.WriteAllBytes(Yol("Gövde1.SLDPRT"), new byte[8]);

        OnarimSonucu s = ReferansOnarimi.Uygula(
            ReferansOnarimi.Planla(Indeks(), Yol("Parça1.SLDPRT"), "Gövde1.SLDPRT"));

        Assert.False(s.Oldu);
        Assert.Contains("zaten var", s.Sebep);
        Assert.True(File.Exists(Yol("Parça1.SLDPRT")));
    }

    [Fact]
    public void EBEVEYNI_OLMAYAN_dosya_da_adlandirilabiliyor()
    {
        // Parça2'yi yalnizca Montaj1 kullaniyor; Montaj2 hicbir sey tarafindan
        // kullanilmiyor olabilir. Ebeveyni olmayan dosyada onarim BOS gecer
        // ama ad yine degismeli.
        OnarimPlani plan = ReferansOnarimi.Planla(
            Indeks(), Yol("Montaj1.SLDASM"), "Montaj9.SLDASM");

        OnarimSonucu s = ReferansOnarimi.Uygula(plan);
        Assert.True(s.Oldu, s.Sebep);
        Assert.True(File.Exists(Yol("Montaj9.SLDASM")));
    }

    // ---------------------------------------------------------------------
    // TASIMA ONARIMI
    //
    // Ad degisiminden farki: dosya ebeveynin YANINDAN AYRILIYOR, yani
    // komsuluk kurali artik kurtarmiyor ve yazili yolun gercekten dogru
    // yeri gostermesi gerekiyor.
    // ---------------------------------------------------------------------

    [Fact]
    public void TASINAN_dosyanin_DISARIDA_KALAN_ebeveyni_onariliyor()
    {
        // INDEKS TASIMADAN ONCE kurulur - gercek akis da boyle: uygulama
        // taranmis bir indeksle calisiyor, sonra tasima oluyor.
        ReferansIndeksi indeks = Indeks();

        // Parça1'i alt klasore tasi; Montaj1 ve teknik resim kokte kaliyor.
        string yeni = Yol("Yeni klasör", "Parça1.SLDPRT");
        File.Move(Yol("Parça1.SLDPRT"), yeni);

        OnarimPlani plan = ReferansOnarimi.TasimaPlani(
            indeks, Yol("Parça1.SLDPRT"), yeni, harictut: null);

        Assert.False(plan.CocuguTasi);
        Assert.True(plan.KlasorDegisti);
        Assert.Equal(2, plan.Ebeveynler.Count);

        OnarimSonucu s = ReferansOnarimi.Uygula(plan);
        Assert.True(s.Oldu, s.Sebep + " | engeller: " + string.Join(";", plan.Engeller));

        // Yazili yol artik YENI klasoru gosteriyor.
        string[] yazili = SwReferans.Oku(Yol("Montaj1.SLDASM")).Dogrudan.ToArray();
        Assert.Contains(yazili, y => y.Contains("Yeni klasör", StringComparison.OrdinalIgnoreCase)
                                  && WindowsYolu.DosyaAdi(y) == "Parça1.SLDPRT");
    }

    [Fact]
    public void BIRLIKTE_TASINAN_ebeveyne_DOKUNULMUYOR()
    {
        // Olculdu (CLAUDE.md 5): birlikte tasinan aile kendiliginden calisiyor.
        // Calisani onarmak bos risktir (1a).
        byte[] once = File.ReadAllBytes(Yol("Montaj1.SLDASM"));

        OnarimPlani plan = ReferansOnarimi.TasimaPlani(
            Indeks(), Yol("Parça1.SLDPRT"), Yol("Yeni klasör", "Parça1.SLDPRT"),
            harictut: [Yol("Montaj1.SLDASM")]);

        Assert.Single(plan.Ebeveynler);          // yalnizca teknik resim kaldi
        Assert.Equal(once, File.ReadAllBytes(Yol("Montaj1.SLDASM")));
    }

    [Fact]
    public void TASIMA_onarimi_GERI_ALINABILIYOR()
    {
        ReferansIndeksi indeks = Indeks();
        string yeni = Yol("Yeni klasör", "Parça1.SLDPRT");
        File.Move(Yol("Parça1.SLDPRT"), yeni);

        OnarimPlani ileri = ReferansOnarimi.TasimaPlani(
            indeks, Yol("Parça1.SLDPRT"), yeni, harictut: null);
        Assert.True(ReferansOnarimi.Uygula(ileri).Oldu);

        // Geri: dosya eski yerine dondu, ebeveynler de geri onarilmali.
        File.Move(yeni, Yol("Parça1.SLDPRT"));
        OnarimSonucu geri = ReferansOnarimi.Uygula(
            ReferansOnarimi.PlanlaBilinenlerle(
                ileri.Ebeveynler, yeni, Yol("Parça1.SLDPRT"), cocuguTasi: false));

        Assert.True(geri.Oldu, geri.Sebep);
        Assert.Contains("Parça1.SLDPRT", Referanslari(Yol("Montaj1.SLDASM")));
    }

    // ---------------------------------------------------------------------
    // ERKAN'IN DUZENI (28.08.2026) - ebeveyn ve parca AYRI klasorlerde.
    //
    // Bugune kadarki testlerde ikisi de ayni klasordeydi; bu duzen HIC
    // OLCULMEDI ve tam da burada kirildi:
    //   1\Parça1.SLDDRW  ->  kok\Parça1.SLDPRT
    // parca 3\ klasorune tasindi; SOLIDWORKS hala kokte ariyordu.
    // ---------------------------------------------------------------------

    [Fact]
    public void AYRI_KLASORDEKI_ebeveyn_onariliyor_ve_yol_YENI_YERI_cozuyor()
    {
        // 1\ ve 3\ klasorleri; teknik resim 1'de, parca kokte.
        string bir = Path.Combine(_kok, "1");
        string uc = Path.Combine(_kok, "3");
        Directory.CreateDirectory(bir);
        Directory.CreateDirectory(uc);

        string resim = Path.Combine(bir, "Parça1.SLDDRW");
        File.Move(Yol("Parça1.SLDDRW"), resim);

        ReferansIndeksi indeks = Indeks();

        // Parcayi 3\ klasorune tasi.
        string yeni = Path.Combine(uc, "Parça1.SLDPRT");
        File.Move(Yol("Parça1.SLDPRT"), yeni);

        (IReadOnlyList<OnarimPlani> planlar, string? sebep) = ReferansOnarimi.TasimaPlanlari(
            indeks, [(Yol("Parça1.SLDPRT"), yeni)], harictut: null);

        Assert.Null(sebep);
        Assert.NotEmpty(planlar);
        Assert.Contains(planlar, pl => pl.Ebeveynler.Contains(resim));

        (int onarilan, IReadOnlyList<string> hatalar, _) = ReferansOnarimi.Onar(planlar);
        Assert.Empty(hatalar);
        Assert.True(onarilan > 0);

        // ASIL OLCUM: teknik resmin ICINDEKI yol, TEKNIK RESMIN KLASORUNE
        // gore cozuldugunde parcanin YENI yerini gostermeli.
        string[] yazili = SwReferans.Oku(resim).Dogrudan.ToArray();
        string? hedef = yazili.FirstOrDefault(
            y => string.Equals(WindowsYolu.DosyaAdi(y), "Parça1.SLDPRT", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(hedef);
        Assert.Equal(
            WindowsYolu.Cozumle(null, yeni),
            WindowsYolu.Cozumle(bir, hedef));
    }

    [Fact]
    public void BASKA_KLASORDEKI_cocugun_ADI_degisince_yol_YINE_COZULMELI()
    {
        // ERKAN'IN EKRANI (31.08.2026): teknik resim 1\, parca 3\ - yani
        // cocuk ebeveynin YANINDA DEGIL. Ad degisiminde yazilan yol
        // "3\.\.\...\11-Parça1.SLDPRT" oldu; ebeveynin klasorune gore
        // cozulunce "1\3\..." eder ve referans KAYBOLUR.
        string bir = Path.Combine(_kok, "1");
        string uc = Path.Combine(_kok, "3");
        Directory.CreateDirectory(bir);
        Directory.CreateDirectory(uc);

        string resim = Path.Combine(bir, "Parça1.SLDDRW");
        File.Move(Yol("Parça1.SLDDRW"), resim);

        string parca = Path.Combine(uc, "Parça1.SLDPRT");
        File.Move(Yol("Parça1.SLDPRT"), parca);

        ReferansIndeksi indeks = Indeks();

        OnarimPlani plan = ReferansOnarimi.Planla(indeks, parca, "11-Parça1.SLDPRT");
        Assert.Contains(resim, plan.Ebeveynler);

        OnarimSonucu sonuc = ReferansOnarimi.Uygula(plan);
        Assert.True(sonuc.Oldu, sonuc.Sebebi);

        string yeni = Path.Combine(uc, "11-Parça1.SLDPRT");
        string? hedef = SwReferans.Oku(resim).Dogrudan.FirstOrDefault(
            y => string.Equals(
                WindowsYolu.DosyaAdi(y), "11-Parça1.SLDPRT", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(hedef);

        // ASIL OLCUM: yazili yol, EBEVEYNIN klasorune gore cozuldugunde
        // parcanin gercek yerini gostermeli.
        Assert.Equal(
            WindowsYolu.Cozumle(null, yeni),
            WindowsYolu.Cozumle(bir, hedef));
    }

    // ---------------------------------------------------------------------
    // TOPLU ONARIM - gecmiste kirilmis baglari toparlar.
    //
    // Erkan'in durumu: dosya Gezgin'de (ya da onarim gelmeden onceki bir
    // surumde) tasindi; dosya duruyor, biz onu buluyoruz ama SOLIDWORKS
    // acamiyor cunku yazili yol eski yeri gosteriyor.
    // ---------------------------------------------------------------------

    [Fact]
    public void BAYAT_YOL_raporda_gorunuyor_ve_DUZELTILIYOR()
    {
        string bir = Path.Combine(_kok, "1");
        string uc = Path.Combine(_kok, "3");
        Directory.CreateDirectory(bir);
        Directory.CreateDirectory(uc);

        string resim = Path.Combine(bir, "Parça1.SLDDRW");
        File.Move(Yol("Parça1.SLDDRW"), resim);

        // Parcayi UYGULAMA DISINDA tasi - yani onarim HIC calismadi.
        string yeni = Path.Combine(uc, "Parça1.SLDPRT");
        File.Move(Yol("Parça1.SLDPRT"), yeni);

        ReferansIndeksi indeks = Indeks();

        // 1) Rapor bunu GORMELI.
        IRapor rapor = RaporListesi.Tumu.First(r => r.Ad == "Bayat yollar");
        RaporSonucu once = rapor.Uret(indeks);
        Assert.Contains(once.Satirlar, r => r.Yol == resim);

        // 2) DUZELT.
        OnarimOzeti? ozet = rapor.Duzelt(indeks);
        Assert.NotNull(ozet);
        Assert.Empty(ozet!.Hatalar);
        Assert.True(ozet.Onarilan > 0);

        // 3) ASIL OLCUM: yazili yol artik gercek yeri gosteriyor.
        string? hedef = SwReferans.Oku(resim).Dogrudan.FirstOrDefault(
            y => string.Equals(WindowsYolu.DosyaAdi(y), "Parça1.SLDPRT", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WindowsYolu.Cozumle(null, yeni), WindowsYolu.Cozumle(bir, hedef));

        // 4) Indeks tazelenince rapor BOSALMALI.
        foreach (string d in ozet.Dokunulan)
        {
            IndeksTarama.Tazele(indeks, d);
        }

        Assert.DoesNotContain(rapor.Uret(indeks).Satirlar, r => r.Yol == resim);
    }

    [Fact]
    public void YANINDAKI_dosya_BAYAT_SAYILMAZ()
    {
        // Dosya ebeveynin yanindaysa SOLIDWORKS onu bulur (CLAUDE.md 5),
        // yazili yol bayat olsa bile. Onu "kirik" saymak yanlis alarm olurdu.
        ReferansIndeksi indeks = Indeks();
        IRapor rapor = RaporListesi.Tumu.First(r => r.Ad == "Bayat yollar");

        Assert.DoesNotContain(
            rapor.Uret(indeks).Satirlar, r => r.Yol == Yol("Parça1.SLDDRW"));
    }

    [Fact]
    public void DUZELTILEMEYEN_raporlar_null_donuyor()
    {
        ReferansIndeksi indeks = Indeks();
        foreach (IRapor r in RaporListesi.Tumu)
        {
            if (r.Ad != "Bayat yollar")
            {
                Assert.Null(r.Duzelt(indeks));
            }
        }
    }
}
