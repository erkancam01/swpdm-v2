using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Referans okuma, GERCEK SOLIDWORKS 2022 dosyalarina karsi.
///
/// Bu testler sentetik dosyayla YAZILAMAZ: SOLIDWORKS 2022 dosyalari OLE
/// Bilesik Belge degil, kendi kaplari. Bu fark ilk turda bir kez yanlis
/// yola soktu - "hicbir sey bulunamadi" denip birakilacakti. O yuzden
/// araclar/ornek-veri/tertemiz/ altindaki gercek dosyalar depoya kondu.
///
/// Ornek kumenin GERCEGI (Erkan sifirdan uretti, hicbiri yeniden
/// adlandirilmadi, hicbiri Farkli Kaydet ile cogaltilmadi):
///   Montaj1.SLDASM -> Parça1.SLDPRT + Yeni klasör\Parça2.SLDPRT
///   Montaj2.SLDASM -> Montaj1.SLDASM
///   Parça1.SLDDRW  -> Parça1.SLDPRT
///   Montaj2.SLDDRW -> Montaj2.SLDASM
///   Parça2.SLDDRW  -> Parça2.SLDPRT
///   Parça1/2.SLDPRT -> (yok)
/// </summary>
public class SwReferansTestleri
{
    private static string Kok => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private static string Yol(params string[] parcalar)
        => Path.Combine([Kok, .. parcalar]);

    /// <summary>Sonuctaki yollarin yalnizca dosya adlari, siradan bagimsiz.</summary>
    private static List<string> Adlar(SwReferanslar r)
        => r.Dogrudan.Select(WindowsYolu.DosyaAdi).OrderBy(a => a, StringComparer.Ordinal).ToList();

    [Fact]
    public void Montaj_KULLANDIGI_PARCALARI_VERIR()
    {
        SwReferanslar r = SwReferans.Oku(Yol("Montaj1.SLDASM"));

        Assert.True(r.Okundu, r.Sebep);
        Assert.Equal(["Parça1.SLDPRT", "Parça2.SLDPRT"], Adlar(r));
    }

    [Fact]
    public void Montaj_ALT_MONTAJI_VERIR()
    {
        // Montaj2 icinde Montaj1 var. Torunlar (Parça1/2) DOGRUDAN referans
        // DEGIL: onlar "Contents/DisplayLists" akisinda geciyor, "Header2"de
        // degil. Ayrimi yapan sey bu.
        SwReferanslar r = SwReferans.Oku(Yol("Yeni klasör", "Montaj2.SLDASM"));

        Assert.True(r.Okundu, r.Sebep);
        Assert.Equal(["Montaj1.SLDASM"], Adlar(r));
    }

    [Fact]
    public void TeknikResim_MODELINI_VERIR()
    {
        // CLAUDE.md 5'te bu zincir "HENUZ OLCULMEDI" diye duruyordu.
        SwReferanslar r = SwReferans.Oku(Yol("Parça1.SLDDRW"));

        Assert.True(r.Okundu, r.Sebep);
        Assert.Equal(["Parça1.SLDPRT"], Adlar(r));
    }

    [Fact]
    public void TeknikResim_MONTAJINI_VERIR()
    {
        SwReferanslar r = SwReferans.Oku(Yol("Yeni klasör", "Montaj2.SLDDRW"));

        Assert.True(r.Okundu, r.Sebep);
        Assert.Equal(["Montaj2.SLDASM"], Adlar(r));
    }

    [Fact]
    public void Yaprak_Parca_REFERANS_VERMEZ_AMA_OKUNUR()
    {
        // Bos liste "okunamadi" DEGIL: okundu ve gercekten referansi yok.
        // Bu ayrim CLAUDE.md 3'un kendisi.
        SwReferanslar r = SwReferans.Oku(Yol("Parça1.SLDPRT"));

        Assert.True(r.Okundu, r.Sebep);
        Assert.Empty(r.Dogrudan);
    }

    [Fact]
    public void Belge_KENDI_YOLUNU_BILIYOR()
    {
        SwReferanslar r = SwReferans.Oku(Yol("Yeni klasör", "Parça2.SLDPRT"));

        Assert.True(r.Okundu, r.Sebep);
        Assert.Equal(@"C:\Users\PC\Desktop\tertemiz\Yeni klasör\Parça2.SLDPRT", r.KendiYolu);
    }

    [Fact]
    public void Belge_KENDINI_REFERANS_SAYMAZ()
    {
        // Dosya kendi yolunu da yaziyor; onu referans listesine koymak
        // "kendini kullaniyor" gibi bir sacmalik uretirdi.
        foreach (string yol in Directory.GetFiles(Kok, "*.SLD*", SearchOption.AllDirectories))
        {
            SwReferanslar r = SwReferans.Oku(yol);
            string kendi = WindowsYolu.DosyaAdi(yol);

            Assert.DoesNotContain(
                r.Dogrudan,
                y => string.Equals(WindowsYolu.DosyaAdi(y), kendi, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void YEDI_DOSYANIN_YEDISI_DE_OKUNUR()
    {
        // Bu test bir HATAYI kilitliyor: ilk halinde okuyucu yedi dosyanin
        // UCUNDE calisiyordu. Sebep, zincir yurumesinin sahte akis basliklari
        // kabul etmesiydi ("Preview 364 -> 728" gibi, acilmis boyut sikisigin
        // tam iki kati). Sayisal denetim ve ad denetimi YETMEDI; veriyi
        // gercekten ACARAK dogrulamak ayirdi.
        string[] hepsi = Directory.GetFiles(Kok, "*.SLD*", SearchOption.AllDirectories);

        Assert.Equal(7, hepsi.Length);
        foreach (string yol in hepsi)
        {
            SwReferanslar r = SwReferans.Oku(yol);
            Assert.True(r.Okundu, $"{WindowsYolu.DosyaAdi(yol)}: {r.Sebep}");
        }
    }

    [Fact]
    public void OlmayanDosya_SEBEBINI_SOYLER()
    {
        SwReferanslar r = SwReferans.Oku(Yol("hic-yok.SLDPRT"));

        Assert.False(r.Okundu);
        Assert.False(string.IsNullOrWhiteSpace(r.Sebep));
        Assert.Empty(r.Dogrudan);
    }

    [Fact]
    public void SwDosyasiOlmayan_SEBEBINI_SOYLER()
    {
        // Bos liste dondurup "referansi yok" demek YALAN olurdu.
        string gecici = Path.Combine(Path.GetTempPath(), "swpdm-sahte-" + Guid.NewGuid().ToString("N")[..8]);
        File.WriteAllText(gecici, "bu bir solidworks dosyasi degil");
        try
        {
            SwReferanslar r = SwReferans.Oku(gecici);

            Assert.False(r.Okundu);
            Assert.Contains("tanınmadı", r.Sebep ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(gecici);
        }
    }

    [Theory]
    [InlineData("a.SLDPRT", true)]
    [InlineData("a.sldasm", true)]
    [InlineData("a.SLDDRW", true)]
    [InlineData("a.pdf", false)]
    [InlineData("a.txt", false)]
    [InlineData(null, false)]
    public void TasiyabilirMi_TURU_AYIRT_EDER(string? yol, bool beklenen)
        => Assert.Equal(beklenen, SwReferans.TasiyabilirMi(yol));
}
