using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>Yazili bir yolun gercek dosyayla eslestirilme sonucu.</summary>
public enum CozumDurumu
{
    /// <summary>Tek bir dosyaya cozuldu.</summary>
    Bulundu,

    /// <summary>Birden cok aday var ve hicbiri digerinden ustun degil.</summary>
    Belirsiz,

    /// <summary>Bu adda dosya taranan agacta YOK.</summary>
    Bulunamadi,
}

/// <summary>Cozum sonucu.</summary>
/// <param name="Durum">Ne oldu.</param>
/// <param name="Yol">Cozulduyse gercek dosyanin yolu; yoksa null.</param>
/// <param name="Adaylar">Belirsizse butun adaylar; digerlerinde bos.</param>
public sealed record Cozum(CozumDurumu Durum, string? Yol, IReadOnlyList<string> Adaylar)
{
    /// <summary>Bulunamamis sonuc.</summary>
    public static readonly Cozum Yok = new(CozumDurumu.Bulunamadi, null, []);
}

/// <summary>
/// YAZILI YOL HANGI GERCEK DOSYA - planin en kritik kurali.
///
/// NEDEN GEREKLI: dosyalarin icindeki yollar MUTLAK ve YAZARIN makinesine
/// ait (olculdu: "C:\Users\PC\Desktop\tertemiz\Parça1.SLDPRT"). Ayni
/// dosyalar baska bir makinede "\\sunucu\ortak\proje\..." altinda duruyor.
/// Yani TAM YOL ESLESTIRMESI CALISMAZ; ada gore eslestirmek zorundayiz.
///
/// SIRA TAHMIN DEGIL - CLAUDE.md 5'te OLCULMUS SOLIDWORKS davranisinin
/// kendisi: bir klasor tasindiginda dosyanin icinde yazan eski yol hala
/// gecerliyken bile SOLIDWORKS YANINDAKI kopyayi secti. Yani "ebeveynin
/// yanindaki dosya, yazili mutlak yolun onune geciyor". Kural bunu izliyor:
///
///   1. Ada gore adaylar (buyuk/kucuk harf duyarsiz)
///   2. Tek aday            -> o
///   3. Birden cok aday     -> KAYNAKLA AYNI KLASORDE olan kazanir
///   4. Hala birden cok     -> yazili tam yola esit olan kazanir
///   5. Hala birden cok     -> BELIRSIZ; tek cevap UYDURULMAZ
///
/// 5. maddenin sebebi CLAUDE.md 3: yanlis bir "bu dosya" cevabi, kullaniciya
/// yanlis dosyayi sildirir. Karar verilemiyorsa adaylar oldugu gibi
/// gosterilir.
///
/// BULUNAMADI ile TARANMADI AYNI SEY DEGILDIR ve burada karistirilmaz: bu
/// sinif yalnizca ELINDEKI adaylara bakar. "Taranmamis kokun disinda mi"
/// sorusunu indeks cevaplar, cunku taramanin nereye kadar gittigini o bilir.
/// </summary>
public static class ReferansCozucu
{
    /// <summary>
    /// Yazili yolu adaylar arasinda cozer.
    /// </summary>
    /// <param name="yazilanYol">Dosyanin icinde yazan yol.</param>
    /// <param name="kaynakDosyaYolu">Bu yolu yazan belgenin GERCEK yolu.</param>
    /// <param name="adaylar">Ayni ada sahip, taranmis gercek dosyalar.</param>
    public static Cozum Coz(
        string yazilanYol, string kaynakDosyaYolu, IReadOnlyList<string> adaylar)
    {
        ArgumentNullException.ThrowIfNull(adaylar);

        if (adaylar.Count == 0)
        {
            return Cozum.Yok;
        }

        if (adaylar.Count == 1)
        {
            return new Cozum(CozumDurumu.Bulundu, adaylar[0], []);
        }

        // 3 - komsu kazanir (SOLIDWORKS'un kendi davranisi)
        string kaynakKlasor = WindowsYolu.Klasor(kaynakDosyaYolu);
        List<string> komsular = Suz(adaylar, a => Ayni(WindowsYolu.Klasor(a), kaynakKlasor));
        if (komsular.Count == 1)
        {
            return new Cozum(CozumDurumu.Bulundu, komsular[0], []);
        }

        IReadOnlyList<string> kalan = komsular.Count > 1 ? komsular : adaylar;

        // 4 - yazili tam yola esit olan
        List<string> tamEsit = Suz(kalan, a => Ayni(a, yazilanYol));
        if (tamEsit.Count == 1)
        {
            return new Cozum(CozumDurumu.Bulundu, tamEsit[0], []);
        }

        // 5 - karar verilemedi; UYDURULMAZ
        return new Cozum(CozumDurumu.Belirsiz, null, kalan);
    }

    private static List<string> Suz(IReadOnlyList<string> kaynak, Func<string, bool> kosul)
    {
        var sonuc = new List<string>();
        foreach (string a in kaynak)
        {
            if (kosul(a))
            {
                sonuc.Add(a);
            }
        }

        return sonuc;
    }

    private static bool Ayni(string? a, string? b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
