using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// TASINAN DOSYANIN KENDI YOLLARI - onarimin ikinci yonu.
///
/// <see cref="ReferansOnarimi"/>'nin ayni ozelligi; ayri dosya olmasinin TEK
/// sebebi boyut kapisi (600 satir): ikisi bir arada 614 satir ediyordu.
/// Kesme yeri satir sayisina degil KONUYA gore (CLAUDE.md 1b): oteki dosya
/// "bu dosyayi KULLANANLARI onar", burasi "bu dosyanin KENDI icini onar".
/// </summary>
public static partial class ReferansOnarimi
{
    /// <summary>
    /// TASINAN DOSYANIN KENDI ICINDEKI yollarindan, TASIMA YUZUNDEN
    /// cozulemez hale gelecek olanlari onaran planlar.
    ///
    /// NEDEN GEREKLI - OLCULDU (02.09.2026, Erkan'in gercek takiminda):
    /// SOLIDWORKS bir cocugu once EBEVEYNIN YANINDA ariyor, bulamazsa
    /// dosyanin icinde YAZILI yola bakiyor (CLAUDE.md 5). Dosyalar bir
    /// yerden KOPYALANIP getirildiyse icindeki yollar hala eski yeri
    /// gosterir; hepsi ayni klasorde durdugu surece KOMSULUK KURALI bunu
    /// gizler ve her sey calisir.
    ///
    /// TASIMA tam da o komsulugu bozar. Eski yol aciga cikar: panel
    /// "hepsi kırık" der VE SOLIDWORKS de gercekten acamaz. Yani tasima
    /// yeni bir kirik URETMIYOR, var olani GORUNUR kiliyor - ama sonuc
    /// kullanici icin ayni: dosya acilmiyor.
    ///
    /// O YUZDEN: tasimadan SONRA komsulukla cozulemeyecek her yazili yol,
    /// cocugun GERCEK konumuna yeniden yazilir.
    ///
    /// KAPSAM DAR TUTULUYOR (CLAUDE.md 1a):
    ///   - Zaten tasimadan ONCE de bayat olan yola DOKUNULMAZ. O, bu
    ///     tasimanin urettigi bir sey degil; "Bulunanları düzelt"in isi.
    ///   - Birlikte tasinan cocuga DOKUNULMAZ: komsulugu bozulmadi.
    ///   - Bulunamayan cocuga DOKUNULMAZ: yazilacak bir hedef yok.
    ///
    /// Planlar olagan <see cref="OnarimPlani"/>; yani Ctrl+Z ile geri alma
    /// ve Ctrl+Y ile ileri alma bedavaya geliyor (CLAUDE.md 1b).
    /// </summary>
    private static IEnumerable<OnarimPlani> KendiYollariPlanlari(
        ReferansIndeksi indeks, string eskiYol, string yeniYol, IReadOnlyList<string>? harictut)
    {
        // Klasor degismediyse komsuluk da degismemistir: yalnizca ad degisti.
        if (string.Equals(
                WindowsYolu.Klasor(eskiYol), WindowsYolu.Klasor(yeniYol),
                StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        if (indeks.Kayit(eskiYol) is not IndeksKaydi kayit)
        {
            yield break;
        }

        foreach (string yazilan in kayit.YazilanYollar)
        {
            Cozum cozum = indeks.Coz(kayit, yazilan);
            if (cozum.Durum != CozumDurumu.Bulundu || cozum.Yol is not string gercek)
            {
                continue;   // yazilacak hedef yok
            }

            if (harictut is not null && Icinde(harictut, gercek))
            {
                continue;   // birlikte tasindi; komsulugu duruyor
            }

            // ONCE saglamdi, SONRA bayat olacak: onarilacak olan tam bu.
            if (ReferansIndeksi.BayatMi(eskiYol, yazilan, cozum)
                || !ReferansIndeksi.BayatMi(yeniYol, yazilan, cozum))
            {
                continue;
            }

            yield return PlanlaBilinenlerle([yeniYol], yazilan, gercek, cocuguTasi: false);
        }
    }
}
