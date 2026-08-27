using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// ============================================================
/// ONIZLEMENIN TEK KAPISI. Onizlemeye dair bir sey degisecekse
/// BU DOSYA degisir - baska hicbiri.
/// ============================================================
///
/// Burada duran KARARLAR:
///   - hangi kaynaklar, hangi SIRAYLA denenir
///   - hicbiri vermezse EKRANDA ne yazar
///   - hangi is parcaciginda kosar
///   - bayat sonuc nasil elenir
///
/// Disaridaki tek yuzey: <see cref="Goster(DosyaOgesi)"/>,
/// <see cref="Goster(KlasorOgesi)"/>, <see cref="Temizle"/>.
/// <see cref="AnaForm"/> bundan fazlasini BILMEZ (CLAUDE.md 7: bir arayuz
/// sinifi hem ekran hem is akisi surucusu olmaz).
///
/// Kaynaklar ayri dosyalarda ama YALNIZCA buradan cagriliyor:
///   Onizleme/KabukOnizleme.cs   - Windows kabugu
///   Cekirdek/OnizlemeOkuyucu.cs - dosyanin icindeki gomulu resim
/// Ikincisi bilerek CEKIRDEKTE: platformdan bagimsiz ve Linux'ta test
/// ediliyor. Buraya tasimak o testleri kaybettirirdi.
/// </summary>
internal sealed class Onizleme : IDisposable
{
    private const string Yukleniyor = "Önizleme yükleniyor…";
    private const string Yok = "Önizleme yok";

    /// <summary>
    /// PDF onizlemesi cikmadiginda gosterilecek YOL GOSTEREN metin.
    ///
    /// KARAR (Erkan, 27.08.2026): PDF motoru uygulamaya GOMULMUYOR.
    /// Denendi, calisti, geri alindi. Iki sebep:
    ///   1. Bu uygulamanin onizleme sozu "Windows ne gosteriyorsa onu goster".
    ///      Kendi motorumuzu gomunce Gezgin'in GOSTERMEDIGI bir onizlemeyi
    ///      gosterir olduk - sozu kendimiz bozduk.
    ///   2. Olculdu: WinRT koprusu paketi 120 KB'den 6,5 MB'a cikariyordu.
    /// Yerine kullaniciya NE YAPACAGI soyleniyor; bir kez kurar, hem burada
    /// hem Gezgin'de gorur.
    /// </summary>
    private const string PdfIcinYapilacaklar =
        "PDF önizlemesi yok.\n\n"
        + "Windows bu bilgisayarda PDF için önizleme üretmiyor "
        + "(Gezgin'de de görünmüyordur).\n\n"
        + "Ücretsiz Adobe Acrobat Reader kurup Tercihler → Genel →\n"
        + "\"Windows Gezgini'nde PDF küçük resimlerini etkinleştir\"\n"
        + "seçeneğini açarsanız önizleme burada da görünür.\n\n"
        + "get.adobe.com/tr/reader";

    /// <summary>Kabuktan istenecek en kucuk olcu; kutu daha kucukse bile net kalsin.</summary>
    private static readonly Size EnKucukIstek = new(256, 256);

    private readonly OnizlemePaneli _panel;
    private readonly Control _arayuz;
    private readonly Thread _isParcacigi;
    private readonly SemaphoreSlim _uyandir = new(0);
    private readonly object _kilit = new();

    private (string Yol, Size Boyut)? _bekleyen;
    private string? _beklenenYol;
    private volatile bool _duruyor;

    internal Onizleme(OnizlemePaneli panel, Control arayuz)
    {
        _panel = panel;
        _arayuz = arayuz;

        // Neden ayri is parcacigi: dosyalar ag surucusunde; bir onizleme
        // saniyeler surebilir ve arayuz donmamali.
        // Neden ThreadPool DEGIL: CLAUDE.md 4 - kabuk onizleme saglayicilari
        // STA istiyor, ThreadPool ise MTA ve E_FAIL doner.
        _isParcacigi = new Thread(Dongu)
        {
            IsBackground = true,
            Name = "onizleme",
        };
        _isParcacigi.SetApartmentState(ApartmentState.STA);   // Start()'tan ONCE
        _isParcacigi.Start();
    }

    /// <summary>Bir dosyanin ust bilgisini yazar ve onizlemesini ister.</summary>
    internal void Goster(DosyaOgesi dosya)
    {
        _beklenenYol = dosya.Yol;

        _panel.UstBilgiyiYaz(
            ad: dosya.Ad,
            tur: DosyaTurleri.Adi(dosya.Tur),
            boyut: Boyut.Yaz(dosya.Boyut),
            degistirme: Zaman.Yaz(dosya.Degistirme),

            // CLAUDE.md 3'un EN SERT kurali burada. Referans indeksi YOK.
            // "0" yazmak "bu parcayi kimse kullanmiyor" demektir ve v1'de tam
            // bu SAGLAM DOSYA SILDIRIYORDU. Bilmiyorsak bilmedigimizi yaziyoruz.
            kullanan: "taranmadı");

        // CLAUDE.md 3: bos kutu "onizlemesi yok" demek DEGILDIR. Yuklenirken
        // de soyluyoruz ki kullanici bekledigini bilsin.
        _panel.MesajGoster(Yukleniyor);

        Size kutu = _panel.KutuBoyutu;
        var istenen = new Size(
            Math.Max(kutu.Width, EnKucukIstek.Width),
            Math.Max(kutu.Height, EnKucukIstek.Height));

        lock (_kilit)
        {
            _bekleyen = (dosya.Yol, istenen);   // SON ISTEK KAZANIR
        }

        _uyandir.Release();
    }

