using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// KOK DISINDA AYRIMI (Erkan'in sectigi is, 30.08.2026): yazili yol diskte
/// VAR ama acik kokun DISINDA -> bu "kayip dosya" degil; BULUNAMADI'dan
/// ayrilir, panelde gizlenmez ve kendi raporuna gider.
///
/// SOZLESMENIN KALBI: diske yalniz <see cref="ReferansIndeksi.DiskiYokla"/>
/// dokunur (tarama, arka planda); <see cref="ReferansIndeksi.Coz"/> hicbir
/// zaman dokunmaz. Ilk hal cozum aninda File.Exists cagiriyordu ve Erkan'in
/// makinesinde secim degisiminde uygulama DONDU (olu ag yollari).
///
/// Ayirt etmenin sinirlari da burada olculuyor:
///   - diskte VAR + kok DISINDA + yoklandi   -> KokDisinda
///   - hic yoklanmadi                        -> Bulunamadi (gizli kalir)
///   - diskte YOK                            -> Bulunamadi
///   - diskte var ama kokun ALTINDA          -> Bulunamadi (o taramanin isi)
/// </summary>
public sealed class KokDisindaTestleri : IDisposable
{
    private readonly string _kok;

    /// <summary>Kokun DISINDA bir klasor - ayrimin olculdugu yer.</summary>
    private readonly string _dis;

    public KokDisindaTestleri()
    {
        string ek = Guid.NewGuid().ToString("N")[..8];
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-kokdisi-" + ek);
        _dis = Path.Combine(Path.GetTempPath(), "swpdm-dis-" + ek);
        Kopyala(Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz"), _kok);
        Directory.CreateDirectory(_dis);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_kok, recursive: true);
            Directory.Delete(_dis, recursive: true);
        }
        catch (IOException)
        {
            // Temizlik sonucu degistirmez.
        }
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

