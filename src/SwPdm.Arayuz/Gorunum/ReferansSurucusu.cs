using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>Onizleme panelindeki iki referans satiri - ikisi de METIN, sayi degil.</summary>
/// <param name="Kullandigi">Bu dosyanin ICINDEKILER icin ozet ("9 dosya").</param>
/// <param name="Kullanan">Bu dosyayi KULLANANLAR icin ozet ("taranmadı").</param>
internal readonly record struct ReferansOzeti(string Kullandigi, string Kullanan);

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
internal sealed class ReferansSurucusu
{
    private const string AsagiBaslik = "▼ KULLANDIKLARI";
    private const string YukariBaslik = "▲ KULLANANLAR";

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
    /// Onizleme panelindeki iki satir: "Kullandığı:" ve "Kullanan:".
    ///
    /// IKISI AYRI SATIR cunku iki ayri soru ve iki ayri guvenilirlik.
    /// Once yalnizca "Kullanan:" yaziyordu; kullanicinin gordugu tek sayi
    /// ters yondekiydi ve asagi yon hic gorunmuyordu.
    /// </summary>
    internal ReferansOzeti Ozet(string? yol) => new(KullandigiMetni(yol), KullananMetni(yol));

    /// <summary>
    /// "Kullandığı:" satiri - ASAGI yon.
    ///
    /// Uc ayri hal, UCU DE farkli yazilir:
    ///   indekste yok  -> taranmadı   (bilmiyoruz)
    ///   okunamadi     -> okunamadı   (denedik, olmadi)
    ///   okundu, 0     -> yok         (gercekten kullanmiyor)
    /// </summary>
    internal string KullandigiMetni(string? yol)
    {
        if (_indeks is null || string.IsNullOrWhiteSpace(yol))
        {
            return "taranmadı";
        }

        if (!SwReferans.TasiyabilirMi(yol))
        {
            return Ilgisiz;   // bu tur zaten referans tasimaz; "taranmadı" yaniltirdi
        }

        IndeksKaydi? kayit = _indeks.Kayit(yol);
        if (kayit is null)
        {
            return "taranmadı";
        }

        if (!kayit.Okundu)
        {
            return "okunamadı";
        }

        // ASAGI YON, YARIM TARAMADA DA TAMDIR: bu sayi dosyanin KENDI
        // icinden (Header2) okundu; baska dosyalarin taranip taranmamasi
        // onu degistirmez. Yukari yonun guvenilirligi ise butun agaca bagli -
        // ikisinin ayri satir olmasinin sebebi tam olarak bu.
        return kayit.YazilanYollar.Count == 0 ? "yok" : $"{kayit.YazilanYollar.Count} dosya";
    }

    /// <summary>
    /// "Kullanan:" satiri - YUKARI yon.
    ///
    /// Uc ayri hal, UCU DE farkli yazilir - "0" hepsini ayni gostermek olurdu:
    ///   taranmadi        -> bilmiyoruz
    ///   taranmis, 0      -> gercekten kullanan yok
    ///   taranmis, n      -> n dosya
    /// </summary>
    internal string KullananMetni(string? yol)
    {
        if (_indeks is null || string.IsNullOrWhiteSpace(yol))
        {
            return "taranmadı";
        }

        if (!SwReferans.TasiyabilirMi(yol))
        {
            return Ilgisiz;
        }

        return YukariMetni(_indeks.Kullananlar(yol), kisa: false);
    }

    /// <summary>
    /// Yukari yonun metni. TEK YERDE duruyor cunku iki musterisi var: panelin
    /// "Kullanan:" satiri (uzun) ve bolum basligi (kisa). Iki kopya yazilsa
    /// biri gunun birinde otekinden FARKLI sayi gosterirdi - v1'de boyut
    /// bicimlendirmesi tam boyle ayrismisti (CLAUDE.md 8).
    /// </summary>
    /// <param name="kisa">Baslikta yer dar; uzun guvenilirlik cumlesi sigmaz.</param>
    private static string YukariMetni(KullanimSonucu sonuc, bool kisa)
    {
        if (!sonuc.Guvenilir)
        {
            if (sonuc.Kullananlar.Count == 0)
            {
                return "taranmadı";
            }

            return kisa
                ? $"{sonuc.Kullananlar.Count} dosya · eksik"
                : $"{sonuc.Kullananlar.Count} dosya (liste eksik olabilir)";
        }

        return sonuc.Kullananlar.Count == 0 ? "yok" : $"{sonuc.Kullananlar.Count} dosya";
    }

