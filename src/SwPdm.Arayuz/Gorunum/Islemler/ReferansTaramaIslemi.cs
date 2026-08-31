using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// REFERANS TARAMASI: koku gezip "kim kimi kullaniyor" indeksini kurar.
///
/// ISTEK UZERINE, kendiliginden DEGIL. Ilk tarama ag surucusunde dakikalar
/// surebilir; her acilista kendiliginden baslatmak uygulamayi kullanilmaz
/// yapardi. Sonrasi ARTIMLI: boyutu ve tarihi degismeyen dosya bir daha
/// ACILMIYOR, yani ikinci tarama yalnizca degisenler kadar suruyor.
///
/// SONUC OLCULEREK YAZILIYOR: "1240 dosya · 38 okundu · 1202 değişmemiş ·
/// 12,4 sn". Ag surucusundeki gercek maliyet tahmin edilmiyor, sayiliyor -
/// boylece "bu is uzun surer mi" sorusunun cevabi kullanicinin kendi
/// makinesinden geliyor.
/// </summary>
internal sealed class ReferansTaramaIslemi : IAgacIslemi
{
    private static bool _kosuyor;

    /// <inheritdoc/>
    public string Ad => "Referansları tara";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.R;

    /// <inheritdoc/>
    public bool Yazar => false;   // okur; kilitli klasor de TARANIR - taranmazsa panel "kimse kullanmiyor" der ve saglam dosya sildirir (CLAUDE.md 3)

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (_kosuyor)
        {
            nedenOlmaz = "Bir tarama zaten sürüyor.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(secim.Kok))
        {
            nedenOlmaz = "Önce bir klasör açın.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        if (baglam.Referanslar.Indeks is null)
        {
            baglam.Bildir("Önce bir klasör açın.");
            return;
        }

        _kosuyor = true;
        var iptal = new CancellationTokenSource();

        // Toplam once BILINMIYOR (agac gezilmeden dosya sayisi bilinemez).
        // Ilk Adim cagrisi gercek toplami getiriyor; uydurma yuzde yok.
        baglam.Ilerleme.Basladi(1, iptal);

        Task.Run(() => Tara(baglam, iptal.Token))
            .ContinueWith(
                _ =>
                {
                    _kosuyor = false;
                    iptal.Dispose();
                },
                TaskScheduler.Default);
    }

    private static void Tara(IslemBaglami baglam, CancellationToken belirtec)
    {
        TaramaSonucu? sonuc = baglam.Referanslar.Tara(
            belirtec,
            (yapilan, toplam, ad) => baglam.Ilerleme.Adim(yapilan, toplam, ad));

        baglam.Ilerleme.Bitti(() =>
        {
            if (sonuc is null)
            {
                baglam.Bildir("Tarama yapılamadı: açık bir klasör yok.");
                return;
            }

            // ONCE tazele, SONRA bildir. Tersi denendi ve OLCULDU: tazeleme
            // secimi yeniden gosteriyor, o da durum cubuguna dosya bilgisini
            // yaziyor ve tarama sonucunun UZERINE biniyordu. Kullanicinin
            // gormesi gereken sey tam da o satir - "kac dosya, kac saniye" -
            // cunku ag surucusundeki gercek maliyeti ancak oradan ogreniyor.
            baglam.Tazele(null);
            baglam.Bildir("Referans taraması — " + sonuc.Yaz());
        });
    }
}
