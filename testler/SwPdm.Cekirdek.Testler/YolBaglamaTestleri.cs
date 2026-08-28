using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// REFERANSI ELLE BAGLAMA - hedefi KULLANICI seciyor.
///
/// NEDEN AYRI BIR YOL VAR: bizim cozucumuz yazili yolu ADA gore ariyor
/// (CLAUDE.md 5). Dosya baska bir programla YENIDEN ADLANDIRILDIYSA
/// eslesecek ad kalmiyor - otomatik onarimin baglayacagi hedef yok. Bu
/// testler tam o durumu kuruyor: parcanin adi disaridan degistiriliyor.
///
/// BURADA OLCULEMEYEN: SOLIDWORKS sonucu aciyor mu. O olcum Erkan'in
/// makinesinde yapildi (28.08.2026): farkli uzunluktaki ad ve goreli yol
/// KABUL EDILDI. Burada olculen sey, dosyaya yazilanin dogru cozuldugu.
/// </summary>
public sealed class YolBaglamaTestleri : IDisposable
{
    private static string Ornek => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private readonly string _kok;

    public YolBaglamaTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-bagla-" + Guid.NewGuid().ToString("N"));
        Kopyala(Ornek, _kok);
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void ADI_DISARIDAN_DEGISMIS_dosya_ELLE_baglaniyor()
    {
        // Parcanin adi baska bir programla degisti: artik ortada "Parça1"
        // diye bir dosya YOK, yani otomatik onarimin bakacagi hedef de yok.
        string resim = Yol("Parça1.SLDDRW");
        string yeniAd = Yol("Kapak.SLDPRT");
        File.Move(Yol("Parça1.SLDPRT"), yeniAd);

        ReferansIndeksi indeks = Indeks();

        // 1) Referans COZULEMIYOR ve bu yuzden elle baglanabilir sayiliyor.
        IReadOnlyList<(string YazilanYol, Cozum Cozum)> adaylar =
            YolBaglama.BaglanabilirYollar(indeks, resim);

        Assert.Contains(
            adaylar,
            a => string.Equals(
                WindowsYolu.DosyaAdi(a.YazilanYol), "Parça1.SLDPRT",
                StringComparison.OrdinalIgnoreCase));

        // 2) BAGLA - hedefi kullanici sectti.
        string? sebep = YolBaglama.Bagla(resim, "Parça1.SLDPRT", yeniAd);
        Assert.True(sebep is null, sebep);   // sebep GORUNSUN (CLAUDE.md 3)

        // 3) ASIL OLCUM: dosyanin ICINDEKI yol artik secilen dosyayi cozuyor.
        string? yazili = SwReferans.Oku(resim).Dogrudan.FirstOrDefault(
            y => string.Equals(
                WindowsYolu.DosyaAdi(y), "Kapak.SLDPRT", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(yazili);
        Assert.Equal(
            WindowsYolu.Cozumle(null, yeniAd),
            WindowsYolu.Cozumle(WindowsYolu.Klasor(resim), yazili));

        // 4) Eski ad geride KALMAMALI: yalniz bir akisi degistirmek eskisini
        //    birakirdi ve SOLIDWORKS onu aramaya devam ederdi (CLAUDE.md 5).
        Assert.DoesNotContain(
            SwReferans.Oku(resim).Dogrudan,
            y => string.Equals(
                WindowsYolu.DosyaAdi(y), "Parça1.SLDPRT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void COZULEN_referans_ELLE_BAGLAMA_listesine_GIRMEZ()
    {
        // Calisan bir bagi elle degistirmek icin sebep yok; listeyi
        // kalabalik yapmak YANLIS olani secme ihtimalini artirir.
        ReferansIndeksi indeks = Indeks();
        Assert.Empty(YolBaglama.BaglanabilirYollar(indeks, Yol("Parça1.SLDDRW")));
    }

    [Fact]
    public void ACIK_DOSYAYA_YAZILMAZ_ve_dosya_DEGISMEZ()
    {
        string resim = Yol("Parça1.SLDDRW");
        string yeniAd = Yol("Kapak.SLDPRT");
        File.Move(Yol("Parça1.SLDPRT"), yeniAd);

        // SOLIDWORKS acik bir belgenin yanina gizli "~$" kilidi yaziyor.
        File.WriteAllText(Kilit.KilidininYolu(resim), "kilit");
        byte[] once = File.ReadAllBytes(resim);

        string? hata = YolBaglama.Bagla(resim, "Parça1.SLDPRT", yeniAd);

        Assert.NotNull(hata);
        Assert.Contains("açık", hata!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(once, File.ReadAllBytes(resim));
    }

    [Fact]
    public void OLMAYAN_HEDEF_reddediliyor_ve_dosya_DEGISMEZ()
    {
        string resim = Yol("Parça1.SLDDRW");
        byte[] once = File.ReadAllBytes(resim);

        string? hata = YolBaglama.Bagla(resim, "Parça1.SLDPRT", Yol("Olmayan.SLDPRT"));

        Assert.NotNull(hata);
        Assert.Equal(once, File.ReadAllBytes(resim));
    }

    [Fact]
    public void YAZILI_OLMAYAN_AD_sessizce_BASARILI_SAYILMAZ()
    {
        // Bu dosyada "Baska.SLDPRT" diye bir yol YOK. Degistirilecek bir sey
        // yokken "oldu" demek, kullanicinin bir sorunu cozdugunu SANMASIDIR
        // (CLAUDE.md 3: sessiz basari yasak).
        string resim = Yol("Parça1.SLDDRW");
        string? hata = YolBaglama.Bagla(resim, "Baska.SLDPRT", Yol("Parça1.SLDPRT"));

        Assert.NotNull(hata);
        Assert.NotEmpty(hata!);
    }

    [Fact]
    public void KILIT_YANINDAYSA_ACIK_SAYILIYOR()
    {
        string resim = Yol("Parça1.SLDDRW");
        Assert.False(Kilit.AcikMi(resim));

        File.WriteAllText(Kilit.KilidininYolu(resim), "kilit");
        Assert.True(Kilit.AcikMi(resim));
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

    private ReferansIndeksi Indeks()
    {
        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);
        return indeks;
    }

    private string Yol(params string[] p) => Path.Combine([_kok, .. p]);
}
