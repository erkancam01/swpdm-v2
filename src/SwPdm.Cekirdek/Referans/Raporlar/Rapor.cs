using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>Bir rapor satiri.</summary>
/// <param name="Yol">Ilgili dosyanin GERCEK yolu.</param>
/// <param name="Aciklama">Bu dosyada ne bulundu.</param>
public sealed record RaporSatiri(string Yol, string Aciklama);

/// <summary>Bir raporun sonucu.</summary>
/// <param name="Satirlar">Bulunanlar.</param>
/// <param name="Guvenilir">
/// Rapor TAM mi. false ise BOS LISTE "sorun yok" ANLAMINA GELMEZ.
/// </param>
/// <param name="Sebep">Guvenilir degilse neden.</param>
public sealed record RaporSonucu(IReadOnlyList<RaporSatiri> Satirlar, bool Guvenilir, string? Sebep)
{
    /// <summary>
    /// Indeksin durumuna bakip sonucu guvenilir isaretler.
    ///
    /// TEK YERDE duruyor cunku her rapor icin AYNI: taranmamis ya da yarim
    /// bir indeksten "sorun yok" sonucu cikarmak, bu uygulamada dosya
    /// sildiren hatanin ta kendisi (CLAUDE.md 3).
    /// </summary>
    public static RaporSonucu Denetle(ReferansIndeksi indeks, IReadOnlyList<RaporSatiri> satirlar)
    {
        ArgumentNullException.ThrowIfNull(indeks);

        if (indeks.TaramaZamani is null)
        {
            return new RaporSonucu(satirlar, Guvenilir: false, "Bu kök henüz taranmadı.");
        }

        if (!indeks.Tam)
        {
            return new RaporSonucu(
                satirlar, Guvenilir: false, "Tarama eksik; rapor eksik olabilir.");
        }

        return new RaporSonucu(satirlar, Guvenilir: true, null);
    }
}

/// <summary>Indeks uzerinde calisan bir rapor.</summary>
public interface IRapor
{
    /// <summary>Kullaniciya gorunen ad.</summary>
    string Ad { get; }

    /// <summary>Ne aradigini bir cumlede anlatir.</summary>
    string Aciklama { get; }

    /// <summary>Raporu uretir.</summary>
    RaporSonucu Uret(ReferansIndeksi indeks);

    /// <summary>
    /// Bulunanlari DUZELTIR. Duzeltilemeyen raporlar bunu EZMEZ ve null
    /// doner; arayuz de o sekmede "Düzelt" dugmesi GOSTERMEZ.
    ///
    /// NEDEN VARSAYILAN GOVDE: cogu rapor duzeltilemez (yetim parca, teknik
    /// resmi olmayan...). Her birine bos bir govde yazdirmak, listeye satir
    /// eklemenin bedelini artirirdi (CLAUDE.md 1b).
    /// </summary>
    /// <param name="kilitler">
    /// Kilitli klasorler - YAZAN her rapor bunu SORMAK ZORUNDA. Duzeltme
    /// secimden bagimsiz calisiyor, yani islemlerin kilit kapisi
    /// (Kilitler.Engel) buraya ulasmiyor.
    /// </param>
    OnarimOzeti? Duzelt(ReferansIndeksi indeks, KilitKumesi? kilitler) => null;
}

/// <summary>
/// RAPORLARIN TEK LISTESI.
///
/// CLAUDE.md 1b: rapor penceresi bu listeden URETILIR. Yeni bir rapor
/// eklemek = bir dosya yaz + buraya bir satir. Kaldirmak = dosyayi sil +
/// o satiri sil. Baska hicbir dosya degismez.
///
/// SIRA, kullanicinin gordugu siradir: once "bir sey kirik mi", sonra
/// "bir sey eksik mi", en sonda "neyi bilmiyoruz".
/// </summary>
public static class RaporListesi
{
    /// <summary>Butun raporlar, gosterim sirasiyla.</summary>
    public static readonly IReadOnlyList<IRapor> Tumu =
    [
        new KirikReferanslar(),
        new BayatYollar(),
        new Yetimler(),
        new TeknikResmiOlmayanlar(),
        new TasinmisDosyalar(),
        new OkunamayanDosyalar(),
    ];
}
