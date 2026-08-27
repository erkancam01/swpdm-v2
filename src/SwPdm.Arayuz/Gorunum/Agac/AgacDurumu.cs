using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>Agacin acik dallari ve secili ogesi. Yeniden kurulurken geri yuklenir.</summary>
internal sealed record AgacDurumu(IReadOnlyList<string> AcikYollar, string? SeciliYol);

/// <summary>
/// AGACIN DURUMU: acik dallar ve secim.
///
/// Ayri bir dosyada cunku ayri bir konu: <see cref="AgacDoldurucu"/> agaci
/// DOLDURUR, burasi kullanicinin YERINI korur. Agac her tazelendiginde
/// (suzgec, yenile, bir dosya islemi) burasi calisiyor - kullanicinin actigi
/// dallarin kapanmamasi ve secimin kaybolmamasi bu koda bagli.
/// </summary>
internal static class AgacDurumlari
{
    /// <summary>Acik dallari ve secili ogeyi yakalar.</summary>
    internal static AgacDurumu Al(SecimliAgac agac)
    {
        var acik = new List<string>();
        Topla(agac.Nodes, acik);

        string? secili = Yolu(agac.SelectedNode);

        // Kisa yollar once: ust dal acilmadan alt dal acilamaz.
        acik.Sort(static (a, b) => a.Length.CompareTo(b.Length));
        return new AgacDurumu(acik, secili);

        static void Topla(TreeNodeCollection dugumler, List<string> acik)
        {
            foreach (TreeNode dugum in dugumler)
            {
                if (dugum.IsExpanded && dugum.Tag is KlasorOgesi klasor)
                {
                    acik.Add(klasor.Yol);
                }

                Topla(dugum.Nodes, acik);
            }
        }
    }

    /// <summary>Yakalanmis durumu geri kurar.</summary>
    internal static void GeriYukle(SecimliAgac agac, AgacDurumu durum)
    {
        agac.BeginUpdate();
        foreach (string yol in durum.AcikYollar)
        {
            DuguuBul(agac, yol)?.Expand();   // Expand -> BeforeExpand -> tembel tarama
        }

        agac.EndUpdate();

        // Secim BeginUpdate'in DISINDA konuyor: icerideyken secim degistirmek
        // olay zincirini cizim kapaliyken calistiriyor ve orasi yerli
        // denetimin guvenilir cevap vermedigi bir hal (bkz. SecimliAgac).
        if (durum.SeciliYol is not null)
        {
            TreeNode? secili = DuguuBul(agac, durum.SeciliYol);
            if (secili is not null)
            {
                agac.YalnizSec(secili);
                secili.EnsureVisible();
            }
        }
    }

    /// <summary>Verilen yoldaki dugumu bulur; yoksa null.</summary>
    internal static TreeNode? DuguuBul(SecimliAgac agac, string yol)
    {
        return Ara(agac.Nodes);

        TreeNode? Ara(TreeNodeCollection dugumler)
        {
            foreach (TreeNode dugum in dugumler)
            {
                if (string.Equals(Yolu(dugum), yol, StringComparison.OrdinalIgnoreCase))
                {
                    return dugum;
                }

                TreeNode? derin = Ara(dugum.Nodes);
                if (derin is not null)
                {
                    return derin;
                }
            }

            return null;
        }
    }

    /// <summary>Dugumun diskteki yolu; yoksa null.</summary>
    internal static string? Yolu(TreeNode? dugum) => AgacDoldurucu.Etiket(dugum) switch
    {
        DosyaOgesi dosya => dosya.Yol,
        KlasorOgesi klasor => klasor.Yol,
        _ => null,
    };
}
