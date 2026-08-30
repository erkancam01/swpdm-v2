using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// KOK DISINDAKILER: referans, acik kokun DISINDA duran gercek bir dosyayi
/// gosteriyor (kutuphane klasoru, baska surucu). Dosya diskte var ve
/// SOLIDWORKS acar - yani bunlar KIRIK DEGIL.
///
/// NEDEN AYRI RAPOR: bu satirlar eskiden "Kırık referanslar"a karisiyordu
/// ve kullanici saglam bir kutuphane bagini kayip dosya saniyordu. Ayrildi
/// (Erkan'in sectigi is, 30.08.2026); simdi "gercekten kayip mi, sadece
/// agacimizin disinda mi" sorusu tek bakista ayriliyor.
///
/// NEDEN YINE DE LISTELENIYOR: bu dosyalari BIZ GOREMEYIZ - tasima ve ad
/// degistirme onarimlari yalnizca taranan agacta calisir. Kok disindaki bir
/// referansi olan dosyayi tasimadan once kullanici bunu bilmeli.
/// </summary>
internal sealed class KokDisindakiler : IRapor
{
    /// <inheritdoc/>
    public string Ad => "Kök dışındakiler";

    /// <inheritdoc/>
    public string Aciklama =>
        "Referans, açık kökün dışında duran gerçek bir dosyayı gösteriyor; kırık değil.";

    /// <inheritdoc/>
    public RaporSonucu Uret(ReferansIndeksi indeks)
    {
        var satirlar = new List<RaporSatiri>();

        foreach (IndeksKaydi kayit in indeks.Kayitlar)
        {
            foreach (string yazilan in kayit.YazilanYollar)
            {
                Cozum cozum = indeks.Coz(kayit, yazilan);
                if (cozum.Durum == CozumDurumu.KokDisinda)
                {
                    satirlar.Add(new RaporSatiri(
                        kayit.Yol,
                        $"\"{WindowsYolu.DosyaAdi(yazilan)}\" kök dışında  ({cozum.Yol})"));
                }
            }
        }

        return RaporSonucu.Denetle(indeks, satirlar);
    }
}
