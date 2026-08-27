using System;
using System.Drawing;
using System.IO;
using System.Threading;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>Bir onizleme isteginin sonucu.</summary>
internal sealed record OnizlemeSonucu(string Yol, Image? Resim, string? Sebep);

/// <summary>
/// Onizlemeleri arayuzu KILITLEMEDEN yukler.
///
/// Neden ayri bir is parcacigi: dosyalar ag surucusunde duruyor ve bir
/// onizleme saniyeler surebilir. Secim degistikce arayuzun donmasi kabul
/// edilemez.
///
/// Neden ThreadPool DEGIL: CLAUDE.md 4 - kabuk onizleme saglayicilari STA
/// istiyor, ThreadPool ise MTA. Bu yuzden KENDI STA is parcacigimizi
/// kuruyoruz.
///
/// SON ISTEK KAZANIR: kullanici agacta ok tuslariyla gezerken onlarca istek
/// birikir. Yalnizca en sonuncusu islenir; arasi atilir.
/// </summary>
internal sealed class OnizlemeYukleyici : IDisposable
{
    /// <summary>
    /// PDF onizlemesi cikmadiginda gosterilecek yol gosterici metin.
    ///
    /// KARAR (Erkan, 27.08.2026): PDF motorunu uygulamaya GOMMUYORUZ.
    /// Denendi ve calisti, ama iki sebeple geri alindi:
    ///   1. Bu uygulamanin onizleme ilkesi "Windows ne gosteriyorsa onu goster".
    ///      Kendi motorumuzu gomunce Gezgin'in gostermedigi bir onizlemeyi biz
    ///      gosterir olduk - ilkeyi kendimiz kirdik.
    ///   2. Olculdu: WinRT koprusu paketi 120 KB'den 6,5 MB'a cikariyordu
    ///      (Microsoft.Windows.SDK.NET.dll, 24,9 MB).
    /// Yerine kullaniciya NE YAPACAGI soyleniyor; bir kez kurar, hem burada
    /// hem Gezgin'de onizleme gorur.
    /// </summary>
    private const string PdfOnizlemesiIcinYapilacaklar =
        "PDF önizlemesi yok.\n\n"
        + "Windows bu bilgisayarda PDF için önizleme üretmiyor "
        + "(Gezgin'de de görünmüyordur).\n\n"
        + "Ücretsiz Adobe Acrobat Reader kurup Tercihler → Genel →\n"
        + "\"Windows Gezgini'nde PDF küçük resimlerini etkinleştir\"\n"
        + "seçeneğini açarsanız önizleme burada da görünür.\n\n"
        + "get.adobe.com/tr/reader";

    private readonly Thread _isParcacigi;
    private readonly SemaphoreSlim _uyandir = new(0);
    private readonly object _kilit = new();
    private readonly Action<OnizlemeSonucu> _bitince;
    private (string Yol, Size Boyut)? _bekleyen;
    private volatile bool _duruyor;

    internal OnizlemeYukleyici(Action<OnizlemeSonucu> bitince)
    {
        _bitince = bitince;
        _isParcacigi = new Thread(Dongu)
        {
            IsBackground = true,
            Name = "onizleme-yukleyici",
        };

        // CLAUDE.md 4: kabuk onizlemesi STA sart. Start()'tan ONCE.
        _isParcacigi.SetApartmentState(ApartmentState.STA);
        _isParcacigi.Start();
    }

    /// <summary>Bir onizleme ister. Onceki bekleyen istek ATILIR.</summary>
    internal void Iste(string yol, Size boyut)
    {
        lock (_kilit)
        {
            _bekleyen = (yol, boyut);
        }

        _uyandir.Release();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _duruyor = true;
        _uyandir.Release();
        _isParcacigi.Join(TimeSpan.FromSeconds(2));
        _uyandir.Dispose();
    }

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

            _bitince(Yukle(istek.Value.Yol, istek.Value.Boyut));
        }
    }

    private static OnizlemeSonucu Yukle(string yol, Size boyut)
    {
        // 1) ONCE KABUK: Gezgin ne goruyorsa o. SOLIDWORKS kuruluysa gercek
        //    parca goruntusu buradan gelir (Erkan: v1 boyle yapiyordu).
        Bitmap? kabuktan = KabukOnizleme.Al(yol, boyut, out string? sebep);
        if (kabuktan is not null)
        {
            return new OnizlemeSonucu(yol, kabuktan, null);
        }

        // 2) YEDEK: dosyanin ICINDEKI gomulu onizleme. SOLIDWORKS kurulu
        //    OLMAYAN bir makinede tek sansimiz bu.
        try
        {
            byte[]? gomulu = OnizlemeOkuyucu.Oku(yol);
            if (gomulu is not null)
            {
                // ============== OLCULMUS TUZAK (CLAUDE.md 4) ==============
                // Image.FromStream AKISI SAHIPLENIYOR: cozumlemeyi tembel
                // yapiyor ve akis kapaninca resim CIZILMIYOR. Ama null da
                // olmuyor - yani "onizleme yok" mesaji da cikmiyor.
                // Belirti tamamen sessiz: kutu bos, sebep yok.
                // Cozum: bagimsiz bir kopya almak.
                // ==========================================================
                using var bellek = new MemoryStream(gomulu, writable: false);
                using Image gecici = Image.FromStream(bellek);
                return new OnizlemeSonucu(yol, new Bitmap(gecici), null);
            }
        }
        catch (Exception hata) when (hata is ArgumentException or OutOfMemoryException
                                         or IOException or NotSupportedException)
        {
            return new OnizlemeSonucu(yol, null, "Gömülü önizleme okunamadı: " + hata.Message);
        }

        // Onizleme yoksa KOSE YAZIP birakmiyoruz: PDF'te sebep genelde
        // duzeltilebilir bir sey ve kullanici ne yapacagini bilmeli.
        // CLAUDE.md 3: her istek bir yanit alir; "yok" demek yetmez, NEDEN
        // yok ve ne yapilabilir de soylenir.
        if (sebep is null && DosyaTurleri.Tani(yol) == DosyaTuru.Pdf)
        {
            sebep = PdfOnizlemesiIcinYapilacaklar;
        }

        return new OnizlemeSonucu(yol, null, sebep);
    }
}
