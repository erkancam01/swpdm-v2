using System;
using System.Collections.Generic;
using System.Globalization;

namespace SwPdm.Cekirdek;

/// <summary>Arama kutusundan cozulmus bir ozellik sorgusu.</summary>
/// <param name="Anahtar">Ozelligin adi ("Malzeme", "Kaydeden"...).</param>
/// <param name="Deger">Aranan deger; BOS ise "bu ozellik VAR" demek.</param>
public sealed record OzellikSorgusu(string Anahtar, string Deger);

/// <summary>Ozellik aramasinin sonucu.</summary>
/// <param name="Bulunanlar">Eslesen dosyalar (yol sirasiyla).</param>
/// <param name="SinirAsildi">Sinira takildi; liste EKSIK.</param>
/// <param name="IndeksOzeti">
/// Durum cubugundaki ozetin indeks kismi: nereden geldigi ve EKSIKLIK.
/// Cumleyi bu dosya uretir - eksikligi indeksle konusan tek yer burasi.
/// </param>
public sealed record OzellikAramaSonucu(
    IReadOnlyList<DosyaOgesi> Bulunanlar, bool SinirAsildi, string IndeksOzeti);

/// <summary>
/// OZELLIGE GORE ARAMA - "malzeme: pirinç" (Erkan'in sectigi is, 30.08.2026;
/// UX karari: ayri kutu degil, ayni arama kutusuna sozdizimi).
///
/// SOZDIZIMI: icinde ':' olan metin ozellik sorgusudur - Windows dosya
/// adinda ':' OLAMAZ, yani ad aramasiyla cakismaz. Ilk ':' boler:
/// "malzeme: pirinç" -> anahtar "malzeme", deger "pirinç".
///
/// MOTOR INDEKS, DISK DEGIL: her aramada 2341 dosyayi acip ozellik okumak
/// saniyeler surerdi (dosya basina ~66 KB, olculdu). Ozellikler tarama
/// sirasinda indekse aliniyor (IndeksTarama.Ozellikleri); sorgu bellekten
/// ve anliktir. Bedeli durustluk kurallari (CLAUDE.md 3):
///   - indeks taranmamissa arama KOSMAZ, sebep soylenir (cagiran bakar)
///   - ozellikleri okunmamis kayit varsa sonuc "eksik olabilir" der
///
/// ESLESME KULTUREL ve harf duyarsiz - ad aramasiyla ayni gerekce
/// (KlasorTarayici: "bu INSAN metni"). Anahtar TAM eslesir, deger ICERIR.
/// </summary>
public static class OzellikAramasi
{
    /// <summary>
    /// Metin bir ozellik sorgusu mu. Degilse null - cagiran ad aramasina
    /// devam eder. Anahtari bos olan (":deger") sorgu sayilmaz.
    /// </summary>
    public static OzellikSorgusu? Coz(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin))
        {
            return null;
        }

        int iki = metin.IndexOf(':', StringComparison.Ordinal);
        if (iki <= 0)
        {
            return null;
        }

        string anahtar = metin[..iki].Trim();
        if (anahtar.Length == 0)
        {
            return null;
        }

        return new OzellikSorgusu(anahtar, metin[(iki + 1)..].Trim());
    }

    /// <summary>
    /// Sorguyu indekste kosturur. Cagiran indeksin TARANMIS olmasini
    /// saglar (TaramaZamani null iken cagirmak sozlesme ihlali - sonuc
    /// "hicbir sey yok" gibi okunur ve o yanlis).
    /// </summary>
    public static OzellikAramaSonucu Ara(
        ReferansIndeksi indeks, OzellikSorgusu sorgu, int enFazla)
    {
        ArgumentNullException.ThrowIfNull(indeks);
        ArgumentNullException.ThrowIfNull(sorgu);

        CompareInfo karsilastirici = CultureInfo.CurrentCulture.CompareInfo;
        var bulunanlar = new List<DosyaOgesi>();
        bool sinirAsildi = false;
        int okunmayan = 0;

        foreach (IndeksKaydi kayit in indeks.Kayitlar)
        {
            if (kayit.Ozellikler is null)
            {
                okunmayan++;   // eski indeks ya da okunamayan dosya: BILMIYORUZ
                continue;
            }

            if (!Esliyor(kayit.Ozellikler, sorgu, karsilastirici))
            {
                continue;
            }

            if (bulunanlar.Count >= enFazla)
            {
                sinirAsildi = true;
                break;
            }

            bulunanlar.Add(new DosyaOgesi(
                kayit.Yol,
                WindowsYolu.DosyaAdi(kayit.Yol),
                DosyaTurleri.Tani(kayit.Yol),
                kayit.Boyut,
                kayit.Degistirme));
        }

        // Kararli sira: sozluk sirasi rastgele, ayni sorgu iki kosuda ayni
        // gorunmeli (kapinin parmak izi olcumu de buna dayaniyor).
        bulunanlar.Sort(
            (a, b) => string.Compare(a.Yol, b.Yol, StringComparison.OrdinalIgnoreCase));

        return new OzellikAramaSonucu(bulunanlar, sinirAsildi, Ozet(indeks, okunmayan));
    }

    private static bool Esliyor(
        IReadOnlyList<KeyValuePair<string, string>> ozellikler,
        OzellikSorgusu sorgu,
        CompareInfo karsilastirici)
    {
        foreach (KeyValuePair<string, string> o in ozellikler)
        {
            if (karsilastirici.Compare(o.Key, sorgu.Anahtar, CompareOptions.IgnoreCase) != 0)
            {
                continue;
            }

            // Deger BOS ise varlik sorgusu: "çizen:" bos degerli Çizen'i de
            // bulur - Erkan'in gercek verisinde Çizen bilerek bos girilmisti.
            if (sorgu.Deger.Length == 0
                || karsilastirici.IndexOf(o.Value, sorgu.Deger, CompareOptions.IgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Ozet cumlenin indeks kismi: kaynak + eksiklik.</summary>
    private static string Ozet(ReferansIndeksi indeks, int okunmayan)
    {
        var parcalar = new List<string> { "indeksten" };

        if (!indeks.Tam)
        {
            parcalar.Add("tarama eksik — sonuç eksik olabilir");
        }

        if (okunmayan > 0)
        {
            parcalar.Add($"{okunmayan} dosyanın özellikleri okunmadı — eksik olabilir");
        }

        return string.Join(" · ", parcalar);
    }
}
