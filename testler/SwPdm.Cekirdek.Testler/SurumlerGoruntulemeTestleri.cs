using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// VERSIYONU ACILABILIR KILMA - "goruntuleme kopyasi" (Erkan, 31.08.2026:
/// "montaj ve teknik resim dosyalarının versiyonlarını açamıyorum").
///
/// SurumlerTestleri'nin PARCASI (partial): ayni gecici kok ve ayni yardimci
/// kullaniliyor, ikinci bir duzenek kurulmuyor (CLAUDE.md 8). Ayri dosya
/// olmasinin sebebi tek dosyanin 660 satira cikmasi - boyut kapisinin siniri
/// 600 ve dosya konusuna gore bolundu (CLAUDE.md 11).
///
/// GERCEK SOLIDWORKS DOSYALARIYLA kosuyor: "bu belgenin cocugu var mi"
/// sorusunun cevabi ancak gercek bir montajin akislarindan okunabilir.
/// </summary>
public partial class SurumlerTestleri
{
    private static string OrnekVeri => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private string OrnegiKoy(string ad)
    {
        string yol = WindowsYolu.Birlestir(_kok, ad);
        File.Copy(Path.Combine(OrnekVeri, ad), yol);
        return yol;
    }

    [Fact]
    public void COCUGU_OLAN_belge_DOGRUDAN_ACILMAZ_cocugu_olmayan_ACILIR()
    {
        // KARAR TIPE GORE DEGIL OLCUME GORE: turetilmis bir parcanin da dis
        // referansi olabilir, o yuzden uzantiya degil akislara bakiliyor.
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        string parca = OrnegiKoy("Parça1.SLDPRT");

        Assert.False(Surumler.DogrudanAcilir(montaj));   // parcalari var
        Assert.True(Surumler.DogrudanAcilir(parca));     // cocugu yok
    }

    [Fact]
    public void GORUNTULEME_KOPYASI_ozgun_klasorde_ve_BIREBIR()
    {
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);
        SurumKaydi kayit = Surumler.Listele(_kok, montaj).Ogeler[0];

        (string? kopya, string? sebep) =
            Surumler.GoruntulemeKopyasi(kayit.ArsivYolu, montaj, kayit.No);

        Assert.Null(sebep);
        Assert.NotNull(kopya);

        // ASIL SART: kopya OZGUN DOSYANIN KLASORUNDE - parcalar yaninda
        // olsun diye. Baska bir yere cikarmak sorunu cozmezdi.
        Assert.Equal(WindowsYolu.Klasor(montaj), WindowsYolu.Klasor(kopya!));
        Assert.Equal("Montaj1 ~v0.SLDASM", WindowsYolu.DosyaAdi(kopya!));
        Assert.Equal(File.ReadAllBytes(kayit.ArsivYolu), File.ReadAllBytes(kopya!));
        Assert.True(File.GetAttributes(kopya!).HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public void IKINCI_CAGRI_yeni_dosya_URETMEZ()
    {
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "", out int _);
        SurumKaydi kayit = Surumler.Listele(_kok, montaj).Ogeler[0];

        (string? bir, _) = Surumler.GoruntulemeKopyasi(kayit.ArsivYolu, montaj, kayit.No);
        (string? iki, _) = Surumler.GoruntulemeKopyasi(kayit.ArsivYolu, montaj, kayit.No);

        Assert.Equal(bir, iki);
        Assert.Single(Directory.GetFiles(_kok, "*~v0*"));
    }

    [Fact]
    public void AYNI_ADDA_YABANCI_dosya_varsa_USTUNE_YAZILMAZ()
    {
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "", out int _);
        SurumKaydi kayit = Surumler.Listele(_kok, montaj).Ogeler[0];

        string yabanci = WindowsYolu.Birlestir(_kok, "Montaj1 ~v0.SLDASM");
        File.WriteAllText(yabanci, "kullanicinin dosyasi");

        (string? kopya, string? sebep) =
            Surumler.GoruntulemeKopyasi(kayit.ArsivYolu, montaj, kayit.No);

        Assert.Null(sebep);
        Assert.Equal("Montaj1 ~v0 (2).SLDASM", WindowsYolu.DosyaAdi(kopya!));
        Assert.Equal("kullanicinin dosyasi", File.ReadAllText(yabanci));   // DOKUNULMADI
    }

    [Fact]
    public void ARSIV_YOKSA_sebep_doner_dosya_URETILMEZ()
    {
        string montaj = OrnegiKoy("Montaj1.SLDASM");

        (string? kopya, string? sebep) = Surumler.GoruntulemeKopyasi(
            WindowsYolu.Birlestir(_kok, "olmayan-v9.SLDASM"), montaj, 9);

        Assert.Null(kopya);
        Assert.NotNull(sebep);
        Assert.Empty(Directory.GetFiles(_kok, "*~v9*"));
    }
}
