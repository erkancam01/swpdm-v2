using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Belge bilgileri ve gomulu onizleme, GERCEK dosyalara karsi.
///
/// veri/ozellikli/Parça1.SLDPRT: Erkan bu dosyaya elle uc ozellik girdi -
/// Malzeme = "Pirinç", Ağırlık (kutle denklemine bagli), Çizen (BOS
/// birakildi). Uc hali birden tek dosyada olcmek icin bilerek boyle.
/// </summary>
public class SwBelgeBilgisiTestleri
{
    private static string Ozellikli
        => Path.Combine(AppContext.BaseDirectory, "veri", "ozellikli", "Parça1.SLDPRT");

    private static string Sade
        => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz", "Parça1.SLDPRT");

    private static string Deger(SwBelgeBilgileri b, string ad)
        => b.Ozel.First(c => c.Key == ad).Value;

    [Fact]
    public void OzelOzellikler_OKUNUYOR()
    {
        SwBelgeBilgileri b = SwBelgeBilgisi.Oku(Ozellikli);

        Assert.True(b.Okundu, b.Sebep);
        Assert.Equal("Pirinç", Deger(b, "Malzeme"));
        Assert.Equal("851.42", Deger(b, "Ağırlık"));
    }

    [Fact]
    public void BOS_DEGERLI_OZELLIK_LISTEDE_KALIR()
    {
        // "Çizen" var ama degeri bos. Listeden dusurmek "boyle bir alan yok"
        // demek olurdu - oysa alan var, doldurulmamis. CLAUDE.md 3.
        SwBelgeBilgileri b = SwBelgeBilgisi.Oku(Ozellikli);

        Assert.Contains(b.Ozel, c => c.Key == "Çizen");
        Assert.Equal(string.Empty, Deger(b, "Çizen"));
    }

    [Fact]
    public void DENKLEM_KAYNAKLARI_OZELLIK_SAYILMAZ()
    {
        // Dosyada adi BOS, degeri "SW-Kütle@Parça1.SLDPRT" olan ic kayitlar
        // var; onlar kullanicinin ozelligi degil, denklemin kaynagi.
        SwBelgeBilgileri b = SwBelgeBilgisi.Oku(Ozellikli);

        Assert.DoesNotContain(b.Ozel, c => string.IsNullOrWhiteSpace(c.Key));
        Assert.DoesNotContain(b.Ozel, c => c.Value.StartsWith("\"SW-", StringComparison.Ordinal));
    }

    [Fact]
    public void OzelligiOlmayanDosya_OKUNUR_AMA_BOS()
    {
        // Bos liste "okunamadi" DEGIL. Bu dosyaya hic ozellik girilmemis.
        SwBelgeBilgileri b = SwBelgeBilgisi.Oku(Sade);

        Assert.True(b.Okundu, b.Sebep);
        Assert.Empty(b.Ozel);
    }

    [Fact]
    public void SistemOzellikleri_VE_YAPILANDIRMA_OKUNUYOR()
    {
        SwBelgeBilgileri b = SwBelgeBilgisi.Oku(Sade);

        Assert.True(b.Okundu, b.Sebep);
        Assert.Equal("Varsayılan", b.Yapilandirma);
        Assert.Equal("Parça1", b.SistemOzelligi("SW-Dosya Adı"));
    }

    [Fact]
    public void KIM_NE_ZAMAN_KAYDETTI_OKUNUYOR()
    {
        SwBelgeBilgileri b = SwBelgeBilgisi.Oku(Sade);

        Assert.Equal("PC", b.SonKaydeden);
        Assert.NotNull(b.Olusturma);
        Assert.NotNull(b.Degistirme);
        Assert.True(b.Degistirme >= b.Olusturma);
    }

    [Fact]
    public void SwOlmayanDosya_SEBEBINI_SOYLER()
    {
        string gecici = Path.Combine(Path.GetTempPath(), "swpdm-bilgi-" + Guid.NewGuid().ToString("N")[..8]);
        File.WriteAllText(gecici, "solidworks degil");
        try
        {
            SwBelgeBilgileri b = SwBelgeBilgisi.Oku(gecici);

            Assert.False(b.Okundu);
            Assert.False(string.IsNullOrWhiteSpace(b.Sebep));
        }
        finally
        {
            File.Delete(gecici);
        }
    }

    [Fact]
    public void Onizleme_GERCEK_PNG_DONUYOR()
    {
        // Bu, uygulamanin bugun OLCEMEDIGI bir alani kapatiyor: kabuk
        // onizleme saglayicisi Wine'da yok, SOLIDWORKS kurulu olmayan
        // Windows'ta da .SLDPRT icin resim vermiyor.
        byte[]? resim = SwOnizleme.Oku(Sade);

        Assert.NotNull(resim);
        Assert.True(resim!.Length > 1000);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], resim.Take(4));
    }

    [Fact]
    public void Onizleme_HER_TERTEMIZ_DOSYADA_VAR()
    {
        string kok = Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

        foreach (string yol in Directory.GetFiles(kok, "*.SLD*", SearchOption.AllDirectories))
        {
            Assert.True(SwOnizleme.Oku(yol) is { Length: > 0 }, WindowsYolu.DosyaAdi(yol));
        }
    }

    [Fact]
    public void Onizleme_SwOlmayanDosyada_NULL()
    {
        string gecici = Path.Combine(Path.GetTempPath(), "swpdm-onz-" + Guid.NewGuid().ToString("N")[..8] + ".SLDPRT");
        File.WriteAllText(gecici, "solidworks degil");
        try
        {
            Assert.Null(SwOnizleme.Oku(gecici));
        }
        finally
        {
            File.Delete(gecici);
        }
    }
}
