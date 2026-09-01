using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Bir islemin uzerinde calisacagi secim. Dugumler DEGIL, cekirdek nesneleri
/// ve yollar tasinir - islemler agac denetimini bilmez.
/// </summary>
/// <param name="Ogeler">Secili ogeler (dosya ve/veya klasor).</param>
/// <param name="EtkinKlasor">
/// "Buraya" anlamina gelen klasor: secili klasor, yoksa secili dosyanin
/// klasoru, o da yoksa kok. Yeni klasor buraya acilir.
/// </param>
/// <param name="AramaKipinde">Agac su an arama sonucu mu gosteriyor.</param>
/// <param name="Kok">Acik olan kok klasor.</param>
/// <param name="CopKlasoru">
/// Silinenlerin gidecegi klasor. Kullanici ayarlardan degistirebiliyor;
/// nerede oldugu TEK yerde cozuluyor ki islemler ayari okumak zorunda
/// kalmasin (CLAUDE.md 8).
/// </param>
/// <param name="Kilitler">
/// Kilitli klasorlerin anlik hali. Baglam alani, ozellik degil: islemlerin
/// buradan OKUMASI kilidin icini bilmek degildir (CLAUDE.md 1b'nin
/// AramaKipinde/CopKlasoru icin verilmis karari). Kilit denetimi tek yerde
/// yapiliyor - bkz. <see cref="Kilitler.Engel"/>.
/// </param>
internal sealed record SecimBaglami(
    IReadOnlyList<object> Ogeler,
    string? EtkinKlasor,
    bool AramaKipinde,
    string? Kok,
    string? CopKlasoru,
    KilitKumesi? Kilitler = null)
{
    /// <summary>
    /// Baglami KURALIYLA kurar. "Etkin klasor" kurali BURADA yasar
    /// (CLAUDE.md 1b) - once AnaForm'daydi ve tipin kendi kurali baska
    /// dosyada duruyordu: secili klasor kazanir; klasor yoksa secili
    /// dosyanin klasoru; o da yoksa kok.
    /// </summary>
    internal static SecimBaglami Kur(
        IReadOnlyList<object> ogeler, string? kok, bool aramaKipinde, string? copKlasoru,
        KilitKumesi? kilitler = null)
    {
        string? etkin = null;
        foreach (object oge in ogeler)
        {
            etkin = oge switch
            {
                KlasorOgesi klasor => klasor.Yol,
                DosyaOgesi dosya => WindowsYolu.Klasor(dosya.Yol),
                _ => etkin,
            };

            if (oge is KlasorOgesi)
            {
                break;   // klasor secimi dosyanin klasorune tercih edilir
            }
        }

        return new SecimBaglami(ogeler, etkin ?? kok, aramaKipinde, kok, copKlasoru, kilitler);
    }

    /// <summary>Secili tek oge; birden fazlaysa null.</summary>
    internal object? TekOge => Ogeler.Count == 1 ? Ogeler[0] : null;

    /// <summary>Bir ogenin diskteki yolu.</summary>
    internal static string? Yolu(object? oge) => oge switch
    {
        DosyaOgesi dosya => dosya.Yol,
        KlasorOgesi klasor => klasor.Yol,
        _ => null,
    };

    /// <summary>Bir ogenin adi.</summary>
    internal static string Adi(object? oge) => oge switch
    {
        DosyaOgesi dosya => dosya.Ad,
        KlasorOgesi klasor => klasor.Ad,
        _ => "?",
    };
}

/// <summary>
/// Bir islemin dis dunyaya acilan yuzu: pencere sahibi ve "bitti, agaci
/// tazele" cagrisi. Islem, agacin nasil tazelendigini BILMEZ.
/// </summary>
/// <param name="Sahip">Iletisim kutularinin sahibi.</param>
/// <param name="Secim">Uzerinde calisilacak secim.</param>
/// <param name="Tazele">Islem bittiginde cagrilir; yol verilirse orasi secilir.</param>
/// <param name="Bildir">Durum cubuguna yazilacak cumle.</param>
/// <param name="Ilerleme">Uzun suren isin ilerlemeyi bildirdigi yuzey.</param>
/// <param name="AgaciKapat">Butun dallari kapatip koke doner.</param>
/// <param name="Referanslar">
/// Referans indeksi. ORTAK ARAC, ozellik degil (CLAUDE.md 1b): birden cok
/// islem ona soruyor - tarama, raporlar, ve silme/tasima oncesi uyari.
/// </param>
internal sealed record IslemBaglami(
    IWin32Window Sahip,
    SecimBaglami Secim,
    Action<string?> Tazele,
    Action<string> Bildir,
    IIlerlemeYuzeyi Ilerleme,
    Action AgaciKapat,
    ReferansSurucusu Referanslar);

