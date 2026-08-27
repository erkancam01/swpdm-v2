using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Tur suzgeci seridi: Tumu / Montaj / Parca / Teknik resim / PDF.
///
/// Yalnizca GORUNUM. Bir dugmeye basmak SADECE secili gorunumu degistirir;
/// hicbir sey suzulmez. Suzme geldiginde <see cref="SecimDegisti"/> baglanacak.
/// </summary>
internal sealed class SuzgecSeridi : FlowLayoutPanel
{
    private readonly List<Button> _dugmeler = [];
    private Button? _secili;

    internal SuzgecSeridi(params string[] etiketler)
    {
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = false;
        AutoSize = false;
        Height = 28;
        Padding = new Padding(4, 2, 4, 2);
        BackColor = Renkler.GovdeArkaPlan;

        foreach (string etiket in etiketler)
        {
            Button d = Dugme(etiket);
            _dugmeler.Add(d);
            Controls.Add(d);
        }

        if (_dugmeler.Count > 0)
        {
            Sec(_dugmeler[0]);
        }
    }

    /// <summary>Secili suzgecin etiketi degistiginde tetiklenir.</summary>
    internal event EventHandler<string>? SecimDegisti;

    /// <summary>Su an secili olan suzgecin etiketi.</summary>
    internal string SeciliEtiket => _secili?.Text ?? string.Empty;

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

        SecimDegisti?.Invoke(this, d.Text);
    }
}
