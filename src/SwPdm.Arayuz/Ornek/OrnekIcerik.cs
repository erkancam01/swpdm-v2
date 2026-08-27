using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SwPdm.Arayuz.Gorunum;

namespace SwPdm.Arayuz.Ornek;

/// <summary>
/// ===================== GECICI - SILINECEK =====================
/// Buradaki HICBIR SEY gercek degildir. Ne dosya okundu, ne klasor tarandi,
/// ne bir referans cozuldu. Butun metinler tasarim ekran goruntusunden
/// KOPYALANMIS yer tutuculardir; amaci yalnizca yerlesimin gorulebilmesi.
///
/// Gercek tarama geldiginde bu dosya ve Ornek/ klasoru TUMUYLE silinecek;
/// yerine gecen kod CLAUDE.md 3'e uymak zorunda:
///   - tarama yoksa hicbir sayi ve hicbir liste gosterilmez, sebebi yazilir
///   - bos liste "yok" demek DEGILDIR
/// ==============================================================
/// </summary>
internal static class OrnekIcerik
{
    internal static void Yerlestir(
        TreeView agac,
        OnizlemePaneli onizleme,
        ReferansListesi referanslar,
        ToolStripStatusLabel durumSol,
        ToolStripStatusLabel durumSag)
    {
        TreeNode secili = AgaciDoldur(agac);
        onizleme.Onizleme = OrnekTeknikResim();

        // DIKKAT: "0" burada ORNEK metindir. Gercek kodda tarama yapilmadan
        // Kullanan alanina 0 YAZILAMAZ - CLAUDE.md 3. Orada "taranmadı" yazilir.
        onizleme.UstBilgiyiYaz(
            ad: "Parça2.SLDDRW",
            tur: "Teknik resim",
            boyut: "81 KB",
            degistirme: "26.08.2026 17:36",
            kullanan: "0");

        referanslar.Ekle("Parça2.SLDPRT", "Baz aldığı model", SimgeSirasi.Parca);

        durumSol.Text = "Parça2.SLDDRW  ·  81 KB  ·  26.08.2026 17:36";
        durumSag.Text = "507 ref";

        agac.SelectedNode = secili;
    }

    private static TreeNode AgaciDoldur(TreeView agac)
    {
        agac.BeginUpdate();
        agac.Nodes.Clear();

        TreeNode kok = Dugum(agac.Nodes, "ORJINAL", SimgeSirasi.Klasor);

        TreeNode bir = Dugum(kok.Nodes, "1 (2)", SimgeSirasi.Klasor);
        Dugum(bir.Nodes, "Parça3.SLDPRT", SimgeSirasi.Parca);
        Dugum(bir.Nodes, "Montaj1.SLDASM", SimgeSirasi.Montaj);

        TreeNode iki = Dugum(kok.Nodes, "2 (1)", SimgeSirasi.Klasor);
        Dugum(iki.Nodes, "Parça1.SLDPRT", SimgeSirasi.Parca);

        TreeNode otuzUc = Dugum(kok.Nodes, "33 (3)", SimgeSirasi.Klasor);
        Dugum(otuzUc.Nodes, "Montaj2.SLDASM", SimgeSirasi.Montaj);
        TreeNode secili = Dugum(otuzUc.Nodes, "Parça2.SLDDRW", SimgeSirasi.TeknikResim);
        Dugum(otuzUc.Nodes, "Parça2.SLDPRT 1M-1R", SimgeSirasi.Parca);

        TreeNode ikiYuzYirmiIki = Dugum(kok.Nodes, "222 (1)", SimgeSirasi.Klasor);
        Dugum(ikiYuzYirmiIki.Nodes, "asaParçaa1.SLDPRT", SimgeSirasi.Parca);

        kok.Expand();
        iki.Expand();
        otuzUc.Expand();
        ikiYuzYirmiIki.Expand();
        // "1 (2)" bilerek kapali: ekran goruntusunde de kapali duruyor.

        agac.EndUpdate();
        return secili;
    }

    private static TreeNode Dugum(TreeNodeCollection nereye, string metin, int simge)
    {
        var d = new TreeNode(metin)
        {
            ImageIndex = simge,
            SelectedImageIndex = simge,
        };
        nereye.Add(d);
        return d;
    }

    /// <summary>Onizleme kutusundaki ornek teknik resim: koddan cizilmis bos pafta.</summary>
    private static Bitmap OrnekTeknikResim()
    {
        var bmp = new Bitmap(400, 300);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.White);

        using var ince = new Pen(Color.FromArgb(0x30, 0x30, 0x30), 1f);
        using var pafta = new Pen(Color.FromArgb(0x60, 0x60, 0x60), 1f);

        g.DrawRectangle(pafta, 6, 6, 387, 287);

        // Alti gen
        var merkez = new PointF(110f, 105f);
        const float yaricap = 46f;
        var kose = new PointF[6];
        for (int i = 0; i < 6; i++)
        {
            double aci = System.Math.PI / 3.0 * i - System.Math.PI / 2.0;
            kose[i] = new PointF(
                merkez.X + (float)(yaricap * System.Math.Cos(aci)),
                merkez.Y + (float)(yaricap * System.Math.Sin(aci)));
        }
        g.DrawPolygon(ince, kose);

        // Sagdaki yan gorunus
        g.DrawRectangle(ince, 250, 62, 34, 86);

        // Alttaki ust gorunus
        g.DrawRectangle(ince, 62, 196, 96, 34);
        g.DrawLine(ince, 62, 213, 158, 213);

        // Antet
        g.DrawRectangle(pafta, 288, 246, 104, 46);
        g.DrawLine(pafta, 288, 261, 392, 261);
        g.DrawLine(pafta, 288, 276, 392, 276);
        g.DrawLine(pafta, 340, 246, 340, 292);

        return bmp;
    }
}
