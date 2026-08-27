using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// GERCEK klasorlerle kosuyor - sahte dosya sistemi yok. Sebep CLAUDE.md 2:
/// bu kod Erkan'in dosyalarina dokunacak; "herhalde calisir" yetmez.
/// </summary>
public class DosyaIslemleriTestleri : IDisposable
{
    private readonly string _kok;

    public DosyaIslemleriTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-test-" + Guid.NewGuid().ToString("N")[..8]);
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
            // Temizlik tutmazsa test sonucunu degistirmez.
        }

        GC.SuppressFinalize(this);
    }

    private string Yol(params string[] parcalar)
    {
        string yol = _kok;
        foreach (string p in parcalar)
        {
            yol = WindowsYolu.Birlestir(yol, p);
        }

        return yol;
    }

    // ------------------------------------------------------------ olusturma

    [Fact]
    public void KlasorOlustur_Olusturur()
    {
        IslemRaporu rapor = DosyaIslemleri.KlasorOlustur(_kok, "Yeni klasör");

        Assert.True(rapor.Oldu);
        Assert.True(Directory.Exists(Yol("Yeni klasör")));
    }

    [Fact]
    public void KlasorOlustur_GecersizAdiREDDEDER_VE_SEBEBINI_SOYLER()
    {
        IslemRaporu rapor = DosyaIslemleri.KlasorOlustur(_kok, "a<b");

        Assert.Equal(IslemSonucu.GecersizAd, rapor.Sonuc);
        Assert.False(string.IsNullOrWhiteSpace(rapor.Sebep));   // CLAUDE.md 3
    }

    [Fact]
    public void KlasorOlustur_AyrilmisAdiREDDEDER()
        => Assert.Equal(IslemSonucu.GecersizAd, DosyaIslemleri.KlasorOlustur(_kok, "CON").Sonuc);

    [Fact]
    public void KlasorOlustur_ZatenVarsa_USTUNE_YAZMAZ()
    {
        Directory.CreateDirectory(Yol("var"));

        IslemRaporu rapor = DosyaIslemleri.KlasorOlustur(_kok, "var");

        Assert.Equal(IslemSonucu.ZatenVar, rapor.Sonuc);
    }

    // -------------------------------------------------------- ad cakismasi

    [Fact]
    public void BosAdBul_CakismaYoksaAyniAdiVerir()
        => Assert.Equal("Yeni klasör", DosyaIslemleri.BosAdBul(_kok, "Yeni klasör"));

    [Fact]
    public void BosAdBul_CakisincaNumaralar()
    {
        Directory.CreateDirectory(Yol("Yeni klasör"));
        Assert.Equal("Yeni klasör (2)", DosyaIslemleri.BosAdBul(_kok, "Yeni klasör"));

        Directory.CreateDirectory(Yol("Yeni klasör (2)"));
        Assert.Equal("Yeni klasör (3)", DosyaIslemleri.BosAdBul(_kok, "Yeni klasör"));
    }

    [Fact]
    public void BosAdBul_UZANTIYI_KORUR()
    {
        File.WriteAllText(Yol("Parca.SLDPRT"), string.Empty);

        // "Parca.SLDPRT (2)" olsaydi dosya SOLIDWORKS dosyasi olmaktan cikardi.
        Assert.Equal("Parca (2).SLDPRT", DosyaIslemleri.BosAdBul(_kok, "Parca.SLDPRT"));
    }

    // ------------------------------------------------------ yeniden adlandirma

    [Fact]
    public void YenidenAdlandir_DosyayiAdlandirir()
    {
        File.WriteAllText(Yol("eski.SLDPRT"), "x");

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(Yol("eski.SLDPRT"), "yeni.SLDPRT");

        Assert.True(rapor.Oldu);
        Assert.False(File.Exists(Yol("eski.SLDPRT")));
        Assert.Equal("x", File.ReadAllText(Yol("yeni.SLDPRT")));
    }

    [Fact]
    public void YenidenAdlandir_KlasoruAdlandirir_ICERIGI_KORUR()
    {
        Directory.CreateDirectory(Yol("eski"));
        File.WriteAllText(Yol("eski", "ic.SLDPRT"), "veri");

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(Yol("eski"), "yeni");

        Assert.True(rapor.Oldu);
        Assert.Equal("veri", File.ReadAllText(Yol("yeni", "ic.SLDPRT")));
    }

    [Fact]
    public void YenidenAdlandir_HedefVarsa_USTUNE_YAZMAZ()
    {
        File.WriteAllText(Yol("a.SLDPRT"), "a");
        File.WriteAllText(Yol("b.SLDPRT"), "b");

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(Yol("a.SLDPRT"), "b.SLDPRT");

        Assert.Equal(IslemSonucu.ZatenVar, rapor.Sonuc);

        // ASIL SINAV: hicbiri kaybolmadi.
        Assert.Equal("a", File.ReadAllText(Yol("a.SLDPRT")));
        Assert.Equal("b", File.ReadAllText(Yol("b.SLDPRT")));
    }

    [Fact]
    public void YenidenAdlandir_OlmayanKaynak_Bulunamadi()
        => Assert.Equal(
            IslemSonucu.Bulunamadi,
            DosyaIslemleri.YenidenAdlandir(Yol("yok.SLDPRT"), "olur.SLDPRT").Sonuc);

    // ---------------------------------------------------------------- tasima

    [Fact]
    public void Tasi_DosyayiTasir()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "icerik");

        IslemRaporu rapor = DosyaIslemleri.Tasi(Yol("p.SLDPRT"), Yol("hedef"));

        Assert.True(rapor.Oldu);
        Assert.False(File.Exists(Yol("p.SLDPRT")));
        Assert.Equal("icerik", File.ReadAllText(Yol("hedef", "p.SLDPRT")));
    }

    [Fact]
    public void Tasi_KlasoruTasir_ALTINDAKILERLE()
    {
        Directory.CreateDirectory(Yol("hedef"));
        Directory.CreateDirectory(Yol("montaj"));
        File.WriteAllText(Yol("montaj", "p.SLDPRT"), "v");

        IslemRaporu rapor = DosyaIslemleri.Tasi(Yol("montaj"), Yol("hedef"));

        Assert.True(rapor.Oldu);
        Assert.Equal("v", File.ReadAllText(Yol("hedef", "montaj", "p.SLDPRT")));
    }

    [Fact]
    public void Tasi_AyniKlasore_YAPMAZ()
    {
        File.WriteAllText(Yol("p.SLDPRT"), "v");

        IslemRaporu rapor = DosyaIslemleri.Tasi(Yol("p.SLDPRT"), _kok);

        Assert.Equal(IslemSonucu.ZatenVar, rapor.Sonuc);
        Assert.True(File.Exists(Yol("p.SLDPRT")));
    }

    [Fact]
    public void Tasi_KENDI_ALTINA_TASIMAZ()
    {
        Directory.CreateDirectory(Yol("ust", "alt"));

        IslemRaporu rapor = DosyaIslemleri.Tasi(Yol("ust"), Yol("ust", "alt"));

        Assert.Equal(IslemSonucu.KendiAltina, rapor.Sonuc);
        Assert.True(Directory.Exists(Yol("ust", "alt")));
    }

    [Fact]
    public void Tasi_HedefteAyniAdVarsa_USTUNE_YAZMAZ()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "kaynak");
        File.WriteAllText(Yol("hedef", "p.SLDPRT"), "hedefteki");

        IslemRaporu rapor = DosyaIslemleri.Tasi(Yol("p.SLDPRT"), Yol("hedef"));

        Assert.Equal(IslemSonucu.ZatenVar, rapor.Sonuc);
        Assert.Equal("kaynak", File.ReadAllText(Yol("p.SLDPRT")));
        Assert.Equal("hedefteki", File.ReadAllText(Yol("hedef", "p.SLDPRT")));
    }

    [Fact]
    public void Tasi_OlmayanHedef_Bulunamadi()
    {
        File.WriteAllText(Yol("p.SLDPRT"), "v");

        Assert.Equal(
            IslemSonucu.Bulunamadi,
            DosyaIslemleri.Tasi(Yol("p.SLDPRT"), Yol("yok")).Sonuc);
    }

    // -------------------------------------------------------------- kopyalama

    [Fact]
    public void Kopyala_DosyayiKopyalar_KAYNAK_YERINDE_KALIR()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "icerik");

        IslemRaporu rapor = DosyaIslemleri.Kopyala(Yol("p.SLDPRT"), Yol("hedef"));

        Assert.True(rapor.Oldu);
        Assert.Equal("icerik", File.ReadAllText(Yol("p.SLDPRT")));            // kaynak duruyor
        Assert.Equal("icerik", File.ReadAllText(Yol("hedef", "p.SLDPRT")));   // kopya var
    }

    [Fact]
    public void Kopyala_AYNI_KLASORE_KOPYALAYINCA_COGALTIR()
    {
        File.WriteAllText(Yol("Parca.SLDPRT"), "v");

        IslemRaporu rapor = DosyaIslemleri.Kopyala(Yol("Parca.SLDPRT"), _kok);

        Assert.True(rapor.Oldu);
        Assert.True(File.Exists(Yol("Parca.SLDPRT")));
        Assert.True(File.Exists(Yol("Parca (2).SLDPRT")));   // uzanti korunmus
    }

    [Fact]
    public void Kopyala_KlasoruALTINDAKILERLE_kopyalar()
    {
        Directory.CreateDirectory(Yol("hedef"));
        Directory.CreateDirectory(Yol("montaj", "derin"));
        File.WriteAllText(Yol("montaj", "a.SLDPRT"), "a");
        File.WriteAllText(Yol("montaj", "derin", "b.SLDPRT"), "b");

        IslemRaporu rapor = DosyaIslemleri.Kopyala(Yol("montaj"), Yol("hedef"));

        Assert.True(rapor.Oldu);
        Assert.Equal("a", File.ReadAllText(Yol("hedef", "montaj", "a.SLDPRT")));
        Assert.Equal("b", File.ReadAllText(Yol("hedef", "montaj", "derin", "b.SLDPRT")));
        Assert.True(File.Exists(Yol("montaj", "a.SLDPRT")));   // kaynak duruyor
    }

    [Fact]
    public void Kopyala_BASKA_KLASORDE_AYNI_AD_VARSA_USTUNE_YAZMAZ()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "kaynak");
        File.WriteAllText(Yol("hedef", "p.SLDPRT"), "hedefteki");

        IslemRaporu rapor = DosyaIslemleri.Kopyala(Yol("p.SLDPRT"), Yol("hedef"));

        Assert.Equal(IslemSonucu.ZatenVar, rapor.Sonuc);
        Assert.Equal("hedefteki", File.ReadAllText(Yol("hedef", "p.SLDPRT")));
    }

    [Fact]
    public void Kopyala_KENDI_ALTINA_KOPYALAMAZ()
    {
        Directory.CreateDirectory(Yol("ust", "alt"));

        Assert.Equal(
            IslemSonucu.KendiAltina,
            DosyaIslemleri.Kopyala(Yol("ust"), Yol("ust", "alt")).Sonuc);
    }

    // ----------------------------------------------------------- cakisma

    [Fact]
    public void Cakisma_ATLA_HICBIRINE_DOKUNMAZ()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "kaynak");
        File.WriteAllText(Yol("hedef", "p.SLDPRT"), "hedefteki");

        IslemRaporu rapor = DosyaIslemleri.Tasi(Yol("p.SLDPRT"), Yol("hedef"), Cakisma.Atla);

        Assert.Equal(IslemSonucu.Atlandi, rapor.Sonuc);
        Assert.Equal("kaynak", File.ReadAllText(Yol("p.SLDPRT")));
        Assert.Equal("hedefteki", File.ReadAllText(Yol("hedef", "p.SLDPRT")));
    }

    [Fact]
    public void Cakisma_IKISINI_DE_TUT_NUMARALAR()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "kaynak");
        File.WriteAllText(Yol("hedef", "p.SLDPRT"), "hedefteki");

        IslemRaporu rapor = DosyaIslemleri.Tasi(
            Yol("p.SLDPRT"), Yol("hedef"), Cakisma.IkisiniDeTut);

        Assert.True(rapor.Oldu);
        Assert.Equal("hedefteki", File.ReadAllText(Yol("hedef", "p.SLDPRT")));
        Assert.Equal("kaynak", File.ReadAllText(Yol("hedef", "p (2).SLDPRT")));
    }

    [Fact]
    public void Cakisma_DEGISTIR_ESKISINI_ONCE_KURTARIR()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "yeni");
        File.WriteAllText(Yol("hedef", "p.SLDPRT"), "eski");

        string? kurtarilan = null;
        IslemRaporu rapor = DosyaIslemleri.Tasi(
            Yol("p.SLDPRT"), Yol("hedef"), Cakisma.Degistir,
            eskisi =>
            {
                kurtarilan = File.ReadAllText(eskisi);
                File.Delete(eskisi);   // gercekte cope tasinir
                return true;
            });

        Assert.True(rapor.Oldu);
        Assert.Equal("eski", kurtarilan);                                 // kurtarildi
        Assert.Equal("yeni", File.ReadAllText(Yol("hedef", "p.SLDPRT"))); // degisti
    }

    [Fact]
    public void Cakisma_DEGISTIR_KURTARMA_TUTMAZSA_ISLEM_YAPILMAZ()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "yeni");
        File.WriteAllText(Yol("hedef", "p.SLDPRT"), "eski");

        IslemRaporu rapor = DosyaIslemleri.Tasi(
            Yol("p.SLDPRT"), Yol("hedef"), Cakisma.Degistir, _ => false);

        // Kurtarma tutmadiysa USTUNE YAZILMAZ - CLAUDE.md 1a.
        Assert.False(rapor.Oldu);
        Assert.Equal("eski", File.ReadAllText(Yol("hedef", "p.SLDPRT")));
        Assert.Equal("yeni", File.ReadAllText(Yol("p.SLDPRT")));
    }

    [Fact]
    public void Cakisma_DEGISTIR_KLASORDE_GECERSIZ()
    {
        Directory.CreateDirectory(Yol("hedef", "montaj"));
        Directory.CreateDirectory(Yol("montaj"));
        File.WriteAllText(Yol("hedef", "montaj", "onemli.SLDPRT"), "kaybolmamali");

        IslemRaporu rapor = DosyaIslemleri.Tasi(
            Yol("montaj"), Yol("hedef"), Cakisma.Degistir, _ => true);

        // Klasoru "degistirmek" icini silmek demektir; yasak.
        Assert.Equal(IslemSonucu.ZatenVar, rapor.Sonuc);
        Assert.Equal("kaybolmamali", File.ReadAllText(Yol("hedef", "montaj", "onemli.SLDPRT")));
    }

    [Fact]
    public void Cakisma_Kopyalamada_da_GECERLI()
    {
        Directory.CreateDirectory(Yol("hedef"));
        File.WriteAllText(Yol("p.SLDPRT"), "kaynak");
        File.WriteAllText(Yol("hedef", "p.SLDPRT"), "hedefteki");

        IslemRaporu rapor = DosyaIslemleri.Kopyala(
            Yol("p.SLDPRT"), Yol("hedef"), Cakisma.IkisiniDeTut);

        Assert.True(rapor.Oldu);
        Assert.Equal("kaynak", File.ReadAllText(Yol("p.SLDPRT")));          // kaynak duruyor
        Assert.Equal("kaynak", File.ReadAllText(Yol("hedef", "p (2).SLDPRT")));
    }

    [Fact]
    public void KendiAltindaMi_KomsuKlasoruALTI_SAYMAZ()
    {
        // "C:\a" ile "C:\ab" - metin olarak biri otekiyle basliyor ama
        // ALT KLASOR DEGIL. Ayirici eklenmeseydi burasi yanlis cevap verirdi.
        Assert.False(DosyaIslemleri.KendiAltindaMi(@"C:\a", @"C:\ab"));
        Assert.True(DosyaIslemleri.KendiAltindaMi(@"C:\a", @"C:\a\b"));
        Assert.True(DosyaIslemleri.KendiAltindaMi(@"C:\a", @"C:\a"));
    }
}
