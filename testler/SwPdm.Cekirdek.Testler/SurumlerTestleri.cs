using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Versiyon arsivi GERCEK klasorlerle kosuyor. Asil sinav CLAUDE.md 1a:
/// hicbir icerik hicbir islemle kaybolmamali - "don" bile once bugunku hali
/// arsivlemeli. Kaybolursa uygulama, kullanicinin PARCASINI kaybettirir.
/// </summary>
public class SurumlerTestleri : IDisposable
{
    private readonly string _kok;

    public SurumlerTestleri()
    {
        _kok = Path.Combine(
            Path.GetTempPath(), "swpdm-surum-" + Guid.NewGuid().ToString("N")[..8]);
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

        GC.SuppressFinalize(this);
    }

    private string DosyaKoy(string ad, string icerik)
    {
        string yol = WindowsYolu.Birlestir(_kok, ad);
        Directory.CreateDirectory(WindowsYolu.Klasor(yol));
        File.WriteAllText(yol, icerik);
        return yol;
    }

    [Fact]
    public void IlkVersiyon_SIFIRDIR_ve_icerik_birebir()
    {
        // Erkan'in 1. beklentisi: mevcut dosya v0 sayilir.
        string yol = DosyaKoy("Parca1.SLDPRT", "ilk hal");

        IslemRaporu rapor = Surumler.Olustur(_kok, yol, "ilk", out int no);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal(0, no);
        Assert.Equal("ilk hal", File.ReadAllText(rapor.YeniYol!));
    }

