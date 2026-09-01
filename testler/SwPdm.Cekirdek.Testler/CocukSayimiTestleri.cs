using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// VERSIYONA GIRECEK COCUKLARIN SAYIMI (Erkan, 01.09.2026: "241 dosya
/// diyor, bi hata var").
///
/// Kutu BUTUN AGACI sayiyor (torunlar dahil), panel yalnizca DOGRUDAN
/// cocuklari - ikisi ayri sey. Ama olcum iki GERCEK hata da buldu ve bu
/// dosya onlari kilitliyor:
///
///   H1 - TEKILLEME YOL DIZESINE gore yapiliyordu, dosyaya gore degil.
///        Onarimin uzunluk dolgusu mutlak yollara ".\" yaziyor; ayni dosya
///        "C:\a\Ad" ve "C:\a\.\Ad" olarak IKI AYRI cocuk sayiliyor ve alt
///        agaci IKI KEZ yurunuyordu.
///   H2 - "Cozulemeyen" GECIS sayiyordu: on montajda gecen TEK bir kayip
///        parca "10 referans bulunamadi" diye yaziliyordu.
///
/// Testler gercek SOLIDWORKS dosyalariyla kuruluyor (araclar/ornek-veri):
/// yazili yollari SwYazici ile degistirmek yerine, ornekteki hazir
/// referanslar ve DISK duzeni kullaniliyor - cozumleme diskte yoklaniyor.
/// </summary>
public sealed class CocukSayimiTestleri : IDisposable
{
    private static string Ornek => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private readonly string _kok;

    public CocukSayimiTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-cocuk-" + Guid.NewGuid().ToString("N"));
        Kopyala(Ornek, _kok);
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, recursive: true); } catch (IOException) { }
    }

    // ------------------------------------------------------------------
    // H1: ayni dosyanin iki yazimi TEK dosya sayilmali.
    // ------------------------------------------------------------------

    [Fact]
    public void DOLGULU_ve_DUZ_yazim_AYNI_dosya_sayiliyor()
    {
        // Onarimin yazdigi dolgulu MUTLAK yol ile duz mutlak yol AYNI yere
        // cozulmeli; yoksa tekilleme dizeye takilir ve dosya iki kez sayilir.
        //
        // YOLLAR ELLE YAZILI WINDOWS YOLLARI - VE BU SART (CLAUDE.md 4):
        // ilk yazista gecici klasor (Linux'ta "/tmp/...") kullanilmisti ve
        // o, Windows'un MUTLAK yol tanimina girmiyor; olcum duzeltilen dali
        // HIC calistirmiyordu. 9 dongusu bunu yakaladi: duzlestirme
        // kaldirildiginda test yine gecti. TabandanCoz saf dize mantigi,
        // diske bakmiyor - gercek klasore ihtiyac yok.
        const string Taban = @"C:\Proje";
        const string Duz = @"C:\Proje\Parça1.SLDPRT";

        Assert.Equal(Duz, WindowsYolu.TabandanCoz(Taban, Duz));
        Assert.Equal(Duz, WindowsYolu.TabandanCoz(Taban, @"C:\Proje\.\.\Parça1.SLDPRT"));
        Assert.Equal(Duz, WindowsYolu.TabandanCoz(Taban, @"C:\Proje\\Parça1.SLDPRT"));
        Assert.Equal(Duz, WindowsYolu.TabandanCoz(Taban, @"C:\Proje\alt\..\Parça1.SLDPRT"));
    }

    [Fact]
    public void DUZLESTIRME_kokun_USTUNE_CIKMIYOR()
    {
        // SURUCU KOKU TUZAGI (CLAUDE.md 4): kok bozulursa Birlestir
        // "C:x" gibi SURUCUYE GORELI bir yol uretir.
        Assert.Equal(@"C:\", WindowsYolu.Duzlestir(@"C:\"));
        Assert.Equal(@"C:\", WindowsYolu.Duzlestir(@"C:\..\..\"));
        Assert.Equal(@"C:\a", WindowsYolu.Duzlestir(@"C:\a\b\.."));
        Assert.Equal(@"C:\a\b.SLDPRT", WindowsYolu.Duzlestir(@"C:\a\.\.\b.SLDPRT"));
        Assert.Equal(@"\\sunucu\pay\a", WindowsYolu.Duzlestir(@"\\sunucu\pay\.\a"));

        // GORELI yola dokunulmaz: onun duzlestirmesi TabandanCoz'un isi.
        Assert.Equal(@"a\.\b", WindowsYolu.Duzlestir(@"a\.\b"));
    }

    // ------------------------------------------------------------------
    // Sayilar: dogrudan / torun / cozulemeyen
    // ------------------------------------------------------------------

    [Fact]
    public void DOGRUDAN_ve_TORUN_ayri_sayiliyor()
    {
        // Montaj1 -> Parça1 (+ Yeni klasör altindakiler). Kutu "241 dosya"
        // derken panelin "14" demesinin sebebi tam bu ayrim; ikisi de
        // ekranda yaziyor artik.
        CocukKumesi kume = Surumler.Cocuklari(Path.Combine(_kok, "Montaj1.SLDASM"));

        Assert.True(kume.Yollar.Count > 0, "ornek montajin cocugu yok - test kendini kandirir");
        Assert.True(kume.Dogrudan > 0);
        Assert.True(kume.Dogrudan <= kume.Yollar.Count);
    }

    [Fact]
    public void AYNI_dosya_iki_yerde_gecse_de_BIR_KEZ_sayiliyor()
    {
        CocukKumesi kume = Surumler.Cocuklari(Path.Combine(_kok, "Montaj1.SLDASM"));

        var gorulen = new System.Collections.Generic.HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string yol in kume.Yollar)
        {
            Assert.True(gorulen.Add(yol), "ayni yol listede IKI KEZ: " + yol);
        }
    }

    [Fact]
    public void COZULEMEYEN_DOSYA_sayiyor_GECIS_degil()
    {
        // Ayni kayip parca birden cok ebeveynde geciyor: sorun BIR tanedir.
        // Eskiden sayac tekillemenin onundeydi ve her gecis sayiliyordu.
        string kayip = Path.Combine(_kok, "Parça1.SLDPRT");
        File.Delete(kayip);

        CocukKumesi kume = Surumler.Cocuklari(Path.Combine(_kok, "Parça1.SLDDRW"));

        // Teknik resim yalniz o parcayi kullaniyor: tek eksik.
        Assert.Equal(1, kume.Cozulemeyen);
    }

    // ------------------------------------------------------------------

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
