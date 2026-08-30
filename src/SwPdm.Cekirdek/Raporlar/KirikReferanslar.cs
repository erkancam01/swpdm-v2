using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// KIRIK REFERANSLAR: dosyanin icinde yazan bir yolun karsiligi ne taranan
/// agacta ne diskteki yazili yerde VAR.
///
/// En degerli rapor bu: bir montaj acildiginda SOLIDWORKS'un "dosya
/// bulunamadi" diyecegi durumlari, montaji hic acmadan onceden gosteriyor.
///
/// Eskiden "bulunamadi" iki ayri seyi birden kapsiyordu: gercekten kayip
/// dosya VE taranan kokun disinda duran saglam dosya. Ikincisi artik
/// <see cref="CozumDurumu.KokDisinda"/> olarak ayrildi ve KENDI raporunda
/// (<see cref="KokDisindakiler"/>) listeleniyor - SOLIDWORKS o dosyalari
/// acar, kirik degiller. Burada kalan, gercekten kayip olanlar.
/// </summary>
internal sealed class KirikReferanslar : IRapor
{
    /// <inheritdoc/>
    public string Ad => "Kırık referanslar";

    /// <inheritdoc/>
    public string Aciklama => "İçinde yazan bir dosya ne taranan ağaçta ne yazılı yerde bulundu.";

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
