using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// YETIM PARCALAR: hicbir montajin ve hicbir teknik resmin kullanmadigi
/// parcalar.
///
/// YALNIZCA PARCA (.SLDPRT) sayiliyor - ve bu bilincli. Bir montaji ya da
/// teknik resmi kimsenin kullanmamasi NORMALDIR: onlar zaten en ustte
/// durur. Onlari da "yetim" diye listelemek, raporu kullanilmaz kadar
/// gurultulu yapar ve gercek yetimleri gizlerdi.
///
/// CLAUDE.md 3: bu rapor SILME ONERISI DEGILDIR. Bir parca taranan agacta
/// kullanilmiyor olabilir ama baska bir projede kullaniliyordur. Rapor
/// "taranan ağaçta kullanan yok" der, "silinebilir" demez.
/// </summary>
internal sealed class Yetimler : IRapor
{
    /// <inheritdoc/>
    public string Ad => "Yetim parçalar";

    /// <inheritdoc/>
    public string Aciklama => "Taranan ağaçta hiçbir montajın/teknik resmin kullanmadığı parçalar.";

    /// <inheritdoc/>
    public RaporSonucu Uret(ReferansIndeksi indeks)
    {
        var satirlar = new List<RaporSatiri>();

        foreach (IndeksKaydi kayit in indeks.Kayitlar)
        {
            if (DosyaTurleri.Tani(kayit.Yol) != DosyaTuru.Parca)
            {
                continue;
            }

            if (indeks.Kullananlar(kayit.Yol).Kullananlar.Count == 0)
            {
                satirlar.Add(new RaporSatiri(kayit.Yol, "Taranan ağaçta kullanan yok."));
            }
        }

        return RaporSonucu.Denetle(indeks, satirlar);
    }
}
