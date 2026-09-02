using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// ARSIV DOSYAYLA BIRLIKTE TASINIR - SurumlerTestleri'nin ayni ozelligi.
///
/// Ayri dosya olmasinin TEK sebebi boyut kapisi (600 satir): 02.09.2026'da
/// yerel oturumun eklediklerinden sonra tek dosya 611 satir etti. Kesme yeri
/// satir sayisina degil KONUYA gore (CLAUDE.md 1b) - burasi bastan sona
/// "ad/klasor degisince versiyonlar takip ediyor mu" sorusu.
/// </summary>
public partial class SurumlerTestleri
{
    // ---------------------------------------------------------------------
    // ARSIV DOSYAYLA BIRLIKTE TASINIR (Erkan, 31.08.2026: "parçanın adını
    // veya bağlı bulunduğu klasörün adını değiştirince versiyonlar
    // gözükmüyor, versiyon yok diyor").
    // ---------------------------------------------------------------------

    [Fact]
    public void AD_degisince_VERSIYONLAR_yeni_adda_gorunuyor()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "v0 hali");
        Surumler.Olustur(_kok, yol, "ilk", out int _);

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(yol, "11-Parca1.SLDPRT");
        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Null(rapor.Sebep);            // arsiv sorunsuz tasindi

        SurumDurumu durum = Surumler.Listele(_kok, rapor.YeniYol!);
        Assert.Single(durum.Ogeler);
        Assert.Equal("v0 hali", File.ReadAllText(durum.Ogeler[0].ArsivYolu));

        // Eski yuva OKSUZ kalmamali.
        Assert.Empty(Surumler.Listele(_kok, yol).Ogeler);
    }

    [Fact]
    public void KLASOR_ADI_degisince_icindeki_dosyanin_versiyonlari_DURUYOR()
    {
        string yol = DosyaKoy(WindowsYolu.Birlestir("55", "Parca1.SLDPRT"), "v0 hali");
        Surumler.Olustur(_kok, yol, "ilk", out int _);

        string eskiKlasor = WindowsYolu.Birlestir(_kok, "55");
        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(eskiKlasor, "56");
        Assert.True(rapor.Oldu, rapor.Sebebi);

        // Tek Directory.Move, klasordeki BUTUN yuvalari birden tasidi.
        string yeniYol = WindowsYolu.Birlestir(
            WindowsYolu.Birlestir(_kok, "56"), "Parca1.SLDPRT");
        SurumDurumu durum = Surumler.Listele(_kok, yeniYol);
        Assert.Single(durum.Ogeler);
        Assert.Equal("v0 hali", File.ReadAllText(durum.Ogeler[0].ArsivYolu));
    }

    [Fact]
    public void BASKA_KLASORE_tasininca_versiyonlar_TAKIP_EDIYOR()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "v0 hali");
        Surumler.Olustur(_kok, yol, "ilk", out int _);

        string hedef = WindowsYolu.Birlestir(_kok, "3");
        Directory.CreateDirectory(hedef);

        IslemRaporu rapor = DosyaIslemleri.Tasi(yol, hedef, Cakisma.Sor);
        Assert.True(rapor.Oldu, rapor.Sebebi);

        SurumDurumu durum = Surumler.Listele(_kok, rapor.YeniYol!);
        Assert.Single(durum.Ogeler);
        Assert.Equal("v0 hali", File.ReadAllText(durum.Ogeler[0].ArsivYolu));
    }

    [Fact]
    public void GERI_ALINCA_arsiv_de_ESKI_ADA_donuyor()
    {
        // Ctrl+Z ayni DosyaIslemleri.YenidenAdlandir'dan geciyor; kanca
        // cekirdekte oldugu icin geri alma da bedava calisiyor.
        string yol = DosyaKoy("Parca1.SLDPRT", "v0 hali");
        Surumler.Olustur(_kok, yol, "ilk", out int _);

        IslemRaporu ileri = DosyaIslemleri.YenidenAdlandir(yol, "Gecici.SLDPRT");
        Assert.True(ileri.Oldu, ileri.Sebebi);

        IslemRaporu geri = DosyaIslemleri.YenidenAdlandir(ileri.YeniYol!, "Parca1.SLDPRT");
        Assert.True(geri.Oldu, geri.Sebebi);

        Assert.Single(Surumler.Listele(_kok, yol).Ogeler);
    }

    [Fact]
    public void HEDEFTE_ARSIV_VARSA_tasinmaz_ve_IKISI_DE_yerinde_kalir()
    {
        // Hedef yuva, cope gitmis eski bir dosyadan kalmis olabilir.
        // Ustune yazmak ikisinden birini yok ederdi (CLAUDE.md 1a).
        string a = DosyaKoy("A.SLDPRT", "a icerigi");
        Surumler.Olustur(_kok, a, "a-notu", out int _);

        string b = DosyaKoy("B.SLDPRT", "b icerigi");
        Surumler.Olustur(_kok, b, "b-notu", out int _);
        File.Delete(b);                       // B'nin dosyasi gitti, yuvasi kaldi

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(a, "B.SLDPRT");

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.NotNull(rapor.Sebep);          // SESSIZ GECMIYOR
        Assert.Contains("zaten arşiv var", rapor.Sebep!, StringComparison.Ordinal);

        // B'nin eski arsivi yerinde; A'nin arsivi de silinmedi.
        Assert.Equal("b-notu", Surumler.Listele(_kok, b).Ogeler[0].Not);
        Assert.Equal("a-notu", Surumler.Listele(_kok, a).Ogeler[0].Not);
    }

    [Fact]
    public void VERSIYONU_OLMAYAN_dosyada_hicbir_sey_URETILMIYOR()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "icerik");

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(yol, "Yeni.SLDPRT");

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Null(rapor.Sebep);
        Assert.False(Directory.Exists(WindowsYolu.Birlestir(_kok, Surumler.KlasorAdi)));
    }
}
