using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Versiyon arsivi GERCEK klasorlerle kosuyor. Asil sinav CLAUDE.md 1a:
/// hicbir icerik hicbir islemle kaybolmamali - "don" bile once bugunku hali
/// arsivlemeli. Kaybolursa uygulama, kullanicinin PARCASINI kaybettirir.
/// </summary>
public partial class SurumlerTestleri : IDisposable
{
    private readonly string _kok;

    public SurumlerTestleri()
    {
        _kok = Path.Combine(
            Path.GetTempPath(), "swpdm-surum-" + Guid.NewGuid().ToString("N")[..8]);
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

        GC.SuppressFinalize(this);
    }

    private string DosyaKoy(string ad, string icerik)
    {
        string yol = WindowsYolu.Birlestir(_kok, ad);
        Directory.CreateDirectory(WindowsYolu.Klasor(yol));
        File.WriteAllText(yol, icerik);
        return yol;
    }

    [Fact]
    public void IlkVersiyon_SIFIRDIR_ve_icerik_birebir()
    {
        // Erkan'in 1. beklentisi: mevcut dosya v0 sayilir.
        string yol = DosyaKoy("Parca1.SLDPRT", "ilk hal");

        IslemRaporu rapor = Surumler.Olustur(_kok, yol, "ilk", out int no);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal(0, no);
        Assert.Equal("ilk hal", File.ReadAllText(rapor.YeniYol!));
    }

