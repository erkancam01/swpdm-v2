using System;
using System.Drawing;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// ONIZLEME'NIN 3B KIPI - surucunun eDrawings tarafina baglanan yarisi.
/// Ayri dosya cunku ayri konu (AnaForm'un uc parcasiyla ayni kalip) ve
/// boyut kapisi Onizleme.cs'i 604 satirda YAKALADI. eDrawings'in kendisi
/// <see cref="UcBoyutluGorunum"/>'da; ozelligi silmek = o dosya + bu dosya
/// + Goster/KomsuGoster'deki uc satirlik dallanma (CLAUDE.md 1b).
/// </summary>
internal sealed partial class Onizleme
{
    /// <summary>Ayar: 3B kip acik mi. Lambda - ayar aninda etkir.</summary>
    private readonly Func<bool> _ucBoyutluMu;

    /// <summary>eDrawings tarafi; ilk 3B istekte kurulur.</summary>
    private UcBoyutluGorunum? _ucBoyutlu;
    private bool _ucBoyutluSoylendi;

    /// <summary>3B kur/ac tutmadiginda sebep buradan duyurulur (durum cubugu).</summary>
    internal event EventHandler<string>? Durum;

    /// <summary>
    /// 3B gorunumun actigi belgeyi birakir - DOSYA KILIDI icin (CLAUDE.md 1a):
    /// eDrawings actigi dosyayi tutar; tasima/ad degistirme/onarim o kilide
    /// carpardi. AnaForm bunu HER islemin basina baglar.
    /// </summary>
    internal void BelgeyiBirak() => _ucBoyutlu?.BelgeyiKapat();

    /// <summary>
    /// 3B kipte ve dosya SOLIDWORKS turindeyse eDrawings'te acar.
    /// Doner: gosterim 3B'ye gecti mi (2B boru hattina gerek kalmadi).
    /// Tutmazsa sebep <see cref="Durum"/>'a yazilir ve 2B devam eder -
    /// sessiz dusus yok (CLAUDE.md 3).
    /// </summary>
    private bool UcBoyutluDene(string yol)
    {
        if (!_ucBoyutluMu() || !SwReferans.TasiyabilirMi(yol))
        {
            UcBoyutluGizle();
            return false;
        }

        _ucBoyutlu ??= new UcBoyutluGorunum();
        if (!_ucBoyutlu.Kur(_panel.UcBoyutluYuvasi))
        {
            // Sebep BIR kez soylenir; Kur zaten yeniden denemiyor ve her
            // secimde ayni cumle durum cubugunu ise yaramaz yapardi.
            if (!_ucBoyutluSoylendi)
            {
                _ucBoyutluSoylendi = true;
                Durum?.Invoke(
                    this, _ucBoyutlu.KurulamamaSebebi + " — 2B önizleme kullanılıyor.");
            }

            UcBoyutluGizle();
            return false;
        }

        if (!_ucBoyutlu.Ac(yol, out string? sebep))
        {
            Durum?.Invoke(this, sebep + " — bu dosya için 2B önizleme gösteriliyor.");
            UcBoyutluGizle();
            return false;
        }

        _panel.UcBoyutlu(_ucBoyutlu.Denetim);
        return true;
    }

    /// <summary>3B denetimi gizler, belgeyi birakir; 2B kutu geri gelir.</summary>
    private void UcBoyutluGizle()
    {
        if (_ucBoyutlu?.Denetim is not null)
        {
            _panel.UcBoyutlu(null);
            _ucBoyutlu.BelgeyiKapat();
        }
    }

    /// <summary>
    /// Yalnizca OZELLIK satirini arka planda ister - 3B kipte resim boru
    /// hatti kosmaz ama belge ozellikleri (Malzeme, Kaydeden...) yine
    /// gorunmeli; 2B'de vardi, 3B'ye gecince kaybolmasi gerileme olurdu.
    /// </summary>
    private void OzellikleriIste(string yol)
    {
        lock (_kilit)
        {
            _bekleyen = (yol, Size.Empty, true);
        }

        _uyandir.Release();
    }
}
