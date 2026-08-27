using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Indeks, cozucu ve raporlar - GERCEK SOLIDWORKS dosyalarindan olusan bir
/// agac uzerinde.
///
/// Ornek agacin GERCEGI (olculdu):
///   Montaj1.SLDASM -> Parça1.SLDPRT · Yeni klasör\Parça2.SLDPRT
///   Montaj2.SLDASM -> Montaj1.SLDASM
///   Parça1.SLDDRW  -> Parça1.SLDPRT
///   Montaj2.SLDDRW -> Montaj2.SLDASM
///   Parça2.SLDDRW  -> Parça2.SLDPRT
/// Yani TERSTEN:
///   Parça1.SLDPRT  <- Montaj1.SLDASM, Parça1.SLDDRW
///   Parça2.SLDPRT  <- Montaj1.SLDASM, Parça2.SLDDRW
///   Montaj1.SLDASM <- Montaj2.SLDASM
/// </summary>
public class ReferansIndeksiTestleri : IDisposable
{
    private readonly string _kok;

    public ReferansIndeksiTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-indeks-" + Guid.NewGuid().ToString("N")[..8]);
        Kopyala(Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz"), _kok);
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

        GC.SuppressFinalize(this);
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

    private ReferansIndeksi Taranmis(out TaramaSonucu sonuc)
    {
        var indeks = new ReferansIndeksi(_kok);
        sonuc = IndeksTarama.Tara(indeks);
        return indeks;
    }

    private string Yol(params string[] p) => Path.Combine([_kok, .. p]);

    private static List<string> Adlar(IEnumerable<string> yollar)
        => yollar.Select(WindowsYolu.DosyaAdi).OrderBy(a => a, StringComparer.Ordinal).ToList();

    private static RaporSonucu Rapor(string ad, ReferansIndeksi indeks)
        => RaporListesi.Tumu.First(r => r.Ad == ad).Uret(indeks);

    // ---------------------------------------------------------------- tarama

    [Fact]
    public void Tarama_YEDI_DOSYAYI_DA_OKUR()
    {
        ReferansIndeksi indeks = Taranmis(out TaramaSonucu sonuc);

        Assert.Equal(7, sonuc.Toplam);
        Assert.Equal(7, sonuc.Okunan);
        Assert.Equal(0, sonuc.Okunamayan);
        Assert.True(sonuc.Tam);
        Assert.Equal(7, indeks.DosyaSayisi);
    }

    [Fact]
    public void Tarama_IKINCI_KEZ_HIC_DOSYA_ACMAZ()
    {
        // ARTIMLILIK: ozelligi kullanilabilir yapan tek sey bu. Boyut ve
        // tarih aynysa dosya acilmamali.
        ReferansIndeksi indeks = Taranmis(out _);
        TaramaSonucu ikinci = IndeksTarama.Tara(indeks);

        Assert.Equal(0, ikinci.Okunan);
        Assert.Equal(7, ikinci.Atlanan);
    }

    [Fact]
    public void Tarama_DEGISEN_DOSYAYI_YENIDEN_OKUR()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        string hedef = Yol("Parça1.SLDPRT");
        File.SetLastWriteTime(hedef, DateTime.Now.AddMinutes(1));

        TaramaSonucu ikinci = IndeksTarama.Tara(indeks);

        Assert.Equal(1, ikinci.Okunan);
        Assert.Equal(6, ikinci.Atlanan);
    }

    [Fact]
    public void Tarama_SILINEN_DOSYAYI_INDEKSTEN_DUSURUR()
    {
        ReferansIndeksi indeks = Taranmis(out _);
        File.Delete(Yol("Parça1.SLDDRW"));

        TaramaSonucu ikinci = IndeksTarama.Tara(indeks);

        Assert.Equal(1, ikinci.Dusen);
        Assert.Equal(6, indeks.DosyaSayisi);
        Assert.Null(indeks.Kayit(Yol("Parça1.SLDDRW")));
    }

    [Fact]
    public void Tarama_IPTAL_EDILINCE_SONUCUN_YARIM_OLDUGUNU_SOYLER()
    {
        using var kaynak = new CancellationTokenSource();
        kaynak.Cancel();

        var indeks = new ReferansIndeksi(_kok);
        TaramaSonucu sonuc = IndeksTarama.Tara(indeks, kaynak.Token);

        Assert.True(sonuc.Iptal);
        Assert.False(sonuc.Tam);
        Assert.Contains("YARIM", sonuc.Yaz(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- sorgu

    [Fact]
    public void Kullananlar_TERS_YONU_DOGRU_VERIR()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        KullanimSonucu s = indeks.Kullananlar(Yol("Parça1.SLDPRT"));

        Assert.True(s.Guvenilir, s.Sebep);
        Assert.Equal(["Montaj1.SLDASM", "Parça1.SLDDRW"], Adlar(s.Kullananlar));
    }

    [Fact]
    public void Kullananlar_ALT_KLASORDEKI_PARCAYI_DA_BULUR()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        KullanimSonucu s = indeks.Kullananlar(Yol("Yeni klasör", "Parça2.SLDPRT"));

        Assert.Equal(["Montaj1.SLDASM", "Parça2.SLDDRW"], Adlar(s.Kullananlar));
    }

    [Fact]
    public void Kullandiklari_DOGRUDAN_REFERANSLARI_COZER()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        IReadOnlyList<(string YazilanYol, Cozum Cozum)> liste =
            indeks.Kullandiklari(Yol("Montaj1.SLDASM"));

        Assert.Equal(2, liste.Count);
        Assert.All(liste, c => Assert.Equal(CozumDurumu.Bulundu, c.Cozum.Durum));
        Assert.Equal(["Parça1.SLDPRT", "Parça2.SLDPRT"], Adlar(liste.Select(c => c.Cozum.Yol!)));
    }

