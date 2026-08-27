using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// SAG TIK MENUSU. Menuyu <see cref="AgacIslemleri.Tumu"/>'den URETIR ve
/// hicbir islemi ADIYLA bilmez (CLAUDE.md 1b) - islem eklendiginde/
/// kaldirildiginda bu dosya degismez.
///
/// Kisayollari da ayni listeden kurar; boylece menudeki yazi ile calisan tus
/// AYRISAMAZ.
/// </summary>
internal sealed class AgacMenusu
{
    private readonly SecimliAgac _agac;
    private readonly ContextMenuStrip _menu = new();
    private readonly Dictionary<ToolStripMenuItem, IAgacIslemi> _islemler = [];

    private Func<SecimBaglami>? _secimKaynagi;

    internal AgacMenusu(SecimliAgac agac)
    {
        _agac = agac;

        foreach (IAgacIslemi? islem in AgacIslemleri.Tumu)
        {
            if (islem is null)
            {
                _menu.Items.Add(new ToolStripSeparator());
                continue;
            }

            var oge = new ToolStripMenuItem(islem.Ad)
            {
                ShortcutKeys = islem.Kisayol,
                ShowShortcutKeys = islem.Kisayol != Keys.None,
            };
            oge.Click += (_, _) => Calistir(islem);
            _islemler[oge] = islem;
            _menu.Items.Add(oge);
        }

        _menu.Opening += MenuAcilirken;
        _agac.ContextMenuStrip = _menu;
    }

    /// <summary>Islem bittiginde agac tazelenir; yol verilirse orasi secilir.</summary>
    internal event EventHandler<string?>? Tazele;

    /// <summary>Durum cubuguna yazilacak cumle.</summary>
    internal event EventHandler<string>? Durum;

    /// <summary>Secimi nereden okuyacagini soyler.</summary>
    internal void SecimKaynagi(Func<SecimBaglami> kaynak) => _secimKaynagi = kaynak;

    /// <summary>
    /// Bir tusa basildi; listedeki bir islemin kisayoluysa calistirir.
    /// Doner deger: islendi mi.
    /// </summary>
    internal bool TusaBasildi(Keys tuslar)
    {
        foreach (IAgacIslemi? islem in AgacIslemleri.Tumu)
        {
            if (islem is not null && islem.Kisayol != Keys.None && islem.Kisayol == tuslar)
            {
                Calistir(islem);
                return true;
            }
        }

        return false;
    }

    private void MenuAcilirken(object? gonderen, System.ComponentModel.CancelEventArgs e)
    {
        SecimBaglami secim = Secim();

        foreach ((ToolStripMenuItem oge, IAgacIslemi islem) in _islemler)
        {
            bool olur = islem.Uygulanabilir(secim, out string neden);

            // CLAUDE.md 3: uygulanamayan oge GIZLENMEZ. Gizlemek "boyle bir sey
            // yok" demektir; gri durup sebebini soylemek dogrudur.
            oge.Enabled = olur;
            oge.ToolTipText = olur ? string.Empty : neden;
        }
    }

    private SecimBaglami Secim()
        => _secimKaynagi?.Invoke() ?? new SecimBaglami([], null, AramaKipinde: false);

    private void Calistir(IAgacIslemi islem)
    {
        SecimBaglami secim = Secim();

        if (!islem.Uygulanabilir(secim, out string neden))
        {
            // Kisayolla gelindiyse menu gorunmedi; sebep yine SOYLENIR.
            Durum?.Invoke(this, neden);
            return;
        }

        islem.Uygula(new IslemBaglami(
            Sahip: _agac.FindForm() ?? (IWin32Window)_agac,
            Secim: secim,
            Tazele: yol => Tazele?.Invoke(this, yol),
            Bildir: cumle => Durum?.Invoke(this, cumle)));
    }
}
