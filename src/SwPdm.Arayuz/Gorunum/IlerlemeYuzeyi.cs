using System;
using System.Threading;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// ILERLEME GOSTERIMININ TEK KAPISI. Alttaki cubuk, sayac ve iptal dugmesine
/// dair her sey burada (CLAUDE.md 1b).
///
/// Isin kendisi ARKA PLANDA kosuyor; buradaki her cagri arayuz is
/// parcacigina gecirilir. Sebep olculmus (CLAUDE.md 6): ilerleme cubugu, is
/// arayuzu bloke ederse HIC CIZILMEZ - kullanici bos bir oluk gorur ve
/// uygulamayi donmus sanir.
/// </summary>
internal sealed class IlerlemeYuzeyi : IIlerlemeYuzeyi
{
    private readonly Control _arayuz;
    private readonly DurumCubugu _durum;
    private readonly Action<bool> _mesgul;

    private CancellationTokenSource? _iptal;

    internal IlerlemeYuzeyi(Control arayuz, DurumCubugu durum, Action<bool> mesgul)
    {
        _arayuz = arayuz;
        _durum = durum;
        _mesgul = mesgul;
        _durum.IptalIstendi += (_, _) =>
        {
            _iptal?.Cancel();
            _durum.IptalBekleniyor();
        };
    }

    /// <inheritdoc/>
    public void Basladi(int toplam, CancellationTokenSource iptal)
    {
        _iptal = iptal;
        Arayuzde(() =>
        {
            _durum.IsBasladi(toplam);
            _mesgul(true);
        });
    }

    /// <inheritdoc/>
    public void Adim(int yapilan, int toplam, string ad)
        => Arayuzde(() => _durum.Ilerleme(yapilan, toplam, ad));

    /// <inheritdoc/>
    public void Bitti(Action arayuzdeCalistir)
        => Arayuzde(() =>
        {
            _durum.IsBitti();
            _mesgul(false);
            _iptal = null;
            arayuzdeCalistir();
        });

    /// <summary>
    /// Arayuz is parcacigina gecer. Pencere kapandiysa hicbir sey yapmaz -
    /// kapanmis pencereye yazmak coker.
    /// </summary>
    private void Arayuzde(Action is_)
    {
        if (_arayuz.IsDisposed || !_arayuz.IsHandleCreated)
        {
            return;
        }

        try
        {
            if (_arayuz.InvokeRequired)
            {
                _arayuz.BeginInvoke(is_);
            }
            else
            {
                is_();
            }
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
