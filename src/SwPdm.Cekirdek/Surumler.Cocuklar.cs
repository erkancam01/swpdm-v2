using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Bir belgenin arsivlenecek cocuklari.</summary>
/// <param name="Yollar">Cozulmus gercek dosya yollari (torunlar dahil).</param>
/// <param name="Cozulemeyen">Yazili ama diskte bulunamayan referans sayisi.</param>
public sealed record CocukKumesi(IReadOnlyList<string> Yollar, int Cozulemeyen);

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
        int cozulemeyen = 0;

        foreach (AgacDugumu dugum in BelgeAgaci.Yur(yol))
        {
            if (dugum.Sorunlu)
            {
                // EKSIK ARSIV SESSIZ GECILMEZ (CLAUDE.md 3): bulunamayan bir
                // cocuk da, icine bakilamayan bir belge de sayilir - ikisi de
                // "bu versiyon eksik olabilir" demek.
                cozulemeyen++;
            }

            if (dugum.Seviye == 0 || !dugum.Bulundu || !gorulen.Add(dugum.Yol))
            {
                continue;
            }

            yollar.Add(dugum.Yol);
        }

        return new CocukKumesi(yollar, cozulemeyen);
    }
}
