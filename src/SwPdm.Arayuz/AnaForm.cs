using System;
using System.ComponentModel;
using System.Diagnostics;
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

    /// <summary>
    /// Tusa her basista aramaya baslamak ag surucusunu bogar. Bu gecikme,
    /// yazma duruncaya kadar bekletir - kullaniciya "anlik" hissettirir ama
    /// diske her harfte gitmez.
    /// </summary>
    private const int AramaGecikmesiMs = 350;

    private readonly AgacDoldurucu _doldurucu;
    private readonly string? _acilistaAcilacakKok;
    private readonly System.Windows.Forms.Timer _aramaGecikmesi = new() { Interval = AramaGecikmesiMs };
    private CancellationTokenSource? _aramaIptali;

    /// <summary>Arama kutusunu KOD degistirdiginde arama tetiklenmesin diye.</summary>
    private bool _araKutusunuKodDegistiriyor;

    internal AnaForm(string? acilistaAcilacakKok = null)
    {
        TasarimiKur();
        _acilistaAcilacakKok = acilistaAcilacakKok;

        _doldurucu = new AgacDoldurucu(_agac);
        _doldurucu.Durum += (_, cumle) => _durumSag.Text = cumle;

        _acDugmesi.ButtonClick += (_, _) => KlasorSec();
        _agac.AfterSelect += (_, e) => SecimiGoster(e.Node);
        _agac.NodeMouseDoubleClick += (_, e) => OgeyiAc(e.Node);
        _suzgecler.SecimDegisti += (_, tur) => _doldurucu.TurSuzgeci = tur;

        // Anlik arama: yazarken suzuluyor, Enter beklemiyor.
        _araKutusu.TextChanged += AramaMetniDegisti;
        _araKutusu.KeyDown += AramaTusu;
        _aramaGecikmesi.Tick += (_, _) =>
        {
            _aramaGecikmesi.Stop();
            AramayiBaslat(_araKutusu.Text);
        };

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
        _aramaGecikmesi.Stop();
        _aramaGecikmesi.Dispose();
        _aramaIptali?.Cancel();
        _aramaIptali?.Dispose();
        base.OnFormClosed(e);
    }

    // ------------------------------------------------------------- acma

    /// <summary>
    /// Dosyayi Windows'un varsayilan uygulamasiyla acar - Gezgin'de cift
    /// tiklamakla ayni. Klasorlere DOKUNMAZ: orada agacin kendi ac/kapa
    /// davranisi dogru olan.
    ///
    /// CalismaKlasoru BILEREK verilmiyor: cocuk surec bir klasoru calisma
    /// klasoru yaparsa o klasor bir daha silinemez (CLAUDE.md 5'te SOLIDWORKS
    /// icin olculmus tuzagin ta kendisi) ve bu bir dosya yoneticisi icin
    /// dogrudan zarar olurdu.
    /// </summary>
    private void OgeyiAc(TreeNode? dugum)
    {
        if (AgacDoldurucu.Etiket(dugum) is not DosyaOgesi dosya)
        {
            return;
        }

        try
        {
            using Process? surec = Process.Start(new ProcessStartInfo(dosya.Yol)
            {
                UseShellExecute = true,
            });

            _durumSag.Text = dosya.Ad + " açılıyor…";
        }
        catch (Exception hata) when (hata is Win32Exception or InvalidOperationException
                                         or FileNotFoundException or ObjectDisposedException)
        {
            // CLAUDE.md 3: her istek bir YANIT alir. Cift tiklayip hicbir sey
            // olmamasi, kullanicinin ikinci kez tiklamasina yol acar.
            string sebep = hata is Win32Exception
                ? hata.Message + "\n\nBu uzantı için kayıtlı bir uygulama olmayabilir."
                : hata.Message;

            // Durum cubugu KISA kalir - uzun hata metni cubugu tasiriyordu.
            // Ayrinti kutuda; sebep yine de EKRANDA (CLAUDE.md 3).
            _durumSag.Text = "Açılamadı — " + dosya.Ad;
            MessageBox.Show(
                this,
                dosya.Yol + "\n\n" + sebep,
                "Dosya açılamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
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
        _araKutusunuKodDegistiriyor = true;
        _araKutusu.Text = string.Empty;
        _araKutusunuKodDegistiriyor = false;
        _aramaGecikmesi.Stop();

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

    private void AramaMetniDegisti(object? gonderen, EventArgs e)
    {
        if (_araKutusunuKodDegistiriyor)
        {
            return;
        }

        // Kutu bosaltildiysa beklemeye gerek yok: hemen gezinmeye don.
        if (string.IsNullOrWhiteSpace(_araKutusu.Text))
        {
            _aramaGecikmesi.Stop();
            AramayiBaslat(string.Empty);
            return;
        }

        _aramaGecikmesi.Stop();
        _aramaGecikmesi.Start();
    }

    private void AramaTusu(object? gonderen, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;   // Windows'un uyari sesini bastirir
        _aramaGecikmesi.Stop();      // beklemeden, hemen
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
            _agac.Enabled = true;

            if (_doldurucu.AramaKipinde)
            {
                // Aramadan cikarken kullanici actigi dallari ACIK bulmali.
                _doldurucu.GezinmeyeDon();
            }

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
