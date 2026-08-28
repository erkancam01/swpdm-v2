using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// OTOMATIK TAZELEME. Diskte bir sey degisince agaci kendiliginden tazeler.
///
/// NEDEN VAR: dosyalar ORTAK ag surucusunde duruyor. Baskasi bir klasor
/// eklediginde ya da bir parcayi sildiginde agac bunu gormuyordu; kullanici
/// olmayan bir dosyaya tiklayip hata aliyordu.
///
/// GECIKME SART: tek bir kopyalama onlarca olay uretiyor (Created, Changed,
/// Changed, ...). Her olayda tazelemek agaci titretir ve ag surucusunu
/// bogar. Son olaydan sonra sessizlik beklenip TEK tazeleme yapiliyor.
///
/// KENDI ISLEMLERIMIZ SAYILMAZ: sildigimizde/tasidigimizda zaten kendimiz
/// tazeliyoruz. Izleyici o sirada susturuluyor, yoksa iki tazeleme carpisir
/// ve "yeni klasoru sec" davranisi kaybolur.
///
/// FileSystemWatcher AG SURUCUSUNDE GUVENILIR DEGIL - olay kacirabiliyor ve
/// tampon tasabiliyor. O yuzden: hata olursa SESSIZCE olmez, sebebi
/// bildirilir ve F5 hala elde (CLAUDE.md 3).
/// </summary>
internal sealed class DiskIzleyici : IDisposable
{
    /// <summary>Son olaydan sonra beklenen sessizlik.</summary>
    private const int GecikmeMs = 900;

    /// <summary>Degisti olayinin "sessiz" parametresi: durum cubuguna YAZILSIN.</summary>
    private const bool Duyurarak = false;

    /// <summary>Degisti olayinin "sessiz" parametresi: hicbir sey YAZILMASIN.</summary>
    private const bool Sessizce = true;

    /// <summary>
    /// Kendi islemimiz bittikten sonra izlemenin ne kadar susmaya devam
    /// ettigi. OLCULDU (27.08.2026, Wine): 900 ms YETMEDI - iki dosyalik bir
    /// kopyalamanin olaylari isten sonra hala geliyordu ve durum cubugundaki
    /// "2 oge kopyalandi" yazisinin uzerine "diskte degisiklik goruldu"
    /// yaziliyordu. Sure uzatildi VE asagidaki "bekleyen" mekanizmasi
    /// eklendi; ikisi birden (CLAUDE.md 2: iki ucuz hipotezden birini secme,
    /// ikisini birden kapat).
    /// </summary>
    private const int SusmaSuresiMs = 2000;

    private readonly Control _arayuz;
    private readonly System.Windows.Forms.Timer _gecikme = new() { Interval = GecikmeMs };
    private readonly System.Windows.Forms.Timer _susturmaSonu = new() { Interval = SusmaSuresiMs };

    /// <summary>Son bildirimden bu yana degisen yollar (indeks icin).</summary>
    private readonly HashSet<string> _kirlenenler = new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _izleyici;
    private bool _susturuldu;
    private bool _bekleyen;

    internal DiskIzleyici(Control arayuz)
    {
        _arayuz = arayuz;
        _gecikme.Tick += (_, _) =>
        {
            _gecikme.Stop();
            if (!_susturuldu)
            {
                Degisti?.Invoke(this, Duyurarak);
            }
        };

        _susturmaSonu.Tick += (_, _) =>
        {
            _susturmaSonu.Stop();
            _susturuldu = false;

            if (!_bekleyen)
            {
                return;
            }

            // Susturulmusken bir sey oldu. Agac YINE DE tazelenir - yoksa
            // gercekten baskasinin yaptigi bir degisiklik kaybolurdu. Ama
            // SESSIZ: bu olaylarin neredeyse tamami kendi islemimizin
            // yankisi ve durum cubugundaki sonucun uzerine yazmak,
            // kullaniciya kendi yaptigi isi baskasi yapmis gibi gosterirdi.
            _bekleyen = false;
            Degisti?.Invoke(this, Sessizce);
        };
    }

    /// <summary>
    /// Diskte bir sey degisti; agac tazelenmeli. Parametre SESSIZ ise durum
    /// cubuguna bir sey YAZILMAZ (degisiklik buyuk ihtimalle kendi
    /// islemimizin yankisi).
    /// </summary>
    internal event EventHandler<bool>? Degisti;

    /// <summary>Izleme kurulamadi ya da koptu; sebep EKRANDA soylenmeli.</summary>
    internal event EventHandler<string>? Sorun;

    /// <summary>Izleme acik mi.</summary>
    internal bool Acik { get; private set; } = true;

    /// <summary>
    /// Izleme SU AN saglam mi - yani "diskte olan biteni goruyorum" denebilir mi.
    ///
    /// NEDEN AYRI BIR BAYRAK: indeks tarafi buna bakip TAM taramayi
    /// atlayabiliyor. Kapali izleme, kurulamamis izleyici ve tampon tasmasi
    /// (bkz. <see cref="Hata"/>) ayni sonucu verir: disariyi GORMUYORUZ, o
    /// halde atlamak yasak (CLAUDE.md 3).
    /// </summary>
    internal bool Guvenilir { get; private set; }

    /// <summary>
    /// Son cagridan bu yana degisen yollari verir ve listeyi bosaltir.
    /// Indeks bunlari hedefli tazeliyor; butun kok taranmiyor.
    /// </summary>
    internal IReadOnlyList<string> Kirlenenler()
    {
        var liste = new List<string>(_kirlenenler);
        _kirlenenler.Clear();
        return liste;
    }

