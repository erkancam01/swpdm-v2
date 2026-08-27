using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// ARAMANIN TEK KAPISI. Aramaya dair bir sey degisecekse BU DOSYA degisir.
///
/// Burada duran kararlar: ne zaman baslar (anlik mi, Enter mi), ne kadar
/// bekler, hangi is parcaciginda kosar, nasil iptal edilir, ilerleme nasil
/// bildirilir.
///
/// <see cref="AnaForm"/> yalnizca olaylari baglar; arama mantigini BILMEZ
/// (CLAUDE.md 7: bir arayuz sinifi hem ekran hem is akisi surucusu olmaz).
/// </summary>
internal sealed class AramaSurucusu : IDisposable
{
    /// <summary>En fazla kac eslesme toplanir. Asilirsa SOYLENIR, sessizce kirpilmaz.</summary>
    private const int Sinir = 2000;

    /// <summary>
    /// Tusa her basista aramaya baslamak ag surucusunu bogar. Bu gecikme,
    /// yazma duruncaya kadar bekletir - kullaniciya "anlik" hissettirir ama
    /// diske her harfte gitmez.
    /// </summary>
    private const int GecikmeMs = 350;

    private readonly ToolStripTextBox _kutu;
    private readonly Control _arayuz;
    private readonly System.Windows.Forms.Timer _gecikme = new() { Interval = GecikmeMs };
    private CancellationTokenSource? _iptal;
    private bool _metniKodDegistiriyor;

    internal AramaSurucusu(ToolStripTextBox kutu, Control arayuz)
    {
        _kutu = kutu;
        _arayuz = arayuz;

        _kutu.TextChanged += MetinDegisti;
        _kutu.KeyDown += TusaBasildi;
        _gecikme.Tick += (_, _) =>
        {
            _gecikme.Stop();
            Baslat(_kutu.Text);
        };
    }

    /// <summary>Arama bitti ve sonuc var.</summary>
    internal event EventHandler<(string Metin, AramaSonucu Sonuc)>? Bitti;

    /// <summary>Kutu bosaltildi; gezinmeye donulmeli.</summary>
    internal event EventHandler? Bosaltildi;

    /// <summary>Ekranda gosterilecek durum cumlesi.</summary>
    internal event EventHandler<string>? Durum;

    /// <summary>Arama kosuyor mu; agaci kilitlemek icin.</summary>
    internal event EventHandler<bool>? Mesgul;

    /// <summary>Icinde arama yapilacak kok. null ise arama yapilmaz.</summary>
    internal string? Kok { get; set; }

    /// <summary>Kutuyu KOD ile bosaltir; arama tetiklenmez.</summary>
    internal void MetniTemizle()
    {
        _metniKodDegistiriyor = true;
        _kutu.Text = string.Empty;
        _metniKodDegistiriyor = false;
        _gecikme.Stop();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gecikme.Stop();
        _gecikme.Dispose();
        _iptal?.Cancel();
        _iptal?.Dispose();
    }

    private void MetinDegisti(object? gonderen, EventArgs e)
    {
        if (_metniKodDegistiriyor)
        {
            return;
        }

        // Kutu bosaltildiysa beklemeye gerek yok: hemen gezinmeye don.
        if (string.IsNullOrWhiteSpace(_kutu.Text))
        {
            _gecikme.Stop();
            Baslat(string.Empty);
            return;
        }

        _gecikme.Stop();
        _gecikme.Start();
    }

    private void TusaBasildi(object? gonderen, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;   // Windows'un uyari sesini bastirir
        _gecikme.Stop();             // beklemeden, hemen
        Baslat(_kutu.Text);
    }

    private void Baslat(string metin)
    {
        string? kok = Kok;
        if (kok is null)
        {
            Durum?.Invoke(this, "Önce bir klasör açın.");
            return;
        }

        // Yeniden giris kilidi: onceki arama HER ZAMAN once iptal edilir
        // (CLAUDE.md 6). Aksi halde iki arama ayni agaca yazar.
        _iptal?.Cancel();
        _iptal?.Dispose();

        if (string.IsNullOrWhiteSpace(metin))
        {
            _iptal = null;
            Mesgul?.Invoke(this, false);
            Bosaltildi?.Invoke(this, EventArgs.Empty);
            return;
        }

        var kaynak = new CancellationTokenSource();
        _iptal = kaynak;
        CancellationToken belirtec = kaynak.Token;

        Durum?.Invoke(this, "Aranıyor…");
        Mesgul?.Invoke(this, true);

        Task.Run(
            () => KlasorTarayici.Ara(kok, metin, Sinir, belirtec,
                (klasor, eslesme) => Ilerleme(belirtec, klasor, eslesme)),
            belirtec)
            .ContinueWith(is_ => Sonuclandi(is_, metin, belirtec), TaskScheduler.Default);
    }

    private void Ilerleme(CancellationToken belirtec, int taranan, int eslesme)
    {
        // Her klasorde mesaj yollamak arayuzu bogar; ellide bir yeter.
        // CLAUDE.md 3: uydurma yuzde YOK - sayilabilen sey sayiliyor.
        if (taranan % 50 != 0)
        {
            return;
        }

        ArayuzeYolla(belirtec,
            () => Durum?.Invoke(this, $"Aranıyor… {taranan} klasör · {eslesme} eşleşme"));
    }

    private void Sonuclandi(Task<AramaSonucu> is_, string metin, CancellationToken belirtec)
    {
        ArayuzeYolla(belirtec, () =>
        {
            Mesgul?.Invoke(this, false);

            if (is_.IsFaulted)
            {
                // Sessiz basarisizlik YASAK (CLAUDE.md 3).
                Durum?.Invoke(this, "Arama başarısız: "
                    + (is_.Exception?.GetBaseException().Message ?? "bilinmeyen sebep"));
                return;
            }

            if (is_.IsCanceled)
            {
                return;
            }

            Bitti?.Invoke(this, (metin, is_.Result));
        });
    }

    /// <summary>
    /// Arayuz is parcacigina gecer. Pencere kapandiysa ya da arama iptal
    /// edildiyse hicbir sey yapmaz - kapanmis pencereye yazmak coker.
    /// </summary>
    private void ArayuzeYolla(CancellationToken belirtec, Action is_)
    {
        if (belirtec.IsCancellationRequested || _arayuz.IsDisposed || !_arayuz.IsHandleCreated)
        {
            return;
        }

        try
        {
            _arayuz.BeginInvoke(is_);
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
}
