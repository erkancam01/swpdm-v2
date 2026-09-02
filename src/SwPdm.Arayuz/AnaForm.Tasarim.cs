using System;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Arayuz.Gorunum;
using SwPdm.Cekirdek;

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
    private SecimliAgac _agac = null!;
    private YolCubugu _yol = null!;
    private Label _altBolumBasligi = null!;
    private SplitContainer _altBolen = null!;
    private OnizlemePaneli _onizlemePaneli = null!;
    private ReferansListesi _referanslar = null!;
    private ReferansSeridi _referansSeridi = null!;
    private DurumCubugu _durum = null!;

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

        _simgeler = TurSimgeleri.Liste();
        _baslik = new BaslikSeridi { Dock = DockStyle.Top };
        _sekmeler = SekmeleriKur();
        _durum = new DurumCubugu();

        // WinForms yerlestirme sirasi: SONRA eklenen ONCE yerlesir ve dis kenari alir.
        // Bu yuzden Fill olan once, kenara yapisanlar sonra eklenir.
        Controls.Add(_sekmeler);
        Controls.Add(_durum);
        Controls.Add(_baslik);

        ResumeLayout(performLayout: true);
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
        _suzgecler = new SuzgecSeridi { Dock = DockStyle.Top };
        _dikeyBolen = GovdeyiKur();

        _dosyalarSekmesi.Controls.Add(_dikeyBolen);
        _dosyalarSekmesi.Controls.Add(_suzgecler);
        _dosyalarSekmesi.Controls.Add(_araclar);

        sekmeler.TabPages.Add(_dosyalarSekmesi);
        _ayarlarSekmesi.Controls.Add(AyarlarSayfasiKur());
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
            ToolTipText = "Klasör aç (Ctrl+O)",
        };

        // BASLANGIC HALI: yazi ve etkinlik kurucuda hemen uzerine yaziliyor
        // (AnaForm.CopDugmesiniTazele) - kok acik degilken gri, acikken
        // "Çöp kutusu (N)". Buradaki deger yalnizca ilk cizime kadar yasiyor.
        // Onceki hali "henüz yapılmadı" diyordu ve o yazi COKTAN bayatlamisti;
        // gorunmuyor olmasi dogru olmasini saglamiyor (CLAUDE.md 3).
        _copDugmesi = new ToolStripButton
        {
            Text = "Çöp kutusu",
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Çöp kutusu",
            Enabled = false,
        };

        // Cop kutusunun YANINDA, yazisiyla birlikte: simge tek basina "bu ne
        // yapiyor" sorusunu birakiyordu.
        _geriAlDugmesi = new ToolStripButton
        {
            Image = Simgeler.GeriAl(),
            Text = "Geri al",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ToolTipText = "Geri al (Ctrl+Z)",
            Enabled = false,
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
        // EN KUCUK PANEL OLCULERI BURADA VERILMEZ - KURUCUDA VERMEK
        // UYGULAMAYI ACILISTA COKERTIYOR (01.09.2026'da olculdu):
        // SplitContainer daha varsayilan olcusundeyken Panel2MinSize'i
        // buyutmek "SplitterDistance, Panel1MinSize ile Width-Panel2MinSize
        // arasinda olmali" diye InvalidOperationException atiyor.
        // Olculer Yerlesim.Uygula'da, pencere boyutu KONDUKTAN SONRA
        // veriliyor (CLAUDE.md 6'nin "kurucuda boyut degistirme" tuzagi).
        var bolen = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = Renkler.GovdeArkaPlan,
        };

        _agac = new SecimliAgac
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Renkler.AgacArkaPlan,
            ImageList = _simgeler,
            ShowLines = true,
            ShowNodeToolTips = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            ItemHeight = 18,
        };
        // Yol cubugu agacin HEMEN USTUNDE. WinForms yerlestirme sirasi:
        // once Fill olan, sonra kenara yapisan eklenir.
        _yol = new YolCubugu { Dock = DockStyle.Top };
        bolen.Panel1.Controls.Add(_agac);
        bolen.Panel1.Controls.Add(_yol);

        _altBolumBasligi = new Label
        {
            Text = "Önizleme ve Referanslar",
            Dock = DockStyle.Top,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Renkler.BolumBasligiYazi,
            BackColor = Renkler.GovdeArkaPlan,
        };

        // Panel2 = referans paneli. En kucuk olcusu Yerlesim.Uygula'da.
        _altBolen = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Renkler.GovdeArkaPlan,
        };

        _onizlemePaneli = new OnizlemePaneli();
        _referanslar = new ReferansListesi { SmallImageList = _simgeler };

        // SERIT LISTENIN USTUNDE ve SABIT: bolum basligi satiri liste
        // kaydirilinca ekrandan cikiyordu, serit cikmiyor - "hangi yone
        // bakiyorum" sorusu her an cevapli (CLAUDE.md 3).
        _referansSeridi = new ReferansSeridi { Dock = DockStyle.Top };

        _altBolen.Panel1.Controls.Add(_onizlemePaneli);

        // SIRA ONEMLI: Dock=Fill olan liste ONCE eklenir, Dock=Top olan serit
        // SONRA - WinForms z-sirasi doldurma sirasini belirliyor.
        _altBolen.Panel2.Controls.Add(_referanslar);
        _altBolen.Panel2.Controls.Add(_referansSeridi);

        bolen.Panel2.Controls.Add(_altBolen);
        bolen.Panel2.Controls.Add(_altBolumBasligi);
        return bolen;
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
