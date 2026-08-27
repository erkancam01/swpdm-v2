using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// GERCEK bir bilesik belgeye karsi kosuyor. Dosya
/// araclar/ornek-veri/bilesik_dosya_uret.py ile uretildi ve BAGIMSIZ bir
/// okuyucuyla (python olefile) capraz dogrulandi - yani beklenen degerler
/// benim kendi kodumdan turetilmedi.
/// </summary>
public class BilesikDosyaTestleri
{
    private static string VeriYolu(string ad) => Path.Combine(AppContext.BaseDirectory, "veri", ad);

    private static byte[] BeklenenPng => File.ReadAllBytes(VeriYolu("onizleme.png"));

    [Fact]
    public void Ac_GercekBilesikBelgeyiAcar()
    {
        using BilesikDosya? b = BilesikDosya.Ac(VeriYolu("ornek.sldprt"));
        Assert.NotNull(b);
        Assert.Contains("PreviewPNG", b.AkisAdlari);
        Assert.Contains("BuyukAkis", b.AkisAdlari);
    }

    [Fact]
    public void AkisiOku_MiniAkisYolu_BIREBIR_dogru()
    {
        // 3.330 bayt: mini akis esiginin (4096) ALTINDA -> miniFAT yolu.
        using BilesikDosya? b = BilesikDosya.Ac(VeriYolu("ornek.sldprt"));
        byte[]? veri = b!.AkisiOku("PreviewPNG");

        Assert.NotNull(veri);
        Assert.Equal(BeklenenPng, veri);
    }

    [Fact]
    public void AkisiOku_NormalSektorYolu_BIREBIR_dogru()
    {
        // 6.144 bayt: esigin USTUNDE -> normal sektor zinciri.
        using BilesikDosya? b = BilesikDosya.Ac(VeriYolu("ornek.sldprt"));
        byte[]? veri = b!.AkisiOku("BuyukAkis");

        Assert.NotNull(veri);
        Assert.Equal(6144, veri.Length);
        Assert.Equal(Enumerable.Range(0, 256).Select(i => (byte)i).ToArray(), veri.Take(256));
    }

    [Fact]
    public void AkisiOku_BuyukKucukHarfAyirmaz()
    {
        using BilesikDosya? b = BilesikDosya.Ac(VeriYolu("ornek.sldprt"));
        Assert.NotNull(b!.AkisiOku("previewpng"));
    }

    [Fact]
    public void AkisiOku_OlmayanAkis_null()
    {
        using BilesikDosya? b = BilesikDosya.Ac(VeriYolu("ornek.sldprt"));
        Assert.Null(b!.AkisiOku("BoyleBirAkisYok"));
    }

    [Fact]
    public void Ac_BilesikOlmayanDosya_null()
    {
        string gecici = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sldprt");
        File.WriteAllText(gecici, "bu duz metin, bilesik belge degil");
        try
        {
            Assert.Null(BilesikDosya.Ac(gecici));
        }
        finally
        {
            File.Delete(gecici);
        }
    }

    [Fact]
    public void Ac_OlmayanDosya_ISTISNA_ATMAZ()
    {
        Assert.Null(BilesikDosya.Ac(Path.Combine(Path.GetTempPath(), "boyle-bir-dosya-yok.sldprt")));
    }

    [Fact]
    public void Ac_KIRPILMIS_dosya_istisna_atmaz()
    {
        // Bu dosyalar disaridan geliyor: bozugu cokme degil null uretmeli.
        byte[] tam = File.ReadAllBytes(VeriYolu("ornek.sldprt"));
        string gecici = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sldprt");
        File.WriteAllBytes(gecici, tam[..1024]);
        try
        {
            using BilesikDosya? b = BilesikDosya.Ac(gecici);
            if (b is not null)
            {
                _ = b.AkisiOku("PreviewPNG");   // cokmemeli
            }
        }
        finally
        {
            File.Delete(gecici);
        }
    }

    [Fact]
    public void Ac_BOZULMUS_govde_istisna_atmaz()
    {
        byte[] tam = File.ReadAllBytes(VeriYolu("ornek.sldprt"));
        var bozuk = (byte[])tam.Clone();
        for (int i = 600; i < 900 && i < bozuk.Length; i++)
        {
            bozuk[i] = 0xFF;   // FAT'in ortasini cop yap
        }

        string gecici = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sldprt");
        File.WriteAllBytes(gecici, bozuk);
        try
        {
            using BilesikDosya? b = BilesikDosya.Ac(gecici);
            if (b is not null)
            {
                foreach (string ad in b.AkisAdlari.ToArray())
                {
                    _ = b.AkisiOku(ad);
                }
            }
        }
        finally
        {
            File.Delete(gecici);
        }
    }

    // ------------------------------------------------------ OnizlemeOkuyucu

    [Fact]
    public void Onizleme_GomuluResmiCikarir()
    {
        byte[]? resim = OnizlemeOkuyucu.Oku(VeriYolu("ornek.sldprt"));

        Assert.NotNull(resim);
        Assert.Equal(BeklenenPng, resim);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Onizleme_BosGirdi_null(string? yol) => Assert.Null(OnizlemeOkuyucu.Oku(yol));

    [Fact]
    public void ResmeCevir_TanidigiBicimleriGecirir()
    {
        Assert.NotNull(OnizlemeOkuyucu.ResmeCevir([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));  // PNG
        Assert.NotNull(OnizlemeOkuyucu.ResmeCevir([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0]));               // JPEG
        Assert.NotNull(OnizlemeOkuyucu.ResmeCevir([0x42, 0x4D, 0, 0, 0, 0, 0, 0]));                     // BMP
    }

    [Fact]
    public void ResmeCevir_CiplakDIBe_BMP_basligi_ekler()
    {
        // 40 baytlik BITMAPINFOHEADER, 24 bit, sikistirmasiz, palet yok.
        var dib = new byte[40 + 12];
        BitConverter.GetBytes(40u).CopyTo(dib, 0);
        BitConverter.GetBytes((ushort)24).CopyTo(dib, 14);

        byte[]? bmp = OnizlemeOkuyucu.ResmeCevir(dib);

        Assert.NotNull(bmp);
        Assert.Equal(0x42, bmp[0]);                                      // 'B'
        Assert.Equal(0x4D, bmp[1]);                                      // 'M'
        Assert.Equal(dib.Length + 14, BitConverter.ToInt32(bmp, 2));     // dosya boyu
        Assert.Equal(54, BitConverter.ToInt32(bmp, 10));                 // veri basi = 14 + 40
    }

    [Fact]
    public void ResmeCevir_TanimadigiBicim_null()
        => Assert.Null(OnizlemeOkuyucu.ResmeCevir([1, 2, 3, 4, 5, 6, 7, 8]));
}
