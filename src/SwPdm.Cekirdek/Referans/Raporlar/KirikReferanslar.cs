using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// KIRIK REFERANSLAR: dosyanin icinde yazan bir yolun karsiligi taranan
/// agacta YOK.
///
/// En degerli rapor bu: bir montaj acildiginda SOLIDWORKS'un "dosya
/// bulunamadi" diyecegi durumlari, montaji hic acmadan onceden gosteriyor.
///
/// DIKKAT - "bulunamadi" iki sey olabilir ve ikisi de burada listelenir
/// ama sebep AYNI degil: dosya gercekten silinmis olabilir, ya da taranan
/// kokun disinda durabilir (baska bir surucude, kutuphane klasorunde).
/// Bu yuzden aciklamada "taranan ağaçta" yaziyor - "yok" demiyor.
/// </summary>
internal sealed class KirikReferanslar : IRapor
{
    /// <inheritdoc/>
    public string Ad => "Kırık referanslar";

    /// <inheritdoc/>
    public string Aciklama => "İçinde yazan bir dosyanın karşılığı taranan ağaçta bulunamadı.";

    /// <inheritdoc/>
    public RaporSonucu Uret(ReferansIndeksi indeks)
    {
        var satirlar = new List<RaporSatiri>();

        foreach (IndeksKaydi kayit in indeks.Kayitlar)
        {
            foreach (string yazilan in kayit.YazilanYollar)
            {
                if (indeks.Coz(kayit, yazilan).Durum == CozumDurumu.Bulunamadi)
                {
                    satirlar.Add(new RaporSatiri(
                        kayit.Yol,
                        $"\"{WindowsYolu.DosyaAdi(yazilan)}\" taranan ağaçta bulunamadı  ({yazilan})"));
                }
            }
        }

        return RaporSonucu.Denetle(indeks, satirlar);
    }
}