    /// <summary>
    /// Sag alt listeyi doldurur: once ASAGI bolumu (kullandiklari), sonra
    /// YUKARI bolumu (kullananlar). Her bolum kendi basligini, kendi sayisini
    /// ve BOSSA kendi sebebini tasir - bos bir liste tek basina hicbir sey
    /// iddia etmemeli (CLAUDE.md 3).
    /// </summary>
    internal void Doldur(ReferansListesi liste, string? yol)
    {
        ArgumentNullException.ThrowIfNull(liste);

        liste.BeginUpdate();
        try
        {
            liste.Items.Clear();

            if (_indeks is null || string.IsNullOrWhiteSpace(yol) || !SwReferans.TasiyabilirMi(yol))
            {
                return;
            }

            Asagiyi(liste, yol);
            Yukariyi(liste, yol);
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

    /// <summary>ASAGI bolumu: bu dosyanin ICINDEKILER.</summary>
    private void Asagiyi(ReferansListesi liste, string yol)
    {
        liste.Baslik(AsagiBaslik, KullandigiMetni(yol));

        IndeksKaydi? kayit = _indeks!.Kayit(yol);
        if (kayit is null)
        {
            Aciklama(liste, "Bu kök henüz taranmadı.", "Ctrl+Shift+R", Renkler.ReferansAsagiYazi);
            return;
        }

        if (!kayit.Okundu)
        {
            Aciklama(
                liste, kayit.Sebep ?? "Dosyanın referansları okunamadı.", "hata",
                Renkler.ReferansAsagiYazi);
            return;
        }

        if (kayit.YazilanYollar.Count == 0)
        {
            Aciklama(
                liste, "Bu dosya başka dosya kullanmıyor.", Ilgisiz, Renkler.ReferansAsagiYazi);
            return;
        }

        foreach ((string yazilan, Cozum cozum) in _indeks.Kullandiklari(yol))
        {
            bool bayat = ReferansIndeksi.BayatMi(yol, yazilan, cozum);

            // IPUCUNDA HANGI YOL: bulunduysa dosyanin GERCEK yeri (kullanici
            // "hangi dosya" diye ona bakiyor), bulunamadiysa dosyanin ICINDE
            // yazan yol (aranan seyin ne oldugunu ancak o soyluyor).
            liste.Ekle(
                WindowsYolu.DosyaAdi(yazilan),
                bayat ? "yol BAYAT" : AsagiRol(cozum),
                Simge(yazilan),
                bayat ? Renkler.YolBayatYazi : Renkler.ReferansAsagiYazi,
                cozum.Durum == CozumDurumu.Bulundu ? cozum.Yol : null,
                cozum.Yol ?? yazilan);
        }
    }

    /// <summary>YUKARI bolumu: bu dosyayi KIM KULLANIYOR.</summary>
    private void Yukariyi(ReferansListesi liste, string yol)
    {
        KullanimSonucu sonuc = _indeks!.Kullananlar(yol);
        liste.Baslik(YukariBaslik, YukariMetni(sonuc, kisa: true));

        foreach (string kullanan in sonuc.Kullananlar)
        {
            liste.Ekle(
                WindowsYolu.DosyaAdi(kullanan), "kullanan", Simge(kullanan),
                Renkler.ReferansYukariYazi, kullanan, kullanan);
        }

        // GUVENILIR DEGILSE SEBEP HER ZAMAN YAZILIR - liste dolu olsa bile.
        // "5 dosya" gorup listeyi eksiksiz sanmak, eksik uyarmanin ta kendisi;
        // ve eksik uyarmak bu uygulamada dosya sildirir (CLAUDE.md 1a, 3).
        if (!sonuc.Guvenilir)
        {
            Aciklama(
                liste, sonuc.Sebep ?? "Liste eksik olabilir.", "eksik",
                Renkler.ReferansYukariYazi);
            return;
        }

        if (sonuc.Kullananlar.Count == 0)
        {
            Aciklama(liste, "Bunu kullanan dosya yok.", Ilgisiz, Renkler.ReferansYukariYazi);
        }
    }

    /// <summary>
    /// Bolumun bos ya da eksik olma SEBEBINI yazan satir.
    /// Simgesi YOK (-1): bir dosya satiri gibi gorunmemeli, cunku degil.
    /// </summary>
    private static void Aciklama(ReferansListesi liste, string cumle, string rol, Color yazi)
        => liste.Ekle(cumle, rol, -1, yazi, hedefYol: null, tamMetin: cumle);

    /// <summary>
    /// Asagi yondeki satirin rol kelimesi. BELIRSIZ olan SAKLANMAZ: tek bir
    /// cevap uydurmak yanlis dosyayi sildirir (CLAUDE.md 5).
    /// </summary>
    private static string AsagiRol(Cozum cozum) => cozum.Durum switch
    {
        CozumDurumu.Bulundu => "içinde",
        CozumDurumu.Belirsiz => $"içinde? {cozum.Adaylar.Count} aday",
        _ => "BULUNAMADI",
    };

    private static int Simge(string yol) => TurSimgeleri.Sira(DosyaTurleri.Tani(yol));
}
