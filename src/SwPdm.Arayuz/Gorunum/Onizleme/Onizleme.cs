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

    /// <summary>Panelde gosterilecek en fazla ozel ozellik.</summary>
    private const int EnFazlaOzellik = 3;

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
    /// CIPA: agacta secili dosya ve onun referans metinleri. Referans
    /// satirina tiklamak GECICI bir gosterimdir; cipa degismez ve basliga
    /// tiklaninca buraya donulur. Klasor/coklu secim/temizlik cipayi siler.
    /// </summary>
    private (DosyaOgesi Dosya, string Kullandigi, string Kullanan)? _capa;
    private volatile bool _duruyor;

    internal Onizleme(OnizlemePaneli panel, Control arayuz)
    {
        _panel = panel;
        _arayuz = arayuz;
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
    /// Bir dosyanin ust bilgisini yazar ve onizlemesini ister.
    ///
    /// <paramref name="referans"/> DISARIDAN geliyor cunku cevabi referans
    /// indeksi biliyor; onizleme onu uretmez, yalnizca yazar.
    /// </summary>
    internal void Goster(DosyaOgesi dosya, string kullandigi, string kullanan)
    {
        _capa = (dosya, kullandigi, kullanan);
        _beklenenYol = dosya.Yol;

        // Cipadayken baslik tiklanmaz - donulecek baska yer yok.
        _panel.BasligiYaz(dosya.Ad, geriDonulebilir: false);

        _panel.UstBilgiyiYaz(
            ad: dosya.Ad,
            tur: DosyaTurleri.Adi(dosya.Tur),
            boyut: Boyut.Yaz(dosya.Boyut),
            degistirme: Zaman.Yaz(dosya.Degistirme),

            // CLAUDE.md 3'un EN SERT kurali burada: "0" yazmak "bu parcayi
            // kimse kullanmiyor" demektir ve v1'de tam bu SAGLAM DOSYA
            // SILDIRIYORDU. Indeks taranmamissa buraya sayi degil "taranmadı"
            // geliyor - ayrimi ReferansSurucusu yapiyor. Iki yon iki ayri
            // satir: "Kullandığı" asagi, "Kullanan" yukari.
            //
            // DUZ METIN ALINIYOR, ReferansOzeti tipi DEGIL (29.08.2026):
            // once o tip buradan geciyordu ve iki OZELLIK birbirine
            // kilitleniyordu - referans panelini kaldirmak onizlemeyi de
            // degistirtirdi (CLAUDE.md 1b).
            kullandigi: kullandigi,
            kullanan: kullanan);

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
    /// KOMSU GOSTERIMI: referans panelinde tiklanan dosyanin onizlemesi ve
    /// bilgileri - agactaki secim (cipa) DEGISMEDEN. Baslik cipanin adini
    /// tasir ve tiklaninca cipaya donulur.
    ///
    /// NEDEN VAR (Erkan, 29.08.2026): "kullananlar listesindeki 13 dosyanin
    /// resmine bakmak icin 13 kez gidip donmek" gerekiyordu.
    /// </summary>
    internal void KomsuGoster(string yol, string kullandigi, string kullanan)
    {
        _beklenenYol = yol;

        _panel.BasligiYaz(
            _capa is { } c ? c.Dosya.Ad : WindowsYolu.DosyaAdi(yol),
            geriDonulebilir: _capa is not null);

        // Bilgiler diskten TEK kapidan okunur (DosyaIslemleri.Ozet);
        // okunamayan alan "—" olur, uydurma deger yazilmaz (CLAUDE.md 3).
        DosyaIslemleri.YolOzeti ozet = DosyaIslemleri.Ozet(yol);
        _panel.UstBilgiyiYaz(
            ad: WindowsYolu.DosyaAdi(yol),
            tur: DosyaTurleri.Adi(DosyaTurleri.Tani(WindowsYolu.DosyaAdi(yol))),
            boyut: ozet.Boyut is long b ? Boyut.Yaz(b) : "—",
            degistirme: ozet.Degistirme is DateTime z ? Zaman.Yaz(z) : "—",
            kullandigi: kullandigi,
            kullanan: kullanan);

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
        if (_capa is { } c)
        {
            Goster(c.Dosya, c.Kullandigi, c.Kullanan);
        }
    }

    /// <summary>Klasor secilince: onizleme aranmaz, ust bilgi yine de yazilir.</summary>
    internal void Goster(KlasorOgesi klasor)
    {
        _capa = null;
        _panel.BasligiYaz(klasor.Ad, geriDonulebilir: false);
        _beklenenYol = null;
        _panel.MesajGoster("Klasör");
        _panel.UstBilgiyiYaz(
            ad: klasor.Ad,
            tur: "Klasör",
            boyut: "—",
            degistirme: "—",

            // Klasorun "kullanani" olmaz; "taranmadı" yazmak taranınca bir
            // sey cikacagini ima ederdi.
            kullandigi: "—",
            kullanan: "—");
    }

    /// <summary>
    /// Birden cok oge seciliyken: tek bir dosyanin onizlemesi gosterilemez.
    /// Bos kutu birakmak yerine NE SECILDIGI yaziliyor (CLAUDE.md 3).
    /// </summary>
    internal void Goster(SecimOzeti ozet)
    {
        _capa = null;
        _panel.BasligiYaz(ozet.Yaz(), geriDonulebilir: false);
        _beklenenYol = null;
        _panel.MesajGoster($"{ozet.Toplam} öğe seçildi");
        _panel.UstBilgiyiYaz(
            ad: ozet.Yaz(),
            tur: "Çoklu seçim",
            boyut: ozet.DosyaSayisi > 0 && ozet.BoyutTam ? Boyut.Yaz(ozet.ToplamBoyut) : "—",
            degistirme: "—",
            kullandigi: "—",
            kullanan: "—");
    }

    /// <summary>Secim yokken paneli bosaltir.</summary>
    internal void Temizle()
    {
        _capa = null;
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

            Sonucu(Yukle(istek.Value.Yol, istek.Value.Boyut), Ozellikleri(istek.Value.Yol));
        }
    }

    /// <summary>
    /// Belgenin icindeki ozellikleri tek satira dizer.
    ///
    /// ARKA PLANDA cagriliyor cunku dosya aciliyor - olculdu: dosya basina
    /// ~66 KB ve birkac ms, ama ag surucusunde her tiklamada arayuzu
    /// bekletmek kabul edilemez.
    ///
    /// EN COK <see cref="EnFazlaOzellik"/> tane gosteriliyor: bir parcada
    /// onlarca ozellik olabilir ve panel iki satirlik bir yer. Kirpildiysa
    /// bu SOYLENIYOR ("+3 daha") - sessizce kirpmak, kullanicinin gormedigi
    /// bir ozelligi yok saymasina yol acardi (CLAUDE.md 3).
    /// </summary>
    private static string Ozellikleri(string yol)
    {
        if (!SwReferans.TasiyabilirMi(yol))
        {
            return string.Empty;
        }

        SwBelgeBilgileri bilgi = SwBelgeBilgisi.Oku(yol);
        if (!bilgi.Okundu)
        {
            // "OZELLIGI YOK" ILE "OKUNAMADI" AYRI SEY - ve cekirdek bu ayrimi
            // zaten yapiyor (SwBelgeBilgileri.Sebep). Burasi ikisini de bos
            // metne cevirip sebebi yutuyordu; referans paneli ayni ayrimi
            // titizlikle yapiyor, onizleme geride kalmisti (CLAUDE.md 3).
            return "Özellikler okunamadı: " + (bilgi.Sebep ?? "sebep bilinmiyor");
        }

        var parcalar = new System.Collections.Generic.List<string>();
        if (bilgi.SonKaydeden is string kim)
        {
            parcalar.Add("Kaydeden: " + kim);
        }

        if (bilgi.Yapilandirma is string yapi)
        {
            parcalar.Add("Yapılandırma: " + yapi);
        }

        int sayi = 0;
        foreach (System.Collections.Generic.KeyValuePair<string, string> o in bilgi.Ozel)
        {
            if (sayi == EnFazlaOzellik)
            {
                parcalar.Add($"+{bilgi.Ozel.Count - EnFazlaOzellik} daha");
                break;
            }

            parcalar.Add($"{o.Key}: {(o.Value.Length == 0 ? "—" : o.Value)}");
            sayi++;
        }

        return string.Join("  ·  ", parcalar);
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

    private void Sonucu((string Yol, Image? Resim, string? Sebep) sonuc, string ozellikler)
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

                _panel.OzellikleriYaz(ozellikler);

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
