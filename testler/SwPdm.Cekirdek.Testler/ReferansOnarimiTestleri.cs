using System;
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

        Assert.True(plan.OlculmusGuvenli);       // "Parça1" ve "Gövde1" ayni harf sayisi
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

    [Fact]
    public void FARKLI_UZUNLUKTAKI_ad_OLCULMEMIS_diye_isaretleniyor()
    {
        // Uzunluk degisince yol dolgu/goreli hale getiriliyor ve BU HENUZ
        // OLCULMEDI. Plan bunu SOYLUYOR; kullaniciya sormadan uygulanmamali.
        OnarimPlani plan = ReferansOnarimi.Planla(Indeks(), Yol("Parça1.SLDPRT"), "G1.SLDPRT");

        Assert.False(plan.OlculmusGuvenli);
        Assert.Equal(2, plan.Ebeveynler.Count);
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
}
