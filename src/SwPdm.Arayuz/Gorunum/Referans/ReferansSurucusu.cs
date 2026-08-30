using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Referans panelinin UC BOLUMU. Sira, kullanicinin seritte gordugu siradir
/// ve varsayilan ILKIDIR (Erkan, 30.08.2026: "varsayılan olarak en başta
/// içindekiler gelsin").
/// </summary>
internal enum ReferansBolumu
{
    /// <summary>Bu dosyanin ICINDEKILER - asagi yon.</summary>
    Icindekiler,

    /// <summary>Bu dosyayi KIM KULLANIYOR - yukari yon.</summary>
    KullanildigiYerler,

    /// <summary>Cozulemeyen ve bayat yollar - onarilacaklar.</summary>
    Kirik,
}

/// <summary>
/// Bolumlerin ADI TEK YERDE. Serit dugmelerini bundan URETIYOR; elle yazilmis
/// ikinci bir liste yok (CLAUDE.md 1b'nin 2. kurali - iki listenin sirasi
/// kayarsa hata SESSIZDIR, yanlis basligin altinda dogru liste cizilir).
/// </summary>
internal static class ReferansBolumleri
{
    /// <summary>Seritte gorunen sirayla.</summary>
    internal static ReferansBolumu[] Tumu => Enum.GetValues<ReferansBolumu>();

    /// <summary>Seritte yazan ad.</summary>
    internal static string Adi(ReferansBolumu bolum) => bolum switch
    {
        ReferansBolumu.Icindekiler => "İÇİNDEKİLER",
        ReferansBolumu.KullanildigiYerler => "KULLANILDIĞI YERLER",
        _ => "KIRIK",
    };
}

/// <summary>
/// REFERANS BILGISININ ARAYUZDEKI TEK KAPISI.
///
/// Indeksi tutar, diskten yukler, diske yazar, sorgular ve sag alt
/// listeyi doldurur. "Referanslar arayuzde nasil gorunur" sorusunun
/// cevabinin TAMAMI burada (CLAUDE.md 1b).
///
/// IKI YON, IKI BOLUM (Erkan, 28.08.2026):
///   ASAGI  = bu dosyanin ICINDEKILER (montaj -> alt montaj/parca;
///            teknik resim -> modeli)
///   YUKARI = bu dosyayi KIM KULLANIYOR (parca -> montajlar, teknik resimler)
/// Once bu ayrimi yalnizca rol sutunundaki bir OK isareti yapiyordu
/// ("kullanıyor →" ile "kullanıyor") ve okunmuyordu. Ayni ad iki bolumde
/// birden cikabiliyor - bu HATA DEGIL: montaj baglaminda (in-context)
/// yapilmis bir parca montaji referans verir, montaj da o parcayi. Yani
/// iliski gercekten cift yonlu ve duz bir liste bunu okunamaz yapiyordu.
///
/// Ayrim UC isaretle birden yapiliyor - baslik, satirin rol kelimesi ve
/// satirin rengi. Sebep: liste kayinca BASLIK GORUNMEZ OLUR; o an satirin
/// yonunu anlatan tek sey kendi rengi ve kelimesi kalir.
///
/// EN SERT KURAL (CLAUDE.md 3): taranmamis bir kokte "0 kullanan" YAZILMAZ.
/// Sayi yerine "taranmadı" yazilir. Bos bir liste "bu parcayi kimse
/// kullanmiyor" demek DEGILDIR ve o yanlis, kullaniciya saglam dosya
/// sildirir. Bu yuzden iki bolumun de kendi bosluk cumlesi var: "kullandigi
/// yok" ile "kullanani yok" ayni satirla anlatilamaz.
/// </summary>
internal sealed partial class ReferansSurucusu
{
    /// <summary>Referans tasimayan bir dosyada (PDF, resim...) yazilan.</summary>
    private const string Ilgisiz = "—";

