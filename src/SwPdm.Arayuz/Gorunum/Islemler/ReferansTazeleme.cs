using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// HER ISLEMIN ONCESINDE VE SONRASINDA REFERANS TARAMASI (Erkan, 28.08.2026).
///
/// NEDEN VAR - GERCEK BIR HATA: bir parca tasindi, indeks guncel degildi,
/// "kimin kullandigi" bilinmedigi icin onarim SESSIZCE yapilmadi ve
/// SOLIDWORKS dosyayi acamadi. Islem oncesi tarama bunu imkansiz kiliyor.
///
/// ONCESI BEKLETIR, SONRASI BEKLETMEZ - ve bu ayrim bilincli:
///   ONCE  islem BUNA BAGLI. Kim kullaniyor bilinmeden ne onay kutusundaki
///         sayi dogru olur ne de onarim yapilabilir. O yuzden beklenir;
///         ilerleme cubugunda gorunur ve IPTAL edilebilir. Iptal edilirse
///         ISLEM DE YAPILMAZ - yarim bilgiyle dosyaya dokunmaktansa hic
///         dokunmamak dogru (CLAUDE.md 1a).
///   SONRA hicbir sey buna bagli degil; arka planda kosar ve bitince paneli
///         tazeler. Her adlandirmadan sonra kullaniciyi bekletmek gereksiz.
///
/// MALIYET: tarama ARTIMLI - boyutu ve tarihi degismeyen dosya ACILMIYOR.
/// Degisen yoksa is yalnizca agaci gezmek. Gercek maliyet ag surucusunde
/// olculur ve uygulama kendi hizini durum cubuguna YAZIYOR (CLAUDE.md 2:
/// tahmin etme, olc) - yavassa Erkan sayiyi gorur.
/// </summary>
internal static class ReferansTazeleme
{
    /// <summary>
    /// Bu kadar kirli dosyaya kadar hedefli tazeleme yapilir; ustunde tam
    /// tarama daha ucuz VE daha guvenli (tam tarama arka planda, iptal
    /// edilebilir ve ilerleme gosteriyor; hedefli tazeleme arayuzde koşuyor).
    /// </summary>
    private const int EnFazlaKirli = 50;

    /// <summary>Ayni anda iki tarama kosmasin.</summary>
    private static volatile bool _kosuyor;

    /// <summary>
    /// Once TARAR, sonra <paramref name="devam"/>'i ARAYUZ parcaciginda
    /// kosturur. Tarama iptal edilirse devam CAGRILMAZ.
    ///
    /// Kok acik degilse ya da bir tarama zaten kosuyorsa dogrudan devam eder;
    /// beklemenin bir karsiligi olmazdi.
    /// </summary>
    internal static void Once(IslemBaglami baglam, Action devam)
    {
        ArgumentNullException.ThrowIfNull(baglam);
        ArgumentNullException.ThrowIfNull(devam);

        ReferansSurucusu referanslar = baglam.Referanslar;
        if (referanslar.Indeks is null || _kosuyor)
        {
            devam();
            return;
        }

        // ============ TAM TARAMA NE ZAMAN GEREKLI ============
        //
        // OLCULDU (28.08.2026): degismeyen 5000 dosyalik bir agacta tam
        // tarama, dosya acmasa bile klasor basina iki dizin listesi ve dosya
        // basina bir metadata sorgusu odetiyor. Bunu HER islemden once
        // odemek, ag surucusunde islemi baslatmadan once uzun bir bekleme
        // demek.
        //
        // Atlamanin SARTI var ve sart dur ust: diskte olup biteni izleyen
        // saglam bir izleyici olmali. Yoksa disarida yapilan bir degisiklik
        // gorunmez kalir ve onarim BAYAT indeksle calisir - bu hata bir kez
        // yasandi (CLAUDE.md 11).
        if (!Gerekli(referanslar))
        {
            referanslar.KirlileriIsle();
            devam();
            return;
        }

        _kosuyor = true;
        var iptal = new CancellationTokenSource();

        // Toplam ONCE bilinmiyor: agac gezilmeden dosya sayisi bilinemez.
        // Ilk Adim cagrisi gercek toplami getiriyor; uydurma yuzde yok.
        baglam.Ilerleme.Basladi(1, iptal);

        Task.Run(() => Kostur(baglam, iptal.Token, devam))
            .ContinueWith(_ => iptal.Dispose(), TaskScheduler.Default);
    }

