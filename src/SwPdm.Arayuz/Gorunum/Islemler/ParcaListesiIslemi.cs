using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// PARCA LISTESI (BOM) ISLEMI - secilen belgenin butun agacini tabloya
/// cikarir.
///
/// ARKA PLANDA ve IPTAL EDILEBILIR: liste, agactaki HER dosyayi acip
/// ozelliklerini okuyor. Yuzlerce parcali bir montajda bu saniyeler surer;
/// arayuz is parcaciginda yapilirsa uygulama DONAR ve kullanici onu cokmus
/// sanar (CLAUDE.md 6'da olculdu).
///
/// KISAYOL SART (CLAUDE.md 11): Wine'da hicbir acilir menu acilamiyor, yani
/// menuye baglanan bir ozellik burada OLCULEMEZ. Ctrl+Shift+M ayni kodu
/// cagiriyor ve kapi onu olcuyor.
/// </summary>
internal sealed class ParcaListesiIslemi : IAgacIslemi
{
    private static bool _kosuyor;

    /// <inheritdoc/>
    public string Ad => "Parça listesi (BOM)";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.M;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (_kosuyor)
        {
            nedenOlmaz = "Bir parça listesi zaten çıkarılıyor.";
            return false;
        }

        if (secim.TekOge is not DosyaOgesi dosya)
        {
            nedenOlmaz = "Tek bir SOLIDWORKS dosyası seçin.";
            return false;
        }

        if (DosyaTurleri.Tani(dosya.Ad) == DosyaTuru.Bilinmeyen
            || DosyaTurleri.Tani(dosya.Ad) == DosyaTuru.Pdf)
        {
            nedenOlmaz = "Parça listesi yalnızca SOLIDWORKS dosyalarından çıkarılır.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        if (baglam.Secim.TekOge is not DosyaOgesi dosya)
        {
            return;
        }

        _kosuyor = true;
        var iptal = new CancellationTokenSource();

        // TOPLAM AGAC YURUNMEDEN BILINMIYOR (CLAUDE.md 3: uydurma yuzde yok).
        // Cubuk 1 ile basliyor ve gercek sayi bulununca kendini duzeltiyor -
        // ilk adimda "1/1" gorunmesi, "%40" uydurmaktan durust.
        baglam.Ilerleme.Basladi(1, iptal);

        Task.Run(() => Cikar(baglam, dosya, iptal.Token))
            .ContinueWith(
                _ =>
                {
                    _kosuyor = false;
                    iptal.Dispose();
                },
                TaskScheduler.Default);
    }

    private static void Cikar(IslemBaglami baglam, DosyaOgesi dosya, CancellationToken belirtec)
    {
        ParcaListesiSonucu sonuc = ParcaListesi.Uret(
            dosya.Yol,
            belirtec,
            (yapilan, toplam, ad) => baglam.Ilerleme.Adim(yapilan, toplam, ad));

        if (!baglam.Ilerleme.Bitti(() => Goster(baglam, dosya, sonuc)))
        {
            // Pencere kapandi: gosterecek kimse yok. SESSIZ ASKIDA KALMA
            // OLMASIN diye bayrak burada dusuyor - yoksa "bir liste zaten
            // cikariliyor" mesaji uygulamanin sonuna kadar kalirdi.
            _kosuyor = false;
        }
    }

    private static void Goster(IslemBaglami baglam, DosyaOgesi dosya, ParcaListesiSonucu sonuc)
    {
        if (sonuc.Satirlar.Count == 0)
        {
            // BOS LISTE "AGAC BOS" DEMEK DEGIL (CLAUDE.md 3): sebebi yazilir,
            // pencere hic acilmaz.
            baglam.Bildir("Parça listesi çıkarılamadı: " + (sonuc.Sebep ?? "sebep bilinmiyor"));
            return;
        }

        baglam.Bildir(
            $"Parça listesi: {sonuc.Satirlar.Count} satır"
            + (sonuc.Sorunlu > 0 ? $" · {sonuc.Sorunlu} eksik" : string.Empty));

        ParcaListesiPenceresi.Ac(baglam.Sahip, dosya.Ad, sonuc, baglam.Bildir, yol => baglam.Tazele(yol));
    }
}
