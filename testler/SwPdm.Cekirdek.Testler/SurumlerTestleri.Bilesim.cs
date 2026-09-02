using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// VERSIYON BILESIMI - "o gunku hali" ile "bugunku parcalar" ayrimi
/// (Erkan, 02.09.2026: "aynı mekanizma versiyonlardada calışması lazım").
///
/// UC ASIL SINAV:
///   1. Montajin eski versiyonu O GUNKU parcalariyla acilabilmeli - parca
///      degismis, tasinmis, adi degismis, hatta SILINMIS olsa bile.
///   2. Parcanin KENDI versiyon listesi kirlenmemeli (Erkan: "montajın
///      versiyonunu oluştur dediğimde içindeki tüm parçaların versiyonunu
///      oluşturuyor") - "versiyon = yalniz o dosya" kurali duruyor.
///   3. Kayittaki yol O GUNKU yerdir ve DONMUSTUR: arsivdeki montaj
///      parcalarini o gunku yerlerinde arar (arsiv kopyasi salt-okunur,
///      hic onarilmaz), yani sahne bugunku yeri degil o gunku yeri kurmali.
/// </summary>
public partial class SurumlerTestleri
{
    private string ArsivYolu(string yol, int no)
        => Surumler.Listele(_kok, yol).Ogeler.Single(k => k.No == no).ArsivYolu;

    private string DepoKlasoru()
        => WindowsYolu.Birlestir(WindowsYolu.Birlestir(_kok, Surumler.KlasorAdi), ".icerik");

    private string SahneyeKur(string montaj, int no, out SahneSonucu sahne)
    {
        string arsiv = ArsivYolu(montaj, no);
        sahne = SurumSahnesi.KurBilesimle(
            _kok, arsiv, SurumSahnesi.OrijinalYol(_kok, arsiv), Surumler.BilesimOku(arsiv));
        return WindowsYolu.Klasor(sahne.AcilacakYol!);
    }

    [Fact]
    public void Bilesim_parcalarin_KENDI_versiyon_listesini_KIRLETMEZ()
    {
        // Erkan'in itirazi (02.09.2026). Ilk denemede her cocuk kendi
        // arsivinde surumleniyordu ve parcanin listesinde "(otomatik - ...)"
        // satirlari cikiyordu.
        string montaj = DosyaKoy("MONTAJ.SLDASM", "m0");
        string parca = DosyaKoy("PARCA.SLDPRT", "p0");

        Assert.True(Surumler.Olustur(_kok, montaj, "ilk", out int no).Oldu);
        Assert.True(Surumler.BilesimYaz(_kok, montaj, no, [parca], out int yeni, out var atlanan).Oldu);

        Assert.Equal(1, yeni);
        Assert.Empty(atlanan);

        SurumDurumu parcanin = Surumler.Listele(_kok, parca);
        Assert.True(parcanin.Guvenilir);     // "okunamadi" DEMIYOR...
        Assert.Empty(parcanin.Ogeler);       // ...ama versiyonu da YOK

        // Montajin kendi vN klasorunde de hala TEK dosya var.
        Assert.Single(Directory.GetFiles(WindowsYolu.Klasor(ArsivYolu(montaj, no))));
    }

    [Fact]
    public void Icerik_deposu_salt_okunur_ve_ayni_icerigi_IKI_KEZ_yazmaz()
    {
        string montaj = DosyaKoy("M.SLDASM", "m0");
        string sabit = DosyaKoy("SABIT.SLDPRT", "hep-ayni");
        string degisen = DosyaKoy("DEGISEN.SLDPRT", "eski");

        Surumler.Olustur(_kok, montaj, "n", out int v0);
        Surumler.BilesimYaz(_kok, montaj, v0, [sabit, degisen], out int yeni0, out _);
        Assert.Equal(2, yeni0);

        File.WriteAllText(montaj, "m1");
        File.WriteAllText(degisen, "yeni");
        Surumler.Olustur(_kok, montaj, "n", out int v1);
        Surumler.BilesimYaz(_kok, montaj, v1, [sabit, degisen], out int yeni1, out _);

        Assert.Equal(1, yeni1);   // yalniz degisen icerik depoya girdi

        string[] blob = Directory.GetFiles(DepoKlasoru(), "*", SearchOption.AllDirectories);
        Assert.Equal(3, blob.Length);
        Assert.All(blob, b => Assert.True(new FileInfo(b).IsReadOnly));
        Assert.All(blob, b => Assert.Equal(64, WindowsYolu.DosyaAdi(b).Length));
    }