    private ReferansIndeksi Taranmis()
    {
        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);
        return indeks;
    }

    private string Yol(params string[] p) => Path.Combine([_kok, .. p]);

    /// <summary>
    /// Montaj1'in kaydina, kokun disindaki bir yolu yazar. Dosyanin kendisi
    /// yalnizca <paramref name="dosyaOlsun"/> ise olusturulur.
    /// </summary>
    private string DisReferansEkle(ReferansIndeksi indeks, bool dosyaOlsun = true)
    {
        string disDosya = Path.Combine(_dis, "Kutuphane.SLDPRT");
        if (dosyaOlsun)
        {
            File.WriteAllText(disDosya, "icerik onemsiz; varligi olculuyor");
        }

        IndeksKaydi kayit = indeks.Kayit(Yol("Montaj1.SLDASM"))!;
        indeks.Koy(kayit with { YazilanYollar = [.. kayit.YazilanYollar, disDosya] });
        return disDosya;
    }

    private Cozum Cozumu(ReferansIndeksi indeks, string yazilan)
        => indeks.Kullandiklari(Yol("Montaj1.SLDASM")).Single(x => x.YazilanYol == yazilan).Cozum;

    [Fact]
    public void YOKLANMIS_DIS_DOSYA_BULUNAMADI_SAYILMAZ()
    {
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks);
        indeks.DiskiYokla();

        Cozum cozum = Cozumu(indeks, disDosya);

        Assert.Equal(CozumDurumu.KokDisinda, cozum.Durum);
        Assert.Equal(disDosya, cozum.Yol);   // onizleme ve ipucu bu yolu kullaniyor
    }

    [Fact]
    public void YOKLANMAMIS_YOL_BULUNAMADI_KALIR_cozum_diske_dokunmaz()
    {
        // DONMA ONARIMININ SOZLESMESI: DiskiYokla kosmadan cozum "kok
        // disinda" DIYEMEZ, cunku diyebilmesi icin diske bakmasi gerekirdi.
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks);

        Assert.Equal(CozumDurumu.Bulunamadi, Cozumu(indeks, disDosya).Durum);
    }

    [Fact]
    public void TARAMA_YOKLAMAYI_KENDI_KOSUYOR()
    {
        // Normal akis: kullanici DiskiYokla diye bir sey bilmez; tarama
        // sonunda kendiliginden kosar.
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks);

        IndeksTarama.Tara(indeks);

        Assert.Equal(CozumDurumu.KokDisinda, Cozumu(indeks, disDosya).Durum);
    }

    [Fact]
    public void DISKTE_DE_OLMAYAN_YOL_BULUNAMADI_KALIR()
    {
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks, dosyaOlsun: false);
        indeks.DiskiYokla();

        Assert.Equal(CozumDurumu.Bulunamadi, Cozumu(indeks, disDosya).Durum);
    }

    [Fact]
    public void AYNI_YOL_IKINCI_KEZ_YOKLANMAZ_yeni_indeks_yoklar()
    {
        // Bilinclii bayatlik (DiskiYokla belgesi): olu sunucu adi her
        // taramada yeniden dakikalar yedirmesin diye cevap indeks nesli
        // boyunca tutuluyor. Kok yeniden acilinca (yeni indeks) tazelenir.
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks);
        indeks.DiskiYokla();
        Assert.Equal(CozumDurumu.KokDisinda, Cozumu(indeks, disDosya).Durum);

        File.Delete(disDosya);
        indeks.DiskiYokla();
        Assert.Equal(CozumDurumu.KokDisinda, Cozumu(indeks, disDosya).Durum);   // bayat, bilerek

        ReferansIndeksi yeni = Taranmis();
        DisReferansEkle(yeni, dosyaOlsun: false);
        yeni.DiskiYokla();
        Assert.Equal(CozumDurumu.Bulunamadi, Cozumu(yeni, disDosya).Durum);
    }

    [Fact]
    public void KOKUN_ALTINDAKI_TARANMAMIS_DOSYA_KOK_DISINDA_SAYILMAZ()
    {
        // Dosya diskte ve kokun ALTINDA ama indekste yok (bayat indeks).
        // "Kok disinda" demek yalan olurdu; orasi taramanin isi.
        ReferansIndeksi indeks = Taranmis();

        string icDosya = Yol("Taranmamis.SLDPRT");
        File.WriteAllText(icDosya, "diskte var, indekste yok");

        IndeksKaydi kayit = indeks.Kayit(Yol("Montaj1.SLDASM"))!;
        indeks.Koy(kayit with { YazilanYollar = [.. kayit.YazilanYollar, icDosya] });
        indeks.DiskiYokla();

        Assert.Equal(CozumDurumu.Bulunamadi, Cozumu(indeks, icDosya).Durum);
    }

    [Fact]
    public void KOK_DISINDAKI_SATIR_PANELDE_GIZLENMEZ()
    {
        ReferansIndeksi indeks = Taranmis();
        DisReferansEkle(indeks);
        indeks.DiskiYokla();

        PanelSatirlari p = indeks.KullandiklariGorunur(Yol("Montaj1.SLDASM"));

        Assert.Contains(p.Gosterilecekler, x => x.Cozum.Durum == CozumDurumu.KokDisinda);
        Assert.Equal(0, p.Gizlenen);
    }

    [Fact]
    public void Rapor_KOK_DISINDAKINI_KENDI_RAPORU_LISTELER_KIRIK_LISTELEMEZ()
    {
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks);
        indeks.DiskiYokla();

        RaporSonucu disaridakiler = Uret("Kök dışındakiler", indeks);
        RaporSonucu kirik = Uret("Kırık referanslar", indeks);

        Assert.Single(disaridakiler.Satirlar);
        Assert.Contains(disDosya, disaridakiler.Satirlar[0].Aciklama, StringComparison.Ordinal);
        Assert.Empty(kirik.Satirlar);   // saglam kutuphane bagi KIRIK degildir
    }

    private static RaporSonucu Uret(string ad, ReferansIndeksi indeks)
        => RaporListesi.Tumu.First(r => r.Ad == ad).Uret(indeks);
}
