using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Tur suzgeci seridi: Tumu / Montaj / Parca / Teknik resim / PDF.
///
/// Etiketler ile TURLER burada birlikte duruyor; boylece disarida
/// "Montaj yazisi hangi ture karsilik geliyordu" diye metin esleme yapilmiyor
/// (CLAUDE.md 8: ayni bilginin ikinci kopyasi yazilmaz).
/// </summary>
internal sealed class SuzgecSeridi : FlowLayoutPanel
{
    private readonly List<Button> _dugmeler = [];
    private Button? _secili;

    internal SuzgecSeridi(params (string Etiket, DosyaTuru? Tur)[] secenekler)
    {
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = false;
        AutoSize = false;
        Height = 28;
        Padding = new Padding(4, 2, 4, 2);
        BackColor = Renkler.GovdeArkaPlan;

        foreach ((string etiket, DosyaTuru? tur) in secenekler)
        {
            Button d = Dugme(etiket);
            d.Tag = tur;
            _dugmeler.Add(d);
            Controls.Add(d);
        }

        if (_dugmeler.Count > 0)
        {
            Sec(_dugmeler[0]);
        }
    }

    /// <summary>Secim degistiginde tetiklenir. null = butun turler.</summary>
    internal event EventHandler<DosyaTuru?>? SecimDegisti;

    /// <summary>Su an secili tur. null = butun turler.</summary>
    internal DosyaTuru? SeciliTur => _secili?.Tag as DosyaTuru?;

    private Button Dugme(string etiket)
    {
        var d = new Button
        {
            Text = etiket,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            BackColor = Renkler.GovdeArkaPlan,
            ForeColor = Renkler.SuzgecYazi,
            Margin = new Padding(1, 0, 1, 0),
            Padding = new Padding(6, 1, 6, 1),
            TabStop = false,
        };
        d.FlatAppearance.BorderSize = 0;
        d.Click += (_, _) => Sec(d);
        return d;
    }

    private void Sec(Button d)
    {
        if (ReferenceEquals(_secili, d))
        {
            return;
        }

        if (_secili is not null)
        {
            _secili.BackColor = Renkler.GovdeArkaPlan;
            _secili.FlatAppearance.BorderSize = 0;
        }

        _secili = d;
        d.BackColor = Renkler.SuzgecSeciliArkaPlan;
        d.FlatAppearance.BorderSize = 1;
        d.FlatAppearance.BorderColor = Renkler.SuzgecSeciliKenar;

        SecimDegisti?.Invoke(this, d.Tag as DosyaTuru?);
    }
}