    /// <summary>Diskte degistigi BILINEN ama indekse islenmemis dosyalar.</summary>
    private readonly HashSet<string> _kirli = new(StringComparer.OrdinalIgnoreCase);

    private ReferansIndeksi? _indeks;

    /// <summary>Hedefli tazeleme YETMEZ; butun kok taranmali.</summary>
    private bool _tamGerekli = true;

    /// <summary>Su anki kokun indeksi; kok acilmadiysa null.</summary>
    internal ReferansIndeksi? Indeks => _indeks;

    /// <summary>Indeks taranmis ve tam mi.</summary>
    internal bool Hazir => _indeks is { TaramaZamani: not null, Tam: true };

    /// <summary>
    /// Diski izleyen bir sey var ve saglam mi.
    ///
    /// NEDEN ONEMLI: islem oncesi TAM tarama ancak bu dogruysa atlanabilir -
    /// yoksa disarida yapilan bir degisiklikten haberimiz olmaz ve onarim
    /// bayat indeksle calisir. Tam olarak bu hata bir kez yasandi
    /// (CLAUDE.md 11: "tasimada onarim sessizce atlaniyordu").
    /// </summary>
    internal bool IzlemeGuvenilir { get; set; }

    /// <summary>Bekleyen kirli dosya sayisi.</summary>
    internal int KirliSayisi => _kirli.Count;

    /// <summary>Hedefli tazeleme yetmiyor; tam tarama gerekiyor.</summary>
    internal bool TamGerekli => _tamGerekli;

    /// <summary>Indeks diske yazilamadiysa sebebi; yazildiysa null.</summary>
    internal string? SonYazmaHatasi { get; private set; }

    /// <summary>Kok degisti: o kokun indeksi diskten yuklenir.</summary>
    internal void KokuKur(string? kok)
    {
        _indeks = string.IsNullOrWhiteSpace(kok) ? null : IndeksDosyasi.Oku(kok);
        _kirli.Clear();

        // Yeni kokte disarida ne olup bittigini BILMIYORUZ.
        _tamGerekli = true;
        SonYazmaHatasi = null;
    }

    /// <summary>
    /// Diskte degisen bir yolu isaretler (izleyiciden gelir).
    ///
    /// SW DOSYASI DEGILSE ya da KLASORSE hedefli tazeleme yetmez: bir klasor
    /// adi degisince altindaki butun dosyalarin yolu degisir. O durumda
    /// "tam tarama gerekli" deniyor - tahmin edip yarim tazelemek, indekse
    /// yalan yazmak olurdu (CLAUDE.md 3).
    /// </summary>
    internal void Kirlet(IEnumerable<string>? yollar)
    {
        if (_indeks is null || yollar is null)
        {
            return;
        }

        foreach (string yol in yollar)
        {
            if (string.IsNullOrWhiteSpace(yol) || !SwReferans.TasiyabilirMi(yol))
            {
                _tamGerekli = true;
                continue;
            }

            _kirli.Add(yol);
        }
    }

    /// <summary>
    /// Kirli dosyalari indekse isler - butun kok taranmadan. Cagiran once
    /// <see cref="KirliSayisi"/>'na bakip bunun ucuz olduguna karar vermeli.
    /// </summary>
    internal void KirlileriIsle()
    {
        if (_indeks is null || _kirli.Count == 0)
        {
            return;
        }

        foreach (string yol in _kirli)
        {
            IndeksTarama.Tazele(_indeks, yol);
        }

        _kirli.Clear();
    }