    [Fact]
    public void TARANMAMIS_INDEKS_BOS_LISTEYI_GUVENILIR_SAYMAZ()
    {
        // CLAUDE.md 3'un tam kalbi: bos liste "kimse kullanmiyor" DEGILDIR.
        var indeks = new ReferansIndeksi(_kok);

        KullanimSonucu s = indeks.Kullananlar(Yol("Parça1.SLDPRT"));

        Assert.Empty(s.Kullananlar);
        Assert.False(s.Guvenilir);
        Assert.Contains("taranmadı", s.Sebep ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void TARANAN_KOKUN_DISINDAKI_DOSYA_GUVENILIR_DEGIL()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        KullanimSonucu s = indeks.Kullananlar(@"C:\baska\yer\Parça9.SLDPRT");

        Assert.False(s.Guvenilir);
        Assert.Contains("dışında", s.Sebep ?? string.Empty, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- cozucu

    [Fact]
    public void Cozucu_TEK_ADAY_ONU_SECER()
    {
        Cozum c = ReferansCozucu.Coz(@"C:\eski\a.SLDPRT", @"D:\yeni\m.SLDASM", [@"D:\yeni\a.SLDPRT"]);

        Assert.Equal(CozumDurumu.Bulundu, c.Durum);
        Assert.Equal(@"D:\yeni\a.SLDPRT", c.Yol);
    }

    [Fact]
    public void Cozucu_KOMSU_KAZANIR()
    {
        // CLAUDE.md 5'te OLCULEN SOLIDWORKS davranisi: ebeveynin yanindaki
        // dosya, yazili mutlak yolun onune gecer.
        Cozum c = ReferansCozucu.Coz(
            @"C:\eski\a.SLDPRT",
            @"D:\yeni\m.SLDASM",
            [@"D:\baska\a.SLDPRT", @"D:\yeni\a.SLDPRT"]);

        Assert.Equal(CozumDurumu.Bulundu, c.Durum);
        Assert.Equal(@"D:\yeni\a.SLDPRT", c.Yol);
    }

    [Fact]
    public void Cozucu_KOMSU_YOKSA_TAM_YOL_KAZANIR()
    {
        Cozum c = ReferansCozucu.Coz(
            @"D:\bir\a.SLDPRT",
            @"D:\yeni\m.SLDASM",
            [@"D:\bir\a.SLDPRT", @"D:\iki\a.SLDPRT"]);

        Assert.Equal(CozumDurumu.Bulundu, c.Durum);
        Assert.Equal(@"D:\bir\a.SLDPRT", c.Yol);
    }

    [Fact]
    public void Cozucu_KARAR_VEREMEZSE_UYDURMAZ()
    {
        // Yanlis bir "bu dosya" cevabi kullaniciya YANLIS DOSYAYI sildirir.
        Cozum c = ReferansCozucu.Coz(
            @"C:\eski\a.SLDPRT",
            @"D:\yeni\m.SLDASM",
            [@"D:\bir\a.SLDPRT", @"D:\iki\a.SLDPRT"]);

        Assert.Equal(CozumDurumu.Belirsiz, c.Durum);
        Assert.Null(c.Yol);
        Assert.Equal(2, c.Adaylar.Count);
    }

    [Fact]
    public void Cozucu_ADAY_YOKSA_BULUNAMADI()
    {
        Cozum c = ReferansCozucu.Coz(@"C:\eski\a.SLDPRT", @"D:\yeni\m.SLDASM", []);

        Assert.Equal(CozumDurumu.Bulunamadi, c.Durum);
    }

    // ---------------------------------------------------------------- kalicilik

    [Fact]
    public void IndeksDosyasi_GIDIS_DONUS()
    {
        ReferansIndeksi indeks = Taranmis(out _);
        string dosya = Path.Combine(_kok, "indeks.txt");

        Assert.True(IndeksDosyasi.Yaz(indeks, dosya));
        ReferansIndeksi geri = IndeksDosyasi.Oku(_kok, dosya);

        Assert.Equal(indeks.DosyaSayisi, geri.DosyaSayisi);
        Assert.True(geri.Tam);
        Assert.Equal(
            ["Montaj1.SLDASM", "Parça1.SLDDRW"],
            Adlar(geri.Kullananlar(Yol("Parça1.SLDPRT")).Kullananlar));
    }

    [Fact]
    public void IndeksDosyasi_OLMAYAN_DOSYA_BOS_INDEKS()
    {
        ReferansIndeksi geri = IndeksDosyasi.Oku(_kok, Path.Combine(_kok, "hic-yok.txt"));

        Assert.Equal(0, geri.DosyaSayisi);
        Assert.Null(geri.TaramaZamani);
    }

    [Fact]
    public void IndeksDosyasi_BOZUK_SATIR_ATLANIR()
    {
        string dosya = Path.Combine(_kok, "bozuk.txt");
        File.WriteAllLines(dosya,
        [
            "bu satirda esittir yok",
            "kok=" + _kok,
            "tanimadigimiz=deger",
            "dosya=" + Yol("Parça1.SLDPRT"),
            "boyut=abc",
            "ref=" + Yol("Yok.SLDPRT"),
        ]);

        ReferansIndeksi geri = IndeksDosyasi.Oku(_kok, dosya);

        Assert.Equal(1, geri.DosyaSayisi);
    }

    // ---------------------------------------------------------------- raporlar

    [Fact]
    public void Rapor_TEMIZ_AGACTA_KIRIK_REFERANS_YOK()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        RaporSonucu r = Rapor("Kırık referanslar", indeks);

        Assert.True(r.Guvenilir, r.Sebep);
        Assert.Empty(r.Satirlar);
    }

    [Fact]
    public void Rapor_SILINEN_PARCA_KIRIK_REFERANS_OLARAK_CIKAR()
    {
        // Once temiz, sonra parca silinip yeniden taraninca YAKALAMALI.
        ReferansIndeksi indeks = Taranmis(out _);
        Assert.Empty(Rapor("Kırık referanslar", indeks).Satirlar);

        File.Delete(Yol("Yeni klasör", "Parça2.SLDPRT"));
        IndeksTarama.Tara(indeks);

        RaporSonucu r = Rapor("Kırık referanslar", indeks);

        Assert.Contains(r.Satirlar, s => WindowsYolu.DosyaAdi(s.Yol) == "Montaj1.SLDASM");
        Assert.Contains(r.Satirlar, s => s.Aciklama.Contains("Parça2.SLDPRT", StringComparison.Ordinal));
    }

    [Fact]
    public void Rapor_KULLANILAN_PARCA_YETIM_DEGILDIR()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        RaporSonucu r = Rapor("Yetim parçalar", indeks);

        Assert.Empty(r.Satirlar);
    }

    [Fact]
    public void Rapor_KULLANILMAYAN_PARCA_YETIM_OLARAK_CIKAR()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        // Kimsenin kullanmadigi bir parca: var olan bir parcanin kopyasi,
        // yeni adla. Icerigi gecerli bir SOLIDWORKS dosyasi.
        File.Copy(Yol("Parça1.SLDPRT"), Yol("Kullanilmayan.SLDPRT"));
        IndeksTarama.Tara(indeks);

        RaporSonucu r = Rapor("Yetim parçalar", indeks);

        Assert.Single(r.Satirlar);
        Assert.Equal("Kullanilmayan.SLDPRT", WindowsYolu.DosyaAdi(r.Satirlar[0].Yol));
    }