    /// <summary>
    /// Islem bitti: arka planda tarar. BEKLETMEZ ve SESSIZDIR.
    ///
    /// NEDEN SESSIZ: durum cubugunda islemin kendi sonucu yaziyor
    /// ("2 dosya onarıldı"). Buradan bir sey yazmak ya da agaci tazelemek
    /// onun UZERINE binerdi - kullanicinin gormesi gereken tam da o satir.
    /// Ayni tuzak once yasandi (CLAUDE.md 11: "once tazele, sonra bildir").
    /// Burada iddia edilen bir sey yok; yalnizca indeks guncelleniyor.
    /// </summary>
    internal static void Sonra(IslemBaglami baglam, IEnumerable<string>? dokunulanlar = null)
    {
        ArgumentNullException.ThrowIfNull(baglam);

        if (baglam.Referanslar.Indeks is null)
        {
            return;
        }

        // ISLEM HANGI DOSYALARA DOKUNDUGUNU BILIYORSA tam tarama gereksiz:
        // yalnizca onlar tazelenir. Bu, "islemden sonra da tara" kuralini
        // BOZMAZ - indeks yine islem biter bitmez dogru hale geliyor,
        // yalnizca bedeli dokunulan dosya kadar.
        if (dokunulanlar is not null && YalnizDosya(dokunulanlar, out List<string> dosyalar))
        {
            baglam.Referanslar.Tazele(dosyalar);
            return;
        }

        if (_kosuyor)
        {
            return;
        }

        _kosuyor = true;
        Task.Run(() =>
        {
            try
            {
                baglam.Referanslar.Tara(CancellationToken.None, (_, _, _) => { });
            }
            finally
            {
                _kosuyor = false;
            }
        });
    }

    /// <summary>
    /// Verilen yollarin HEPSI referans tasiyabilen birer DOSYA mi.
    ///
    /// KLASOR VARSA HEDEFLI TAZELEME YETMEZ: bir klasor tasinip silinince
    /// altindaki butun dosyalarin yolu degisir; yalnizca klasor yolunu
    /// tazelemek indekste ESKI yollari birakirdi - yani indekse yalan
    /// yazardik (CLAUDE.md 3). O durumda tam tarama.
    /// </summary>
    private static bool YalnizDosya(IEnumerable<string> yollar, out List<string> dosyalar)
    {
        dosyalar = [];
        foreach (string yol in yollar)
        {
            if (!SwReferans.TasiyabilirMi(yol))
            {
                return false;
            }

            dosyalar.Add(yol);
        }

        return dosyalar.Count > 0;
    }

    /// <summary>
    /// Tam tarama gerekli mi. Gerekmiyorsa yalnizca kirli dosyalar tazelenir.
    /// </summary>
    private static bool Gerekli(ReferansSurucusu referanslar)
        => referanslar.Indeks!.TaramaZamani is null   // hic taranmadi
        || !referanslar.IzlemeGuvenilir               // disariyi goremiyoruz
        || referanslar.TamGerekli                     // klasor degisti vb.
        || referanslar.KirliSayisi > EnFazlaKirli;    // hedefli tazeleme pahalilasti

    private static void Kostur(IslemBaglami baglam, CancellationToken belirtec, Action devam)
    {
        TaramaSonucu? sonuc;
        try
        {
            sonuc = baglam.Referanslar.Tara(
                belirtec, (yapilan, toplam, ad) => baglam.Ilerleme.Adim(yapilan, toplam, ad));
        }
        catch
        {
            // BAYRAK HER HALUKARDA DUSER. Onceki hal yalnizca arayuz geri
            // cagrisinda sifirliyordu; buraya bir istisna gelirse (ya da
            // pencere kapanmissa) bayrak KALICI true kaliyor ve bundan
            // sonraki butun ON TARAMALAR SESSIZCE atlaniyordu - CLAUDE.md 3'un
            // "sessiz askida kalma"si.
            _kosuyor = false;
            throw;
        }

        bool devredildi = baglam.Ilerleme.Bitti(() =>
        {
            _kosuyor = false;

            if (sonuc is not null && sonuc.Iptal)
            {
                baglam.Bildir("İşlem yapılmadı — tarama iptal edildi.");
                return;
            }

            if (baglam.Referanslar.SonYazmaHatasi is string hata)
            {
                baglam.Bildir("Uyarı: " + hata + " — bir sonraki açılışta yeniden taranacak.");
            }

            devam();
        });

        if (!devredildi)
        {
            _kosuyor = false;   // pencere kapandi; kimse sifirlamayacak
        }
    }
}