    /// <summary>Izlenecek kok. null ise izleme durur.</summary>
    internal void Izle(string? kok)
    {
        Birak();

        if (kok is null || !Acik || !Directory.Exists(kok))
        {
            return;
        }

        try
        {
            _izleyici = new FileSystemWatcher(kok)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite | NotifyFilters.Size,
            };

            _izleyici.Created += Olay;
            _izleyici.Deleted += Olay;
            _izleyici.Renamed += Olay;
            _izleyici.Changed += Olay;
            _izleyici.Error += Hata;
            _izleyici.EnableRaisingEvents = true;
            Guvenilir = true;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                         or ArgumentException or PlatformNotSupportedException)
        {
            Birak();
            Bildir("Otomatik tazeleme kurulamadı — F5 ile elle yenileyin. " + hata.Message);
        }
    }

    /// <summary>
    /// Kendi islemimiz sirasinda izlemeyi susturur.
    ///
    /// ACMA GECIKMELI - ve bu ONEMLI: kendi tasima/silme islemimizin urettigi
    /// olaylar disk katmanindan GECIKMELI geliyor. Susturmayi isin bittigi an
    /// kaldirsak o olaylar hemen ardindan gelir, agac bir daha tazelenir ve
    /// durum cubugundaki "3 dosya tasindi" yazisinin uzerine "diskte
    /// degisiklik goruldu" yazilir. Kullaniciya kendi yaptigi isi BASKASI
    /// yapmis gibi gostermek CLAUDE.md 3'un ihlalidir.
    /// </summary>
    internal void Sustur(bool sustur)
    {
        _susturmaSonu.Stop();

        if (sustur)
        {
            _susturuldu = true;
            _gecikme.Stop();
            return;
        }

        // Kendi islemimizin artcil olaylari gecene kadar sussun.
        _susturmaSonu.Start();
    }

    /// <summary>Kullanici acip kapatabiliyor.</summary>
    internal void AcKapat(bool acik, string? kok)
    {
        Acik = acik;
        Izle(kok);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Birak();
        _gecikme.Stop();
        _gecikme.Dispose();
        _susturmaSonu.Stop();
        _susturmaSonu.Dispose();
    }

    private void Olay(object gonderen, FileSystemEventArgs e)
    {
        // KENDI cop klasorumuzdeki hareket sayilmaz: her silme orada dosya
        // olusturuyor ve bu sonsuz tazeleme dongusu yaratirdi.
        if (e.FullPath.Contains(
                WindowsYolu.Ayirici + Cop.KlasorAdi, StringComparison.OrdinalIgnoreCase)
            || e.FullPath.Contains(
                WindowsYolu.EgikAyirici + Cop.KlasorAdi, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // DEGISEN YOL SAKLANIYOR: eskiden yalnizca "bir sey degisti" bilgisi
        // tasiniyor, yolun kendisi ATILIYORDU. Oysa indeksi butun kok
        // taranmadan tazelemek icin gereken tek sey bu.
        _kirlenenler.Add(e.FullPath);
        if (e is RenamedEventArgs yeniden)
        {
            _kirlenenler.Add(yeniden.OldFullPath);
        }

        Zamanlayiciyi_Kur();
    }

    private void Hata(object gonderen, ErrorEventArgs e)
    {
        // Tampon tastiysa KACIRDIGIMIZ olaylar var: artik "disariyi
        // goruyorum" diyemeyiz.
        Guvenilir = false;
        // OLCULMEMIS AMA BILINEN: tampon tasarsa izleyici olayları KACIRIR ve
        // sessizce yanlis bir agac gosterirdik. Sessiz kalmaktansa bir kez
        // tazeleyip sorunu SOYLUYORUZ.
        Zamanlayiciyi_Kur();
        Bildir("Otomatik tazeleme kesintiye uğradı — F5 ile elle yenileyin.");
    }

    private void Zamanlayiciyi_Kur()
    {
        if (_arayuz.IsDisposed || !_arayuz.IsHandleCreated)
        {
            return;
        }

        if (_susturuldu)
        {
            // Olay ATILMIYOR, bekletiliyor: susturma bitince sessizce
            // tazelenecek. Atmak, kendi islemimiz sirasinda BASKASININ
            // yaptigi degisikligi sessizce kaybettirirdi (CLAUDE.md 3).
            _bekleyen = true;
            return;
        }

        try
        {
            // Olay ARKA PLAN is parcaciginda geliyor; zamanlayici arayuzun.
            _arayuz.BeginInvoke(() =>
            {
                _gecikme.Stop();
                _gecikme.Start();
            });
        }
        catch (Exception hata) when (hata is ObjectDisposedException or InvalidOperationException)
        {
            // Pencere tam bu sirada kapandi.
        }
    }

    private void Bildir(string cumle)
    {
        if (_arayuz.IsDisposed || !_arayuz.IsHandleCreated)
        {
            return;
        }

        try
        {
            _arayuz.BeginInvoke(() => Sorun?.Invoke(this, cumle));
        }
        catch (Exception hata) when (hata is ObjectDisposedException or InvalidOperationException)
        {
            // Pencere kapandi.
        }
    }

    private void Birak()
    {
        _gecikme.Stop();
        Guvenilir = false;

        if (_izleyici is null)
        {
            return;
        }

        _izleyici.EnableRaisingEvents = false;
        _izleyici.Created -= Olay;
        _izleyici.Deleted -= Olay;
        _izleyici.Renamed -= Olay;
        _izleyici.Changed -= Olay;
        _izleyici.Error -= Hata;
        _izleyici.Dispose();
        _izleyici = null;
    }
}