    [Fact]
    public void Rapor_TEKNIK_RESMI_OLMAYAN_MONTAJI_BULUR()
    {
        // Montaj1'in teknik resmi yok; otekilerin var.
        ReferansIndeksi indeks = Taranmis(out _);

        RaporSonucu r = Rapor("Teknik resmi olmayanlar", indeks);

        Assert.Equal(["Montaj1.SLDASM"], Adlar(r.Satirlar.Select(s => s.Yol)));
    }

    [Fact]
    public void Rapor_OKUNAMAYAN_DOSYA_BOS_OLMALI()
    {
        ReferansIndeksi indeks = Taranmis(out _);

        RaporSonucu r = Rapor("Okunamayan dosyalar", indeks);

        Assert.Empty(r.Satirlar);
    }

    [Fact]
    public void Rapor_TARANMAMIS_INDEKSTE_GUVENILIR_DEGIL()
    {
        var indeks = new ReferansIndeksi(_kok);

        foreach (IRapor rapor in RaporListesi.Tumu)
        {
            RaporSonucu r = rapor.Uret(indeks);
            Assert.False(r.Guvenilir, rapor.Ad);
            Assert.False(string.IsNullOrWhiteSpace(r.Sebep), rapor.Ad);
        }
    }

    [Fact]
    public void RaporListesi_HEPSININ_ADI_VE_ACIKLAMASI_VAR()
    {
        Assert.NotEmpty(RaporListesi.Tumu);
        foreach (IRapor r in RaporListesi.Tumu)
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Ad));
            Assert.False(string.IsNullOrWhiteSpace(r.Aciklama));
        }

        // Adlar BENZERSIZ olmali: pencere onlarla sekme uretiyor.
        Assert.Equal(
            RaporListesi.Tumu.Count,
            RaporListesi.Tumu.Select(r => r.Ad).Distinct(StringComparer.Ordinal).Count());
    }
}