    /// <summary>Klasor secilince: onizleme aranmaz, ust bilgi yine de yazilir.</summary>
    internal void Goster(KlasorOgesi klasor)
    {
        _beklenenYol = null;
        _panel.MesajGoster("Klasör");
        _panel.UstBilgiyiYaz(
            ad: klasor.Ad,
            tur: "Klasör",
            boyut: "—",
            degistirme: "—",
            kullanan: "taranmadı");
    }

    /// <summary>Secim yokken paneli bosaltir.</summary>
    internal void Temizle()
    {
        _beklenenYol = null;
        _panel.Temizle();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _duruyor = true;
        _uyandir.Release();
        _isParcacigi.Join(TimeSpan.FromSeconds(2));
        _uyandir.Dispose();
    }

    // ------------------------------------------------------- is parcacigi

    private void Dongu()
    {
        while (!_duruyor)
        {
            _uyandir.Wait();
            if (_duruyor)
            {
                return;
            }

            (string Yol, Size Boyut)? istek;
            lock (_kilit)
            {
                istek = _bekleyen;
                _bekleyen = null;
            }

            if (istek is null)
            {
                continue;   // daha yeni bir istek zaten aldi
            }

            Sonucu(Yukle(istek.Value.Yol, istek.Value.Boyut));
        }
    }

    /// <summary>
    /// KAYNAK SIRASI. Onizlemeye kaynak eklemek/cikarmak isteyen buraya bakar.
    /// </summary>
    private static (string Yol, Image? Resim, string? Sebep) Yukle(string yol, Size boyut)
    {
        // 1) KABUK: Gezgin ne goruyorsa o. SOLIDWORKS kuruluysa gercek parca
        //    goruntusu buradan gelir.
        Bitmap? kabuktan = KabukOnizleme.Al(yol, boyut, out string? sebep);
        if (kabuktan is not null)
        {
            return (yol, kabuktan, null);
        }

        // 2) GOMULU: dosyanin icindeki onizleme. SOLIDWORKS kurulu OLMAYAN bir
        //    makinede tek sansimiz bu.
        try
        {
            byte[]? gomulu = OnizlemeOkuyucu.Oku(yol);
            if (gomulu is not null)
            {
                // OLCULMUS TUZAK (CLAUDE.md 4): Image.FromStream AKISI
                // SAHIPLENIYOR - akis kapaninca resim cizilmiyor ama null da
                // olmuyor, yani "onizleme yok" dalina da girilmiyor. Belirti
                // tamamen sessiz: bos kutu, sebep yok. Bagimsiz kopya sart.
                using var bellek = new MemoryStream(gomulu, writable: false);
                using Image gecici = Image.FromStream(bellek);
                return (yol, new Bitmap(gecici), null);
            }
        }
        catch (Exception hata) when (hata is ArgumentException or OutOfMemoryException
                                         or IOException or NotSupportedException)
        {
            return (yol, null, "Gömülü önizleme okunamadı: " + hata.Message);
        }

        // 3) HICBIRI YOKSA: "yok" demek yetmez, NEDEN yok ve ne yapilabilir
        //    de soylenir (CLAUDE.md 3).
        if (sebep is null && DosyaTurleri.Tani(yol) == DosyaTuru.Pdf)
        {
            sebep = PdfIcinYapilacaklar;
        }

        return (yol, null, sebep);
    }

    private void Sonucu((string Yol, Image? Resim, string? Sebep) sonuc)
    {
        if (_arayuz.IsDisposed || !_arayuz.IsHandleCreated)
        {
            sonuc.Resim?.Dispose();
            return;
        }

        try
        {
            _arayuz.BeginInvoke(() =>
            {
                // Kullanici baska dosyaya gectiyse bu sonuc BAYAT. Gostermek,
                // yanlis dosyanin onizlemesini dogru sanmaya yol acar - bu
                // uygulamada tehlikeli.
                if (!string.Equals(_beklenenYol, sonuc.Yol, StringComparison.OrdinalIgnoreCase))
                {
                    sonuc.Resim?.Dispose();
                    return;
                }

                if (sonuc.Resim is not null)
                {
                    _panel.Onizleme = sonuc.Resim;
                    return;
                }

                _panel.MesajGoster(sonuc.Sebep ?? Yok);
            });
        }
        catch (Exception hata) when (hata is ObjectDisposedException or InvalidOperationException)
        {
            sonuc.Resim?.Dispose();   // pencere tam bu sirada kapandi
        }
    }
}
