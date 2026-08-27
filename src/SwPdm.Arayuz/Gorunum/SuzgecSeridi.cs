using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Tur suzgeci seridi: "Tumu" + cekirdekteki her tur.
///
/// Dugmeler <see cref="DosyaTurleri.Tumu"/>'den URETILIYOR - burada elle
/// yazilmis bir tur listesi YOK (CLAUDE.md 1b). Yeni bir tur eklendiginde
/// dugmesi kendiliginden cikar, bir tur kaldirildiginda kendiliginden gider;
/// bu dosya degismez.
/// </summary>
internal sealed class SuzgecSeridi : FlowLayoutPanel
{
    private readonly List<Button> _dugmeler = [];
    private Button? _secili;

    internal SuzgecSeridi()
    {
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = false;
        AutoSize = false;
        Height = 28;
        Padding = new Padding(4, 2, 4, 2);
        BackColor = Renkler.GovdeArkaPlan;

        Ekle("Tümü", null);
        foreach (DosyaTuru tur in DosyaTurleri.Turler())
        {
            Ekle(DosyaTurleri.Adi(tur), tur);
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

    private void Ekle(string etiket, DosyaTuru? tur)
    {
        Button d = Dugme(etiket);
        d.Tag = tur;
        _dugmeler.Add(d);
        Controls.Add(d);
    }

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

        // ================== BU SATIR BIR KEZ SILINDI ==================
        // Bu baglanti olmadan dugmeler CIZILIYOR, odagi aliyor, uzerine
        // gelince renk degistiriyor - ama TIKLAMA HICBIR SEY YAPMIYOR.
        // Erkan'in "Montaj/Parca/Teknik resim/PDF tepki vermiyor" dedigi sey
        // tam olarak buydu ve sebebi bir WinForms tuzagi degil, bu eksik
        // satirdi. Nasil silindigi CLAUDE.md 8'de yaziyor.
        // ==============================================================
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
