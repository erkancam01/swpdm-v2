using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// AGAC YONU: PARCA MONTAJI ICERMEZ (Erkan, 01.09.2026: "knk versiyon
/// olusturma o parcanin bir kopyasini olusturma degil mi, ne alaka dosyalari
/// arsivleme. buyuk bi yanlislik var").
///
/// OLCULEN HATA: 3 MB'lik TEK bir parcanin versiyonu <b>162 dosya</b>
/// suruklyordu - "1 dogrudan · 161 alt seviye". O tek dogrudan referans,
/// parcanin IN-CONTEXT yapildigi MONTAJ; yuruyus ondan asagi inince butun
/// urun agaci parcanin arsivine giriyordu. Bag yukari dogru: parcanin
/// icinde montaj yok, parca o montajin ICINDE yapilmis (CLAUDE.md 5'te
/// olculu: "TEK ACILIM.SLDASM").
///
/// OLCUM TEK DEGISKENLI (CLAUDE.md 2): ayni dosya koke IKI KEZ kopyalaniyor -
/// "Sahte.SLDDRW" ve "Sahte.SLDPRT". Baytlar birebir ayni, yani okunan
/// referanslar da ayni; degisen TEK sey EBEVEYNIN TURU. Boylece olculen sey
/// yon kuralinin kendisi, baska hicbir sey degil.
///
/// Ornek verinin olculmus gercegi (SwReferansTestleri):
///   Montaj2.SLDDRW -> Montaj2.SLDASM -> Montaj1.SLDASM
/// </summary>
public sealed class AgacYonuTestleri : IDisposable
{
    private static string Ornek => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private readonly string _kok;

    public AgacYonuTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-agacyonu-" + Guid.NewGuid().ToString("N"));
        Kopyala(Ornek, _kok);

        // Montaji referans veren GERCEK bir dosya, iki uzantiyla.
        string kaynak = Path.Combine(_kok, "Yeni klasör", "Montaj2.SLDDRW");
        File.Copy(kaynak, Path.Combine(_kok, "Sahte.SLDDRW"));
        File.Copy(kaynak, Path.Combine(_kok, "Sahte.SLDPRT"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, recursive: true); } catch (IOException) { }
    }

    // ------------------------------------------------------------------

    [Fact]
    public void TABAN_TEKNIK_RESIM_montajini_ARSIVE_ALIYOR()
    {
        // BU TEST OLMADAN ASIL TEST KENDINI KANDIRIR: "yuruyus hic
        // calismiyor" hali de bos liste verirdi. Ayni baytlar, .SLDDRW
        // uzantisiyla: montaj cocuk olarak GORUNMELI.
        CocukKumesi kume = Surumler.Cocuklari(Path.Combine(_kok, "Sahte.SLDDRW"));

        Assert.Contains(
            Path.Combine(_kok, "Yeni klasör", "Montaj2.SLDASM"), kume.Yollar);
    }

    [Fact]
    public void PARCANIN_referans_verdigi_MONTAJ_ARSIVE_GIRMIYOR()
    {
        // ASIL OLCUM: ayni baytlar, .SLDPRT uzantisiyla - hicbir cocuk yok.
        CocukKumesi kume = Surumler.Cocuklari(Path.Combine(_kok, "Sahte.SLDPRT"));

        Assert.Empty(kume.Yollar);

        // ATLANAN BAG "BULUNAMADI" DEGILDIR (CLAUDE.md 3): eskiden bu yol
        // izlendigi icin montajin cozulemeyen cocugu da sayiliyordu ve kutu
        // "1 referans bulunamadi - versiyon EKSIK" diyordu. Hicbiri eksik
        // degil: o dal versiyonun kapsaminda hic yok.
        Assert.Equal(0, kume.Cozulemeyen);
    }

    [Fact]
    public void MONTAJ_cocuklarini_VERMEYE_DEVAM_EDIYOR()
    {
        // ASIRI SUZME KAPISI: kural yalniz parca -> montaj yonunu kesiyor.
        // Montajin kendi parcalari kesilseydi montaj versiyonlari parcasiz
        // arsivlenirdi ve SOLIDWORKS onlari "dosya bozuk" diye acmazdi
        // (CLAUDE.md 1a).
        CocukKumesi kume = Surumler.Cocuklari(Path.Combine(_kok, "Montaj1.SLDASM"));

        Assert.Contains(Path.Combine(_kok, "Parça1.SLDPRT"), kume.Yollar);
        Assert.True(kume.Dogrudan > 0);
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
