using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// ARSIV DUZENI - gercek SOLIDWORKS dosyalariyla.
///
/// IKI SEYI BIRDEN OLCUYOR:
///   1. YENI KURAL (Erkan, 01.09.2026): versiyon YALNIZ o dosyadir - montaj
///      ve teknik resim dahil. "ne alaka dosyalari arsivleme."
///   2. ESKI ARSIVLER BOZULMADI (CLAUDE.md 1a): Erkan'in diskinde 31.08-01.09
///      arasi olusmus COCUKLU (kendi kendine yeten) arsivler DURUYOR;
///      listelenmeye, acilmaya ve donulmeye devam etmeli. Onlari artik
///      Olustur uretmedigi icin testler EskiCocukluArsiv ile elle kuruyor.
///
/// SurumlerTestleri'nin PARCASI (partial): ayni gecici kok, ayni yardimcilar;
/// ikinci bir duzenek kurulmuyor (CLAUDE.md 8). Ayri dosya olmasinin sebebi
/// tek dosyanin 688 satira cikmasi - boyut kapisinin siniri 600.
/// </summary>
public partial class SurumlerTestleri
{
    // ---------------------------------------------------------------------
    // VERSIYON = YALNIZ O DOSYA (Erkan, 01.09.2026: "versiyon olusturma o
    // parcanin bir kopyasini olusturma degil mi, ne alaka dosyalari
    // arsivleme").
    //
    // 31.08.2026 - 01.09.2026 arasinda montajin/teknik resmin O GUNKU
    // COCUKLARI da ayni klasore kopyalaniyordu ("kendi kendine yeten
    // versiyon"). Sebebi gecerliydi - arsivdeki montaj parcalarini yaninda
    // bulamayinca acilmiyordu - ama bedeli Erkan'in agacinda tek bir teknik
    // resim icin 5, bir parca icin 162 dosyaydi. Karar soruldu, cevap:
    // "yalniz o dosya (parca gibi)".
    // ---------------------------------------------------------------------

    private static string OrnekVeri => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private string OrnegiKoy(string ad)
    {
        string yol = WindowsYolu.Birlestir(_kok, ad);
        File.Copy(Path.Combine(OrnekVeri, ad), yol);
        return yol;
    }

    /// <summary>
    /// 31.08.2026 - 01.09.2026 arasindaki KENDI KENDINE YETEN arsivi elle
    /// kurar: asil dosyanin yanina o gunku cocuk kopyalarini koyar.
    ///
    /// NEDEN ELLE: Olustur artik cocuk kopyalamiyor (Erkan'in karari). Ama
    /// Erkan'in diskindeki o duzendeki arsivler DURUYOR ve calismaya devam
    /// etmeli (CLAUDE.md 1a) - asagidaki testlerin olctugu sey bu.
    ///
    /// SALT-OKUNUR da konuyor: gercek arsiv kopyalari oyle dogar, ve donus
    /// yolu bunu asabiliyor mu sorusu testin PARCASI.
    /// </summary>
    private void EskiCocukluArsiv(string asil, int no, params string[] cocuklar)
    {
        string klasor = null!;
        foreach (SurumKaydi kayit in Surumler.Listele(_kok, asil).Ogeler)
        {
            if (kayit.No == no)
            {
                klasor = WindowsYolu.Klasor(kayit.ArsivYolu);
            }
        }

        Assert.NotNull(klasor);

        foreach (string cocuk in cocuklar)
        {
            string hedef = WindowsYolu.Birlestir(klasor, WindowsYolu.DosyaAdi(cocuk));
            File.Copy(cocuk, hedef, overwrite: true);
            File.SetAttributes(hedef, FileAttributes.ReadOnly);
        }
    }

    [Fact]
    public void MONTAJ_versiyonu_da_TEK_DOSYA_kalir()
    {
        // ASIL OLCUM (Erkan'in karari, 01.09.2026): montajin yaninda parcasi
        // DURUYOR olmasina ragmen arsive YALNIZ montaj giriyor.
        //
        // TABAN SART: parca gercekten kokte duruyor ve montaj onu referans
        // veriyor - yoksa "zaten cocugu yoktu" hali de testi gecerdi.
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Assert.True(File.Exists(parca));

        IslemRaporu rapor = Surumler.Olustur(_kok, montaj, "ilk", out int no);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal(0, no);

        // Asil dosya GERCEK ADIYLA duruyor: "v0.SLDASM" adinda bir dosya
        // montajin aradigi ad DEGIL. Yuva duzeni degismedi.
        string arsiv = rapor.YeniYol!;
        Assert.Equal("Montaj1.SLDASM", WindowsYolu.DosyaAdi(arsiv));
        Assert.Equal("v0", WindowsYolu.DosyaAdi(WindowsYolu.Klasor(arsiv)));

        // VE BASKA HICBIR DOSYA YOK.
        Assert.Single(Directory.GetFiles(WindowsYolu.Klasor(arsiv)));
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
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "ilk", out int _);
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");
        Surumler.Olustur(_kok, montaj, "eski kayit", out int _);
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

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
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

        // Parca DEGISMEDI: geri yazmaya da arsivlemeye de gerek yok.
        Assert.True(Surumler.Don(_kok, montaj, 0, [parca]).Oldu);

        Assert.Empty(Surumler.Listele(_kok, parca).Ogeler);
    }

    // ---------------------------------------------------------------------
    // "PARCALARI YANINDA DEGIL" UYARISI HANGI ARSIVDE CIKAR (CLAUDE.md 3).
    //
    // Yeni versiyon yalniz o dosya; arsivdeki montaji cift tiklayan
    // kullanici BUGUNKU parcalarla acilmis bir montaj gorur ve bunu
    // anlamasinin baska hicbir yolu yoktur. Durum cubugu bunu soyluyor -
    // ama YALNIZCA gercekten oyleyse: Erkan'in elindeki cocuklu ESKI
    // arsivlerde cikmamali, yoksa bayat uyari olur (CLAUDE.md 6).
    // ---------------------------------------------------------------------

    [Fact]
    public void YENI_versiyonun_YANINDA_COCUK_YOK()
    {
        OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");

        IslemRaporu rapor = Surumler.Olustur(_kok, montaj, "ilk", out int _);

        Assert.False(Surumler.YanindaCocukVarMi(rapor.YeniYol));
    }

    [Fact]
    public void ESKI_cocuklu_arsivde_YANINDA_COCUK_VAR()
    {
        // TABAN SART: ustteki test tek basina "hep false donen" bir metotla
        // da gecerdi.
        string parca = OrnegiKoy("Parça1.SLDPRT");
        string montaj = OrnegiKoy("Montaj1.SLDASM");

        IslemRaporu rapor = Surumler.Olustur(_kok, montaj, "ilk", out int _);
        EskiCocukluArsiv(montaj, 0, parca);   // 31.08-01.09 duzeni

        Assert.True(Surumler.YanindaCocukVarMi(rapor.YeniYol));
    }

    [Fact]
    public void YANINDA_COCUK_sorusu_OLMAYAN_yolda_PATLAMIYOR()
    {
        // Bakilamayan yolda UYARI VERILMEZ: olmayan bir eksigi soylemek de
        // yalandir (CLAUDE.md 3).
        Assert.False(Surumler.YanindaCocukVarMi(null));
        Assert.False(Surumler.YanindaCocukVarMi(WindowsYolu.Birlestir(_kok, "yok/v0/A.SLDPRT")));
    }
}
