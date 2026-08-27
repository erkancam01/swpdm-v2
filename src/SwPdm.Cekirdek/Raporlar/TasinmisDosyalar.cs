using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// TASINMIS DOSYALAR: dosyanin ICINDE yazan "en son buraya kaydedildim"
/// yolu, dosyanin SIMDIKI yerinden farkli.
///
/// NE ANLAMA GELIR: dosya son kaydedilisinden sonra tasindi ya da
/// kopyalandi. Kendisi sorunsuz acilir - ama ONU KULLANAN dosyalarin
/// icindeki yol artik bayattir. SOLIDWORKS cogu zaman yanindaki kopyayi
/// bulup acmaya devam eder (CLAUDE.md 5'te olculdu), o yuzden bu bir HATA
/// DEGIL, bir UYARIDIR.
///
/// DURUST NOT: bir klasoru baska bir makineden kopyaladiysaniz BUTUN
/// dosyalar burada gorunur - cunku hepsinin icinde eski makinenin yolu
/// yazilidir. Bu dogru davranistir, gurultu degil: o yollarin hepsi
/// gercekten bayattir.
/// </summary>
internal sealed class TasinmisDosyalar : IRapor
{
    /// <inheritdoc/>
    public string Ad => "Taşınmış dosyalar";

    /// <inheritdoc/>
    public string Aciklama => "Son kaydedildiği yer ile şimdiki yeri farklı olan dosyalar.";

    /// <inheritdoc/>
    public RaporSonucu Uret(ReferansIndeksi indeks)
    {
        var satirlar = new List<RaporSatiri>();

        foreach (IndeksKaydi kayit in indeks.Kayitlar)
        {
            if (kayit.KendiYolu is null)
            {
                continue;
            }

            if (!string.Equals(kayit.KendiYolu, kayit.Yol, StringComparison.OrdinalIgnoreCase))
            {
                satirlar.Add(new RaporSatiri(
                    kayit.Yol, "Son kaydedildiği yer: " + kayit.KendiYolu));
            }
        }

        return RaporSonucu.Denetle(indeks, satirlar);
    }
}
