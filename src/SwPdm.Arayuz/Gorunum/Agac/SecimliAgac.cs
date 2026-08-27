using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// COKLU SECIMIN TEK KAPISI. Secime dair bir sey degisecekse BU DOSYA degisir
/// (CLAUDE.md 1b).
///
/// NEDEN VAR - olculmus kisit: WinForms <see cref="TreeView"/> coklu secimi
/// DESTEKLEMIYOR. <c>SelectedNode</c> tektir; <c>MultiSelect</c> ozelligi
/// yoktur (o <see cref="ListView"/>'de var), dikdortgenle secim de yoktur.
/// O yuzden secim burada kendi elimizde tutuluyor ve secili satir kendimiz
/// boyaniyor.
///
/// DENETIMIN KENDI SECIMIYLE ILISKI: <c>SelectedNode</c> "odaklanmis dugum"
/// olarak yasamaya devam ediyor ve her zaman kumenin icinde. Boylece
/// onizleme, durum cubugu, arama ve suzgec hicbir sey bilmeden calismaya
/// devam ediyor - hepsi <c>AfterSelect</c> ve <c>SelectedNode</c> uzerinden
/// konusuyor.
/// </summary>
internal sealed class SecimliAgac : TreeView
{
    /// <summary>
    /// Dikdortgenin baslamasi icin gereken en kucuk suruklenme. Altinda kalan
    /// hareket "titreyen el"dir; her tikta dikdortgen baslatmak secimi bozar.
    /// </summary>
    private const int SurtunmeEsigi = 4;

    private readonly HashSet<TreeNode> _secililer = [];

    /// <summary>Shift ile aralik secerken sabit kalan uc.</summary>
    private TreeNode? _capa;

    private bool _dikdortgenBasladi;
    private Point _basildigiYer;
    private Rectangle _dikdortgen;

    /// <summary>Kod secimi degistirirken olay yagmurunu kesen bayrak.</summary>
    private bool _kendimDegistiriyorum;

    internal SecimliAgac()
    {
        // OwnerDrawText BILEREK secildi (OwnerDrawAll degil): cizgileri, +/-
        // kutularini ve simgeleri denetim cizmeye devam etsin, biz yalnizca
        // METIN alanini - yani secim vurgusunu - cizelim. Az kod, az risk.
        DrawMode = TreeViewDrawMode.OwnerDrawText;
    }

    /// <summary>Secim degistiginde tetiklenir.</summary>
    internal event EventHandler? SecimDegisti;

    /// <summary>
    /// Su an secili dugumler, agactaki sirayla.
    ///
    /// ================== OLCULMUS TUZAK - UYGULAMA DONUYORDU ==================
    /// Burasi once <c>NextVisibleNode</c> ile yuruyordu. <c>BeginUpdate</c>
    /// (yani WM_SETREDRAW kapaliyken) icinden cagrildiginda o yuruyus
    /// BITMIYOR ve uygulama SESSIZCE kilitleniyor: cokme yok, hata yok,
    /// gunluk yok - pencere oldugu gibi duruyor ve bir daha hicbir sey
    /// cizmiyor (CLAUDE.md 3'un "sessiz askida kalma"si).
    ///
    /// Belirti tamamen baska yerde gorundu: agacta bir dosyaya tikladiktan
    /// SONRA suzgec dugmesine basmak "ise yaramiyordu". Dugmenin Click'i
    /// aslinda doguyordu; ekran bayat oldugu icin oyle sanildi. Uc yanlis
    /// hipotez (fare yakalamasi · base cagri sirasi · Focus) 3x2 olcumle
    /// elendi; sebep bu dongu cikti.
    ///
    /// Bu yuzden burada dugum agaci DOGRUDAN yuruyor: yerli denetime hic
    /// soru sorulmuyor, dolayisiyla cizim durumundan da etkilenmiyor.
    /// =========================================================================
    /// </summary>
    internal IReadOnlyList<TreeNode> Secililer
    {
        get
        {
            var sonuc = new List<TreeNode>(_secililer.Count);
            Topla(Nodes, sonuc);
            return sonuc;

            void Topla(TreeNodeCollection dugumler, List<TreeNode> hedef)
            {
                foreach (TreeNode d in dugumler)
                {
                    if (_secililer.Contains(d))
                    {
                        hedef.Add(d);
                    }

                    Topla(d.Nodes, hedef);
                }
            }
        }
    }

