using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    private readonly AgacDoldurucu _doldurucu;
    private readonly AramaSurucusu _arama;
    private readonly string? _acilistaAcilacakKok;
    private Onizleme? _onizleme;

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

        // Aramaya dair HER SEY AramaSurucusu'nda: ne zaman baslar, ne kadar
        // bekler, hangi is parcaciginda kosar, nasil iptal edilir. Bu sinif
        // yalnizca sonucu agaca baglar (CLAUDE.md 7).
        _arama = new AramaSurucusu(_araKutusu, this);
        _arama.Durum += (_, cumle) => _durumSag.Text = cumle;
        _arama.Mesgul += (_, mesgul) => _agac.Enabled = !mesgul;
        _arama.Bitti += (_, sonuc) => _doldurucu.AramaSonucunuGoster(sonuc.Metin, sonuc.Sonuc);
        _arama.Bosaltildi += (_, _) =>
        {
            if (_doldurucu.AramaKipinde)
            {
                // Aramadan cikarken kullanici actigi dallari ACIK bulmali.
                _doldurucu.GezinmeyeDon();
            }
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

        // Onizlemeye dair HER SEY Onizleme sinifinda. Bu sinif yalnizca
        // "sunu goster" der; hangi kaynak, hangi sira, hangi mesaj, hangi is
        // parcacigi - hicbirini bilmez (CLAUDE.md 7).
        _onizleme = new Onizleme(_onizlemePaneli, this);

        _onizleme?.Temizle();
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
        _arama.Dispose();
        _onizleme?.Dispose();
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
        _arama.MetniTemizle();

        _doldurucu.KokuAc(yol);

        // Kokun tek sahibi AgacDoldurucu; arama onun bildigi kokte arar.
        _arama.Kok = _doldurucu.Kok;

        _onizleme?.Temizle();
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
                _onizleme?.Goster(dosya);
                _durumSol.Text = string.Join("  ·  ",
                    dosya.Ad, Boyut.Yaz(dosya.Boyut), Zaman.Yaz(dosya.Degistirme));
                break;

            case KlasorOgesi klasor:
                _onizleme?.Goster(klasor);
                _durumSol.Text = klasor.Hata is null ? klasor.Yol : klasor.Yol + "  ·  " + klasor.Hata;
                break;

            default:
                _onizleme?.Temizle();
                break;
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
