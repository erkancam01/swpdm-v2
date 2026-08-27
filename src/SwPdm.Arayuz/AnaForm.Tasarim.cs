using System;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Arayuz.Gorunum;

namespace SwPdm.Arayuz;

internal sealed partial class AnaForm
{
    private ImageList _simgeler = null!;
    private BaslikSeridi _baslik = null!;
    private TabControl _sekmeler = null!;
    private TabPage _dosyalarSekmesi = null!;
    private TabPage _ayarlarSekmesi = null!;
    private ToolStrip _araclar = null!;
    private ToolStripSplitButton _acDugmesi = null!;
    private ToolStripButton _copDugmesi = null!;
    private ToolStripButton _geriAlDugmesi = null!;
    private ToolStripTextBox _araKutusu = null!;
    private SuzgecSeridi _suzgecler = null!;
    private SplitContainer _dikeyBolen = null!;
    private TreeView _agac = null!;
    private Label _altBolumBasligi = null!;
    private SplitContainer _altBolen = null!;
    private OnizlemePaneli _onizleme = null!;
    private ReferansListesi _referanslar = null!;
    private StatusStrip _durumCubugu = null!;
    private ToolStripStatusLabel _durumSol = null!;
    private ToolStripStatusLabel _durumSag = null!;

    private void TasarimiKur()
    {
        SuspendLayout();

        Text = "SW PDM — Dosya Yöneticisi (referans korumalı)";
        ClientSize = new Size(572, 880);
        MinimumSize = new Size(420, 520);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Renkler.GovdeArkaPlan;
        Icon = PencereSimgesi();

        _simgeler = SimgeListesi();
        _baslik = new BaslikSeridi { Dock = DockStyle.Top };
        _sekmeler = SekmeleriKur();
        _durumCubugu = DurumCubugunuKur();

        // WinForms yerlestirme sirasi: SONRA eklenen ONCE yerlesir ve dis kenari alir.
        // Bu yuzden Fill olan once, kenara yapisanlar sonra eklenir.
        Controls.Add(_sekmeler);
        Controls.Add(_durumCubugu);
        Controls.Add(_baslik);

        ResumeLayout(performLayout: true);
    }

    private static ImageList SimgeListesi()
    {
        var liste = new ImageList
        {
            ImageSize = new Size(Simgeler.Boy, Simgeler.Boy),
            ColorDepth = ColorDepth.Depth32Bit,
        };

        // Sira SimgeSirasi ile birebir ayni olmak ZORUNDA.
        //
        // Once WINDOWS KABUGU denenir: SOLIDWORKS kurulu bir makinede
        // .SLDPRT/.SLDASM/.SLDDRW simgeleri kabuga kayitlidir ve Gezgin'de
        // gorunen GERCEK simge gelir. Kabuk vermezse koda cizilmis yedege
        // dusulur - hicbir durumda simgesiz kalinmaz.
        (string? Uzanti, Func<Bitmap> Yedek)[] girdiler =
        [
            (null,      Simgeler.Klasor),        // SimgeSirasi.Klasor
            (".SLDPRT", Simgeler.Parca),         // SimgeSirasi.Parca
            (".SLDASM", Simgeler.Montaj),        // SimgeSirasi.Montaj
            (".SLDDRW", Simgeler.TeknikResim),   // SimgeSirasi.TeknikResim
            (".PDF",    Simgeler.Pdf),           // SimgeSirasi.Pdf
        ];

        foreach ((string? uzanti, Func<Bitmap> yedek) in girdiler)
        {
            Bitmap? kabuktan = uzanti is null
                ? KabukSimgeleri.Klasor()
                : KabukSimgeleri.Dosya(uzanti);

            liste.Images.Add(kabuktan ?? yedek());
        }

        return liste;
    }

