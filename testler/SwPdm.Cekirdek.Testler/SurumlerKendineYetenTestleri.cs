using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// VERSIYON KENDI KENDINE YETER MI - gercek SOLIDWORKS dosyalariyla.
///
/// SurumlerTestleri'nin PARCASI (partial): ayni gecici kok, ayni yardimcilar;
/// ikinci bir duzenek kurulmuyor (CLAUDE.md 8). Ayri dosya olmasinin sebebi
/// tek dosyanin 688 satira cikmasi - boyut kapisinin siniri 600.
/// </summary>
public partial class SurumlerTestleri
{
    // ---------------------------------------------------------------------
    // VERSIYON KENDI KENDINE YETER (Erkan, 31.08.2026: "part dosyası eskiden
    // ne güzel versiyon çalışıyordu, diğerleri de öyle olamaz mı").
    //
    // Montajin arsiv kopyasi tek basina duruyordu ve SOLIDWORKS onu
    // acamiyordu - parcalari yaninda degildi. Artik o gunku cocuklar da
    // ayni klasore arsivleniyor.
    // ---------------------------------------------------------------------

    private static string OrnekVeri => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private string OrnegiKoy(string ad)
    {
        string yol = WindowsYolu.Birlestir(_kok, ad);
        File.Copy(Path.Combine(OrnekVeri, ad), yol);
        return yol;
    }

    [Fact]
    public void MONTAJ_versiyonunda_COCUKLARI_da_YANINDA()
    {
        // ASIL OLCUM: SOLIDWORKS once ebeveynin YANINA bakiyor (CLAUDE.md 5).
        // Arsivdeki montajin yaninda parcasi yoksa acilmiyor - Erkan'da
        // "dosya bozuk" kutusu tam bu yuzden cikiyordu.
        OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");

        IslemRaporu rapor = Surumler.Olustur(_kok, montaj, "ilk", out int no);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal(0, no);

        // Asil dosya GERCEK ADIYLA duruyor: "v0.SLDASM" adinda bir dosya
        // montajin aradigi ad DEGIL.
        string arsiv = rapor.YeniYol!;
        Assert.Equal("Montaj1.SLDASM", WindowsYolu.DosyaAdi(arsiv));
        Assert.Equal("v0", WindowsYolu.DosyaAdi(WindowsYolu.Klasor(arsiv)));

        string yanindaki = WindowsYolu.Birlestir(
            WindowsYolu.Klasor(arsiv), "Parça1.SLDPRT");
        Assert.True(File.Exists(yanindaki), "montajin parcasi arsivde yaninda degil");
        Assert.Equal(
            File.ReadAllBytes(WindowsYolu.Birlestir(_kok, "Parça1.SLDPRT")).Length,
            File.ReadAllBytes(yanindaki).Length);
    }

    [Fact]
    public void PARCA_versiyonu_TEK_DOSYA_kalir()
    {
        // Cocugu olmayan belgeye gereksiz dosya eklenmiyor.
        string parca = OrnegiKoy("Parça1.SLDPRT");

        IslemRaporu rapor = Surumler.Olustur(_kok, parca, "", out int _);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Single(Directory.GetFiles(WindowsYolu.Klasor(rapor.YeniYol!)));
        Assert.Equal("Parça1.SLDPRT", WindowsYolu.DosyaAdi(rapor.YeniYol!));
    }

    [Fact]
    public void ESKI_DUZEN_hala_LISTELENIYOR_ve_DONULEBILIYOR()
    {
        // Erkan'in elindeki versiyonlar eski duzende ("v0.SLDPRT" duz dosya).
        // Onlari gormemek "versiyonlarim kayboldu" demek olurdu (CLAUDE.md 3).
        string yol = DosyaKoy("Parca1.SLDPRT", "bugunku hal");

        string yuva = WindowsYolu.Birlestir(
            WindowsYolu.Birlestir(_kok, Surumler.KlasorAdi), "Parca1.SLDPRT");
        Directory.CreateDirectory(yuva);
        File.WriteAllText(WindowsYolu.Birlestir(yuva, "v0.SLDPRT"), "eski hal");
        File.WriteAllText(
            WindowsYolu.Birlestir(yuva, "kayit.txt"),
            "0\t2026-08-30T10:00:00.0000000\t8\teski duzen\n");

        SurumDurumu durum = Surumler.Listele(_kok, yol);
        Assert.Single(durum.Ogeler);
        Assert.Equal("eski duzen", durum.Ogeler[0].Not);

        Assert.True(Surumler.Don(_kok, yol, 0).Oldu);
        Assert.Equal("eski hal", File.ReadAllText(yol));
    }

    [Fact]
    public void SIL_versiyon_KLASORUNUN_TAMAMINI_kaldirir()
    {
        OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);
        File.AppendAllText(montaj, " ");
        Surumler.Olustur(_kok, montaj, "ikinci", out int _);

        string v0 = WindowsYolu.Klasor(Surumler.Listele(_kok, montaj).Ogeler[^1].ArsivYolu);

        Assert.True(Surumler.Sil(_kok, montaj, 0).Oldu);

        Assert.False(Directory.Exists(v0));                       // klasorun tamami gitti
        Assert.Single(Surumler.Listele(_kok, montaj).Ogeler);      // komsu versiyon duruyor
    }

    [Fact]
    public void COZULEMEYEN_COCUK_sayisi_SOYLENIYOR()
    {
        // Parca yoksa montajin referansi cozulemiyor: versiyon YINE olusur
        // ama EKSIK oldugu SOYLENIR (CLAUDE.md 3).
        string montaj = OrnegiKoy("Montaj1.SLDASM");   // Parça1 KOYULMADI

        IslemRaporu rapor = Surumler.Olustur(_kok, montaj, "", out int _);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.NotNull(rapor.Sebep);
        Assert.Contains("bulunamadı", rapor.Sebep!, StringComparison.Ordinal);
    }
}
