using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// KLASOR KILIDI - "bitmis isler acilmasin" (Erkan, 31.08.2026).
///
/// Asil sinav CLAUDE.md 3: kilit KAZA korumasi, guvenlik degil - ve
/// GERCEGI saklamiyor. Referans taramasi kilitli klasore GIRMEYE devam
/// etmeli; girmezse panel "bu parcayi kimse kullanmiyor" der ve SAGLAM
/// DOSYA SILDIRIR. Bu ayrim asagida ayri bir testle kilitli.
/// </summary>
public sealed class KlasorKilidiTestleri : IDisposable
{
    private readonly string _kok;

    public KlasorKilidiTestleri()
    {
        _kok = Path.Combine(
            Path.GetTempPath(), "swpdm-kilit-" + Guid.NewGuid().ToString("N")[..8]);
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
            // Temizlik tutmazsa sonucu degistirmez.
        }
    }

    private string KlasorKoy(string ad)
    {
        string yol = WindowsYolu.Birlestir(_kok, ad);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private string DosyaKoy(string klasor, string ad, string icerik = "x")
    {
        string yol = WindowsYolu.Birlestir(klasor, ad);
        File.WriteAllText(yol, icerik);
        return yol;
    }

    // ---------------------------------------------------------------------

    [Fact]
    public void KILITLEME_KALICI_ve_geri_alinabilir()
    {
        string bitmis = KlasorKoy("Bitmis Is");

        Assert.True(KlasorKilidi.Degistir(_kok, [bitmis], kilitle: true).Oldu);
        Assert.True(KlasorKilidi.Oku(_kok).KendisiKilitli(bitmis));

        Assert.True(KlasorKilidi.Degistir(_kok, [bitmis], kilitle: false).Oldu);
        Assert.False(KlasorKilidi.Oku(_kok).KendisiKilitli(bitmis));
    }

    [Fact]
    public void KILIT_ALTTAKI_dosyalari_da_KAPSAR()
    {
        // ALTINI SAYMAK SART: agac kilitli klasoru acmiyor ama referans
        // paneli oradaki bir dosyayi satir olarak gosterip F2'ye izin
        // verebilirdi. Yalniz klasorun kendisine bakan bir kontrol o
        // kapidan sizardi.
        string bitmis = KlasorKoy("Bitmis Is");
        string alt = KlasorKoy(WindowsYolu.Birlestir("Bitmis Is", "alt"));
        string dosya = DosyaKoy(alt, "Parça1.SLDPRT");

        KlasorKilidi.Degistir(_kok, [bitmis], kilitle: true);
        KilitKumesi kilitler = KlasorKilidi.Oku(_kok);

        Assert.True(kilitler.Kilitli(dosya));
        Assert.True(kilitler.Kilitli(alt));

        // ...ama KENDISI kilitli olan yalnizca ust klasor: menudeki yazi
        // ve agactaki isaret buna bakiyor.
        Assert.False(kilitler.KendisiKilitli(alt));
    }

    [Fact]
    public void ILK_KILITLI_kumeden_HANGISI_oldugunu_SOYLUYOR()
    {
        // GERI ALMA BUNU KULLANIYOR (01.09.2026 denetimi): Ctrl+Z secime
        // degil YIGININ KENDI YOLLARINA yaziyor, yani islemlerin kilit
        // kapisi oraya hic ulasmiyordu. Adim, dokunacagi yollari bildiriyor
        // ve denetim bu kumeye bakiyor.
        //
        // SAYI DEGIL YOL DONUYOR: ekranda "hangi dosya yuzunden" yazacak
        // (CLAUDE.md 3); "1 dosya kilitli" demek kullaniciya hicbir sey
        // anlatmaz.
        string bitmis = KlasorKoy("Bitmis Is");
        string canli = KlasorKoy("Canli");
        string kilitliDosya = DosyaKoy(bitmis, "Parça1.SLDPRT");
        string serbest = DosyaKoy(canli, "Parça2.SLDPRT");

        KlasorKilidi.Degistir(_kok, [bitmis], kilitle: true);
        KilitKumesi kilitler = KlasorKilidi.Oku(_kok);

        Assert.Equal(kilitliDosya, kilitler.IlkKilitli([serbest, kilitliDosya]));
        Assert.Null(kilitler.IlkKilitli([serbest]));
        Assert.Null(kilitler.IlkKilitli([]));
        Assert.Null(kilitler.IlkKilitli(null));
    }

    [Fact]
    public void KOMSU_klasor_ETKILENMEZ()
    {
        // "C:\Kok2" yolunu "C:\Kok"un ici sayan StartsWith hatasi
        // CLAUDE.md 8'de yazili; kilit de ayni tuzaga dusmemeli.
        string bir = KlasorKoy("Is");
        string iki = KlasorKoy("Is2");

        KlasorKilidi.Degistir(_kok, [bir], kilitle: true);
        KilitKumesi kilitler = KlasorKilidi.Oku(_kok);

        Assert.True(kilitler.Kilitli(bir));
        Assert.False(kilitler.Kilitli(iki));
    }

    [Fact]
    public void KOKUN_KENDISI_KILITLENMEZ()
    {
        // Kilitlenirse uygulama hicbir sey gosteremez ve kullanici kilidi
        // kaldiracak satiri da bulamaz - kendi kendini kilitleyen bir kutu.
        IslemRaporu rapor = KlasorKilidi.Degistir(_kok, [_kok], kilitle: true);

        Assert.False(rapor.Oldu);
        Assert.False(KlasorKilidi.Oku(_kok).Kilitli(_kok));
    }

    [Fact]
    public void SILINMIS_klasorun_satiri_DUSER()
    {
        string gecici = KlasorKoy("Silinecek");
        KlasorKilidi.Degistir(_kok, [gecici], kilitle: true);
        Assert.Equal(1, KlasorKilidi.Oku(_kok).Sayi);

        Directory.Delete(gecici);

        // Kayit diskte duruyor ama klasor yok: liste onu GOSTERMEZ.
        // "Kilitli ama olmayan" bir satir kullaniciyi kilidi kaldirmaya
        // ugrastirirdi.
        Assert.Equal(0, KlasorKilidi.Oku(_kok).Sayi);
    }

    [Fact]
    public void KILIT_LISTESI_KOKE_GORELI_yaziliyor()
    {
        // GORELI SART: kok baska bir harften baglanirsa (ag surucusu
        // \\sunucu\ortak bugun Z:, yarin Y:) kilitler gecerli kalmali.
        string bitmis = KlasorKoy("Bitmis Is");
        KlasorKilidi.Degistir(_kok, [bitmis], kilitle: true);

        string kayit = WindowsYolu.Birlestir(
            WindowsYolu.Birlestir(_kok, KlasorKilidi.KlasorAdi), "kilitler.txt");

        string yazilan = File.ReadAllText(kayit);
        Assert.DoesNotContain(_kok, yazilan, StringComparison.Ordinal);
        Assert.Contains("Bitmis Is", yazilan, StringComparison.Ordinal);
    }

    [Fact]
    public void KILIT_KLASORU_UYGULAMANIN_KENDI_klasorlerinden()
    {
        // Agacta gorunmemeli, indekste taranmamali, boyuta katilmamali.
        // Dort tarayici da bu listeden turetiyor (CLAUDE.md 1b).
        Assert.True(GizliKlasorler.Bizim(KlasorKilidi.KlasorAdi));
        Assert.Contains(KlasorKilidi.KlasorAdi, GizliKlasorler.Tumu);
    }

    [Fact]
    public void ARAMA_kilitli_klasore_GIRMEZ_ve_ATLANDIGINI_SOYLER()
    {
        string bitmis = KlasorKoy("Bitmis Is");
        DosyaKoy(bitmis, "Parça1.SLDPRT");
        string canli = KlasorKoy("Canli");
        DosyaKoy(canli, "Parça2.SLDPRT");

        KlasorKilidi.Degistir(_kok, [bitmis], kilitle: true);
        KilitKumesi kilitler = KlasorKilidi.Oku(_kok);

        AramaSonucu sonuc = KlasorTarayici.Ara(_kok, "Parça", 100, default, null, kilitler);

        Assert.Single(sonuc.Bulunanlar);
        Assert.Equal("Parça2.SLDPRT", sonuc.Bulunanlar[0].Ad);

        // SESSIZ ATLAMA YOK (CLAUDE.md 3): sayisi soyleniyor.
        Assert.Equal(1, sonuc.AtlananKilitli);
    }

    [Fact]
    public void ARAMA_KENDI_KLASORLERIMIZE_de_GIRMEZ()
    {
        // Bu, kilit turunda yakalanan GERCEK bir kusurdu: cop ve versiyon
        // arsivi dort yerde dislaniyordu ama ARAMA besinci yerdi ve
        // atlanmisti. Arama arsivdeki KOPYAYI gosteriyor, kullanici onu
        // gercek dosya sanip adlandirabiliyordu.
        string arsiv = KlasorKoy(Surumler.KlasorAdi);
        DosyaKoy(arsiv, "Parça1.SLDPRT");
        string cop = KlasorKoy(Cop.KlasorAdi);
        DosyaKoy(cop, "Parça2.SLDPRT");

        Assert.Empty(KlasorTarayici.Ara(_kok, "Parça", 100).Bulunanlar);
    }

    [Fact]
    public void REFERANS_TARAMASI_kilitli_klasore_GIRMEYE_DEVAM_EDER()
    {
        // EN ONEMLI AYRIM (CLAUDE.md 3): kilit GOZDEN saklar, GERCEGI
        // degil. Bitmis montaj hala parcalari kullaniyor; indeksten
        // duserse panel "bu parcayi kimse kullanmiyor" der ve kullanici
        // SAGLAM DOSYAYI SILER.
        string bitmis = KlasorKoy("Bitmis Is");
        string montaj = WindowsYolu.Birlestir(bitmis, "Montaj1.SLDASM");
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz", "Montaj1.SLDASM"),
            montaj);

        KlasorKilidi.Degistir(_kok, [bitmis], kilitle: true);

        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);

        Assert.NotNull(indeks.Kayit(montaj));
    }
}
