using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// TEKNIK RESMI OLMAYAN MODELLER: hicbir .SLDDRW tarafindan kullanilmayan
/// parca ve montajlar.
///
/// Imalata giden bir parcanin teknik resmi olmamasi genellikle EKSIKLIKTIR;
/// bu rapor onu once gosterir. Ama her modelin teknik resmi olmak zorunda
/// degil (alt bilesenler, satin alma parcalari), o yuzden bu da bir
/// EKSIKLIK IDDIASI degil, bir listedir.
/// </summary>
internal sealed class TeknikResmiOlmayanlar : IRapor
{
    /// <inheritdoc/>
    public string Ad => "Teknik resmi olmayanlar";

    /// <inheritdoc/>
    public string Aciklama => "Hiçbir teknik resmin baz almadığı parça ve montajlar.";

    /// <inheritdoc/>
    public RaporSonucu Uret(ReferansIndeksi indeks)
    {
        var satirlar = new List<RaporSatiri>();

        foreach (IndeksKaydi kayit in indeks.Kayitlar)
        {
            DosyaTuru tur = DosyaTurleri.Tani(kayit.Yol);
            if (tur is not (DosyaTuru.Parca or DosyaTuru.Montaj))
            {
                continue;
            }

            bool resmiVar = false;
            foreach (string kullanan in indeks.Kullananlar(kayit.Yol).Kullananlar)
            {
                if (DosyaTurleri.Tani(kullanan) == DosyaTuru.TeknikResim)
                {
                    resmiVar = true;
                    break;
                }
            }

            if (!resmiVar)
            {
                satirlar.Add(new RaporSatiri(kayit.Yol, "Bunu baz alan bir teknik resim yok."));
            }
        }

        return RaporSonucu.Denetle(indeks, satirlar);
    }
}