    /// <summary>Bir dugum secili mi.</summary>
    internal bool Secili(TreeNode dugum) => _secililer.Contains(dugum);

    /// <summary>Secimi tek bir dugume indirir. null ise secimi bosaltir.</summary>
    internal void YalnizSec(TreeNode? dugum)
    {
        _secililer.Clear();
        if (dugum is not null)
        {
            _secililer.Add(dugum);
        }

        _capa = dugum;
        OdagiTasi(dugum);
        Bildir();
    }

    /// <summary>
    /// Agac yeniden kuruldugunda cagrilir: artik var olmayan dugumler kumede
    /// kalmasin. Kalirsa "3 oge secili" yazip ekranda hicbir sey secili
    /// gorunmez - CLAUDE.md 3'un yasakladigi sessiz yalan.
    /// </summary>
    internal void SecimiTemizle()
    {
        if (_secililer.Count == 0 && _capa is null)
        {
            return;
        }

        _secililer.Clear();
        _capa = null;

        // Odak da birakilir. Yoksa iki uclu bir yalan olur: kume bos
        // ("hicbir sey secili degil") ama SelectedNode hala eski dugumu
        // gosteriyor ve ona bakan kod baska bir sey saniyor.
        OdagiTasi(null);
        Bildir();
    }

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        if (e.Node is null)
        {
            base.OnDrawNode(e);
            return;
        }

        bool secili = _secililer.Contains(e.Node);
        bool odakli = ReferenceEquals(e.Node, SelectedNode);

        // Dugumun kendi metin dikdortgeni bazen bir iki piksel dar cikiyor ve
        // vurgu yaziyi kirpiyor; biraz genisletiliyor.
        Rectangle alan = e.Node.Bounds;
        alan.Inflate(1, 0);

        Color arka = secili
            ? (Focused ? Renkler.SecimArkaPlan : Renkler.SecimArkaPlanPasif)
            : BackColor;
        Color yazi = secili && Focused ? Renkler.SecimYazi : ForeColor;

        using (var firca = new SolidBrush(arka))
        {
            e.Graphics.FillRectangle(firca, alan);
        }

        TextRenderer.DrawText(
            e.Graphics,
            e.Node.Text,
            e.Node.NodeFont ?? Font,
            alan,
            yazi,
            TextFormatFlags.GlyphOverhangPadding | TextFormatFlags.NoPrefix);