    [Fact]
    public void IkinciVersiyon_BIRDIR_ve_liste_yeniden_eskiye()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "ilk hal");
        Surumler.Olustur(_kok, yol, "ilk", out _);

        File.WriteAllText(yol, "ikinci hal");
        Surumler.Olustur(_kok, yol, "ikinci", out int no);

        Assert.Equal(1, no);

        SurumDurumu durum = Surumler.Listele(_kok, yol);
        Assert.True(durum.Guvenilir);
        Assert.Equal(2, durum.Ogeler.Count);
        Assert.Equal(1, durum.Ogeler[0].No);      // en yeni basta
        Assert.Equal("ikinci", durum.Ogeler[0].Not);
        Assert.Equal(0, durum.Ogeler[1].No);
    }

    [Fact]
    public void Don_ICERIGI_GERI_GETIRIR_ve_once_bugunu_arsivler()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "eski hal");
        Surumler.Olustur(_kok, yol, "ilk", out _);
        File.WriteAllText(yol, "bugunku hal");

        IslemRaporu rapor = Surumler.Don(_kok, yol, 0);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal("eski hal", File.ReadAllText(yol));

        // DONUS DE BIR VERSIYONDUR: "bugunku hal" kaybolmamali.
        SurumDurumu durum = Surumler.Listele(_kok, yol);
        Assert.Equal(2, durum.Ogeler.Count);
        Assert.Equal("bugunku hal", File.ReadAllText(durum.Ogeler[0].ArsivYolu));
        Assert.Contains("dönmeden önce", durum.Ogeler[0].Not, StringComparison.Ordinal);
    }

    [Fact]
    public void Don_KILITLI_dosyada_reddedilir_ve_dosyaya_dokunmaz()
    {
        // SOLIDWORKS acik belge icin ~$ kilidi yazar (CLAUDE.md 5).
        string yol = DosyaKoy("Parca1.SLDPRT", "eski");
        Surumler.Olustur(_kok, yol, "", out _);
        File.WriteAllText(yol, "acik ve degismis");
        File.WriteAllText(Kilit.KilidininYolu(yol), "");

        IslemRaporu rapor = Surumler.Don(_kok, yol, 0);

        Assert.False(rapor.Oldu);
        Assert.Equal(IslemSonucu.Kilitli, rapor.Sonuc);
        Assert.Equal("acik ve degismis", File.ReadAllText(yol));
    }

    [Fact]
    public void OlmayanVersiyona_don_SEBEBIYLE_reddedilir()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "", out _);

        IslemRaporu rapor = Surumler.Don(_kok, yol, 7);

        Assert.False(rapor.Oldu);
        Assert.Contains("v7", rapor.Sebebi, StringComparison.Ordinal);
    }

    [Fact]
    public void BozukKayitSatiri_ATLANIR_ve_SAYILIR()
    {
        // CLAUDE.md 3: bozuk satiri sessizce yutmak, kullaniciya "o versiyon
        // hic olmadi" dedirtir. Atlanir ama SAYISI soylenir.
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "saglam", out _);

        string kayit = WindowsYolu.Birlestir(
            WindowsYolu.Klasor(Surumler.Listele(_kok, yol).Ogeler[0].ArsivYolu), "kayit.txt");
        File.AppendAllText(kayit, "bu satir bozuk\n");

        SurumDurumu durum = Surumler.Listele(_kok, yol);

        Assert.True(durum.Guvenilir);
        Assert.Single(durum.Ogeler);
        Assert.Equal(1, durum.BozukSatir);
    }

    [Fact]
    public void AyniAdliPRTveDRW_ayri_yuvalarda_CARPISMAZ()
    {
        string prt = DosyaKoy("X.SLDPRT", "parca");
        string drw = DosyaKoy("X.SLDDRW", "resim");

        Surumler.Olustur(_kok, prt, "", out _);
        Surumler.Olustur(_kok, drw, "", out _);

        Assert.Equal(
            "parca", File.ReadAllText(Surumler.Listele(_kok, prt).Ogeler[0].ArsivYolu));
        Assert.Equal(
            "resim", File.ReadAllText(Surumler.Listele(_kok, drw).Ogeler[0].ArsivYolu));
    }

    [Fact]
    public void AltKlasordekiDosyanin_yuvasi_goreli_yolu_izler()
    {
        string yol = DosyaKoy("33/derin/Parca9.SLDPRT", "derin hal");

        IslemRaporu rapor = Surumler.Olustur(_kok, yol, "", out _);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Contains(
            Surumler.KlasorAdi, rapor.YeniYol, StringComparison.Ordinal);
        Assert.Equal("derin hal", File.ReadAllText(rapor.YeniYol!));
    }

    [Fact]
    public void KokDisindakiDosya_versiyonlanmaz_SEBEBIYLE()
    {
        string dis = Path.Combine(
            Path.GetTempPath(), "swpdm-dis-" + Guid.NewGuid().ToString("N")[..8] + ".SLDPRT");
        File.WriteAllText(dis, "disarida");

        try
        {
            IslemRaporu rapor = Surumler.Olustur(_kok, dis, "", out _);
            Assert.False(rapor.Oldu);
            Assert.Contains("Kök dışı", rapor.Sebebi, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dis);
        }
    }

    [Fact]
    public void ArsivKlasoru_TARAYICIDA_GORUNMEZ()
    {
        // Gorunse kullanici arsivi dosya sanir, tasir/siler ve versiyonlar
        // sessizce olur (CLAUDE.md 3). Cop klasoruyle ayni dislama.
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "", out _);

        KlasorIcerigi icerik = KlasorTarayici.Tara(_kok);

        Assert.All(
            icerik.Klasorler,
            k => Assert.NotEqual(Surumler.KlasorAdi, k.Ad));
    }

    [Fact]
    public void ArsivKlasoru_INDEKS_taramasina_girmez()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "", out _);
        File.WriteAllText(yol, "yeni hal");
        Surumler.Olustur(_kok, yol, "", out _);

        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks, default, (_, _, _) => { });

        // Arsivdeki v0/v1 kopyalari taransaydi sayi 3 olurdu.
        Assert.Equal(1, indeks.DosyaSayisi);
    }
}
