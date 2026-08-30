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
internal sealed record SecimBaglami(
    IReadOnlyList<object> Ogeler,
    string? EtkinKlasor,
    bool AramaKipinde,
    string? Kok,
    string? CopKlasoru)
{
    /// <summary>
    /// Baglami KURALIYLA kurar. "Etkin klasor" kurali BURADA yasar
    /// (CLAUDE.md 1b) - once AnaForm'daydi ve tipin kendi kurali baska
    /// dosyada duruyordu: secili klasor kazanir; klasor yoksa secili
    /// dosyanin klasoru; o da yoksa kok.
    /// </summary>
    internal static SecimBaglami Kur(
        IReadOnlyList<object> ogeler, string? kok, bool aramaKipinde, string? copKlasoru)
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

        return new SecimBaglami(ogeler, etkin ?? kok, aramaKipinde, kok, copKlasoru);
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
    /// REFERANS PANELINDEN calistirildiginda hedef, tiklanan SATIR mi yoksa
    /// satirin SAHIBI mi (panelin o an gosterdigi, agacta secili dosya).
    ///
    /// Varsayilan SATIR: paneldeki satirlar da gercek dosyalar (Erkan,
    /// 30.08.2026: "sonuçta ordakilerde parça"). Varsayilan govdesi oldugu
    /// icin oteki islem dosyalari bu uye yuzunden DEGISMEZ (CLAUDE.md 1b).
    ///
    /// true diyen tek islem <see cref="ElleBaglaIslemi"/>: onun isi satirin
    /// YAZILI yolunu duzeltmektir ve o yol sahibin ICINDE yazar. Hedefi
    /// satir yapmak, ozelligi asil kullanildigi yerde (cozulememis
    /// "BULUNAMADI" satirinda - orada satirin dosyasi yoktur) oldururdu.
    /// </summary>
    bool SahibineUygulanir => false;

    /// <summary>
    /// Bu secimde uygulanabilir mi. Uygulanamiyorsa <paramref name="nedenOlmaz"/>
    /// EKRANDA gosterilecek bir cumle doner - oge GIZLENMEZ, gri durur ve
    /// sebebini soyler (CLAUDE.md 3).
    /// </summary>
    bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz);

    /// <summary>Islemi yapar.</summary>
    void Uygula(IslemBaglami baglam);
}