    [Fact]
    public void IkinciVersiyon_BIRDIR_ve_liste_yeniden_eskiye()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "ilk hal");
        Surumler.Olustur(_kok, yol, "ilk", out _);

        File.WriteAllText(yol, "ikinci hal");
        Surumler.Olustur(_kok, yol, "ikinci", out int no);

        Assert.Equal(1, no);

        SurumDurumu durum = Surumler.Listele(_kok, yol);
        Assert.True(durum.Guvenilir);
        Assert.Equal(2, durum.Ogeler.Count);
        Assert.Equal(1, durum.Ogeler[0].No);      // en yeni basta
        Assert.Equal("ikinci", durum.Ogeler[0].Not);
        Assert.Equal(0, durum.Ogeler[1].No);
    }

    [Fact]
    public void Don_ICERIGI_GERI_GETIRIR_ve_once_bugunu_arsivler()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "eski hal");
        Surumler.Olustur(_kok, yol, "ilk", out _);
        File.WriteAllText(yol, "bugunku hal");

        IslemRaporu rapor = Surumler.Don(_kok, yol, 0);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal("eski hal", File.ReadAllText(yol));

        // DONUS DE BIR VERSIYONDUR: "bugunku hal" kaybolmamali.
        SurumDurumu durum = Surumler.Listele(_kok, yol);
        Assert.Equal(2, durum.Ogeler.Count);
        Assert.Equal("bugunku hal", File.ReadAllText(durum.Ogeler[0].ArsivYolu));
        Assert.Contains("dönmeden önce", durum.Ogeler[0].Not, StringComparison.Ordinal);
    }

    [Fact]
    public void Don_KILITLI_dosyada_reddedilir_ve_dosyaya_dokunmaz()
    {
        // SOLIDWORKS acik belge icin ~$ kilidi yazar (CLAUDE.md 5).
        string yol = DosyaKoy("Parca1.SLDPRT", "eski");
        Surumler.Olustur(_kok, yol, "", out _);
        File.WriteAllText(yol, "acik ve degismis");
        File.WriteAllText(Kilit.KilidininYolu(yol), "");

        IslemRaporu rapor = Surumler.Don(_kok, yol, 0);

        Assert.False(rapor.Oldu);
        Assert.Equal(IslemSonucu.Kilitli, rapor.Sonuc);
        Assert.Equal("acik ve degismis", File.ReadAllText(yol));
    }

    [Fact]
    public void OlmayanVersiyona_don_SEBEBIYLE_reddedilir()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "", out _);

        IslemRaporu rapor = Surumler.Don(_kok, yol, 7);

        Assert.False(rapor.Oldu);
        Assert.Contains("v7", rapor.Sebebi, StringComparison.Ordinal);
    }

    [Fact]
    public void BozukKayitSatiri_ATLANIR_ve_SAYILIR()
    {
        // CLAUDE.md 3: bozuk satiri sessizce yutmak, kullaniciya "o versiyon
        // hic olmadi" dedirtir. Atlanir ama SAYISI soylenir.
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "saglam", out _);

        string kayit = WindowsYolu.Birlestir(
            WindowsYolu.Klasor(Surumler.Listele(_kok, yol).Ogeler[0].ArsivYolu), "kayit.txt");
        File.AppendAllText(kayit, "bu satir bozuk\n");

        SurumDurumu durum = Surumler.Listele(_kok, yol);

        Assert.True(durum.Guvenilir);
        Assert.Single(durum.Ogeler);
        Assert.Equal(1, durum.BozukSatir);
    }

    [Fact]
    public void ArsivKopyasi_SALT_OKUNUR_dogar()
    {
        // Kullanici versiyonu cift tikla ACIYOR; salt-okunur olmazsa
        // SOLIDWORKS'te kaza ile gecmisin ustune kaydedilebilir (CLAUDE.md 1a).
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");

        IslemRaporu rapor = Surumler.Olustur(_kok, yol, "", out _);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.True(
            (File.GetAttributes(rapor.YeniYol!) & FileAttributes.ReadOnly) != 0,
            "arsiv kopyasi salt-okunur degil");
    }

    [Fact]
    public void Don_saltOkunurArsivden_calisir_ve_CANLI_dosya_YAZILABILIR_kalir()
    {
        // File.Copy OZNITELIGI DE kopyalar; temizlenmezse donusten sonra
        // canli dosya salt-okunur kalir ve SOLIDWORKS kaydedemez olurdu.
        string yol = DosyaKoy("Parca1.SLDPRT", "eski");
        Surumler.Olustur(_kok, yol, "", out _);
        File.WriteAllText(yol, "yeni");

        IslemRaporu rapor = Surumler.Don(_kok, yol, 0);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal("eski", File.ReadAllText(yol));
        Assert.True(
            (File.GetAttributes(yol) & FileAttributes.ReadOnly) == 0,
            "donusten sonra canli dosya salt-okunur kaldi");

        File.WriteAllText(yol, "yazilabilir mi");   // atilabilmeli
    }

    [Fact]
    public void Don_KAYITLA_DOSYASI_AYRISMIS_versiyona_yine_doner_ve_SOYLER()
    {
        // ERKAN'DA OLCULDU (31.08.2026): v4'un arsiv dosyasi 68197 bayt,
        // kaydi 62729 diyordu; donus her denemede reddediliyordu. Dogru
        // davranis: kopya KAYNAGA gore dogrulanir, kayit farki GIZLENMEDEN
        // soylenir ve donus yapilir - kullanici tikali kalmaz.
        string yol = DosyaKoy("Parca1.SLDPRT", "eski hal");
        Surumler.Olustur(_kok, yol, "", out _);

        // arsiv dosyasini kaydindan AYRISTIR (buyut)
        string arsiv = Surumler.Listele(_kok, yol).Ogeler[0].ArsivYolu;
        File.SetAttributes(arsiv, FileAttributes.Normal);
        File.WriteAllText(arsiv, "eski hal ama BUYUMUS");

        File.WriteAllText(yol, "bugunku hal");
        IslemRaporu rapor = Surumler.Don(_kok, yol, 0);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal("eski hal ama BUYUMUS", File.ReadAllText(yol));
        Assert.Contains("kayıt", rapor.Sebep, StringComparison.Ordinal);
    }

    [Fact]
    public void AyniNodan_iki_satir_TEKLESIR_ve_bozuk_sayilir()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "ilk", out _);

        // kayit.txt'e ayni No'dan IKINCI bir satir (gecmis carpisma taklidi)
        string yuva = WindowsYolu.Klasor(Surumler.Listele(_kok, yol).Ogeler[0].ArsivYolu);
        File.AppendAllText(
            WindowsYolu.Birlestir(yuva, "kayit.txt"),
            "0\t2026-08-31T10:00:00.0000000\t999\tikinci satir\n");

        SurumDurumu durum = Surumler.Listele(_kok, yol);

        Assert.Single(durum.Ogeler);                       // teklesti
        Assert.Equal("ikinci satir", durum.Ogeler[0].Not); // EN SON satir esas
        Assert.Equal(1, durum.BozukSatir);                 // oncekini sakladigi soyleniyor
    }

    [Fact]
    public void DosyasiKayip_EN_YENI_kaydin_numarasi_CALINMAZ()
    {
        // Numara "gosterilebilenlerin en buyugu"nden turetilseydi, dosyasi
        // kayip v1'in numarasi yeniden dagitilir ve ayni No'dan iki satir
        // dogardi - Erkan'daki boyut uyusmazliginin uretici mekanizmasi.
        string yol = DosyaKoy("Parca1.SLDPRT", "a");
        Surumler.Olustur(_kok, yol, "", out _);
        File.WriteAllText(yol, "b");
        Surumler.Olustur(_kok, yol, "", out int no1);
        Assert.Equal(1, no1);

        // v1'in dosyasini kaybet
        string arsiv1 = Surumler.Listele(_kok, yol).Ogeler[0].ArsivYolu;
        File.SetAttributes(arsiv1, FileAttributes.Normal);
        File.Delete(arsiv1);

        Surumler.Olustur(_kok, yol, "", out int yeniNo);

        Assert.Equal(2, yeniNo);   // 1 DEGIL - kayip kaydin numarasi sayildi
    }

    [Fact]
    public void AyniIcerikle_don_denemesi_guard_YIGMAZ()
    {
        // ERKAN'DA OLCULDU: uc basarisiz deneme uc "donmeden once" kopyasi
        // yigmisti (v5/v6/v7 ayni icerik). Bugunku hal zaten son versiyonda
        // arsivliyken yeniden arsivlemek hicbir seyi korumaz.
        string yol = DosyaKoy("Parca1.SLDPRT", "eski");
        Surumler.Olustur(_kok, yol, "", out _);
        File.WriteAllText(yol, "yeni");
        Surumler.Olustur(_kok, yol, "", out _);   // v1 = "yeni" (canliyla ayni)

        IslemRaporu rapor = Surumler.Don(_kok, yol, 0);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal("eski", File.ReadAllText(yol));

        // v2 ACILMADI: v0, v1 duruyor, yenisi yok.
        SurumDurumu durum = Surumler.Listele(_kok, yol);
        Assert.Equal(2, durum.Ogeler.Count);
        Assert.Contains("zaten v1'da arşivli", rapor.Sebep, StringComparison.Ordinal);
    }

    [Fact]
    public void AyniAdliPRTveDRW_ayri_yuvalarda_CARPISMAZ()
    {
        string prt = DosyaKoy("X.SLDPRT", "parca");
        string drw = DosyaKoy("X.SLDDRW", "resim");

        Surumler.Olustur(_kok, prt, "", out _);
        Surumler.Olustur(_kok, drw, "", out _);

        Assert.Equal(
            "parca", File.ReadAllText(Surumler.Listele(_kok, prt).Ogeler[0].ArsivYolu));
        Assert.Equal(
            "resim", File.ReadAllText(Surumler.Listele(_kok, drw).Ogeler[0].ArsivYolu));
    }

    [Fact]
    public void AltKlasordekiDosyanin_yuvasi_goreli_yolu_izler()
    {
        string yol = DosyaKoy("33/derin/Parca9.SLDPRT", "derin hal");

        IslemRaporu rapor = Surumler.Olustur(_kok, yol, "", out _);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Contains(
            Surumler.KlasorAdi, rapor.YeniYol, StringComparison.Ordinal);
        Assert.Equal("derin hal", File.ReadAllText(rapor.YeniYol!));
    }

    [Fact]
    public void KokDisindakiDosya_versiyonlanmaz_SEBEBIYLE()
    {
        string dis = Path.Combine(
            Path.GetTempPath(), "swpdm-dis-" + Guid.NewGuid().ToString("N")[..8] + ".SLDPRT");
        File.WriteAllText(dis, "disarida");

        try
        {
            IslemRaporu rapor = Surumler.Olustur(_kok, dis, "", out _);
            Assert.False(rapor.Oldu);
            Assert.Contains("Kök dışı", rapor.Sebebi, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dis);
        }
    }

    [Fact]
    public void ArsivKlasoru_TARAYICIDA_GORUNMEZ()
    {
        // Gorunse kullanici arsivi dosya sanir, tasir/siler ve versiyonlar
        // sessizce olur (CLAUDE.md 3). Cop klasoruyle ayni dislama.
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "", out _);

        KlasorIcerigi icerik = KlasorTarayici.Tara(_kok);

        Assert.All(
            icerik.Klasorler,
            k => Assert.NotEqual(Surumler.KlasorAdi, k.Ad));
    }

    [Fact]
    public void ArsivKlasoru_INDEKS_taramasina_girmez()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "hal");
        Surumler.Olustur(_kok, yol, "", out _);
        File.WriteAllText(yol, "yeni hal");
        Surumler.Olustur(_kok, yol, "", out _);

        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks, default, (_, _, _) => { });

        // Arsivdeki v0/v1 kopyalari taransaydi sayi 3 olurdu.
        Assert.Equal(1, indeks.DosyaSayisi);
    }

    [Fact]
    public void Sil_ARSIV_KOPYASINI_VE_KAYDI_SILER_dosyaya_dokunmaz()
    {
        // Erkan, 31.08.2026: "versiyon silme ve not düzenlemeyi ekle".
        string yol = DosyaKoy("Parca1.SLDPRT", "v0 hali");
        Surumler.Olustur(_kok, yol, "ilk", out int _);
        File.WriteAllText(yol, "v1 hali");
        Surumler.Olustur(_kok, yol, "ikinci", out int _);

        string v0 = Surumler.Listele(_kok, yol).Ogeler[^1].ArsivYolu;

        IslemRaporu rapor = Surumler.Sil(_kok, yol, 0);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.False(File.Exists(v0));                      // kopya gitti
        Assert.Equal("v1 hali", File.ReadAllText(yol));     // CANLI DOSYA yerinde

        SurumDurumu durum = Surumler.Listele(_kok, yol);
        Assert.Single(durum.Ogeler);
        Assert.Equal(1, durum.Ogeler[0].No);
        Assert.Equal(0, durum.BozukSatir);                  // kayit satiri da gitti
    }

    [Fact]
    public void Sil_OLMAYAN_NUMARADA_HICBIR_SEYE_dokunmaz()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "tek hal");
        Surumler.Olustur(_kok, yol, "ilk", out int _);

        IslemRaporu rapor = Surumler.Sil(_kok, yol, 7);

        Assert.False(rapor.Oldu);
        Assert.Contains("v7", rapor.Sebebi, StringComparison.Ordinal);
        Assert.Single(Surumler.Listele(_kok, yol).Ogeler);
    }

    [Fact]
    public void Sil_EN_YENIYI_silince_numara_YENIDEN_kullanilir()
    {
        // BILINCLI DAVRANIS (Surumler.Bakim.cs'te yazili): bos numara
        // birakmak "bir versiyon kayip mi" dedirtiyor; silinenin dosyasi
        // zaten yok. Kayit dosyasindan hem satir hem dosya gittigi icin
        // EnBuyukNo dusuyor - yeni kopya carpisacak bir dosya bulmuyor.
        string yol = DosyaKoy("Parca1.SLDPRT", "a");
        Surumler.Olustur(_kok, yol, "", out int _);
        File.WriteAllText(yol, "b");
        Surumler.Olustur(_kok, yol, "", out int ikinci);
        Assert.Equal(1, ikinci);

        Assert.True(Surumler.Sil(_kok, yol, 1).Oldu);

        File.WriteAllText(yol, "c");
        IslemRaporu rapor = Surumler.Olustur(_kok, yol, "yeni", out int no);

        Assert.True(rapor.Oldu, rapor.Sebebi);
        Assert.Equal(1, no);
        Assert.Equal("c", File.ReadAllText(rapor.YeniYol!));
    }

    [Fact]
    public void NotDegistir_YALNIZ_NOTU_yazar_otekilere_dokunmaz()
    {
        string yol = DosyaKoy("Parca1.SLDPRT", "a");
        Surumler.Olustur(_kok, yol, "eski not", out int _);
        File.WriteAllText(yol, "b");
        Surumler.Olustur(_kok, yol, "oteki", out int _);

        SurumKaydi onceki = Surumler.Listele(_kok, yol).Ogeler[^1];

        IslemRaporu rapor = Surumler.NotDegistir(_kok, yol, 0, "yeni not");

        Assert.True(rapor.Oldu, rapor.Sebebi);

        SurumDurumu durum = Surumler.Listele(_kok, yol);
        SurumKaydi sonraki = durum.Ogeler[^1];

        Assert.Equal("yeni not", sonraki.Not);
        Assert.Equal(onceki.No, sonraki.No);
        Assert.Equal(onceki.Zaman, sonraki.Zaman);       // olcum alanlari
        Assert.Equal(onceki.Boyut, sonraki.Boyut);       // AYNEN kaliyor
        Assert.Equal("a", File.ReadAllText(sonraki.ArsivYolu));
        Assert.Equal("oteki", durum.Ogeler[0].Not);      // komsu kayit yerinde
        Assert.Equal(0, durum.BozukSatir);
    }

    [Fact]
    public void NotDegistir_SEKME_ve_SATIR_SONU_kaydi_BOZAMAZ()
    {
        // Kayit sekmeyle ayrilmis duz metin; nota kacan bir sekme sonraki
        // okumada alanlari kaydirirdi (sessiz bozulma).
        string yol = DosyaKoy("Parca1.SLDPRT", "a");
        Surumler.Olustur(_kok, yol, "", out int _);

        Assert.True(Surumler.NotDegistir(_kok, yol, 0, "iki\tsatir\nnot").Oldu);

        SurumDurumu durum = Surumler.Listele(_kok, yol);
        Assert.Single(durum.Ogeler);
        Assert.Equal(0, durum.BozukSatir);
        Assert.Equal("iki satir not", durum.Ogeler[0].Not);
    }

    [Fact]
    public void Sil_KOK_DISINDA_reddeder()
    {
        string disarisi = Path.Combine(Path.GetTempPath(), "swpdm-disari.SLDPRT");
        File.WriteAllText(disarisi, "x");
        try
        {
            IslemRaporu rapor = Surumler.Sil(_kok, disarisi, 0);
            Assert.False(rapor.Oldu);
        }
        finally
        {
            File.Delete(disarisi);
        }
    }

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
