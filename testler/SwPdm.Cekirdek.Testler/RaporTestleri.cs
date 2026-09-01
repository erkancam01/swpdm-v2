using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// ALTI RAPOR - 01.09.2026 denetiminde HIC TESTI OLMADIGI bulundu.
///
/// NEDEN TEHLIKELI: bu raporlar kullaniciya DOSYA SILDIREN listeler.
/// "Yetim parcalar" ekraninda bir ad gorunuyorsa kullanici o parcayi
/// kimsenin kullanmadigini dusunur ve siler. Liste yanlissa saglam dosya
/// gider (CLAUDE.md 3). Kapida da olculmuyorlardi - yani hicbir sey
/// bakmiyordu.
///
/// EN ONEMLI TEST BURADA "GUVENILIR" TESTI: taranmamis ya da yarim bir
/// indekste BOS LISTE "sorun yok" DEMEK DEGILDIR. O bayragi kimse
/// tutmuyordu; biri Denetle'yi "her zaman true" yapsa hicbir sey
/// kirilmazdi.
///
/// Ornek agac (araclar/ornek-veri/tertemiz) sabit ve olculmus:
/// Montaj1.SLDASM · Parça1.SLDPRT · Parça1.SLDDRW ·
/// Yeni klasör/{Montaj2.SLDASM, Montaj2.SLDDRW, Parça2.SLDPRT, Parça2.SLDDRW}
/// </summary>
public sealed class RaporTestleri : IDisposable
{
    private static string Ornek => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private readonly string _kok;

