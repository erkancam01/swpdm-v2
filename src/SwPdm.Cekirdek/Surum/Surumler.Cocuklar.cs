using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Bir belgenin arsivlenecek cocuklari.</summary>
/// <param name="Yollar">Cozulmus gercek dosya yollari (torunlar dahil).</param>
/// <param name="Cozulemeyen">
/// Yazili ama diskte bulunamayan AYRI dosya sayisi.
///
/// DOSYA SAYIYOR, GECIS DEGIL - 01.09.2026'da duzeldi: sayac tekillemenin
/// ONUNDEYDI ve bulunamayan dugumler kumeye hic girmiyordu, yani on
/// montajda gecen TEK bir kayip parca "10 referans bulunamadi" diye
/// yaziliyordu. Kullanici o sayiya bakip arsivin ne kadar eksik oldugunu
/// tahmin ediyor (CLAUDE.md 3).
/// </param>
/// <param name="Dogrudan">
/// Bunlarin kaci belgenin DOGRUDAN cocugu (seviye 1); gerisi torun.
///
/// NEDEN AYRI SAYILIYOR: kutu "241 dosya" derken panel "İÇİNDEKİLER 14"
/// diyor ve ikisi AYRI SEY sayiyor. Fark ekranda yazmayinca kullanici
/// bunu celiski okuyor - Erkan 01.09.2026'da tam bunu bildirdi.
/// </param>
public sealed record CocukKumesi(
    IReadOnlyList<string> Yollar, int Cozulemeyen, int Dogrudan = 0);

/// <summary>
/// VERSIYONA GIRECEK COCUKLAR - "o gunku hal" ne demek.
///
/// NEDEN (Erkan, 31.08.2026: "part dosyası eskiden ne güzel versiyon
/// çalışıyordu, diğerleri de öyle olamaz mı"): parcanin arsiv kopyasi KENDI
/// KENDINE YETIYOR, montajinki yetmiyordu - parcalari yaninda olmadigi icin
/// SOLIDWORKS onu acamiyordu. Cozum, versiyonu kendi kendine yeter yapmak:
/// montajla birlikte o gunku cocuklari da arsivlenir.
///
/// AGACI YURUYEN KOD BURADA DEGIL: <see cref="BelgeAgaci"/>. Orasi ORTAK
/// ARAC - SOLIDWORKS'un cozme kurali (CLAUDE.md 1b); burada kalan tek sey,
/// o agaci "arsivlenecek dosya listesi"ne cevirmek.
/// </summary>
public static partial class Surumler
{
    /// <summary>
    /// Belgenin arsivlenecek butun cocuklari - TORUNLAR DAHIL. Alt montajin
    /// parcalari da gerekiyor: yoksa arsivdeki alt montaj yine acilamaz.
    ///
    /// Belgenin KENDISI listede YOKTUR. Ayni dosya iki kez sayilmaz (bir
    /// montaj kendi alt montajini iki yerde kullanabilir; kopya tektir).
    /// </summary>
    public static CocukKumesi Cocuklari(string? yol)
    {
        var yollar = new List<string>();
        if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol))
        {
            return new CocukKumesi(yollar, 0);
        }

        var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { yol };

        // SORUNLULAR ICIN AYRI KUME: onlar "yollar"a girmiyor, yani ayni
        // kumeyle tekillenemezler - kendi kumeleri olmazsa GECIS sayilir.
        var sorunlular = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int dogrudan = 0;

        foreach (AgacDugumu dugum in BelgeAgaci.Yur(yol))
        {
            if (dugum.Sorunlu)
            {
                // EKSIK ARSIV SESSIZ GECILMEZ (CLAUDE.md 3): bulunamayan bir
                // cocuk da, icine bakilamayan bir belge de sayilir - ikisi de
                // "bu versiyon eksik olabilir" demek. Ama AYRI DOSYA sayilir:
                // ayni kayip parca on montajda geciyorsa sorun BIR tanedir.
                sorunlular.Add(dugum.Yol);
            }

            if (dugum.Seviye == 0 || !dugum.Bulundu || !gorulen.Add(dugum.Yol))
            {
                continue;
            }

            if (dugum.Seviye == 1)
            {
                dogrudan++;
            }

            yollar.Add(dugum.Yol);
        }

        return new CocukKumesi(yollar, sorunlular.Count, dogrudan);
    }
}
