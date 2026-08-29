using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// 29.08.2026 §1b denetiminde tek kopyaya indirilen mantiklarin testleri.
///
/// EN ONEMLISI AltindaMi: "altinda mi" sorusu bes yerde uc farkli bicimde
/// elle yazilmisti ve ikisi HATALIYDI - ayirici eklemeyen StartsWith,
/// "C:\Kok2"yi "C:\Kok"un ici sayiyordu. O hata artik BURADA olculuyor;
/// bir gun geri gelirse bu dosya kirilir.
/// </summary>
public class TekKopyaTestleri : IDisposable
{
    private readonly string _kok;

    public TekKopyaTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-tek-" + Guid.NewGuid().ToString("N")[..8]);
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
            // Temizlik tutmazsa testin sonucunu degistirmez.
        }

        GC.SuppressFinalize(this);
    }

    // ---------- WindowsYolu.AltindaMi ----------

    [Fact]
    public void ALTINDAKI_DOSYA_ALTINDA_SAYILIR()
        => Assert.True(WindowsYolu.AltindaMi(@"C:\Kok\Parça1.SLDPRT", @"C:\Kok"));

    [Fact]
    public void KOMSU_KLASOR_ALTINDA_SAYILMAZ()
    {
        // ESKI HATANIN KENDISI: ayiricisiz StartsWith bunu "icinde" sayardi.
        Assert.False(WindowsYolu.AltindaMi(@"C:\Kok2\Parça1.SLDPRT", @"C:\Kok"));
        Assert.False(WindowsYolu.AltindaMi(@"C:\Kokta.txt", @"C:\Kok"));
    }

    [Fact]
    public void KLASORUN_KENDISI_ALTINDA_SAYILIR()
        => Assert.True(WindowsYolu.AltindaMi(@"C:\Kok", @"C:\Kok"));

    [Fact]
    public void BUYUK_KUCUK_HARF_AYIRT_EDILMEZ()
        => Assert.True(WindowsYolu.AltindaMi(@"c:\kok\alt\a.txt", @"C:\KOK"));

    [Fact]
    public void SURUCU_KOKU_HER_SEYI_KAPSAR()
        => Assert.True(WindowsYolu.AltindaMi(@"C:\a.txt", @"C:\"));

    [Fact]
    public void EGIK_AYIRICI_DA_TANINIR()
        => Assert.True(WindowsYolu.AltindaMi(@"C:\Kok/alt/a.txt", @"C:\Kok"));

    [Fact]
    public void BASKA_SURUCU_ALTINDA_SAYILMAZ()
        => Assert.False(WindowsYolu.AltindaMi(@"D:\Kok\a.txt", @"C:\Kok"));

    [Fact]
    public void BOS_GIRDILER_ALTINDA_SAYILMAZ()
    {
        Assert.False(WindowsYolu.AltindaMi(null, @"C:\Kok"));
        Assert.False(WindowsYolu.AltindaMi(@"C:\Kok\a", null));
        Assert.False(WindowsYolu.AltindaMi(string.Empty, string.Empty));
    }

    // ---------- DosyaIslemleri.BosKlasoruSil ----------

    [Fact]
    public void BOS_KLASOR_SILINIR()
    {
        string yol = Path.Combine(_kok, "bos");
        Directory.CreateDirectory(yol);

        Assert.True(DosyaIslemleri.BosKlasoruSil(yol).Oldu);
        Assert.False(Directory.Exists(yol));
    }

    [Fact]
    public void DOLU_KLASORA_DOKUNULMAZ_VE_SEBEP_SOYLENIR()
    {
        string yol = Path.Combine(_kok, "dolu");
        Directory.CreateDirectory(yol);
        File.WriteAllText(Path.Combine(yol, "degerli.txt"), "kaybolmamali");

        IslemRaporu rapor = DosyaIslemleri.BosKlasoruSil(yol);

        Assert.False(rapor.Oldu);
        Assert.Equal(IslemSonucu.Dolu, rapor.Sonuc);
        Assert.True(File.Exists(Path.Combine(yol, "degerli.txt")));
    }

    [Fact]
    public void ZATEN_OLMAYAN_KLASOR_BASARILI_SAYILIR()
        => Assert.True(DosyaIslemleri.BosKlasoruSil(Path.Combine(_kok, "yok")).Oldu);

    // ---------- DosyaIslemleri.Ozet ----------

    [Fact]
    public void DOSYA_OZETI_BOYUT_VE_TARIH_TASIR()
    {
        string yol = Path.Combine(_kok, "a.txt");
        File.WriteAllText(yol, "12345");

        DosyaIslemleri.YolOzeti ozet = DosyaIslemleri.Ozet(yol);

        Assert.False(ozet.KlasorMu);
        Assert.Equal(5, ozet.Boyut);
        Assert.NotNull(ozet.Degistirme);
    }

    [Fact]
    public void KLASOR_OZETINDE_BOYUT_YOKTUR()
    {
        DosyaIslemleri.YolOzeti ozet = DosyaIslemleri.Ozet(_kok);

        Assert.True(ozet.KlasorMu);
        Assert.Null(ozet.Boyut);   // klasore boyut uydurulmaz
        Assert.NotNull(ozet.Degistirme);
    }

    [Fact]
    public void OLMAYAN_YOLUN_OZETI_BOS_DONER()
    {
        DosyaIslemleri.YolOzeti ozet = DosyaIslemleri.Ozet(Path.Combine(_kok, "yok.txt"));

        Assert.Null(ozet.Boyut);
        Assert.Null(ozet.Degistirme);   // uydurma tarih yok (CLAUDE.md 3)
    }

    // ---------- Zaman.Yaz (cekirdekteki TEK testsiz sinifti) ----------

    [Fact]
    public void ZAMAN_GUN_AY_YIL_SAAT_YAZAR()
        => Assert.Equal("05.03.2026 14:07", Zaman.Yaz(new DateTime(2026, 3, 5, 14, 7, 33)));

    // ---------- Cop.DegisenAd ----------

    [Fact]
    public void AYNI_ADLA_DONEN_OGE_DEGISMEMIS_SAYILIR()
    {
        var oge = new CopOgesi("1", "a.txt", @"C:\K\a.txt", DateTime.Now, 1, false);
        var rapor = IslemRaporu.Basarili(@"C:\K\a.txt");

        Assert.Null(Cop.DegisenAd(rapor, oge));
    }

    [Fact]
    public void NUMARALANAN_OGENIN_YENI_ADI_DONER()
    {
        var oge = new CopOgesi("1", "a.txt", @"C:\K\a.txt", DateTime.Now, 1, false);
        var rapor = IslemRaporu.Basarili(@"C:\K\a (2).txt");

        Assert.Equal("a (2).txt", Cop.DegisenAd(rapor, oge));
    }
}