    /// <summary>Taramayi kosturur (ARKA PLANDA cagrilmali) ve sonucu diske yazar.</summary>
    internal TaramaSonucu? Tara(CancellationToken belirtec, Action<int, int, string> ilerleme)
    {
        ReferansIndeksi? indeks = _indeks;
        if (indeks is null)
        {
            return null;
        }

        TaramaSonucu sonuc = IndeksTarama.Tara(indeks, belirtec, ilerleme);

        // Tam tarama kirli listeyi kapsar; iptal edilse bile gezilen agac
        // buydu, kalan belirsizlik zaten "Tam degil" olarak tasiniyor.
        _kirli.Clear();
        _tamGerekli = sonuc.Iptal;

        // DISKE YAZIM: yalnizca DEGISTIYSE. Onceki hal her taramada ~2,5 MB'lik
        // dosyayi bastan yaziyordu - hicbir sey degismese bile.
        if (indeks.Degisti)
        {
            if (IndeksDosyasi.Yaz(indeks))
            {
                indeks.YazildiIsaretle();
                SonYazmaHatasi = null;
            }
            else
            {
                // CLAUDE.md 3: "Yaz" sebebini DONDURUYORDU ve cagiran onu
                // ATIYORDU. Yazilamayan indeks sessizce kaybolur, sonraki
                // acilista her sey yeniden taranir ve kullanici NEDEN
                // oldugunu hicbir yerde goremezdi.
                SonYazmaHatasi = "indeks diske yazılamadı";
            }
        }

        return sonuc;
    }

    /// <summary>
    /// Belli dosyalarin indeks kaydini TAZELER - butun koku taramadan.
    ///
    /// NEDEN VAR: ad degistirme onarimindan sonra indeks eski adi bilmeye
    /// devam eder ve referans paneli YALAN soyler (artik olmayan bir dosyayi
    /// "kullaniyor" diye gosterir). Dokunulan birkac dosya buradan
    /// tazeleniyor; tarama diske geri donmuyor.
    /// </summary>
    internal void Tazele(IEnumerable<string>? yollar)
    {
        if (_indeks is null || yollar is null)
        {
            return;
        }

        foreach (string yol in yollar)
        {
            IndeksTarama.Tazele(_indeks, yol);
        }
    }

    /// <summary>
    /// KULLANILDIGI YERLER sekmesinin sayisi.
    ///
    /// "· EKSIK" KALDIRILDI (Erkan, 30.08.2026: "eksik yazmasın"). O kelime
    /// KIRIK bolumuyle ilgili DEGILDI - taramanin yarim kaldigini, yani bu
    /// dosyayi kullanan baska bir belgenin GORULEMEMIS olabilecegini
    /// soyluyordu. Erkan'a bu soylendi ve karari "tamamen kaldır" oldu.
    ///
    /// TEHLIKELI HAL YINE DE KORUNUYOR (CLAUDE.md 3): liste BOSKEN ve tarama
    /// guvenilir DEGILKEN sayi "yok" DEMIYOR, "taranmadı" diyor - ve bolumun
    /// icindeki sebep satiri da duruyor. Bos bir listeye "bunu kimse
    /// kullanmiyor" dedirtmek saglam dosya sildirir; kalkan yalnizca DOLU
    /// listedeki kelime, ayrinti durum cubugundaki tarama cumlesinde
    /// ("EKSİK — 15 dosya okunamadı").
    /// </summary>
    private static string YukariMetni(KullanimSonucu sonuc)
    {
        if (sonuc.Kullananlar.Count > 0)
        {
            return $"{sonuc.Kullananlar.Count} dosya";
        }

        return sonuc.Guvenilir ? "yok" : "taranmadı";
    }

    /// <summary>
    /// Su an gosterilen bolum. Seritten geliyor; degisince cagiran
    /// <see cref="Doldur"/>'u yeniden cagirir.
    ///
    /// YAPISKAN (oturum boyu): dosyadan dosyaya gecerken sifirlanmiyor -
    /// "kirik referanslari gez" gibi bir isi her dosyada yeniden tiklamak
    /// gerekmesin. Acilista ILK bolum secili gelir.
    /// </summary>
    internal ReferansBolumu Bolum { get; set; } = ReferansBolumu.Icindekiler;