/// <summary>
/// KILIT DENETIMI TEK YERDE (CLAUDE.md 1b/8).
///
/// Kilitli bir klasorde YAZAN islem calismaz. Denetim burada duruyor, her
/// islemin icinde degil: yoksa kilit ozelligi yirmi dosyaya satir ekletir ve
/// KALDIRILIRKEN yirmi dosyadan satir sildirirdi - biri unutulur, hata da
/// sessiz olurdu (bitmis is sessizce degisir).
/// </summary>
internal static class Kilitler
{
    /// <summary>
    /// Bu islem bu secimde kilit yuzunden engelli mi. Engelliyse EKRANDA
    /// gosterilecek sebep doner (CLAUDE.md 3).
    /// </summary>
    internal static bool Engel(IAgacIslemi islem, SecimBaglami secim, out string neden)
    {
        neden = string.Empty;
        if (islem is null || secim?.Kilitler is null || !islem.Yazar)
        {
            return false;
        }

        foreach (object oge in secim.Ogeler)
        {
            if (Kapali(secim, SecimBaglami.Yolu(oge), SecimBaglami.Adi(oge), out neden))
            {
                return true;
            }
        }

        // HEDEF DE SAYILIR: secim bos olsa bile "buraya yapistir" ya da
        // "yeni klasor" ETKIN KLASORE yaziyor.
        return Kapali(secim, secim.EtkinKlasor, WindowsYolu.DosyaAdi(secim.EtkinKlasor), out neden);
    }

    private static bool Kapali(SecimBaglami secim, string? yol, string ad, out string neden)
    {
        if (yol is not null && secim.Kilitler!.Kilitli(yol))
        {
            neden = $"\"{ad}\" kilitli — sağ tık ile kilidi kaldırın.";
            return true;
        }

        neden = string.Empty;
        return false;
    }
}

/// <summary>
/// Uzun suren islerin ilerlemeyi bildirdigi yuzey.
///
/// Islem IS PARCACIGI BILMEZ - arayuze gecmek uygulayanin isi. Boylece
/// islemler saf kalir ve ilerleme gosterimi tek bir dosyada degisir
/// (CLAUDE.md 1b).
/// </summary>
internal interface IIlerlemeYuzeyi
{
    /// <summary>Is basladi; toplam SAYILABILIR olmali (CLAUDE.md 3).</summary>
    void Basladi(int toplam, CancellationTokenSource iptal);

    /// <summary>Bir adim bitti.</summary>
    void Adim(int yapilan, int toplam, string ad);

    /// <summary>
    /// Is bitti; verilen is ARAYUZ parcaciginda kosar.
    /// </summary>
    /// <returns>
    /// Is kuyruga girdiyse true. false donerse pencere kapanmistir ve
    /// verilen is HIC CALISMAYACAKTIR - cagiran, o ise birakmis oldugu
    /// temizligi (bayrak dusurme gibi) kendisi yapmali (CLAUDE.md 3).
    /// </returns>
    bool Bitti(Action arayuzdeCalistir);

    /// <summary>
    /// Verilen isi ARAYUZ parcaciginda kosturur (is bitirmeden). Arka
    /// plandan pencere acmak icin - oradan dogrudan acmak coker.
    /// </summary>
    /// <returns>
    /// Is kuyruga girdiyse true. false donerse pencere kapanmistir ve
    /// cagiran CEVAP BEKLEMEMELIDIR (CLAUDE.md 3).
    /// </returns>
    bool Arayuzde(Action is_);
}

/// <summary>
/// REFERANS PANELINDEN calistirilan bir islem KIME uygulanir.
///
/// UC HAL VAR ve ucu de gercek bir ihtiyactan cikti - IKI BOOL degil ENUM,
/// cunku iki bool dort kombinasyon uretirdi ve ikisi anlamsiz olurdu
/// (CLAUDE.md 1b).
/// </summary>
internal enum IslemHedefi
{
    /// <summary>
    /// TIKLANAN SATIR. Varsayilan: paneldeki satirlar da gercek dosyalar
    /// (Erkan, 30.08.2026: "sonuçta ordakilerde parça"). Satir bir dosyaya
    /// cozulemiyorsa islem GRI durur ve sebebini soyler - "sessizce
    /// agactakine uygula" bu uygulamada saglam dosya sildirir (CLAUDE.md 3).
    /// </summary>
    Satir,

    /// <summary>
    /// SATIRIN SAHIBI (panelin o an gosterdigi, agacta secili dosya).
    /// Tek kullanani <see cref="ElleBaglaIslemi"/>: onun isi satirin YAZILI
    /// yolunu duzeltmektir ve o yol sahibin ICINDE yazar. Hedefi satir
    /// yapmak, ozelligi asil kullanildigi yerde (cozulememis "BULUNAMADI"
    /// satirinda - orada satirin dosyasi yoktur) oldururdu.
    /// </summary>
    Sahip,

