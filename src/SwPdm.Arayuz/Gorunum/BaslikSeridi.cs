using System;
using System.Drawing;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Pencerenin ust koyu seridi: solda urun adi, ortada SOLIDWORKS durumu,
/// sagda raptiye ve ayar dugmeleri.
///
/// Yalnizca GORUNUM. Durum isareti disaridan set edilir; bu sinif SOLIDWORKS'e
/// bakmaz. CLAUDE.md 3: uydurma durum gosterilmez - varsayilan "kapali" cunku
/// henuz kimse bakmiyor.
/// </summary>
internal sealed class BaslikSeridi : Control
{
    private readonly Button _raptiye;
    private readonly Button _ayarlar;
    private string _durumMetni = "SOLIDWORKS: kapalı";
    private bool _bagli;

    internal BaslikSeridi()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Renkler.BaslikArkaPlan;
        ForeColor = Renkler.BaslikYazi;
        Height = 32;

        _ayarlar = SeritDugmesi(Simgeler.Disli(Renkler.BaslikYazi), "Ayarlar");
        _raptiye = SeritDugmesi(Simgeler.Raptiye(Renkler.BaslikYazi), "Pencereyi üstte tut");
        _raptiye.BackColor = Renkler.BaslikDugmeVurgu;

        Controls.Add(_ayarlar);
        Controls.Add(_raptiye);
    }

    /// <summary>Raptiye dugmesi. Davranisi disarida baglanir.</summary>
    internal Button RaptiyeDugmesi => _raptiye;

    /// <summary>Ayar dugmesi. Davranisi disarida baglanir.</summary>
    internal Button AyarDugmesi => _ayarlar;

    /// <summary>SOLIDWORKS baglantisinin bilinen durumu. Kimse set etmezse kapali kalir.</summary>
    internal bool Bagli
    {
        get => _bagli;
        set
        {
            if (_bagli == value)
            {
                return;
            }

            _bagli = value;
            _durumMetni = value ? "SOLIDWORKS: açık" : "SOLIDWORKS: kapalı";
            Invalidate();
        }
    }

    private static Button SeritDugmesi(Image simge, string ipucu)
    {
        var d = new Button
        {
            Size = new Size(30, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Renkler.BaslikArkaPlan,
            Image = simge,
            TabStop = false,
        };
        d.FlatAppearance.BorderSize = 0;
        d.FlatAppearance.MouseOverBackColor = Renkler.BaslikDugmeUzerinde;
        new ToolTip().SetToolTip(d, ipucu);
        return d;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        const int bosluk = 4;
        int sag = Width - bosluk;
        int y = (Height - _ayarlar.Height) / 2;

        _ayarlar.Location = new Point(sag - _ayarlar.Width, y);
        sag -= _ayarlar.Width + 2;
        _raptiye.Location = new Point(sag - _raptiye.Width, y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.Clear(BackColor);

        using var kalinYazi = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
        using var urunFircasi = new SolidBrush(Renkler.BaslikYazi);
        using var durumFircasi = new SolidBrush(Renkler.BaslikSolukYazi);

        SizeF urunBoyu = g.MeasureString("SW PDM", kalinYazi);
        g.DrawString("SW PDM", kalinYazi, urunFircasi, 10f, (Height - urunBoyu.Height) / 2f);

        // Ortadaki durum: kucuk daire + metin, ikisi birlikte ortalanir.
        const float daireCap = 9f;
        const float daireBosluk = 6f;
        SizeF durumBoyu = g.MeasureString(_durumMetni, Font);
        float toplam = daireCap + daireBosluk + durumBoyu.Width;
        float x = (Width - toplam) / 2f;
        float dy = (Height - daireCap) / 2f;

        Color daireRengi = _bagli ? Renkler.DurumAcik : Renkler.DurumKapali;
        if (_bagli)
        {
            using var doluDaire = new SolidBrush(daireRengi);
            g.FillEllipse(doluDaire, x, dy, daireCap, daireCap);
        }
        else
        {
            using var bosDaire = new Pen(daireRengi, 1.4f);
            g.DrawEllipse(bosDaire, x, dy, daireCap, daireCap);
        }

        g.DrawString(_durumMetni, Font, durumFircasi,
            x + daireCap + daireBosluk, (Height - durumBoyu.Height) / 2f);
    }
}
