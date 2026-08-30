using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// BAYAT YOLLAR: dosya duruyor ama ONU KULLANAN belgenin icindeki yol
/// baska yeri gosteriyor - yani SOLIDWORKS acamaz.
///
/// "KIRIK REFERANSLAR"DAN FARKI: orada dosya BULUNAMIYOR (silinmis ya da
/// taranan agacin disinda) ve onarilacak bir hedef yok. Burada dosya
/// DURUYOR; yalnizca yazili yol eskimis. Yani bu rapor DUZELTILEBILIR.
///
/// NEDEN VAR - GERCEK BIR HATA GORUNMEZ KALDI (Erkan, 28.08.2026):
/// referans paneli "içinde" diyordu cunku BIZ dosyayi ADA ve KOMSULUGA gore
/// buluyoruz; SOLIDWORKS ise yazili yola bakip bulamiyordu. Rapor bu farki
/// tek listede topluyor.
///
/// Bu durum uygulamanin kendi tasimalarinda ARTIK OLUSMUYOR (tasima
/// onariyor). Rapor GECMISI toparlamak icin: bu surumden onceki tasimalar
/// ve Gezgin'de yapilan tasimalar.
/// </summary>
internal sealed class BayatYollar : IRapor
{
    /// <inheritdoc/>
    public string Ad => "Bayat yollar";

    /// <inheritdoc/>
    public string Aciklama =>
        "Dosya duruyor ama onu kullanan belgenin içindeki yol başka yeri gösteriyor "
        + "— SOLIDWORKS açamaz. Bu liste DÜZELTİLEBİLİR.";

    /// <inheritdoc/>
    public RaporSonucu Uret(ReferansIndeksi indeks)
    {
        var satirlar = new List<RaporSatiri>();

        foreach (IndeksKaydi kayit in indeks.Kayitlar)
        {
            foreach (string yazilan in kayit.YazilanYollar)
            {
                Cozum cozum = indeks.Coz(kayit, yazilan);
                if (ReferansIndeksi.BayatMi(kayit.Yol, yazilan, cozum))
                {
                    satirlar.Add(new RaporSatiri(
                        kayit.Yol,
                        $"\"{WindowsYolu.DosyaAdi(yazilan)}\" artık {cozum.Yol} konumunda; "
                        + $"içinde yazan yol: {yazilan}"));
                }
            }
        }

        return RaporSonucu.Denetle(indeks, satirlar);
    }

    /// <inheritdoc/>
    public OnarimOzeti? Duzelt(ReferansIndeksi indeks) => YolBaglama.BayatlariOnar(indeks);
}
