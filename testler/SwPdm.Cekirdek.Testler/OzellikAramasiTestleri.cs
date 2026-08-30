using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// OZELLIGE GORE ARAMA - "malzeme: pirinç" (Erkan'in sectigi is, 30.08.2026).
///
/// GERCEK dosyalarla: veri/ozellikli/Parça1.SLDPRT'ye Erkan elle uc ozellik
/// girdi (Malzeme="Pirinç", Ağırlık=denklem sonucu, Çizen=BOS). Tertemiz
/// agacin dosyalarinda ozel ozellik yok ama Kaydeden="PC" var.
/// </summary>
public sealed class OzellikAramasiTestleri : IDisposable
{
    private readonly string _kok;

    public OzellikAramasiTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-ozellik-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_kok);
        foreach (string d in Directory.GetFiles(
                     Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz")))
        {
            File.Copy(d, Path.Combine(_kok, Path.GetFileName(d)));
        }

        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "veri", "ozellikli", "Parça1.SLDPRT"),
            Path.Combine(_kok, "Ozellikli.SLDPRT"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_kok, recursive: true);
        }
        catch (IOException)
        {
            // Temizlik sonucu degistirmez.
        }
    }

    private ReferansIndeksi Taranmis()
    {
        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);
        return indeks;
    }

    private static OzellikAramaSonucu Ara(ReferansIndeksi indeks, string metin)
    {
        OzellikSorgusu? sorgu = OzellikAramasi.Coz(metin);
        Assert.NotNull(sorgu);
        return OzellikAramasi.Ara(indeks, sorgu!, 2000);
    }

    private static List<string> Adlar(IEnumerable<DosyaOgesi> dosyalar)
        => dosyalar.Select(d => d.Ad).ToList();

    // ------------------------------------------------------------- sozdizimi

    [Fact]
    public void COZ_duz_metin_SORGU_DEGIL()
    {
        Assert.Null(OzellikAramasi.Coz("Parça1"));
        Assert.Null(OzellikAramasi.Coz(":deger"));   // anahtarsiz
        Assert.Null(OzellikAramasi.Coz("   "));
        Assert.Null(OzellikAramasi.Coz(null));
    }

    [Fact]
    public void COZ_anahtar_ve_degeri_KIRPAR()
    {
        OzellikSorgusu? s = OzellikAramasi.Coz("  malzeme :  pirinç  ");

        Assert.NotNull(s);
        Assert.Equal("malzeme", s!.Anahtar);
        Assert.Equal("pirinç", s.Deger);
    }

    [Fact]
    public void COZ_bos_deger_VARLIK_SORGUSU()
    {
        OzellikSorgusu? s = OzellikAramasi.Coz("çizen:");

        Assert.NotNull(s);
        Assert.Equal(string.Empty, s!.Deger);
    }

    // ------------------------------------------------------------- indeksleme

    [Fact]
    public void TARAMA_ozellikleri_INDEKSE_ALIR()
    {
        ReferansIndeksi indeks = Taranmis();

        IndeksKaydi? kayit = indeks.Kayit(Path.Combine(_kok, "Ozellikli.SLDPRT"));
        Assert.NotNull(kayit);
        Assert.NotNull(kayit!.Ozellikler);
        Assert.Contains(kayit.Ozellikler!, o => o.Key == "Malzeme" && o.Value == "Pirinç");
        Assert.Contains(kayit.Ozellikler!, o => o.Key == "Kaydeden");   // sistemden turetilen
    }

    [Fact]
    public void OZELLIGI_OLMAYAN_DOSYA_null_DEGIL()
    {
        // "hic okunmadi" (null) ile "okundu, ozel ozelligi yok" ayri sey.
        ReferansIndeksi indeks = Taranmis();

        IndeksKaydi? kayit = indeks.Kayit(Path.Combine(_kok, "Parça1.SLDPRT"));
        Assert.NotNull(kayit!.Ozellikler);
    }

    // ------------------------------------------------------------- esleşme

    [Fact]
    public void MALZEME_PIRINC_BULUR()
    {
        OzellikAramaSonucu s = Ara(Taranmis(), "malzeme: pirinç");

        Assert.Equal(["Ozellikli.SLDPRT"], Adlar(s.Bulunanlar));
        Assert.Contains("indeksten", s.IndeksOzeti, StringComparison.Ordinal);
    }

    [Fact]
    public void ANAHTAR_HARFE_DEGER_PARCAYA_DUYARSIZ()
    {
        // Anahtar TAM ama harf duyarsiz; deger ICERIYOR.
        OzellikAramaSonucu s = Ara(Taranmis(), "MALZEME: rinç");

        Assert.Equal(["Ozellikli.SLDPRT"], Adlar(s.Bulunanlar));
    }

    [Fact]
    public void BOS_DEGER_bos_degerli_ozelligi_de_BULUR()
    {
        // Çizen dosyada VAR ama degeri bos birakilmis; "çizen:" onu bulmali.
        OzellikAramaSonucu s = Ara(Taranmis(), "çizen:");

        Assert.Equal(["Ozellikli.SLDPRT"], Adlar(s.Bulunanlar));
    }

    [Fact]
    public void KAYDEDEN_ARANABILIR()
    {
        OzellikAramaSonucu s = Ara(Taranmis(), "kaydeden: pc");

        Assert.Contains("Ozellikli.SLDPRT", Adlar(s.Bulunanlar));
        Assert.True(s.Bulunanlar.Count > 1);   // tertemiz dosyalari da PC kaydetti
    }

    [Fact]
    public void ESLESMEYEN_DEGER_BOS_DONER_ve_ozet_yine_indeksten_DER()
    {
        OzellikAramaSonucu s = Ara(Taranmis(), "malzeme: çelik");

        Assert.Empty(s.Bulunanlar);
        Assert.Contains("indeksten", s.IndeksOzeti, StringComparison.Ordinal);
    }

    [Fact]
    public void OZELLIGI_OKUNMAMIS_KAYIT_VARSA_EKSIKLIK_SOYLENIR()
    {
        ReferansIndeksi indeks = Taranmis();
        IndeksKaydi kayit = indeks.Kayit(Path.Combine(_kok, "Montaj1.SLDASM"))!;
        indeks.Koy(kayit with { Ozellikler = null });   // eski indeksi taklit et

        OzellikAramaSonucu s = Ara(indeks, "malzeme: pirinç");

        Assert.Contains("özellikleri okunmadı", s.IndeksOzeti, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- kalicilik

    [Fact]
    public void IndeksDosyasi_OZELLIKLERI_TASIR()
    {
        ReferansIndeksi indeks = Taranmis();
        string dosya = Path.Combine(_kok, "indeks.txt");
        Assert.True(IndeksDosyasi.Yaz(indeks, dosya));

        ReferansIndeksi geri = IndeksDosyasi.Oku(_kok, dosya);

        Assert.Equal(["Ozellikli.SLDPRT"], Adlar(Ara(geri, "malzeme: pirinç").Bulunanlar));

        // Bos-ama-okunmus liste null'a DONMEMELI (isaretci satiri).
        Assert.NotNull(geri.Kayit(Path.Combine(_kok, "Parça1.SLDPRT"))!.Ozellikler);
    }

    [Fact]
    public void IndeksDosyasi_TAB_ve_ESITTIR_iceren_degeri_BOZMAZ()
    {
        ReferansIndeksi indeks = Taranmis();
        IndeksKaydi kayit = indeks.Kayit(Path.Combine(_kok, "Parça1.SLDPRT"))!;
        indeks.Koy(kayit with
        {
            Ozellikler = new[]
            {
                new KeyValuePair<string, string>("A=B", "C\tD"),
            },
        });

        string dosya = Path.Combine(_kok, "indeks2.txt");
        Assert.True(IndeksDosyasi.Yaz(indeks, dosya));
        ReferansIndeksi geri = IndeksDosyasi.Oku(_kok, dosya);

        // TAB duzlestirilir (bosluk olur), '=' aynen kalir - bicim bozulmaz.
        KeyValuePair<string, string> o =
            geri.Kayit(kayit.Yol)!.Ozellikler!.Single();
        Assert.Equal("A=B", o.Key);
        Assert.Equal("C D", o.Value);
    }

    [Fact]
    public void ESKI_INDEKS_GOC_EDER_dosyalar_bir_kez_yeniden_okunur()
    {
        // Eski surumun indeksi: kayitlar var ama Ozellikler null. Sonraki
        // tarama boyut+tarih ayni olsa da dosyalari BIR KEZ yeniden okur;
        // yoksa arama sessizce bos donerdi (CLAUDE.md 3).
        ReferansIndeksi indeks = Taranmis();
        int dosyaSayisi = indeks.DosyaSayisi;
        foreach (IndeksKaydi k in indeks.Kayitlar.ToArray())
        {
            indeks.Koy(k with { Ozellikler = null });
        }

        TaramaSonucu ikinci = IndeksTarama.Tara(indeks);

        Assert.Equal(dosyaSayisi, ikinci.Okunan);   // atlanmadi, goc etti
        Assert.Equal(["Ozellikli.SLDPRT"], Adlar(Ara(indeks, "malzeme: pirinç").Bulunanlar));

        // Ucuncu tarama yine bedava (artimlilik geri geldi).
        Assert.Equal(dosyaSayisi, IndeksTarama.Tara(indeks).Atlanan);
    }
}
