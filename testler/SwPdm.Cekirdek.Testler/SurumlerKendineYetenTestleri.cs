using System;
using System.Collections.Generic;
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

    [Fact]
    public void ONARIMIN_YAZDIGI_GORELI_YOLLU_cocuk_da_ARSIVLENIR()
    {
        // ERKAN'IN EKRANI (31.08.2026): montaj 3\, parca 3157\ klasorunde ve
        // montajin icindeki yol ONARIMIN YAZDIGI GORELI yol ("..\3157\...").
        // Ilk halde cocuk toplayici bu yolu ebeveyne gore COZMUYORDU:
        // v0'da montaj TEK BASINA kaldi ve SOLIDWORKS "dosya bozuk" dedi.
        string uc = Path.Combine(_kok, "3");
        string binUc = Path.Combine(_kok, "3157");
        Directory.CreateDirectory(uc);
        Directory.CreateDirectory(binUc);

        string montaj = Path.Combine(uc, "Montaj1.SLDASM");
        File.Move(OrnegiKoy("Montaj1.SLDASM"), montaj);

        // Parcayi 3157'ye tasi ve montaji GERCEK onarimla yamala - montajin
        // icine tam da Erkan'daki gibi ebeveyne goreli yol yazilir.
        string parca = Path.Combine(binUc, "Parça1.SLDPRT");
        File.Move(OrnegiKoy("Parça1.SLDPRT"), parca);

        YamaSonucu yama = SwYazici.YoluDegistir(
            montaj, montaj + ".yeni", "Parça1.SLDPRT", parca, uc);
        Assert.True(yama.Oldu, yama.Sebep);
        File.Delete(montaj);
        File.Move(montaj + ".yeni", montaj);

        // ASIL OLCUM: cocuk bulunmali ve arsive girmeli.
        CocukKumesi cocuklar = Surumler.Cocuklari(montaj);
        Assert.Contains(parca, cocuklar.Yollar);

        IslemRaporu rapor = Surumler.Olustur(_kok, montaj, "ilk", out int _);
        Assert.True(rapor.Oldu, rapor.Sebebi);

        string v0 = WindowsYolu.Klasor(rapor.YeniYol!);
        Assert.True(
            File.Exists(WindowsYolu.Birlestir(v0, "Parça1.SLDPRT")),
            "parca kopyasi v0'da montajin yaninda degil");
    }

    // ---------------------------------------------------------------------
    // AD/KLASOR DEGISINCE VERSIYONLAR GORUNMEYE DEVAM EDER (Erkan,
    // 31.08.2026: "versiyonu olan parçanın adını veya bulunduğu klasörün
    // adını değişince versiyonları göremiyor").
    //
    // Arsiv klasor oldugundan beri asil dosya YUVANIN ADIYLA araniyordu; ad
    // degisince yuva yeni ada tasiniyor ama icindeki kopya arsivlendigi
    // gunku adini koruyor -> eslesme kayboluyordu. Cocugu olmayan dosyada
    // "tek dosya" kurali kurtariyordu; COCUKLU dosyada kurtarmiyor.
    // ---------------------------------------------------------------------

    [Fact]
    public void COCUKLU_dosyanin_ADI_degisince_versiyonlar_GORUNUR()
    {
        OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(montaj, "Montaj9.SLDASM");
        Assert.True(rapor.Oldu, rapor.Sebebi);

        SurumDurumu durum = Surumler.Listele(_kok, rapor.YeniYol!);

        Assert.Single(durum.Ogeler);
        Assert.Equal(0, durum.BozukSatir);
        Assert.Equal("Montaj1.SLDASM", WindowsYolu.DosyaAdi(durum.Ogeler[0].ArsivYolu));
    }

    [Fact]
    public void COCUKLU_dosyanin_KLASORU_adlanınca_versiyonlar_GORUNUR()
    {
        string alt = Path.Combine(_kok, "55");
        Directory.CreateDirectory(alt);

        string parca = Path.Combine(alt, "Parça1.SLDPRT");
        File.Move(OrnegiKoy("Parça1.SLDPRT"), parca);
        string montaj = Path.Combine(alt, "Montaj1.SLDASM");
        File.Move(OrnegiKoy("Montaj1.SLDASM"), montaj);

        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        Assert.True(DosyaIslemleri.YenidenAdlandir(alt, "56").Oldu);

        string yeni = WindowsYolu.Birlestir(
            WindowsYolu.Birlestir(_kok, "56"), "Montaj1.SLDASM");
        SurumDurumu durum = Surumler.Listele(_kok, yeni);

        Assert.Single(durum.Ogeler);
        Assert.Equal(0, durum.BozukSatir);
    }

    [Fact]
    public void ADI_DEGISMIS_cocuklu_versiyona_DONULEBILIYOR()
    {
        OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        long ilkBoyut = new FileInfo(montaj).Length;
        string yeni = WindowsYolu.Birlestir(_kok, "Montaj9.SLDASM");
        Assert.True(DosyaIslemleri.YenidenAdlandir(montaj, "Montaj9.SLDASM").Oldu);

        File.AppendAllText(yeni, "bozucu ek");
        Assert.True(Surumler.Don(_kok, yeni, 0).Oldu);
        Assert.Equal(ilkBoyut, new FileInfo(yeni).Length);
    }

    [Fact]
    public void KAYITTA_AD_YOKKEN_uzantiya_gore_bulunur()
    {
        // Bu turdan ONCE yazilmis kayitlar dort alanli: 5. alan (asil ad)
        // yok. Erkan'in elindeki arsivler boyle - onlarin da gorunmesi sart
        // (CLAUDE.md 3), yoksa "versiyonlarim kayboldu" der.
        OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "eski kayit", out int _);

        // 5. alani KIRP: eski bicime dondur.
        string kayit = KayitYolu(montaj);
        string[] satirlar = File.ReadAllLines(kayit);
        File.WriteAllText(
            kayit,
            string.Join("\n", Array.ConvertAll(satirlar, x =>
                string.Join("\t", x.Split('\t')[..4]))) + "\n");

        Assert.True(DosyaIslemleri.YenidenAdlandir(montaj, "Montaj9.SLDASM").Oldu);

        SurumDurumu durum = Surumler.Listele(
            _kok, WindowsYolu.Birlestir(_kok, "Montaj9.SLDASM"));

        Assert.Single(durum.Ogeler);
        Assert.Equal("eski kayit", durum.Ogeler[0].Not);
    }

    // ---------------------------------------------------------------------
    // MONTAJDA VERSIYON SECME (Erkan'in ilk versiyon isteginin 3. maddesi):
    // "montajın içinde parçayı seçtiğimde istediğim versiyona göre
    // güncelleyebilmeliyim". Versiyon kendi kendine yettigi icin o gunku
    // cocuk kopyalari arsivde hazir.
    // ---------------------------------------------------------------------

    [Fact]
    public void COCUKLA_BIRLIKTE_DONUS_parcayi_da_geri_yazar()
    {
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        byte[] ilkParca = File.ReadAllBytes(parca);

        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        // Parca DEGISTI (bugunku hal), montaj da.
        File.AppendAllText(parca, "sonradan eklendi");
        File.AppendAllText(montaj, "sonradan eklendi");

        IslemRaporu rapor = Surumler.Don(_kok, montaj, 0, [parca]);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal(ilkParca, File.ReadAllBytes(parca));      // PARCA da dondu
        Assert.Contains("çocuk geri yazıldı", rapor.Sebep!, StringComparison.Ordinal);
    }

    [Fact]
    public void COCUGUN_BUGUNKU_HALI_de_ARSIVLENIR_kaybolmaz()
    {
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        File.AppendAllText(parca, "bugunku hal");
        long bugunku = new FileInfo(parca).Length;

        Assert.True(Surumler.Don(_kok, montaj, 0, [parca]).Oldu);

        // Parcanin KENDI yuvasinda bugunku hali durmali (1a: kaybolan yok).
        SurumDurumu parcaninki = Surumler.Listele(_kok, parca);
        Assert.NotEmpty(parcaninki.Ogeler);
        Assert.Equal(bugunku, new FileInfo(parcaninki.Ogeler[0].ArsivYolu).Length);
    }

    [Fact]
    public void SECILMEYEN_cocuga_DOKUNULMAZ()
    {
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        File.AppendAllText(parca, "degisiklik");
        byte[] once = File.ReadAllBytes(parca);

        // Cocuk verilmiyor: eski davranis - yalniz asil dosya doner.
        Assert.True(Surumler.Don(_kok, montaj, 0).Oldu);

        Assert.Equal(once, File.ReadAllBytes(parca));
    }

    [Fact]
    public void ACIK_cocuk_ATLANIR_ve_SEBEBI_yazilir()
    {
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        File.AppendAllText(parca, "degisiklik");
        byte[] once = File.ReadAllBytes(parca);
        File.WriteAllBytes(WindowsYolu.Birlestir(_kok, "~$Parça1.SLDPRT"), new byte[4]);

        IslemRaporu rapor = Surumler.Don(_kok, montaj, 0, [parca]);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal(once, File.ReadAllBytes(parca));          // DOKUNULMADI
        Assert.Contains("açık", rapor.Sebep!, StringComparison.Ordinal);
    }

    [Fact]
    public void DONUS_LISTESI_farki_ve_engeli_SOYLER()
    {
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        IReadOnlyList<DonusOgesi> liste = Surumler.DonusListesi(_kok, montaj, 0);

        DonusOgesi oge = Assert.Single(liste);                 // asil dosya listede YOK
        Assert.Equal(parca, oge.CanliYol);
        Assert.Null(oge.Engel);
        Assert.False(oge.Farkli);                              // henuz degismedi

        File.AppendAllText(parca, "degisiklik");
        Assert.True(Surumler.DonusListesi(_kok, montaj, 0)[0].Farkli);
    }

    [Fact]
    public void AYNI_ICERIKLI_cocuk_icin_GEREKSIZ_ARSIV_olusmaz()
    {
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);

        // Parca DEGISMEDI: geri yazmaya da arsivlemeye de gerek yok.
        Assert.True(Surumler.Don(_kok, montaj, 0, [parca]).Oldu);

        Assert.Empty(Surumler.Listele(_kok, parca).Ogeler);
    }
}
