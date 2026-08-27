using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Cop kutusu GERCEK klasorlerle kosuyor. Buradaki asil sinav CLAUDE.md 1a:
/// silinen bir dosya GERI GELMELI. Geri gelmiyorsa uygulama dosya kaybettirir.
/// </summary>
public class CopTestleri : IDisposable
{
    private readonly string _kok;
    private readonly string _cop;

    public CopTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-cop-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_kok);
        _cop = Cop.Yolu(_kok);
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

    private string Yol(params string[] parcalar)
    {
        string yol = _kok;
        foreach (string p in parcalar)
        {
            yol = WindowsYolu.Birlestir(yol, p);
        }

        return yol;
    }

    private string DosyaKoy(string ad, string icerik = "veri")
    {
        string yol = Yol(ad);
        Directory.CreateDirectory(WindowsYolu.Klasor(yol));
        File.WriteAllText(yol, icerik);
        return yol;
    }

    [Fact]
    public void Sil_DosyayiCopeTasir_ESKI_YERINDEN_KALKAR()
    {
        string yol = DosyaKoy("Parca1.SLDPRT");

        IslemRaporu rapor = Cop.Sil(_cop, yol);

        Assert.True(rapor.Oldu);
        Assert.False(File.Exists(yol));

        IReadOnlyList<CopOgesi> liste = Cop.Listele(_cop);
        Assert.Single(liste);
        Assert.Equal("Parca1.SLDPRT", liste[0].Ad);
        Assert.Equal(yol, liste[0].EskiYol);
        Assert.False(liste[0].KlasorMu);
    }

    [Fact]
    public void GeriYukle_DOSYAYI_ICERIGIYLE_GERI_GETIRIR()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "onemli veri");
        Cop.Sil(_cop, yol);

        IslemRaporu rapor = Cop.GeriYukle(_cop, Cop.Listele(_cop)[0]);

        Assert.True(rapor.Oldu);
        Assert.Equal("onemli veri", File.ReadAllText(yol));
        Assert.Empty(Cop.Listele(_cop));
    }

    [Fact]
    public void Sil_KlasoruIcindekilerleTasir_GeriYukleyince_ICI_YERINDE()
    {
        Directory.CreateDirectory(Yol("montaj"));
        File.WriteAllText(Yol("montaj", "ic.SLDPRT"), "ic veri");

        Assert.True(Cop.Sil(_cop, Yol("montaj")).Oldu);
        Assert.False(Directory.Exists(Yol("montaj")));

        CopOgesi oge = Cop.Listele(_cop)[0];
        Assert.True(oge.KlasorMu);
        Assert.True(Cop.GeriYukle(_cop, oge).Oldu);
        Assert.Equal("ic veri", File.ReadAllText(Yol("montaj", "ic.SLDPRT")));
    }

    [Fact]
    public void GeriYukle_ESKI_KLASOR_SILINMISSE_YENIDEN_ACAR()
    {
        DosyaKoy("alt/Parca.SLDPRT");
        Cop.Sil(_cop, Yol("alt", "Parca.SLDPRT"));
        Directory.Delete(Yol("alt"), recursive: true);

        IslemRaporu rapor = Cop.GeriYukle(_cop, Cop.Listele(_cop)[0]);

        // Klasor yok diye "olmaz" demek, dosyayi copte mahsur birakirdi.
        Assert.True(rapor.Oldu);
        Assert.True(File.Exists(Yol("alt", "Parca.SLDPRT")));
    }

    [Fact]
    public void GeriYukle_AYNI_ADDA_DOSYA_VARSA_USTUNE_YAZMAZ()
    {
        string yol = DosyaKoy("Parca.SLDPRT", "eski");
        Cop.Sil(_cop, yol);
        File.WriteAllText(yol, "yeni");   // ayni ada baska bir dosya kondu

        IslemRaporu rapor = Cop.GeriYukle(_cop, Cop.Listele(_cop)[0]);

        Assert.True(rapor.Oldu);
        Assert.Equal("yeni", File.ReadAllText(yol));                    // dokunulmadi
        Assert.Equal("eski", File.ReadAllText(Yol("Parca (2).SLDPRT")));  // yanina kondu
    }

    [Fact]
    public void Sil_AYNI_ADLI_IKI_DOSYA_BIRBIRINI_EZMEZ()
    {
        Directory.CreateDirectory(Yol("a"));
        Directory.CreateDirectory(Yol("b"));
        File.WriteAllText(Yol("a", "P.SLDPRT"), "a-icerik");
        File.WriteAllText(Yol("b", "P.SLDPRT"), "b-icerik");

        Assert.True(Cop.Sil(_cop, Yol("a", "P.SLDPRT")).Oldu);
        Assert.True(Cop.Sil(_cop, Yol("b", "P.SLDPRT")).Oldu);

        Assert.Equal(2, Cop.Listele(_cop).Count);

        foreach (CopOgesi oge in Cop.Listele(_cop))
        {
            Assert.True(Cop.GeriYukle(_cop, oge).Oldu);
        }

        Assert.Equal("a-icerik", File.ReadAllText(Yol("a", "P.SLDPRT")));
        Assert.Equal("b-icerik", File.ReadAllText(Yol("b", "P.SLDPRT")));
    }

    [Fact]
    public void KaliciSil_CoptenTumuylaKaldirir()
    {
        Cop.Sil(_cop, DosyaKoy("P.SLDPRT"));

        Assert.True(Cop.KaliciSil(_cop, Cop.Listele(_cop)[0]).Oldu);
        Assert.Empty(Cop.Listele(_cop));
    }

    [Fact]
    public void Listele_BOZUK_KAYIT_SATIRI_UYGULAMAYI_DUSURMEZ()
    {
        Cop.Sil(_cop, DosyaKoy("P.SLDPRT"));
        File.AppendAllText(
            WindowsYolu.Birlestir(_cop, "kayit.txt"),
            "bu bozuk bir satir" + Environment.NewLine);

        // Bozuk satir ATLANIR; saglam olan yine gorunur.
        Assert.Single(Cop.Listele(_cop));
    }

    [Fact]
    public void Listele_DISKTE_OLMAYAN_KAYIT_GOSTERILMEZ()
    {
        Cop.Sil(_cop, DosyaKoy("P.SLDPRT"));
        CopOgesi oge = Cop.Listele(_cop)[0];
        Directory.Delete(WindowsYolu.Birlestir(_cop, oge.No), recursive: true);

        // CLAUDE.md 3: olmayan bir dosyayi "geri yukleyebilirsin" diye
        // gostermek yalandir.
        Assert.Empty(Cop.Listele(_cop));
    }

    [Fact]
    public void Sil_COP_KLASORUNUN_KENDISI_COPE_ATILAMAZ()
    {
        Cop.Sil(_cop, DosyaKoy("P.SLDPRT"));

        Assert.Equal(IslemSonucu.KendiAltina, Cop.Sil(_cop, _cop).Sonuc);
    }

    [Fact]
    public void Yolu_UST_KLASOR_VERILINCE_ORAYA_KURAR()
    {
        Assert.Equal(
            WindowsYolu.Birlestir(@"D:\Yedek", Cop.KlasorAdi),
            Cop.Yolu(@"C:\Proje", @"D:\Yedek"));

        // Bos/null ise varsayilan: kokun kendi ici.
        Assert.Equal(
            WindowsYolu.Birlestir(@"C:\Proje", Cop.KlasorAdi),
            Cop.Yolu(@"C:\Proje"));
    }

    [Fact]
    public void AyniSurucudeMi_FARKLI_DISKI_AYIRT_EDER()
    {
        Assert.True(Cop.AyniSurucudeMi(@"C:\a\b", @"C:\x"));
        Assert.False(Cop.AyniSurucudeMi(@"C:\a", @"D:\a"));

        // Ag surucusu: paya kadar bakilir.
        Assert.True(Cop.AyniSurucudeMi(@"\\sunucu\ortak\a", @"\\sunucu\ortak\b"));
        Assert.False(Cop.AyniSurucudeMi(@"\\sunucu\ortak", @"\\sunucu\baska"));

        // Cozulemiyorsa "ayni" denir - yanlis yavaslik uyarisi vermemek icin.
        Assert.True(Cop.AyniSurucudeMi("gorece/yol", @"C:\x"));
    }

    [Fact]
    public void Listele_BosCopBosListe()
        => Assert.Empty(Cop.Listele(_cop));
}
