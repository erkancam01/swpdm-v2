using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Sol alt panel: ustte ONIZLENEN dosyanin adi, altinda onizleme resmi.
/// Yalnizca GORUNUM; hicbir dosya okumaz.
///
/// SADELESTI (Erkan, 30.08.2026): altta duran bilgi blogu (Tur, Boyut,
/// Tarih, Kullandigi, Kullanan) KALDIRILDI - "gerek yok". Ayni bilgiler
/// zaten durum cubugunda ve referans panelinin bolum basliklarinda.
/// </summary>
internal sealed class OnizlemePaneli : TableLayoutPanel
{
    private readonly PictureBox _resim;
    private readonly Panel _yuva;
    private readonly Label _baslik;

    internal OnizlemePaneli()
    {
        ColumnCount = 1;
        RowCount = 2;
        Dock = DockStyle.Fill;
        BackColor = Renkler.GovdeArkaPlan;
        Padding = new Padding(8, 4, 8, 4);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // BASLIK: NEYE BAKTIGIN. Bilgi blogu kalkinca onizlenen dosyanin
        // adini yazan tek yer burasi kaldi - o yuzden komsu gosteriminde de
        // KOMSUNUN adini tasiyor, cipanin degil.
        // ToolTip BILEREK yok: Wine'da tiklamayi yiyor (CLAUDE.md 6).
        _baslik = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Renkler.BolumBasligiYazi,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoEllipsis = true,
            Margin = new Padding(0, 0, 0, 2),
        };
        _baslik.Click += (_, _) =>
        {
            if (_geriDonulebilir)
            {
                BasligaTiklandi?.Invoke(this, EventArgs.Empty);
            }
        };

        _resim = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Renkler.OnizlemeArkaPlan,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
        };

        // YUVA: resim kutusu ile 3B denetimi AYNI hucreyi paylasir; ikisi de
        // yuvaya dolar, hangisinin gorundugune UcBoyutlu karar verir.
        // (TableLayoutPanel ayni hucreye iki denetim kabul etmiyor.)
        _yuva = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
        };
        _yuva.Controls.Add(_resim);

        Controls.Add(_baslik, 0, 0);
        Controls.Add(_yuva, 0, 1);
    }

    /// <summary>
    /// 3B denetimin yerlestirilecegi yuva. Denetimi buraya KOYAN ve suren
    /// UcBoyutluGorunum; panel yalnizca yeri verir (CLAUDE.md 1b).
    /// </summary>
    internal Control UcBoyutluYuvasi => _yuva;

    /// <summary>
    /// 2B kutu ile 3B denetim arasinda gecis. null = 2B resim gorunur.
    /// </summary>
    internal void UcBoyutlu(Control? denetim)
    {
        _resim.Visible = denetim is null;
        if (denetim is not null)
        {
            denetim.Visible = true;
            denetim.BringToFront();
        }
        else
        {
            foreach (Control c in _yuva.Controls)
            {
                if (!ReferenceEquals(c, _resim))
                {
                    c.Visible = false;
                }
            }
        }
    }

    /// <summary>Baslik tiklandi - cipaya donulmek isteniyor.</summary>
    internal event EventHandler? BasligaTiklandi;

    private bool _geriDonulebilir;

    /// <summary>
    /// Basligi yazar - <paramref name="ad"/> HER ZAMAN o an ONIZLENEN
    /// dosyanindir. <paramref name="geriDonulebilir"/> true ise bu bir KOMSU
    /// gosterimidir: basa "◂" konur, el imleci ve vurgu rengi gelir;
    /// tiklaninca <see cref="BasligaTiklandi"/> atesler ve cipaya donulur.
    /// </summary>
    internal void BasligiYaz(string ad, bool geriDonulebilir)
    {
        _geriDonulebilir = geriDonulebilir;
        _baslik.Text = geriDonulebilir ? "◂ " + ad : ad;
        _baslik.Cursor = geriDonulebilir ? Cursors.Hand : Cursors.Default;
        _baslik.ForeColor = geriDonulebilir
            ? Renkler.ReferansAsagiYazi
            : Renkler.BolumBasligiYazi;
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

    /// <summary>Secim yokken paneli bosaltir; uydurma deger birakmaz.</summary>
    internal void Temizle()
    {
        _resim.Image = null;
        _baslik.Text = string.Empty;
    }
}
