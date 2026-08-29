using System.Drawing;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// SECIMLI AGACIN CIZIMI - bir satir nasil boyanir.
///
/// NEDEN AYRI DOSYA: SecimliAgac boyut kapisini asti (619 > 600) ve kapi
/// dogru davrandi. Cizim kendi basina bir konu: SECIM boyasi, BIRAKMA
/// HEDEFI vurgusu ve odak dikdortgeni. Secim MANTIGI (Ctrl, Shift, capa)
/// ana dosyada kaldi.
///
/// AYNI SINIF, iki dosya (partial) - AnaForm.Tasarim.cs ile ayni kalip.
/// OnDrawNode bir EZMEDIR, yani sinifin kendisinde olmak zorunda; partial
/// bunu bozmadan bolmeyi sagliyor.
/// </summary>
internal sealed partial class SecimliAgac
{
    /// <summary>
    /// Surukleme sirasinda uzerinde bulunulan KLASOR. Yalnizca cizim icin;
    /// secime dokunmaz.
    ///
    /// Burada duruyor cunku boyayan sinif bu. Kimin ne zaman hedef oldugunu
    /// SurukleBirak biliyor - SecimliAgac yalnizca soyleneni ciziyor
    /// (CLAUDE.md 1b).
    /// </summary>
    internal TreeNode? BirakmaHedefi
    {
        get => _birakmaHedefi;
        set
        {
            if (ReferenceEquals(_birakmaHedefi, value))
            {
                return;
            }

            _birakmaHedefi = value;
            Invalidate();
        }
    }

    private TreeNode? _birakmaHedefi;

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        if (e.Node is null)
        {
            base.OnDrawNode(e);
            return;
        }

        bool secili = _secililer.Contains(e.Node);
        bool odakli = ReferenceEquals(e.Node, SelectedNode);
        bool birakmaHedefi = ReferenceEquals(e.Node, BirakmaHedefi);

        // Dugumun kendi metin dikdortgeni bazen bir iki piksel dar cikiyor ve
        // vurgu yaziyi kirpiyor; biraz genisletiliyor.
        Rectangle alan = e.Node.Bounds;
        alan.Inflate(1, 0);

        // Dugumun KENDI zemini varsa o kullanilir - secim her zaman ustte
        // kalir, cunku kullanicinin neyi sectigi her seyden onemli.
        Color kendiZemini = e.Node.BackColor.IsEmpty ? BackColor : e.Node.BackColor;
        // BIRAKMA HEDEFI SECIMDEN DE USTTE: surukleme sirasinda kullanicinin
        // tek sorusu "nereye birakiyorum". Denetimin kendi DropHighlight'i
        // owner-draw'da cizilmiyor, o yuzden burada.
        Color arka = birakmaHedefi
            ? Renkler.BirakmaHedefiZemin
            : secili
                ? (Focused ? Renkler.SecimArkaPlan : Renkler.SecimArkaPlanPasif)
                : kendiZemini;
        // Dugumun KENDI rengi varsa o kullanilir. Bu genel bir yetenek, belli
        // bir ozelligin bilgisi degil: SecimliAgac neyin neden renkli
        // oldugunu BILMEZ, yalnizca dugumun soyledigini cizer (CLAUDE.md 1b).
        Color kendiRengi = e.Node.ForeColor.IsEmpty ? ForeColor : e.Node.ForeColor;
        Color yazi = secili && Focused ? Renkler.SecimYazi : kendiRengi;

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
}