    public RaporTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-rapor-" + Guid.NewGuid().ToString("N"));
        Kopyala(Ornek, _kok);
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, recursive: true); } catch (IOException) { }
    }

    // ------------------------------------------------------------------
    // DURUSTLUK KORUMASI - bu ucu kirilirsa bos liste "sorun yok" diye
    // okunur ve kullanici dosya siler.
    // ------------------------------------------------------------------

    [Fact]
    public void TARANMAMIS_indekste_HICBIR_rapor_GUVENILIR_DEGIL()
    {
        var indeks = new ReferansIndeksi(_kok);   // hic taranmadi

        foreach (IRapor rapor in RaporListesi.Tumu)
        {
            RaporSonucu sonuc = rapor.Uret(indeks);
            Assert.False(sonuc.Guvenilir, rapor.Ad + " taranmamis indekste GUVENILIR dedi");
            Assert.False(string.IsNullOrWhiteSpace(sonuc.Sebep), rapor.Ad + " sebep yazmadi");
        }
    }

    [Fact]
    public void TARANMIS_indekste_raporlar_GUVENILIR()
    {
        ReferansIndeksi indeks = Indeks();

        foreach (IRapor rapor in RaporListesi.Tumu)
        {
            // TABAN: yukaridaki test tek basina "hep false donen" bir
            // bayrakla da gecerdi.
            Assert.True(rapor.Uret(indeks).Guvenilir, rapor.Ad + " taranmis indekste guvenilmez dedi");
        }
    }

    [Fact]
    public void HER_RAPORUN_ADI_ve_ACIKLAMASI_VAR()
    {
        // Rapor penceresi sekme basligini ve aciklamayi bunlardan yaziyor;
        // bos biri sekmeyi adsiz birakirdi.
        foreach (IRapor rapor in RaporListesi.Tumu)
        {
            Assert.False(string.IsNullOrWhiteSpace(rapor.Ad));
            Assert.False(string.IsNullOrWhiteSpace(rapor.Aciklama));
        }
    }

    // ------------------------------------------------------------------
    // TEK TEK RAPORLAR - her biri kendi durumunu KURARAK olculuyor.
    // ------------------------------------------------------------------

    [Fact]
    public void YETIM_yalniz_KULLANILMAYAN_PARCAYI_sayiyor()
    {
        // EN TEHLIKELI RAPOR: burada gorunen ad, kullanicinin sildigi
        // dosyadir. Iki sart birden olculuyor - kullanilan parca
        // GORUNMEMELI, kullanilmayan GORUNMELI.
        File.Copy(Path.Combine(_kok, "Parça1.SLDPRT"), Yol("Yalniz.SLDPRT"));
        ReferansIndeksi indeks = Indeks();

        RaporSonucu sonuc = Rapor("Yetim parçalar").Uret(indeks);

        Assert.Contains(sonuc.Satirlar, r => r.Yol == Yol("Yalniz.SLDPRT"));
        Assert.DoesNotContain(sonuc.Satirlar, r => r.Yol == Yol("Parça1.SLDPRT"));

        // MONTAJ VE TEKNIK RESIM YETIM SAYILMAZ - en ustte durmalari
        // NORMAL; sayilsalardi rapor gurultuden okunmaz olurdu.
        Assert.DoesNotContain(sonuc.Satirlar, r => r.Yol.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(sonuc.Satirlar, r => r.Yol.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KIRIK_referans_dosya_SILININCE_gorunuyor()
    {
        // Teknik resmin kullandigi parca UYGULAMA DISINDA silindi: artik
        // cozulemiyor, yani KIRIK.
        File.Delete(Yol("Parça1.SLDPRT"));
        ReferansIndeksi indeks = Indeks();

        RaporSonucu sonuc = Rapor("Kırık referanslar").Uret(indeks);

        Assert.Contains(sonuc.Satirlar, r => r.Yol == Yol("Parça1.SLDDRW"));
        Assert.Contains(sonuc.Satirlar, r => r.Aciklama.Contains("Parça1.SLDPRT", StringComparison.Ordinal));
    }

    [Fact]
    public void KIRIK_saglam_agacta_BOS_ama_GUVENILIR()
    {
        // "0 bulgu" ile "bakilmadi" AYRI SEYLER (CLAUDE.md 3) - saglam
        // agacta liste bos ve bu sefer bos olmasi GUVENILIR.
        RaporSonucu sonuc = Rapor("Kırık referanslar").Uret(Indeks());

        Assert.Empty(sonuc.Satirlar);
        Assert.True(sonuc.Guvenilir);
    }

    [Fact]
    public void BAYAT_yol_dosya_DISARIDAN_TASININCA_gorunuyor()
    {
        // Dosya duruyor ve biz onu buluyoruz, ama icinde yazan yol eski
        // yeri gosteriyor - SOLIDWORKS acamaz.
        string alt = Yol("3");
        Directory.CreateDirectory(alt);
        File.Move(Yol("Parça1.SLDPRT"), Path.Combine(alt, "Parça1.SLDPRT"));

        RaporSonucu sonuc = Rapor("Bayat yollar").Uret(Indeks());

        Assert.Contains(sonuc.Satirlar, r => r.Yol == Yol("Parça1.SLDDRW"));
    }

    [Fact]
    public void TEKNIK_RESMI_OLMAYAN_yalniz_parca_ve_montaji_sayiyor()
    {
        File.Copy(Path.Combine(_kok, "Parça1.SLDPRT"), Yol("Resimsiz.SLDPRT"));
        ReferansIndeksi indeks = Indeks();

        RaporSonucu sonuc = Rapor("Teknik resmi olmayanlar").Uret(indeks);

        Assert.Contains(sonuc.Satirlar, r => r.Yol == Yol("Resimsiz.SLDPRT"));

        // Parça1'in teknik resmi VAR (Parça1.SLDDRW) - listede olmamali.
        Assert.DoesNotContain(sonuc.Satirlar, r => r.Yol == Yol("Parça1.SLDPRT"));

        // Teknik resmin kendisi hic sorulmaz.
        Assert.DoesNotContain(sonuc.Satirlar, r => r.Yol.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TASINMIS_dosya_kendi_YOLUNDAN_anlasiliyor()
    {
        // Ornek dosyalarin icinde BASKA BIR MAKINENIN yolu yazili
        // (C:\Users\PC\Desktop\tertemiz\...) - yani hepsi "tasinmis".
        // Rapor bunu dosyanin KENDI kayitli yolundan buluyor.
        RaporSonucu sonuc = Rapor("Taşınmış dosyalar").Uret(Indeks());

        Assert.NotEmpty(sonuc.Satirlar);
        Assert.All(sonuc.Satirlar, r => Assert.StartsWith("Son kaydedildiği yer:", r.Aciklama, StringComparison.Ordinal));
    }

    [Fact]
    public void OKUNAMAYAN_dosya_SEBEBIYLE_listeleniyor()
    {
        // SOLIDWORKS uzantili ama icerigi bozuk bir dosya: okunamaz ve
        // sebebi YAZILMALI - "okunamadi" ile "referansi yok" ayri seyler.
        File.WriteAllText(Yol("Bozuk.SLDPRT"), "bu bir SOLIDWORKS dosyasi degil");

        RaporSonucu sonuc = Rapor("Okunamayan dosyalar").Uret(Indeks());

        Assert.Contains(sonuc.Satirlar, r => r.Yol == Yol("Bozuk.SLDPRT"));
        Assert.All(sonuc.Satirlar, r => Assert.False(string.IsNullOrWhiteSpace(r.Aciklama)));
    }

    [Fact]
    public void DUZELTMEYI_yalnizca_BAYAT_YOLLAR_destekliyor()
    {
        // Rapor penceresi "Düzelt" dugmesini bu cevaba gore ciziyor; oteki
        // raporlar duzeltilemez ve null donmeli (yoksa dugme cikar ve
        // hicbir sey yapmaz).
        ReferansIndeksi indeks = Indeks();

        foreach (IRapor rapor in RaporListesi.Tumu)
        {
            OnarimOzeti? ozet = rapor.Duzelt(indeks, kilitler: null);
            if (rapor.Ad == "Bayat yollar")
            {
                Assert.NotNull(ozet);
            }
            else
            {
                Assert.Null(ozet);
            }
        }
    }

    // ------------------------------------------------------------------

    private static IRapor Rapor(string ad)
        => RaporListesi.Tumu.First(r => r.Ad == ad);

    private ReferansIndeksi Indeks()
    {
        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);
        return indeks;
    }

    private string Yol(params string[] p) => Path.Combine([_kok, .. p]);

    private static void Kopyala(string kaynak, string hedef)
    {
        Directory.CreateDirectory(hedef);
        foreach (string dosya in Directory.GetFiles(kaynak))
        {
            File.Copy(dosya, Path.Combine(hedef, Path.GetFileName(dosya)));
        }

        foreach (string klasor in Directory.GetDirectories(kaynak))
        {
            Kopyala(klasor, Path.Combine(hedef, Path.GetFileName(klasor)));
        }
    }
}
