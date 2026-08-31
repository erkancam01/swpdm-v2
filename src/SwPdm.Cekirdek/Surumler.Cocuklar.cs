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
/// COZUMLEME SOLIDWORKS'UN KENDI KURALIYLA (CLAUDE.md 5'te olculdu):
///   1. EBEVEYNIN YANINDAKI ayni adli dosya kazanir - yazili mutlak yolun
///      onune geciyor.
///   2. Yanında yoksa yazili yolun kendisi denenir.
///   3. Ikisi de yoksa COZULEMEDI sayilir; UYDURULMAZ (CLAUDE.md 3).
/// Indekse ihtiyac yok: bu kural diskte dogrudan yoklanabiliyor ve cekirdegin
/// arayuz durumundan bagimsiz kalmasini sagliyor.
/// </summary>
public static partial class Surumler
{
    /// <summary>Ic ice montajda dip yapmamak icin derinlik siniri.</summary>
    private const int EnFazlaDerinlik = 32;

    /// <summary>
    /// Belgenin arsivlenecek butun cocuklari - TORUNLAR DAHIL. Alt montajin
    /// parcalari da gerekiyor: yoksa arsivdeki alt montaj yine acilamaz.
    ///
    /// Belgenin KENDISI listede YOKTUR. Ayni dosya iki kez sayilmaz (dongu
    /// korumasi: bir montaj kendi alt montajini iki yerde kullanabilir).
    /// </summary>
    public static CocukKumesi Cocuklari(string? yol)
    {
        var yollar = new List<string>();
        var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int cozulemeyen = 0;

        if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol))
        {
            return new CocukKumesi(yollar, 0);
        }

        gorulen.Add(yol);
        Topla(yol, yollar, gorulen, ref cozulemeyen, 0);
        return new CocukKumesi(yollar, cozulemeyen);
    }

    private static void Topla(
        string yol, List<string> yollar, HashSet<string> gorulen,
        ref int cozulemeyen, int derinlik)
    {
        if (derinlik >= EnFazlaDerinlik)
        {
            return;
        }

        SwReferanslar referanslar = SwReferans.Oku(yol);
        if (!referanslar.Okundu)
        {
            // Okunamayan belge "referansi yok" DEMEK DEGIL (CLAUDE.md 3);
            // eksik arsivlemis olabiliriz, sayilir.
            cozulemeyen++;
            return;
        }

        foreach (string yazilan in referanslar.Dogrudan)
        {
            string? cocuk = Bul(yazilan, yol);
            if (cocuk is null)
            {
                cozulemeyen++;
                continue;
            }

            if (!gorulen.Add(cocuk))
            {
                continue;   // ayni dosya iki kez arsivlenmez
            }

            yollar.Add(cocuk);
            Topla(cocuk, yollar, gorulen, ref cozulemeyen, derinlik + 1);
        }
    }

    /// <summary>
    /// Yazili bir referansin diskteki karsiligi: once EBEVEYNIN YANI, sonra
    /// yazili yolun kendisi. SOLIDWORKS'un sirasi bu (CLAUDE.md 5).
    /// </summary>
    private static string? Bul(string yazilanYol, string ebeveynYolu)
    {
        string ad = WindowsYolu.DosyaAdi(yazilanYol);
        if (ad.Length == 0)
        {
            return null;
        }

        try
        {
            string komsu = WindowsYolu.Birlestir(WindowsYolu.Klasor(ebeveynYolu), ad);
            if (File.Exists(komsu))
            {
                return komsu;
            }

            return File.Exists(yazilanYol) ? yazilanYol : null;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
