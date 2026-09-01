using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// OKUNAMAYAN DOSYALAR: referanslari cikarilamayan SOLIDWORKS dosyalari.
///
/// NEDEN BIR RAPOR: bu, oteki butun raporlarin NE KADARINI BILMEDIGIMIZI
/// gosteren rapordur. Burada bir dosya varsa "yetim parça" listesi eksik
/// olabilir, "kırık referans" listesi eksik olabilir. Bunu gizlemek,
/// kullaniciya tam olmayan bir listeye bakip dosya sildirtmek demektir
/// (CLAUDE.md 3).
///
/// Bos olmasi iyi haberdir ve TEK BASINA anlamlidir: oteki raporlarin tam
/// oldugunu ancak burasi bosken soyleyebiliriz.
/// </summary>
internal sealed class OkunamayanDosyalar : IRapor
{
    /// <inheritdoc/>
    public string Ad => "Okunamayan dosyalar";

    /// <inheritdoc/>
    public string Aciklama => "Referansları çıkarılamayan dosyalar — öteki raporlar bu kadar eksik.";

    /// <inheritdoc/>
    public RaporSonucu Uret(ReferansIndeksi indeks)
    {
        var satirlar = new List<RaporSatiri>();

        foreach (IndeksKaydi kayit in indeks.Kayitlar)
        {
            if (!kayit.Okundu)
            {
                satirlar.Add(new RaporSatiri(kayit.Yol, kayit.Sebep ?? "Sebep bilinmiyor."));
            }
        }

        return RaporSonucu.Denetle(indeks, satirlar);
    }
}