        // Odaklanmis dugum, secili olmayan bir dugum de olabilir (Ctrl ile
        // gezinirken). Nokta cerceve onu gorunur tutuyor.
        if (odakli && Focused)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, alan);
        }
    }

    protected override void OnBeforeSelect(TreeViewCancelEventArgs e)
    {
        // Denetimin kendi secimi yalnizca ODAK demektir; kumeyi biz yonetiyoruz.
        base.OnBeforeSelect(e);
    }

    protected override void OnAfterSelect(TreeViewEventArgs e)
    {
        // Klavyeyle (ok tuslari) gezinildiginde denetim SelectedNode'u kendisi
        // degistiriyor ve buraya geliyor. Shift basili degilse bu tek secimdir.
        if (!_kendimDegistiriyorum && e.Node is not null && !_secililer.Contains(e.Node)
            && (ModifierKeys & (Keys.Control | Keys.Shift)) == 0)
        {
            _secililer.Clear();
            _secililer.Add(e.Node);
            _capa = e.Node;
            Invalidate();
            SecimDegisti?.Invoke(this, EventArgs.Empty);
        }

        base.OnAfterSelect(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        TreeNode? vurulan = MetneVuranDugum(e.Location);

        if (vurulan is null)
        {
            // OLCULDU: "+/-" kutusuna tiklamak secimi SILIYORDU. Gezgin
            // silmiyor - kullanici bir dosyayi secip yanindaki dali acabilmeli.
            // Girinti/dugme alani "bos alan" DEGILDIR: dokunulmaz, denetimin
            // kendi ac-kapa davranisi calisir.
            if (GirintiyeVurduMu(e.Location))
            {
                base.OnMouseDown(e);
                return;
            }

            // Gercekten bos alan: dikdortgen secimi ADAYI. Baslamasi icin
            // esigi asan bir hareket gerekiyor - yoksa her bos tik dikdortgen
            // aciyormus gibi gorunurdu.
            // Bos alana tiklaninca odak kendiliginden gelmiyor; Ctrl+A ve ok
            // tuslari calissin diye elle aliniyor. DUGUM uzerinde ise
            // CAGRILMIYOR - orada denetim odagi zaten aliyor ve mousedown'un
            // ortasinda odak degistirmek fare yakalamasini bozuyor (asagida).
            Focus();
            _basildigiYer = e.Location;
            _dikdortgenBasladi = false;

            if (e.Button == MouseButtons.Left && (ModifierKeys & (Keys.Control | Keys.Shift)) == 0)
            {
                YalnizSec(null);
            }

            base.OnMouseDown(e);
            return;
        }

        if (e.Button == MouseButtons.Right && _secililer.Contains(vurulan))
        {
            // Sag tik VAR OLAN cok secimi bozmaz - Gezgin de bozmuyor. Yoksa
            // "5 dosya sec, sag tikla, sil" hic yapilamazdi.
            base.OnMouseDown(e);
            OdagiTasi(vurulan);
            return;
        }

        // ================== OLCULMUS TUZAK ==================
        // Secim, base.OnMouseDown'dan ONCE degistiriliyordu. Belirti hic
        // beklenmedik yerdeydi: agactaki bir DOSYA ADINA tiklandiktan SONRA
        // suzgec dugmesine yapilan tik HIC ISLEMIYORDU - dugme odagi aliyor
        // ama Click olayi hic dogmuyordu.
        //
        // Iki hipotez 3x2 olcumle elendi: "+/-" kutusuna tiklamak ayni seyi
        // YAPMIYORDU (o dalda secime dokunulmuyor), hic tiklamamak da
        // yapmiyordu. Fark tek: SelectedNode'a mousedown'un ORTASINDA yazmak.
        // Yerli agac denetimi o sirada kendi tik/surukleme takibini kuruyor
        // ve altindan halisi cekilince fare yakalamasini birakmiyor.
        //
        // Cozum: once denetim kendi isini yapsin, secimi SONRA kuralim.
        // ====================================================
        base.OnMouseDown(e);

        if ((ModifierKeys & Keys.Shift) != 0 && _capa is not null)
        {
            AraligiSec(_capa, vurulan);
        }
        else if ((ModifierKeys & Keys.Control) != 0)
        {
            if (!_secililer.Remove(vurulan))
            {
                _secililer.Add(vurulan);
            }

            _capa = vurulan;
            OdagiTasi(vurulan);
            Bildir();
        }
        else
        {
            YalnizSec(vurulan);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            base.OnMouseMove(e);
            return;
        }

        if (!_dikdortgenBasladi)
        {
            if (_basildigiYer.IsEmpty
                || (Math.Abs(e.X - _basildigiYer.X) < SurtunmeEsigi
                    && Math.Abs(e.Y - _basildigiYer.Y) < SurtunmeEsigi))
            {
                base.OnMouseMove(e);
                return;
            }

            _dikdortgenBasladi = true;
            _dikdortgen = Rectangle.Empty;
        }

        CerceveyiSil();
        _dikdortgen = Rectangle.FromLTRB(
            Math.Min(_basildigiYer.X, e.X), Math.Min(_basildigiYer.Y, e.Y),
            Math.Max(_basildigiYer.X, e.X), Math.Max(_basildigiYer.Y, e.Y));
        CerceveyiCiz();

        DikdortgendekileriSec();
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dikdortgenBasladi)
        {
            CerceveyiSil();
            _dikdortgenBasladi = false;
            _dikdortgen = Rectangle.Empty;
        }

        _basildigiYer = Point.Empty;
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.A)
        {
            e.SuppressKeyPress = true;
            TumGorunenleriSec();
            return;
        }

        // Shift + ok: capadan itibaren aralik. Denetim SelectedNode'u kendisi
        // tasiyor; biz tasindiktan SONRA aralligi kuruyoruz.
        if (e.Shift && (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End)
            && _capa is not null)
        {
            TreeNode? hedef = e.KeyCode switch
            {
                Keys.Up => SelectedNode?.PrevVisibleNode,
                Keys.Down => SelectedNode?.NextVisibleNode,
                Keys.Home => IlkGorunen(),
                _ => SonGorunen(),
            };

            if (hedef is not null)
            {
                e.SuppressKeyPress = true;
                AraligiSec(_capa, hedef);
                hedef.EnsureVisible();
            }

            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        // Odak gidince vurgu solar; hangi satirlarin secili oldugu YINE de
        // gorunur kalir - kaybolursa kullanici neyi sildigini bilemez.
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    /// <summary>
    /// Yalnizca dugumun METIN alanina vuran tik dugumu sayar. <c>GetNodeAt</c>
    /// satirin tamamini eslestiriyor; oyle olsa metnin sagindaki bos alandan
    /// dikdortgen baslatilamazdi.
    /// </summary>
    private TreeNode? MetneVuranDugum(Point nokta)
    {
        TreeNode? dugum = GetNodeAt(nokta);
        if (dugum is null)
        {
            return null;
        }

        Rectangle alan = dugum.Bounds;
        alan.Inflate(1, 0);

        // Simge de tiklanabilir olmali: metin dikdortgeninin solundaki simge
        // genisligi kadar geri aliniyor.
        alan.X -= ImageList is null ? 0 : ImageList.ImageSize.Width + 2;
        alan.Width += ImageList is null ? 0 : ImageList.ImageSize.Width + 2;

        return alan.Contains(nokta) ? dugum : null;
    }

    /// <summary>
    /// Tik, bir dugumun SOLUNDAKI girinti/"+/-" alanina mi denk geldi.
    /// Orasi ne secim ne dikdortgen alanidir; denetimin kendi isi.
    /// </summary>
    private bool GirintiyeVurduMu(Point nokta)
    {
        TreeNode? satir = GetNodeAt(nokta);
        return satir is not null && nokta.X < satir.Bounds.Left;
    }

    private void AraligiSec(TreeNode capa, TreeNode hedef)
    {
        var aralik = new List<TreeNode>();
        bool topluyor = false;

        for (TreeNode? d = IlkGorunen(); d is not null; d = d.NextVisibleNode)
        {
            bool uc = ReferenceEquals(d, capa) || ReferenceEquals(d, hedef);

            if (uc && !topluyor)
            {
                topluyor = true;
                aralik.Add(d);

                // Capa ile hedef ayni dugumse aralik tek elemanlidir.
                if (ReferenceEquals(capa, hedef))
                {
                    break;
                }

                continue;
            }

            if (topluyor)
            {
                aralik.Add(d);
                if (uc)
                {
                    break;
                }
            }
        }

        _secililer.Clear();
        foreach (TreeNode d in aralik)
        {
            _secililer.Add(d);
        }

        OdagiTasi(hedef);
        Bildir();
    }

    private void TumGorunenleriSec()
    {
        _secililer.Clear();
        for (TreeNode? d = IlkGorunen(); d is not null; d = d.NextVisibleNode)
        {
            _secililer.Add(d);
        }

        Bildir();
    }

    private void DikdortgendekileriSec()
    {
        _secililer.Clear();
        for (TreeNode? d = IlkGorunen(); d is not null; d = d.NextVisibleNode)
        {
            Rectangle alan = d.Bounds;
            alan.Inflate(1, 0);
            if (alan.IntersectsWith(_dikdortgen))
            {
                _secililer.Add(d);
            }
        }

        Bildir();
    }

    private TreeNode? IlkGorunen() => Nodes.Count > 0 ? Nodes[0] : null;

    private TreeNode? SonGorunen()
    {
        TreeNode? son = IlkGorunen();
        for (TreeNode? d = son; d is not null; d = d.NextVisibleNode)
        {
            son = d;
        }

        return son;
    }

    /// <summary>
    /// Odagi tasir. SelectedNode'a yazmak AfterSelect'i tetikliyor; bayrak
    /// oradaki "tek secime dus" dalini susturuyor (CLAUDE.md 6: yeniden giris).
    /// </summary>
    private void OdagiTasi(TreeNode? dugum)
    {
        _kendimDegistiriyorum = true;
        SelectedNode = dugum;
        _kendimDegistiriyorum = false;
    }

    private void Bildir()
    {
        Invalidate();
        SecimDegisti?.Invoke(this, EventArgs.Empty);
    }

    // Ters cerceve EKRAN koordinatlarinda cizilir; ayni cagri ikinci kez
    // yapilinca siliniyor (XOR). Cizim ile silme SIMETRIK olmak zorunda,
    // yoksa ekranda iz kaliyor.
    private void CerceveyiCiz()
    {
        if (_dikdortgen.Width > 0 && _dikdortgen.Height > 0)
        {
            ControlPaint.DrawReversibleFrame(
                RectangleToScreen(_dikdortgen), BackColor, FrameStyle.Dashed);
        }
    }

    private void CerceveyiSil() => CerceveyiCiz();
}
