using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SwPdm.Arayuz.Gorunum;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz;

/// <summary>
/// Uygulamanin tek penceresi.
///
/// CLAUDE.md 7 - v1'in en pahali dersi: bir arayuz sinifi hem ekran hem is akisi
/// surucusu OLMAZ. v1'de tek bir arayuz sinifi 9.918 satira, urun kodunun
/// %38'ine cikti ve bolunemedi. Bu sinif yalnizca BAGLAR: dugmeleri
/// <see cref="AgacDoldurucu"/> ve <see cref="KlasorTarayici"/> ile birlestirir.
/// Tarama ve arama mantigi cekirdekte; agaci doldurmak AgacDoldurucu'da.
/// </summary>
internal sealed partial class AnaForm : Form
{
    /// <summary>Aramada en fazla kac eslesme toplanir. Asilirsa SOYLENIR, sessizce kirpilmaz.</summary>
    private const int AramaSiniri = 2000;

    private readonly AgacDoldurucu _doldurucu;
    private readonly string? _acilistaAcilacakKok;
    private CancellationTokenSource? _aramaIptali;

    internal AnaForm(string? acilistaAcilacakKok = null)
    {
        TasarimiKur();
        _acilistaAcilacakKok = acilistaAcilacakKok;

        _doldurucu = new AgacDoldurucu(_agac);
        _doldurucu.Durum += (_, cumle) => _durumSag.Text = cumle;

        _acDugmesi.ButtonClick += (_, _) => KlasorSec();
        _agac.AfterSelect += (_, e) => SecimiGoster(e.Node);
        _suzgecler.SecimDegisti += (_, tur) => _doldurucu.TurSuzgeci = tur;
        _araKutusu.KeyDown += AramaTusu;

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                e.SuppressKeyPress = true;
                KlasorSec();
            }
        };

        _onizleme.Temizle();
        _durumSol.Text = "Klasör seçilmedi.";
        _durumSag.Text = string.Empty;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Bolenler ancak denetimin gercek boyutu olustuktan sonra ayarlanabilir.
        BoleniAyarla(_dikeyBolen, 320);
        BoleniAyarla(_altBolen, 282);

        if (!string.IsNullOrWhiteSpace(_acilistaAcilacakKok))
        {
            KokuAc(_acilistaAcilacakKok);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _aramaIptali?.Cancel();
        _aramaIptali?.Dispose();
        base.OnFormClosed(e);
    }

    // --------------------------------------------------------------- klasor

    private void KlasorSec()
    {
        // CLAUDE.md 4 - OLCULMUS TUZAK: kabuk dosya iletisim kutulari surecin
        // CALISMA KLASORUNU kaydiriyor ve o klasor bir daha silinemiyor.
        // Bir dosya yoneticisinde bu gercek bir hata: kullanicinin tasidigi
        // klasor "kullanimda" diye silinmez olur. Kutudan once yaziyoruz,
        // kapandiktan sonra geri koyuyoruz.
        string oncekiCalismaKlasoru = Directory.GetCurrentDirectory();

        try
        {
            using var kutu = new FolderBrowserDialog
            {
                Description = "Çalışılacak kök klasörü seçin",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            };

            if (_doldurucu.Kok is not null)
            {
                kutu.SelectedPath = _doldurucu.Kok;
            }

            if (kutu.ShowDialog(this) == DialogResult.OK)
            {
                KokuAc(kutu.SelectedPath);
            }
        }
        finally
        {
            GeriKoy(oncekiCalismaKlasoru);
        }
    }

    private static void GeriKoy(string klasor)
    {
        try
        {
            Directory.SetCurrentDirectory(klasor);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                         or DirectoryNotFoundException)
        {
            // Eski klasor artik yoksa yapacak bir sey yok; uygulamayi dusurmez.
        }
    }

    private void KokuAc(string yol)
    {
        _araKutusu.Text = string.Empty;
        _doldurucu.KokuAc(yol);
        _onizleme.Temizle();
        _durumSol.Text = yol;
        SonKoklereEkle(yol);
    }

    private void SonKoklereEkle(string yol)
    {
        // Yalnizca BU OTURUM icin. Diske yazilmiyor - kalici ayar, Ayarlar
        // adiminin isi (Erkan: "hayir").
        foreach (ToolStripItem oge in _acDugmesi.DropDownItems)
        {
            if (string.Equals(oge.Text, yol, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var girdi = new ToolStripMenuItem(yol);
        girdi.Click += (_, _) => KokuAc(yol);
        _acDugmesi.DropDownItems.Insert(0, girdi);
    }

    // --------------------------------------------------------------- secim

    private void SecimiGoster(TreeNode? dugum)
    {
        switch (AgacDoldurucu.Etiket(dugum))
        {
            case DosyaOgesi dosya:
                _onizleme.UstBilgiyiYaz(
                    ad: dosya.Ad,
                    tur: DosyaTurleri.Adi(dosya.Tur),
                    boyut: Boyut.Yaz(dosya.Boyut),
                    degistirme: dosya.Degistirme.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture),

                    // CLAUDE.md 3'un EN SERT kurali burada. Referans indeksi YOK.
                    // "0" yazmak "bu parcayi kimse kullanmiyor" demektir ve
                    // v1'de tam bu SAGLAM DOSYA SILDIRIYORDU. Bilmiyorsak
                    // bilmedigimizi yaziyoruz.
                    kullanan: "taranmadı");

                _durumSol.Text = string.Join("  ·  ",
                    dosya.Ad,
                    Boyut.Yaz(dosya.Boyut),
                    dosya.Degistirme.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture));
                break;

            case KlasorOgesi klasor:
                _onizleme.UstBilgiyiYaz(
                    ad: klasor.Ad,
                    tur: "Klasör",
                    boyut: "—",
                    degistirme: "—",
                    kullanan: "taranmadı");

                _durumSol.Text = klasor.Hata is null ? klasor.Yol : klasor.Yol + "  ·  " + klasor.Hata;
                break;

            default:
                _onizleme.Temizle();
                break;
        }
    }

    // --------------------------------------------------------------- arama

    private void AramaTusu(object? gonderen, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;   // Windows'un uyari sesini bastirir
        AramayiBaslat(_araKutusu.Text);
    }

    private void AramayiBaslat(string metin)
    {
        string? kok = _doldurucu.Kok;
        if (kok is null)
        {
            _durumSag.Text = "Önce bir klasör açın.";
            return;
        }

        // Yeniden giris kilidi: onceki arama HER ZAMAN once iptal edilir
        // (CLAUDE.md 6). Aksi halde iki arama ayni agaca yazar.
        _aramaIptali?.Cancel();
        _aramaIptali?.Dispose();

        if (string.IsNullOrWhiteSpace(metin))
        {
            _aramaIptali = null;
            _doldurucu.Yenile();     // gezinme kipine don
            return;
        }

        var iptal = new CancellationTokenSource();
        _aramaIptali = iptal;
        CancellationToken belirtec = iptal.Token;

        _durumSag.Text = "Aranıyor…";
        _agac.Enabled = false;

        Task.Run(() => KlasorTarayici.Ara(
                kok, metin, AramaSiniri, belirtec,
                (klasor, eslesme) => Ilerleme(belirtec, klasor, eslesme)),
            belirtec)
            .ContinueWith(is_ => Bitti(is_, metin, belirtec), TaskScheduler.Default);
    }

    private void Ilerleme(CancellationToken belirtec, int taranan, int eslesme)
    {
        // Her klasorde mesaj yollamak arayuzu bogar; ellide bir yeter.
        // CLAUDE.md 3: uydurma yuzde YOK - sayilabilen sey sayiliyor.
        if (taranan % 50 != 0)
        {
            return;
        }

        ArayuzeYolla(belirtec, () => _durumSag.Text = $"Aranıyor… {taranan} klasör · {eslesme} eşleşme");
    }

    private void Bitti(Task<AramaSonucu> is_, string metin, CancellationToken belirtec)
    {
        ArayuzeYolla(belirtec, () =>
        {
            _agac.Enabled = true;

            if (is_.IsFaulted)
            {
                // Sessiz basarisizlik YASAK (CLAUDE.md 3).
                _durumSag.Text = "Arama başarısız: " + (is_.Exception?.GetBaseException().Message ?? "bilinmeyen sebep");
                return;
            }

            if (is_.IsCanceled)
            {
                return;
            }

            _doldurucu.AramaSonucunuGoster(metin, is_.Result);
        });
    }

    /// <summary>
    /// Arayuz is parcacigina gecer. Pencere kapandiysa ya da arama iptal
    /// edildiyse hicbir sey yapmaz - kapanmis pencereye yazmak coker.
    /// </summary>
    private void ArayuzeYolla(CancellationToken belirtec, Action is_)
    {
        if (belirtec.IsCancellationRequested || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(is_);
        }
        catch (ObjectDisposedException)
        {
            // Pencere tam bu sirada kapandi.
        }
        catch (InvalidOperationException)
        {
            // Tutamak yok edilmis.
        }
    }

    /// <summary>
    /// SplitterDistance araligin disinda kalirsa istisna atar. Sinira kirpiyoruz:
    /// pencere kucukken acilmak, acilmamaktan iyidir.
    /// </summary>
    private static void BoleniAyarla(SplitContainer bolen, int hedef)
    {
        int uzunluk = bolen.Orientation == Orientation.Horizontal ? bolen.Height : bolen.Width;
        int enBuyuk = uzunluk - bolen.SplitterWidth - bolen.Panel2MinSize;
        int enKucuk = bolen.Panel1MinSize;

        if (enBuyuk < enKucuk)
        {
            return;
        }

        bolen.SplitterDistance = Math.Clamp(hedef, enKucuk, enBuyuk);
    }
}
