using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Kalici ayarlar. Asil sinav CLAUDE.md 3: BOZUK bir ayar dosyasi uygulamayi
/// dusurmemeli - kullanici uygulamayi hic acamaz hale gelirse ayarin hicbir
/// degeri kalmaz.
/// </summary>
public class AyarlarTestleri : IDisposable
{
    private readonly string _dosya;

    public AyarlarTestleri()
        => _dosya = Path.Combine(
            Path.GetTempPath(), "swpdm-ayar-" + Guid.NewGuid().ToString("N")[..8] + ".txt");

    public void Dispose()
    {
        try
        {
            File.Delete(_dosya);
        }
        catch (IOException)
        {
            // Temizlik sonucu degistirmez.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Oku_DosyaYoksa_BosAyarVerir_HataDegil()
    {
        Ayarlar ayarlar = Ayarlar.Oku(_dosya);

        Assert.Null(ayarlar.SonKok);
        Assert.Empty(ayarlar.SonKokler);
        Assert.Null(ayarlar.CopUstKlasoru);
    }

    [Fact]
    public void YazOku_KOKU_HATIRLAR()
    {
        var yazilan = new Ayarlar();
        yazilan.KokEkle(@"C:\Proje\A");
        Assert.True(yazilan.Yaz(_dosya));

        Ayarlar okunan = Ayarlar.Oku(_dosya);

        Assert.Equal(@"C:\Proje\A", okunan.SonKok);
    }

    [Fact]
    public void YazOku_UC_BOYUTLU_ONIZLEMEYI_HATIRLAR()
    {
        var yazilan = new Ayarlar { OnizlemeUcBoyutlu = true };
        Assert.True(yazilan.Yaz(_dosya));

        Assert.True(Ayarlar.Oku(_dosya).OnizlemeUcBoyutlu);

        // Kapatilinca varsayilana doner ve dosyaya satir YAZILMAZ.
        yazilan.OnizlemeUcBoyutlu = false;
        Assert.True(yazilan.Yaz(_dosya));
        Assert.False(Ayarlar.Oku(_dosya).OnizlemeUcBoyutlu);
    }

    [Fact]
    public void KokEkle_EN_YENI_BASTA()
    {
        var ayarlar = new Ayarlar();
        ayarlar.KokEkle(@"C:\A");
        ayarlar.KokEkle(@"C:\B");

        Assert.Equal(@"C:\B", ayarlar.SonKok);
        Assert.Equal([@"C:\B", @"C:\A"], ayarlar.SonKokler);
    }

    [Fact]
    public void KokEkle_AYNI_KOKU_IKI_KEZ_YAZMAZ_ONE_ALIR()
    {
        var ayarlar = new Ayarlar();
        ayarlar.KokEkle(@"C:\A");
        ayarlar.KokEkle(@"C:\B");
        ayarlar.KokEkle(@"c:\a");   // ayni yol, farkli harf boyu

        Assert.Equal(2, ayarlar.SonKokler.Count);
        Assert.Equal(@"c:\a", ayarlar.SonKok);
    }

    [Fact]
    public void KokEkle_SINIRI_ASMAZ()
    {
        var ayarlar = new Ayarlar();
        for (int i = 0; i < Ayarlar.GecmisSiniri + 5; i++)
        {
            ayarlar.KokEkle($@"C:\K{i}");
        }

        Assert.Equal(Ayarlar.GecmisSiniri, ayarlar.SonKokler.Count);
        Assert.Equal(@"C:\K14", ayarlar.SonKok);   // en yeni basta
    }

    [Fact]
    public void KokCikar_ArtikYokOlanKokuListedenAlir()
    {
        var ayarlar = new Ayarlar();
        ayarlar.KokEkle(@"C:\A");
        ayarlar.KokEkle(@"C:\B");

        ayarlar.KokCikar(@"C:\b");

        Assert.Equal([@"C:\A"], ayarlar.SonKokler);
    }

    [Fact]
    public void YazOku_COP_UST_KLASORUNU_HATIRLAR()
    {
        var yazilan = new Ayarlar { CopUstKlasoru = @"D:\Cop" };
        yazilan.Yaz(_dosya);

        Assert.Equal(@"D:\Cop", Ayarlar.Oku(_dosya).CopUstKlasoru);
    }

    [Fact]
    public void Oku_BOZUK_SATIRLAR_UYGULAMAYI_DUSURMEZ()
    {
        File.WriteAllLines(_dosya, [
            "bu bozuk bir satir",
            "=degersiz anahtar",
            "kok=C:\\Saglam",
            "bilinmeyenAnahtar=deger",
            "kok=",
        ]);

        Ayarlar okunan = Ayarlar.Oku(_dosya);

        // Bozuklar ATLANIR, saglam olan yine okunur.
        Assert.Equal(@"C:\Saglam", okunan.SonKok);
        Assert.Single(okunan.SonKokler);
    }

    [Fact]
    public void KokEkle_BosYoluAlmaz()
    {
        var ayarlar = new Ayarlar();
        ayarlar.KokEkle("   ");

        Assert.Empty(ayarlar.SonKokler);
    }
    [Fact]
    public void YERLESIM_diske_yazilip_geri_OKUNUYOR()
    {
        // Pencere boyutu, iki bolucu ve son suzgec: uygulama her acilista
        // bunlari sifirliyordu.
        var ayarlar = new Ayarlar
        {
            PencereBoyutu = "900x1000",
            DikeyBolen = 400,
            AltBolen = 250,
            Suzgec = "Parça",
        };

        Assert.True(ayarlar.Yaz(_dosya));

        Ayarlar geri = Ayarlar.Oku(_dosya);
        Assert.Equal("900x1000", geri.PencereBoyutu);
        Assert.Equal(400, geri.DikeyBolen);
        Assert.Equal(250, geri.AltBolen);
        Assert.Equal("Parça", geri.Suzgec);
    }

    [Fact]
    public void BOZUK_SAYI_ayarlarin_TAMAMINI_bozmuyor()
    {
        // Dosya elle duzenlenebiliyor; bozuk tek bir satir yuzunden butun
        // ayarlarin kaybolmasi kabul edilemez (CLAUDE.md 1a).
        File.WriteAllLines(_dosya, ["dikeyBolen=abc", "altBolen=250", "otomatikTazele=hayir"]);

        Ayarlar geri = Ayarlar.Oku(_dosya);
        Assert.Null(geri.DikeyBolen);
        Assert.Equal(250, geri.AltBolen);
        Assert.False(geri.OtomatikTazele);
    }

}