    /// <summary>
    /// Bir bolumun SERITTE yazacak sayisi.
    ///
    /// SAYI HER BOLUMDE GORUNMEK ZORUNDA (CLAUDE.md 3): sekmeli duzende
    /// yalnizca bir bolum aciktir ve otekilerin durumu gorunmezse panel
    /// SESSIZCE eksik konusur - "kullanan yok mu, taranmadi mi" sorusu
    /// sekme degistirmeden cevapsiz kalirdi.
    ///
    /// Metinler bugunku ureticilerden geliyor; ikinci kopya yok (CLAUDE.md 8).
    /// </summary>
    internal string Sayi(ReferansBolumu bolum, string? yol)
    {
        if (_indeks is null || string.IsNullOrWhiteSpace(yol) || !SwReferans.TasiyabilirMi(yol))
        {
            return string.Empty;   // panel zaten sebebini yaziyor
        }

        return bolum switch
        {
            ReferansBolumu.Icindekiler => IcindekilerMetni(yol),
            ReferansBolumu.KullanildigiYerler => YukariMetni(_indeks.Kullananlar(yol)),
            _ => KirikMetni(yol),
        };
    }

    /// <summary>
    /// Sag alt listeyi doldurur - YALNIZCA acik olan bolumu. Hangi bolumun
    /// acik oldugunu serit soyluyor; bolum basligi satiri YOK, cunku serit
    /// her zaman ekranda ve liste kaydirilinca kaybolmuyor.
    ///
    /// Her bolum BOSSA kendi sebebini tasir - bos bir liste tek basina
    /// hicbir sey iddia etmemeli (CLAUDE.md 3).
    /// </summary>
    internal void Doldur(ReferansListesi liste, string? yol)
    {
        ArgumentNullException.ThrowIfNull(liste);

        liste.BeginUpdate();
        try
        {
            liste.Items.Clear();

            // SEBEPSIZ BOSALMA YOK (CLAUDE.md 3). Burasi eskiden duz "return"
            // ediyordu: klasor secilince, coklu secimde ve SOLIDWORKS
            // olmayan bir dosyada panel tek kelime etmeden bosaliyordu -
            // oysa ayni dosyadaki diger butun bos haller ("taranmadı",
            // "kullanan yok") cumleyle aciklaniyor. Bos panel, kullaniciya
            // "bu dosyayi kimse kullanmiyor" diye okunabilir.
            if (string.IsNullOrWhiteSpace(yol))
            {
                liste.Ekle("Seçim yok", "—", 0, Renkler.UstBilgiYazi);
                return;
            }

            if (!SwReferans.TasiyabilirMi(yol))
            {
                liste.Ekle(
                    WindowsYolu.DosyaAdi(yol), "SOLIDWORKS dosyası değil", 0,
                    Renkler.UstBilgiYazi,
                    tamMetin: "Referanslar yalnızca .SLDPRT, .SLDASM ve .SLDDRW "
                        + "dosyalarında okunur.");
                return;
            }

            if (_indeks is null)
            {
                liste.Ekle(
                    WindowsYolu.DosyaAdi(yol), "taranmadı", 0, Renkler.UstBilgiYazi,
                    tamMetin: "Referans taraması yapılmadı — Ctrl+Shift+R.");
                return;
            }

            switch (Bolum)
            {
                case ReferansBolumu.KullanildigiYerler:
                    Yukariyi(liste, yol);
                    break;

                case ReferansBolumu.Kirik:
                    Kiriklari(liste, yol);
                    break;

                default:
                    Asagiyi(liste, yol);
                    break;
            }
        }
        finally
        {
            liste.EndUpdate();
        }
    }

    /// <summary>
    /// Bir dosyayi kullananlarin yollari. Guvenilirlik burada DUSMEZ:
    /// cagiran once <see cref="Hazir"/>'a bakmali, yoksa bos liste
    /// "kullanan yok" diye okunur (CLAUDE.md 3).
    /// </summary>
    internal IReadOnlyList<string> Kullananlarin(string yol)
        => _indeks is null ? [] : _indeks.Kullananlar(yol).Kullananlar;
}