    private TabControl SekmeleriKur()
    {
        var sekmeler = new TabControl { Dock = DockStyle.Fill };

        _dosyalarSekmesi = new TabPage("Dosyalar")
        {
            BackColor = Renkler.GovdeArkaPlan,
            Padding = new Padding(0),
        };
        _ayarlarSekmesi = new TabPage("Ayarlar")
        {
            BackColor = Renkler.GovdeArkaPlan,
            Padding = new Padding(0),
        };

        _araclar = AracSeridiniKur();
        _suzgecler = new SuzgecSeridi("Tümü", "Montaj", "Parça", "Teknik resim", "PDF")
        {
            Dock = DockStyle.Top,
        };
        _dikeyBolen = GovdeyiKur();

        _dosyalarSekmesi.Controls.Add(_dikeyBolen);
        _dosyalarSekmesi.Controls.Add(_suzgecler);
        _dosyalarSekmesi.Controls.Add(_araclar);

        sekmeler.TabPages.Add(_dosyalarSekmesi);
        sekmeler.TabPages.Add(_ayarlarSekmesi);
        return sekmeler;
    }

    private ToolStrip AracSeridiniKur()
    {
        var serit = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            ImageScalingSize = new Size(Simgeler.Boy, Simgeler.Boy),
        };

        _acDugmesi = new ToolStripSplitButton
        {
            Image = Simgeler.Ac(),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = "Klasör aç",
        };

        _copDugmesi = new ToolStripButton
        {
            Text = "Çöp",
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Çöp kutusu",
        };

        _geriAlDugmesi = new ToolStripButton
        {
            Image = Simgeler.GeriAl(),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = "Geri al",
        };

        // CLAUDE.md 6 - OLCULMUS TUZAK: ToolStripItem.Width, AutoSize aciksa
        // YOK SAYILIYOR. AutoSize once false yapilmadan verilen genislik hicbir
        // sey yapmiyor. Iki satirin sirasi onemli.
        _araKutusu = new ToolStripTextBox { AutoSize = false };
        _araKutusu.Width = 150;
        _araKutusu.TextBox.PlaceholderText = "Ara...";

        serit.Items.Add(_acDugmesi);
        serit.Items.Add(new ToolStripSeparator());
        serit.Items.Add(_copDugmesi);
        serit.Items.Add(_geriAlDugmesi);
        serit.Items.Add(new ToolStripSeparator());
        serit.Items.Add(_araKutusu);
        return serit;
    }

    private SplitContainer GovdeyiKur()
    {
        var bolen = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = Renkler.GovdeArkaPlan,
        };

        _agac = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Renkler.AgacArkaPlan,
            ImageList = _simgeler,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            ItemHeight = 18,
        };
        bolen.Panel1.Controls.Add(_agac);

        _altBolumBasligi = new Label
        {
            Text = "Önizleme ve Referanslar",
            Dock = DockStyle.Top,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Renkler.BolumBasligiYazi,
            BackColor = Renkler.GovdeArkaPlan,
        };

        _altBolen = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Renkler.GovdeArkaPlan,
        };

        _onizleme = new OnizlemePaneli();
        _referanslar = new ReferansListesi { SmallImageList = _simgeler };
        _altBolen.Panel1.Controls.Add(_onizleme);
        _altBolen.Panel2.Controls.Add(_referanslar);

        bolen.Panel2.Controls.Add(_altBolen);
        bolen.Panel2.Controls.Add(_altBolumBasligi);
        return bolen;
    }

    private StatusStrip DurumCubugunuKur()
    {
        var cubuk = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false,
        };

        _durumSol = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _durumSag = new ToolStripStatusLabel
        {
            TextAlign = ContentAlignment.MiddleRight,
        };

        cubuk.Items.Add(_durumSol);
        cubuk.Items.Add(_durumSag);
        return cubuk;
    }

    /// <summary>
    /// Pencere simgesi koddan uretiliyor; depoda .ico yok.
    /// GetHicon bir tutamak sizdirir - tek adet ve surec omru boyunca yasar,
    /// isletim sistemi cikista geri alir. Gercek .ico gelince bu metot silinecek.
    /// </summary>
    private static Icon PencereSimgesi()
    {
        using Bitmap bmp = Simgeler.Montaj();
        return Icon.FromHandle(bmp.GetHicon());
    }
}
