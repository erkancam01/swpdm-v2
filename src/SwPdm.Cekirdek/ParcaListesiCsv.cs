using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SwPdm.Cekirdek;

/// <summary>
/// PARCA LISTESINI CSV'YE YAZAR - Excel'de acilmak uzere.
///
/// IKI KARAR, IKISI DE TURKCE WINDOWS ICIN:
///
/// 1. AYRAC ";" - virgul DEGIL. Turkce Windows'ta ondalik ayraci virgul
///    oldugu icin Excel, CSV'yi acarken liste ayraci olarak ";" bekliyor;
///    virgullu bir dosyayi TEK SUTUN okur. (Yaygin davranis; Erkan'in
///    makinesinde OLCULMEDI - surum notunda "sende olculecek" diye yaziyor.)
///
/// 2. UTF-8 BOM - Excel BOM'suz bir UTF-8 dosyayi makinenin eski kod
///    sayfasiyla okuyor ve "Parça" bozuluyor. BOM tek basina bunu cozuyor.
///
/// BAYAT AGIRLIK UYARISI DOSYANIN ILK SATIRINDA. Ozel ozellik degerleri
/// (Malzeme, Ağırlık…) dosyada duran SON HESAPLANMIS degerler; yeniden
/// hesaplanmadilar. Bunu yazmamak, tabloyu teklife ceviren birinin bayat bir
/// agirlikla fiyat vermesi demek (CLAUDE.md 3). Excel'de ilk satir tek
/// hucrede gorunur, basliklar ikinci satirdadir.
/// </summary>
public static class ParcaListesiCsv
{
    /// <summary>Ayrac. Tek yerde (CLAUDE.md 8).</summary>
    public const char Ayrac = ';';

    /// <summary>Dosyanin ilk satiri: neyin bayat olabilecegi.</summary>
    public const string Uyari =
        "NOT: Özel özellik değerleri (Malzeme, Ağırlık…) dosyada yazan SON HESAPLANMIŞ "
        + "değerlerdir — model yeniden oluşturulmadıysa bayat olabilir.";

    /// <summary>Listenin CSV metni. Satir sonu CRLF - Excel bunu bekliyor.</summary>
    public static string Metin(ParcaListesiSonucu sonuc)
    {
        ArgumentNullException.ThrowIfNull(sonuc);

        var metin = new StringBuilder();
        Satir(metin, [Uyari]);

        var basliklar = new List<string>
        {
            "Seviye", "Ad", "Tür", "Kaç yerde geçiyor", "Yapılandırma",
            "Son kaydeden", "Değiştirme",
        };
        basliklar.AddRange(sonuc.OzelSutunlar);
        basliklar.Add("Durum");
        basliklar.Add("Yol");
        Satir(metin, basliklar);

        foreach (ParcaSatiri satir in sonuc.Satirlar)
        {
            var hucreler = new List<string>(basliklar.Count)
            {
                satir.Seviye.ToString(CultureInfo.InvariantCulture),
                satir.Ad,
                DosyaTurleri.Adi(satir.Tur),

                // KOK SATIRINDA SAYI YOK: "0 yerde geciyor" yanlis okunurdu -
                // secilen belgenin kendisi bir yerde "geciyor" degil.
                satir.Seviye == 0
                    ? string.Empty
                    : satir.KacYerde.ToString(CultureInfo.InvariantCulture),
                satir.Yapilandirma ?? string.Empty,
                satir.SonKaydeden ?? string.Empty,
                satir.Degistirme is DateTime zaman ? Zaman.Yaz(zaman) : string.Empty,
            };

            foreach (string sutun in sonuc.OzelSutunlar)
            {
                hucreler.Add(satir.Ozel.TryGetValue(sutun, out string? deger) ? deger : string.Empty);
            }

            hucreler.Add(satir.Durum ?? string.Empty);
            hucreler.Add(satir.Yol);
            Satir(metin, hucreler);
        }

        return metin.ToString();
    }

    /// <summary>
    /// Metni dosyaya yazar. BOM'lu UTF-8; yoksa Turkce karakterler Excel'de
    /// bozulur.
    /// </summary>
    public static IslemRaporu Yaz(string yol, ParcaListesiSonucu sonuc)
    {
        try
        {
            File.WriteAllText(yol, Metin(sonuc), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return IslemRaporu.Basarili(yol);
        }
        catch (Exception hata) when (IslemSonuclari.DiskHatasi(hata))
        {
            return IslemSonuclari.HatayiCevir(hata);
        }
    }

    private static void Satir(StringBuilder metin, IReadOnlyList<string> hucreler)
    {
        for (int i = 0; i < hucreler.Count; i++)
        {
            if (i > 0)
            {
                metin.Append(Ayrac);
            }

            metin.Append(Kacisla(hucreler[i]));
        }

        metin.Append("\r\n");
    }

    /// <summary>
    /// Ayrac, tirnak ya da satir sonu iceren deger tirnaklanir; icerideki
    /// tirnak IKIYE katlanir (CSV'nin kendi kacisi). Bu olmazsa bir malzeme
    /// adindaki ";" satiri ikiye boler ve butun sutunlar kayar - hicbir hata
    /// vermeden.
    /// </summary>
    private static string Kacisla(string? deger)
    {
        string metin = deger ?? string.Empty;
        bool gerekli = metin.IndexOf(Ayrac) >= 0
            || metin.IndexOf('"') >= 0
            || metin.IndexOf('\n') >= 0
            || metin.IndexOf('\r') >= 0;

        return gerekli ? "\"" + metin.Replace("\"", "\"\"") + "\"" : metin;
    }
}