    [Fact]
    public void Bilesim_kaydi_vN_klasorunun_ICINDE_degil_yaninda()
    {
        // ICERI konsaydi YanindaCocukVarMi her yeni versiyonu ESKI (cocuklu)
        // duzen sanar ve sahne hic kurulmazdi (CLAUDE.md 1a).
        string montaj = DosyaKoy("M.SLDASM", "m");
        string parca = DosyaKoy("P.SLDPRT", "p");

        Surumler.Olustur(_kok, montaj, "n", out int no);
        Surumler.BilesimYaz(_kok, montaj, no, [parca], out _, out _);

        Assert.False(Surumler.YanindaCocukVarMi(ArsivYolu(montaj, no)));
    }

    [Fact]
    public void Sahne_O_GUNKU_parcayi_dizer_bugunkunu_DEGIL()
    {
        string montaj = DosyaKoy("M.SLDASM", "m0");
        // EGIK BOLU BILEREK (CLAUDE.md 4): WindowsYolu.Birlestir("alt","P...")
        // ayiricisiz bir metinde TERS BOLU seciyor ve Linux'ta bu TEK DOSYA
        // ADI oluyor - "alt" klasoru hic olusmuyor, test yalniz Windows'ta
        // gecerdi. Linux CI bunu yakaladi (02.09.2026).
        string parca = DosyaKoy("alt/P.SLDPRT", "p-ESKI");

        Surumler.Olustur(_kok, montaj, "n", out int v0);
        Surumler.BilesimYaz(_kok, montaj, v0, [parca], out _, out _);
        File.WriteAllText(parca, "p-YENI");

        string sahneKok = SahneyeKur(montaj, v0, out SahneSonucu sahne);

        Assert.Null(sahne.Sebep);
        Assert.Equal(1, sahne.Dizilen);
        Assert.Equal(0, sahne.Bugunku);
        Assert.Empty(sahne.Atlanan);
        Assert.Equal("m0", File.ReadAllText(sahne.AcilacakYol!));
        Assert.Equal(
            "p-ESKI",
            File.ReadAllText(WindowsYolu.Birlestir(
                WindowsYolu.Birlestir(sahneKok, "alt"), "P.SLDPRT")));
    }

    [Fact]
    public void Adi_degisen_parca_O_GUNKU_YERINE_o_gunku_icerikle_dizilir()
    {
        // ERKAN'IN DISKINDE OLCULDU (02.09.2026): parcalar tasinmis ve
        // yeniden adlandirilmisti. Icerik KARMAYLA bulundugu icin yolun
        // bayatlamasi aramayi bozmaz; yerlesim ise O GUNKU yol olmali -
        // arsivdeki montaj parcayi orada ariyor.
        string montaj = DosyaKoy("M.SLDASM", "m0");
        string parca = DosyaKoy("C.SLDPRT", "c-ESKI");

        Surumler.Olustur(_kok, montaj, "n", out int v0);
        Surumler.BilesimYaz(_kok, montaj, v0, [parca], out _, out _);

        string yeniAd = WindowsYolu.Birlestir(_kok, "C-YENI-AD.SLDPRT");
        File.Move(parca, yeniAd);
        Assert.Null(Surumler.Tasindi(parca, yeniAd));
        File.WriteAllText(yeniAd, "c-BUGUN");

        string sahneKok = SahneyeKur(montaj, v0, out SahneSonucu sahne);

        Assert.Equal(1, sahne.Dizilen);
        Assert.Equal(0, sahne.Bugunku);
        Assert.Equal(
            "c-ESKI",
            File.ReadAllText(WindowsYolu.Birlestir(sahneKok, "C.SLDPRT")));
    }

    [Fact]
    public void Klasoru_tasinan_parca_da_O_GUNKU_yerinde_dizilir()
    {
        string montaj = DosyaKoy("M.SLDASM", "m0");
        string parca = DosyaKoy("eski-klasor/C.SLDPRT", "c-ESKI");   // CLAUDE.md 4

        Surumler.Olustur(_kok, montaj, "n", out int v0);
        Surumler.BilesimYaz(_kok, montaj, v0, [parca], out _, out _);

        string eskiKlasor = WindowsYolu.Birlestir(_kok, "eski-klasor");
        string yeniKlasor = WindowsYolu.Birlestir(_kok, "yeni-klasor");
        Directory.Move(eskiKlasor, yeniKlasor);
        Assert.Null(Surumler.Tasindi(eskiKlasor, yeniKlasor));

        string sahneKok = SahneyeKur(montaj, v0, out SahneSonucu sahne);

        Assert.Equal(1, sahne.Dizilen);
        Assert.Equal(0, sahne.Bugunku);
        Assert.Equal(
            "c-ESKI",
            File.ReadAllText(WindowsYolu.Birlestir(
                WindowsYolu.Birlestir(sahneKok, "eski-klasor"), "C.SLDPRT")));
    }

