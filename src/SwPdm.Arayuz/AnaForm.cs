using System;
using System.Windows.Forms;
using SwPdm.Arayuz.Ornek;

namespace SwPdm.Arayuz;

/// <summary>
/// Uygulamanin tek penceresi.
///
/// CLAUDE.md 7 - v1'in en pahali dersi: bir arayuz sinifi hem ekran hem is akisi
/// surucusu OLMAZ. v1'de tek bir arayuz sinifi 9.918 satira, urun kodunun
/// %38'ine cikti ve bolunemedi. Bu sinif yalnizca ekrani kurar; tasima, tarama,
/// onarim mantigi buraya YAZILMAYACAK.
/// </summary>
internal sealed partial class AnaForm : Form
{
    internal AnaForm()
    {
        TasarimiKur();

        // GECICI YER TUTUCU - yalnizca yerlesim gorulebilsin diye.
        // Hicbir dosya okunmadi, hicbir tarama yapilmadi; asagidaki her sey
        // ekran goruntusunden kopyalanmis ORNEK metindir.
        // Gercek veri geldiginde bu satir ve Ornek/ klasoru TUMUYLE silinecek.
        OrnekIcerik.Yerlestir(_agac, _onizleme, _referanslar, _durumSol, _durumSag);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Bolenler ancak denetimin gercek boyutu olustuktan sonra ayarlanabilir.
        BoleniAyarla(_dikeyBolen, 320);
        BoleniAyarla(_altBolen, 282);
    }

    /// <summary>
    /// SplitterDistance araligin disinda kalirsa istisna atar. Sinira kirpiyoruz:
    /// pencere kucukken acilmak, acilmamaktan iyidir.
    /// </summary>
    private static void BoleniAyarla(SplitContainer bolen, int hedef)
    {
        int uzunluk = bolen.Orientation == Orientation.Horizontal ? bolen.Height : bolen.Width;
        int enBuyuk = uzunluk - bolen.SplitterWidth - bolen.Panel2MinSize;
        int enKucuk = bolen.Panel1MinSize;

        if (enBuyuk < enKucuk)
        {
            return;
        }

        bolen.SplitterDistance = Math.Clamp(hedef, enKucuk, enBuyuk);
    }
}
