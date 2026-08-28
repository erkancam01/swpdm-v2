using System;
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
    /// <summary>Ayni anda iki tarama kosmasin.</summary>
    private static bool _kosuyor;

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

        if (baglam.Referanslar.Indeks is null || _kosuyor)
        {
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
    internal static void Sonra(IslemBaglami baglam)
    {
        ArgumentNullException.ThrowIfNull(baglam);

        if (baglam.Referanslar.Indeks is null || _kosuyor)
        {
            return;
        }

        _kosuyor = true;
        Task.Run(() =>
        {
            baglam.Referanslar.Tara(CancellationToken.None, (_, _, _) => { });
            _kosuyor = false;
        });
    }

    private static void Kostur(IslemBaglami baglam, CancellationToken belirtec, Action devam)
    {
        TaramaSonucu? sonuc = baglam.Referanslar.Tara(
            belirtec, (yapilan, toplam, ad) => baglam.Ilerleme.Adim(yapilan, toplam, ad));

        baglam.Ilerleme.Bitti(() =>
        {
            _kosuyor = false;

            if (sonuc is not null && sonuc.Iptal)
            {
                baglam.Bildir("İşlem yapılmadı — tarama iptal edildi.");
                return;
            }

            devam();
        });
    }
}