    [Fact]
    public void SILINMIS_parca_bile_depodan_geri_gelir()
    {
        string montaj = DosyaKoy("M.SLDASM", "m0");
        string parca = DosyaKoy("P.SLDPRT", "p-ESKI");

        Surumler.Olustur(_kok, montaj, "n", out int v0);
        Surumler.BilesimYaz(_kok, montaj, v0, [parca], out _, out _);
        File.Delete(parca);

        string sahneKok = SahneyeKur(montaj, v0, out SahneSonucu sahne);

        Assert.Equal(1, sahne.Dizilen);
        Assert.Equal(0, sahne.Bugunku);
        Assert.Equal("p-ESKI", File.ReadAllText(WindowsYolu.Birlestir(sahneKok, "P.SLDPRT")));
    }

    [Fact]
    public void ESKI_numara_tabanli_kayit_HALA_okunur()
    {
        // Erkan'in diskinde 02.09.2026 sabahi yazilmis numarali kayitlar
        // DURUYOR; okunmaya devam etmeli (CLAUDE.md 1a).
        string montaj = DosyaKoy("M.SLDASM", "m0");
        string parca = DosyaKoy("C.SLDPRT", "c-ESKI");

        Surumler.Olustur(_kok, montaj, "n", out int mv);
        Surumler.Olustur(_kok, parca, "elle", out int cv);
        File.WriteAllText(
            WindowsYolu.Birlestir(
                WindowsYolu.Birlestir(
                    WindowsYolu.Birlestir(_kok, Surumler.KlasorAdi), "M.SLDASM"),
                "v" + mv + ".cocuklar.txt"),
            "C.SLDPRT\t" + cv + Environment.NewLine);
        File.WriteAllText(parca, "c-BUGUN");

        BilesimDurumu durum = Surumler.BilesimOku(ArsivYolu(montaj, mv));
        Assert.True(durum.Kullanilabilir);
        Assert.Null(durum.Ogeler.Single().Karma);
        Assert.Equal(cv, durum.Ogeler.Single().No);

        string sahneKok = SahneyeKur(montaj, mv, out SahneSonucu sahne);
        Assert.Equal(1, sahne.Dizilen);
        Assert.Equal("c-ESKI", File.ReadAllText(WindowsYolu.Birlestir(sahneKok, "C.SLDPRT")));
    }

    [Fact]
    public void Bilesimsiz_ESKI_versiyon_yok_der_patlamaz()
    {
        string montaj = DosyaKoy("M.SLDASM", "m0");
        Surumler.Olustur(_kok, montaj, "n", out int v0);

        BilesimDurumu durum = Surumler.BilesimOku(ArsivYolu(montaj, v0));

        Assert.False(durum.Var);
        Assert.Null(durum.Okunamadi);          // YOK, "okunamadi" DEGIL
        Assert.False(durum.Kullanilabilir);
    }

    [Fact]
    public void Kok_disindaki_cocuk_atlanir_ve_SEBEBI_soylenir()
    {
        string montaj = DosyaKoy("M.SLDASM", "m0");
        string disari = WindowsYolu.Birlestir(
            Path.GetTempPath(), "swpdm-disari-" + Guid.NewGuid().ToString("N")[..8] + ".SLDPRT");
        File.WriteAllText(disari, "d");

        try
        {
            Surumler.Olustur(_kok, montaj, "n", out int v0);
            Surumler.BilesimYaz(_kok, montaj, v0, [disari], out _, out var atlanan);

            Assert.Single(atlanan);
            Assert.Contains("kökün dışında", atlanan[0], StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(disari);
        }
    }

    [Fact]
    public void Depodaki_icerik_yoksa_SESSIZ_KALINMAZ()
    {
        // Depo elle silinmis olabilir. "O gunku hali" deyip bugunku parcayi
        // dizmek sessiz bir yalan olurdu; sayisi ayri tutuluyor (CLAUDE.md 3).
        string montaj = DosyaKoy("M.SLDASM", "m0");
        string parca = DosyaKoy("P.SLDPRT", "p-ESKI");

        Surumler.Olustur(_kok, montaj, "n", out int v0);
        Surumler.BilesimYaz(_kok, montaj, v0, [parca], out _, out _);

        foreach (string blob in Directory.GetFiles(DepoKlasoru(), "*", SearchOption.AllDirectories))
        {
            new FileInfo(blob).IsReadOnly = false;
            File.Delete(blob);
        }

        SahneyeKur(montaj, v0, out SahneSonucu sahne);

        Assert.Equal(1, sahne.Dizilen);
        Assert.Equal(1, sahne.Bugunku);   // bugunku hali dizildi VE SAYILDI
    }
}
