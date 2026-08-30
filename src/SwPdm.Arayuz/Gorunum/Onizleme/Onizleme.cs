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
internal sealed partial class Onizleme : IDisposable
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

    /// <summary>
    /// CIPA: agacta secili dosya. Referans satirina tiklamak GECICI bir
    /// gosterimdir; cipa degismez ve basliga tiklaninca buraya donulur.
    /// Klasor/coklu secim/temizlik cipayi siler.
    /// </summary>
    private DosyaOgesi? _capa;
    private volatile bool _duruyor;

    internal Onizleme(OnizlemePaneli panel, Control arayuz, Func<bool> ucBoyutluMu)
    {
        _panel = panel;
        _arayuz = arayuz;
        _ucBoyutluMu = ucBoyutluMu;
        _panel.BasligaTiklandi += (_, _) => CipayaDon();

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

    /// <summary>
    /// Agacta secilen dosyayi gosterir: baslikta ADI, kutuda onizlemesi.
    /// Bu dosya ayni zamanda CIPA olur (bkz. <see cref="KomsuGoster"/>).
    /// </summary>
    internal void Goster(DosyaOgesi dosya)
    {
        _capa = dosya;
        _beklenenYol = dosya.Yol;

        // Cipadayken geri donulecek baska yer yok - ok isareti cikmaz.
        _panel.BasligiYaz(dosya.Ad, geriDonulebilir: false);

        // 3B KIP (Ayarlar): SOLIDWORKS dosyasi eDrawings'te acilir; 2B boru
        // hatti hic kosmaz. Kurulamaz/acamazsa sebep soylenir ve 2B devam.
        if (UcBoyutluDene(dosya.Yol))
        {
            return;
        }

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

    /// <summary>
    /// KOMSU GOSTERIMI: referans panelinde tiklanan dosyanin onizlemesi -
    /// agactaki secim (cipa) DEGISMEDEN.
    ///
    /// BASLIK KOMSUNUN ADINI TASIR: bilgi blogu kalkinca (30.08.2026)
    /// "neye bakiyorsun" sorusunun cevabi yalniz burada kaldi. Basa konan
    /// "◂" geri isareti; tiklaninca cipaya donulur.
    ///
    /// NEDEN VAR (Erkan, 29.08.2026): "kullananlar listesindeki 13 dosyanin
    /// resmine bakmak icin 13 kez gidip donmek" gerekiyordu.
    /// </summary>
    internal void KomsuGoster(string yol)
    {
        _beklenenYol = yol;
        _panel.BasligiYaz(
            WindowsYolu.DosyaAdi(yol),
            geriDonulebilir: _capa is not null);

        if (UcBoyutluDene(yol))
        {
            return;
        }

        _panel.MesajGoster(Yukleniyor);

        Size kutu = _panel.KutuBoyutu;
        var istenen = new Size(
            Math.Max(kutu.Width, EnKucukIstek.Width),
            Math.Max(kutu.Height, EnKucukIstek.Height));

        lock (_kilit)
        {
            _bekleyen = (yol, istenen);   // SON ISTEK KAZANIR
        }

        _uyandir.Release();
    }

    /// <summary>Basliga tiklandi: cipaya (agacta secili dosyaya) don.</summary>
    private void CipayaDon()
    {
        if (_capa is DosyaOgesi dosya)
        {
            Goster(dosya);
        }
    }

    /// <summary>Klasor secilince: onizleme aranmaz; baslikta klasorun adi.</summary>
    internal void Goster(KlasorOgesi klasor)
    {
        _capa = null;
        UcBoyutluGizle();
        _panel.BasligiYaz(klasor.Ad, geriDonulebilir: false);
        _beklenenYol = null;
        _panel.MesajGoster("Klasör");
    }

    /// <summary>
    /// Birden cok oge seciliyken: tek bir dosyanin onizlemesi gosterilemez.
    /// Bos kutu birakmak yerine NE SECILDIGI yaziliyor (CLAUDE.md 3).
    /// </summary>
    internal void Goster(SecimOzeti ozet)
    {
        _capa = null;
        UcBoyutluGizle();
        _panel.BasligiYaz(ozet.Yaz(), geriDonulebilir: false);
        _beklenenYol = null;
        _panel.MesajGoster($"{ozet.Toplam} öğe seçildi");
    }

    /// <summary>Secim yokken paneli bosaltir.</summary>
    internal void Temizle()
    {
        _capa = null;
        UcBoyutluGizle();
        _panel.BasligiYaz(string.Empty, geriDonulebilir: false);
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
        _ucBoyutlu?.Dispose();
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
    /// Bayt dizisini bagimsiz bir resme cevirir; olmazsa sebebini verir.
    ///
    /// OLCULMUS TUZAK (CLAUDE.md 4): Image.FromStream AKISI SAHIPLENIYOR.
    /// Akis kapaninca resim CIZILMIYOR ama null da olmuyor - yani "onizleme
    /// yok" dalina bile girilmiyor, kullanici bos bir kutu ve hicbir sebep
    /// goruyor. Bagimsiz kopya (new Bitmap) sart.
    /// </summary>
    private static Image? Resme(byte[]? veri, out string? sebep)
    {
        sebep = null;
        if (veri is null || veri.Length == 0)
        {
            return null;
        }

        try
        {
            using var bellek = new MemoryStream(veri, writable: false);
            using Image gecici = Image.FromStream(bellek);
            return new Bitmap(gecici);
        }
        catch (Exception hata) when (hata is ArgumentException or OutOfMemoryException
                                         or IOException or NotSupportedException)
        {
            sebep = "Dosyadaki önizleme çözülemedi: " + hata.Message;
            return null;
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

        // 2) SOLIDWORKS PAKETI: dosyanin icindeki "PreviewPNG" akisi.
        //    OLCULDU (28.08.2026): SOLIDWORKS 2022 dosyalari OLE bilesik belge
        //    DEGIL, kendi kaplari - o yuzden asagidaki (3) numarali gomulu
        //    okuyucu bu dosyalarda HIC calismiyordu. Gelen sey gercek bir PNG.
        //    Kazanci ikili: SOLIDWORKS kurulu olmayan makinede onizleme
        //    cikiyor, VE Wine'da olculebiliyor (kabuk saglayicisi orada yok).
        Image? paketten = Resme(SwOnizleme.Oku(yol), out string? paketSebebi);
        if (paketten is not null)
        {
            return (yol, paketten, null);
        }

        if (paketSebebi is not null)
        {
            return (yol, null, paketSebebi);
        }

        // 3) GOMULU (OLE bilesik belge): eski surumlerin dosyalari icin.
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

        // 4) HICBIRI YOKSA: "yok" demek yetmez, NEDEN yok ve ne yapilabilir
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
