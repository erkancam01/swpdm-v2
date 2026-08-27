using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YOL CUBUGU - agacin hemen ustunde, hangi klasorde oldugunu gosterir ve
/// TIKLANABILIR: bir parcaya tiklayinca agac oraya gider.
///
/// Once bu bilgi en altta, durum cubugunda duruyordu; goz agacin ustunde
/// oldugu icin orada okunmuyordu ve tiklanamiyordu.
///
/// KOKUN USTUNDEKI parcalar (surucu, ust klasorler) SOLUK ve tiklanamaz:
/// onlar agacin disinda kaliyor. Tiklanabilir gostermek, tiklayinca hicbir
/// sey olmamasi demek olurdu (CLAUDE.md 3). Sebep ipucunda yaziyor.
/// </summary>
internal sealed class YolCubugu : FlowLayoutPanel
{
    /// <summary>Sigmayinca soldan bu kadar parca atilir ve "…" konur.</summary>
    private const string Kirpma = "…";

    private string? _kok;
    private string? _sonKlasor;

    internal YolCubugu()
    {
        // CLAUDE.md 6: boyut degistiren atamalardan once alan kalmadi;
        // bu sinifin kendi alani yok, guvenli.
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = false;
        AutoSize = false;
        Height = 26;
        Padding = new Padding(6, 3, 6, 3);
        BackColor = Renkler.GovdeArkaPlan;
        Visible = false;
    }

    /// <summary>Bir klasor parcasina tiklandi; agac oraya gitmeli.</summary>
    internal event EventHandler<string>? Secildi;

    /// <summary>Kok degisti.</summary>
    internal void KokuKur(string? kok)
    {
        _kok = kok;
        Goster(kok);
    }

    /// <summary>
    /// Cubugu verilen klasore gore kurar. null ise gizlenir - bos bir serit
    /// yer kaplamasin.
    /// </summary>
    internal void Goster(string? klasor)
    {
        _sonKlasor = klasor;
        SuspendLayout();
        foreach (Control eski in Controls)
        {
            eski.Dispose();
        }

        Controls.Clear();

        if (string.IsNullOrWhiteSpace(klasor))
        {
            Visible = false;
            ResumeLayout(performLayout: true);
            return;
        }

        Visible = true;

        (string Ad, string? Yol)[] parcalar = Parcala(klasor);
        int ilk = SigacakIlkParca(parcalar);

        if (ilk > 0)
        {
            Controls.Add(Etiket(Kirpma, null, "Yol kısaltıldı"));
            Controls.Add(Ayirac());
        }

        for (int i = ilk; i < parcalar.Length; i++)
        {
            if (i > ilk || ilk > 0)
            {
                if (i > ilk)
                {
                    Controls.Add(Ayirac());
                }
            }

            (string ad, string? yol) = parcalar[i];
            Controls.Add(yol is null
                ? Etiket(ad, null, "Kök klasörün dışında — buraya gitmek için \"Klasör aç\"")
                : Etiket(ad, yol, yol));
        }

        ResumeLayout(performLayout: true);
    }

    /// <summary>
    /// Yolu parcalara ayirir. Kokun ALTINDA kalan parcalarin yolu doludur
    /// (tiklanabilir); ustundekilerin yolu null'dur (soluk).
    /// </summary>
    private (string Ad, string? Yol)[] Parcala(string klasor)
    {
        var parcalar = new List<(string, string?)>();
        string kokAdi = _kok is null ? string.Empty : WindowsYolu.DosyaAdi(_kok);

        // Kokten asagisi: kokun uzunlugundan sonrasini bolumlere ayir.
        bool icerde = _kok is not null
            && klasor.StartsWith(_kok, StringComparison.OrdinalIgnoreCase);

        if (icerde && _kok is not null)
        {
            parcalar.Add((kokAdi.Length > 0 ? kokAdi : _kok, _kok));

            string kalan = klasor[_kok.Length..]
                .Trim(WindowsYolu.Ayirici, WindowsYolu.EgikAyirici);

            if (kalan.Length > 0)
            {
                string yol = _kok;
                foreach (string ad in kalan.Split(
                    new[] { WindowsYolu.Ayirici, WindowsYolu.EgikAyirici },
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    yol = WindowsYolu.Birlestir(yol, ad);
                    parcalar.Add((ad, yol));
                }
            }

            // Kokun USTU: soluk, tiklanamaz. Basa ekleniyor.
            string ust = WindowsYolu.Klasor(_kok);
            var ustler = new List<(string, string?)>();
            while (ust.Length > 0)
            {
                string ad = WindowsYolu.DosyaAdi(ust);
                ustler.Insert(0, (ad.Length > 0 ? ad : ust, null));
                string yeni = WindowsYolu.Klasor(ust);
                if (string.Equals(yeni, ust, StringComparison.Ordinal))
                {
                    break;
                }

                ust = yeni;
            }

            parcalar.InsertRange(0, ustler);
            return [.. parcalar];
        }

        // Kok bilinmiyorsa hepsi soluk.
        foreach (string ad in klasor.Split(
            new[] { WindowsYolu.Ayirici, WindowsYolu.EgikAyirici },
            StringSplitOptions.RemoveEmptyEntries))
        {
            parcalar.Add((ad, null));
        }

        return [.. parcalar];
    }

    /// <summary>
    /// Sigmayan yollarda SOLDAN kirpar: kullanicinin en cok ilgilendigi yer
    /// yolun SONU. Kirpilan yerde "…" durur.
    /// </summary>
    private int SigacakIlkParca((string Ad, string? Yol)[] parcalar)
    {
        int kullanilabilir = ClientSize.Width - Padding.Horizontal - 24;
        if (kullanilabilir <= 0)
        {
            return 0;
        }

        int genislik = 0;
        for (int i = parcalar.Length - 1; i >= 0; i--)
        {
            genislik += TextRenderer.MeasureText(parcalar[i].Ad, Font).Width + 18;
            if (genislik > kullanilabilir)
            {
                return Math.Min(i + 1, parcalar.Length - 1);
            }
        }

        return 0;
    }

    private Control Etiket(string yazi, string? yol, string ipucu)
    {
        var etiket = new Label
        {
            Text = yazi,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
            ForeColor = yol is null ? Renkler.UstBilgiYazi : Renkler.SuzgecYazi,
            Cursor = yol is null ? Cursors.Default : Cursors.Hand,
        };

        var ipucuKutusu = new ToolTip();
        ipucuKutusu.SetToolTip(etiket, ipucu);

        if (yol is not null)
        {
            etiket.Click += (_, _) => Secildi?.Invoke(this, yol);
            etiket.MouseEnter += (_, _) => etiket.Font = new Font(Font, FontStyle.Underline);
            etiket.MouseLeave += (_, _) => etiket.Font = new Font(Font, FontStyle.Regular);
        }

        return etiket;
    }

    private static Control Ayirac() => new Label
    {
        Text = "›",
        AutoSize = true,
        Margin = new Padding(4, 2, 4, 0),
        ForeColor = Renkler.UstBilgiYazi,
    };

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // Pencere daralinca yol yeniden kirpilmali.
        if (Visible && Controls.Count > 0)
        {
            Goster(_sonKlasor);
        }
    }

}