    /// <summary>
    /// SATIR VARSA SATIR, YOKSA SAHIP.
    ///
    /// NEDEN VAR - ERKAN'DA GORULEN HATA (31.08.2026): "önizleme ekranında
    /// dosyaya sağ tıklayıp revizyon oluştur dediğimde SEÇTİĞİM PARÇAYA
    /// revizyon oluştursun." <see cref="SurumOlusturIslemi"/> Sahip diyordu,
    /// yani panelde bir parcaya sag tiklansa bile versiyon AGACTA SECILI
    /// montaja aciliyordu. Kullanici yanlis dosyayi versiyonluyor ve bunu
    /// ancak o versiyona donmek isteyince anliyordu (CLAUDE.md 3).
    ///
    /// Duz "Satir" de olmazdi: VERSIYONLAR sekmesindeki satir bir arsiv
    /// kopyasidir, dosyaya cozulmez ve Ctrl+Shift+U orada GRI kalirdi -
    /// bugun calisan bir sey bozulurdu (CLAUDE.md 1a).
    /// </summary>
    SatirYoksaSahip,
}

/// <summary>
/// BIR AGAC ISLEMI. CLAUDE.md 1b: her islem KENDI dosyasinda yasar; menu
/// listeden URETILIR. Bir islemi kaldirmak = dosyasini sil + AgacIslemleri
/// listesinden bir satir cikar.
/// </summary>
internal interface IAgacIslemi
{
    /// <summary>Menude gorunen yazi.</summary>
    string Ad { get; }

    /// <summary>Kisayol (menude sagda yazar). Yoksa <see cref="Keys.None"/>.</summary>
    Keys Kisayol { get; }

    /// <summary>
    /// Menude kisayolun YERINE yazilacak metin. Bos ise <see cref="Kisayol"/>
    /// yazilir.
    ///
    /// NEDEN VAR - CALISTIRMA KAPISI YAKALADI (31.08.2026): "Aç" islemi
    /// Kisayol olarak <see cref="Keys.Enter"/> diyordu; tek basina Enter
    /// GECERLI BIR MENU KISAYOLU DEGIL ve ToolStripMenuItem.ShortcutKeys'e
    /// yazilinca InvalidEnumArgumentException atiyor. Derleme "0 uyari 0
    /// hata" diyordu, uygulama HIC ACILMIYORDU (CLAUDE.md 11'in kendisi).
    ///
    /// Tusu bu islem KAYDETMEZ: Enter'i agacta AgacTuslari, panelde
    /// ReferansPaneliTuslari daha once yakaliyor. Burada yazilan yalnizca
    /// ETIKET - kullanici tusu ogrenmeye devam ediyor (CLAUDE.md 3: menude
    /// gorunmeyen ozellik yok sayilir).
    /// </summary>
    string KisayolYazisi => string.Empty;

    /// <summary>
    /// REFERANS PANELINDEN calistirildiginda islem KIME uygulanir.
    /// Varsayilan <see cref="IslemHedefi.Satir"/>; varsayilan govdesi oldugu
    /// icin oteki islem dosyalari bu uye yuzunden DEGISMEZ (CLAUDE.md 1b).
    /// </summary>
    IslemHedefi Hedef => IslemHedefi.Satir;

    /// <summary>
    /// Bu islem SECIMDEKI dosyalara/klasorlere YAZAR mi.
    ///
    /// VARSAYILAN true - GUVENLI TARAF (CLAUDE.md 1a): yarin yazilacak yeni
    /// bir islem bu uyeyi unutursa kilitli klasorde CALISMAZ; unutulan bir
    /// "false" ise bitmis isi bozardi. Yalnizca OKUYAN islemler (tarama,
    /// rapor, boyut, kopyala...) bunu false'a cevirir ve sebebini yazar.
    ///
    /// Kilit ozelligi bu uyeyi EKLEDI ama hicbir islem dosyasina satir
    /// ekletmedi (govdesi var); "false" diyenler kendi dogalari geregi
    /// diyor, kilit yuzunden degil (CLAUDE.md 1b).
    /// </summary>
    bool Yazar => true;

    /// <summary>
    /// Bu secimde uygulanabilir mi. Uygulanamiyorsa <paramref name="nedenOlmaz"/>
    /// EKRANDA gosterilecek bir cumle doner - oge GIZLENMEZ, gri durur ve
    /// sebebini soyler (CLAUDE.md 3).
    /// </summary>
    bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz);

    /// <summary>Islemi yapar.</summary>
    void Uygula(IslemBaglami baglam);
}
