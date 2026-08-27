using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Sol alt panel: onizleme resmi ve altinda dosya ust bilgisi.
/// Yalnizca GORUNUM; hicbir dosya okumaz.
/// </summary>
internal sealed class OnizlemePaneli : TableLayoutPanel
{
    private readonly PictureBox _resim;
    private readonly Label _ad;
    private readonly Label _tur;
    private readonly Label _boyut;
    private readonly Label _degistirme;
    private readonly Label _kullanan;

    internal OnizlemePaneli()
    {
        ColumnCount = 1;
        RowCount = 2;
        Dock = DockStyle.Fill;
        BackColor = Renkler.GovdeArkaPlan;
        Padding = new Padding(8, 4, 8, 4);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _resim = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Renkler.OnizlemeArkaPlan,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 0, 6),
        };

        var bilgi = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };

        _ad = BilgiEtiketi();
        _tur = BilgiEtiketi();
        _boyut = BilgiEtiketi();
        _degistirme = BilgiEtiketi();
        _kullanan = BilgiEtiketi();
        bilgi.Controls.AddRange([_ad, _tur, _boyut, _degistirme, _kullanan]);

        Controls.Add(_resim, 0, 0);
        Controls.Add(bilgi, 0, 1);
    }

    /// <summary>Onizleme kutusunun su anki boyutu; istenecek resim olcusu.</summary>
    internal Size KutuBoyutu => _resim.ClientSize;

    /// <summary>Onizleme resmi. null verilirse kutu bos kalir.</summary>
    internal Image? Onizleme
    {
        get => _resim.Image;
        set
        {
            _resim.Image?.Dispose();   // eski resim GDI nesnesi tutar
            _resim.Image = value;
        }
    }

    /// <summary>
    /// Kutunun ORTASINA bir cumle yazar.
    ///
    /// CLAUDE.md 3: bos bir kutu "bu dosyanin onizlemesi yok" demek DEGILDIR -
    /// yukleniyor da olabilir, okunamamis da. Sebebi yazmak, kullanicinin
    /// yanlis sonuc cikarmasini engelliyor. Yeni bir denetim eklemiyoruz:
    /// tasarim degismesin diye yazi resmin icine ciziliyor.
    /// </summary>
    internal void MesajGoster(string cumle)
    {
        int genislik = Math.Max(_resim.ClientSize.Width, 120);
        int yukseklik = Math.Max(_resim.ClientSize.Height, 60);

        var bmp = new Bitmap(genislik, yukseklik);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Renkler.OnizlemeArkaPlan);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var firca = new SolidBrush(Renkler.UstBilgiYazi);
            using var bicim = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };

            g.DrawString(cumle, Font, firca, new RectangleF(6, 6, genislik - 12, yukseklik - 12), bicim);
        }

        Onizleme = bmp;
    }

    /// <summary>
    /// Ust bilgi satirlarini yazar.
    ///
    /// DIKKAT - CLAUDE.md 3: <paramref name="kullanan"/> bilerek METIN, sayi degil.
    /// Tarama yapilmadiysa buraya "0" YAZILMAZ; "taranmadı" yazilir. Bos liste
    /// "yok" demek degildir ve v1'de bu ayrim saglam dosya sildirebiliyordu.
    /// </summary>
    internal void UstBilgiyiYaz(string ad, string tur, string boyut, string degistirme, string kullanan)
    {
        _ad.Text = ad;
        _tur.Text = "Tür: " + tur;
        _boyut.Text = "Boyut: " + boyut;
        _degistirme.Text = "Değiştirme: " + degistirme;
        _kullanan.Text = "Kullanan: " + kullanan;
    }

    /// <summary>Secim yokken paneli bosaltir; uydurma deger birakmaz.</summary>
    internal void Temizle()
    {
        _resim.Image = null;
        _ad.Text = string.Empty;
        _tur.Text = string.Empty;
        _boyut.Text = string.Empty;
        _degistirme.Text = string.Empty;
        _kullanan.Text = string.Empty;
    }

    private static Label BilgiEtiketi() => new()
    {
        AutoSize = true,
        ForeColor = Renkler.UstBilgiYazi,
        Margin = new Padding(0, 0, 0, 1),
    };
}
