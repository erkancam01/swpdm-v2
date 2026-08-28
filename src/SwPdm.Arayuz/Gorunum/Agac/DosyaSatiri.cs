using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// BIR DOSYA SATIRI AGACTA NASIL GORUNUR - simgesi, metni, rengi, ipucu.
///
/// <see cref="AgacDoldurucu"/>'dan AYRILDI: o dosya "agaci diskten nasil
/// kurarim, nasil tazelerim, arama sonucunu nasil gosteririm" sorularinin
/// yeri; satirin GORUNUSU ayri bir konu ve kilit dosyalari eklenince boyut
/// kapisi bunu soyledi (648 > 600). CLAUDE.md 7: bolunemeyen dosya bir
/// gunde olusmuyor.
///
/// Agaca dosya ekleyen HER yol buradan gecer - ikinci bir kopya yazilirsa
/// biri kilitleri unutur (CLAUDE.md 8).
/// </summary>
internal static class DosyaSatiri
{
    /// <summary>
    /// Bir dala dosya satirlarini ekler: once KILIT dosyalari cozulur, sonra
    /// tur suzgeci uygulanir. Agaca dosya ekleyen HER yol buradan gecer;
    /// ikinci bir kopya yazilirsa biri kilitleri unutur (CLAUDE.md 8).
    /// </summary>
    internal static void Ekle(
        TreeNode dal, IReadOnlyList<DosyaOgesi> dosyalar, Func<DosyaTuru, bool> gorunsun)
    {
        KilitDurumu kilit = Kilit.Coz(dosyalar);
        foreach (DosyaOgesi dosya in kilit.Gosterilecek)
        {
            if (gorunsun(dosya.Tur))
            {
                dal.Nodes.Add(Dugum(dosya, kilit));
            }
        }
    }

    /// <summary>
    /// Bir dosya satiri. KILIT DURUMU burada gorunur hale geliyor:
    ///   acik      - yaninda "~$" kilidi var, yani belge su anda acik
    ///   sahipsiz  - kendisi bir kilit ama sahibi bu klasorde yok (kalinti)
    /// Ikisi de hem KELIMEYLE hem RENKLE isaretleniyor; renk olmadan bu
    /// alan Wine'da olculemezdi (CLAUDE.md 11).
    /// </summary>
    internal static TreeNode Dugum(DosyaOgesi dosya, KilitDurumu kilit)
    {
        int simge = TurSimgeleri.Sira(dosya.Tur);
        bool acik = kilit.AcikYollar.Contains(dosya.Yol);

        // Gosterilecek listesinde KALMIS bir kilit, tanimi geregi sahipsizdir.
        bool sahipsiz = Kilit.KilitMi(dosya.Ad);

        var dugum = new TreeNode(Metin(dosya.Ad, acik, sahipsiz))
        {
            ImageIndex = simge,
            SelectedImageIndex = simge,
            Tag = dosya,
            ToolTipText = Ipucu(dosya, acik, sahipsiz),
        };

        // ZEMIN de veriliyor, yalnizca yazi rengi DEGIL: ClearType alt-piksel
        // cizdigi icin metnin hicbir pikseli saf renge esit olmuyor ve bu
        // alan Wine'da olculemez kaliyordu (olculdu). Dolu dikdortgen hem
        // ekranda daha okunur hem de sayilabilir.
        if (acik)
        {
            dugum.ForeColor = Renkler.AcikDosyaYazi;
            dugum.BackColor = Renkler.AcikDosyaZemin;
        }
        else if (sahipsiz)
        {
            dugum.ForeColor = Renkler.SahipsizKilitYazi;
            dugum.BackColor = Renkler.SahipsizKilitZemin;
        }

        return dugum;
    }

    private static string Metin(string ad, bool acik, bool sahipsiz)
    {
        if (acik)
        {
            return $"{ad}   {Kilit.AcikIsareti}";
        }

        return sahipsiz ? $"{ad}   {Kilit.SahipsizIsareti}" : ad;
    }

    private static string Ipucu(DosyaOgesi dosya, bool acik, bool sahipsiz)
    {
        if (acik)
        {
            return dosya.Yol + "\n" + Kilit.AcikIpucu(Kilit.Onek + dosya.Ad);
        }

        return sahipsiz ? dosya.Yol + "\n" + Kilit.SahipsizIpucu : dosya.Yol;
    }
}
