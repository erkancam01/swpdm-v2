using System;
using System.Text;

namespace SwPdm.Cekirdek;

/// <summary>
/// DOSYANIN ICINE YAZILACAK YOL NASIL KURULUR.
///
/// <see cref="SwYazici"/>'dan AYRILDI: o dosya "kabi nasil yamaliyorum"
/// sorusunun yeri; buradaki soru "yerine ne yazacagim". Yazici 645 satira
/// cikinca boyut kapisi bunu soyledi (CLAUDE.md 7).
///
/// ============ TEK KURAL: UZUNLUK DEGISMEZ ============
///
/// OLCULDU (Erkan'in makinesi, 28.08.2026): yazilan dizenin karakter sayisi
/// degisirse SOLIDWORKS dosyayi ACMIYOR. Sebebi bulunamadi - akislarin
/// icinde ne toplam boy ne de dize konumu yaziyor (tarandi, 0 eslesme).
/// Sebep kovalanmadi (CLAUDE.md 1c); yerine SART SAGLANIYOR.
///
/// Fark KLASOR kismindan karsilaniyor. Bu mesru cunku CLAUDE.md 5'te
/// olculdu: SOLIDWORKS once EBEVEYNIN YANINA bakiyor ve bu, yazili mutlak
/// yolun onune geciyor.
/// </summary>
public static class YazilacakYol
{
    /// <summary>
    /// Yeni adi tasiyan, ama KARAKTER SAYISI eskisiyle AYNI olan bir yol uretir.
    /// Uretemezse null.
    ///
    /// NEDEN GEREKLI - OLCULDU (28.08.2026, Erkan'in makinesinde):
    ///   ayni uzunluktaki ad degisimi  -> dosya ACILDI, parcalar yerinde
    ///   daha uzun ad                  -> SOLIDWORKS HATA VERDI, acmadi
    /// Akisin icinde ne toplam boy ne de dize ofseti yaziyor (arandi, 0
    /// eslesme), yani kirilmanin sebebi bulunamadi. Sebebi kovalamak yerine
    /// SARTI SAGLIYORUZ: dizenin uzunlugu hic degismesin.
    ///
    /// FARK KLASOR KISMINDAN KARSILANIYOR. Bu mesru: CLAUDE.md 5'te olculdu -
    /// SOLIDWORKS once EBEVEYNIN YANINA bakiyor ve bu, yazili mutlak yolun
    /// onune geciyor. Yani yazili klasor bir ipucu; belirleyici olan dosya
    /// adi ve ebeveyne gore konum.
    ///
    ///   ad KISALDIYSA  -> araya ".\" eklenir (yol ayni yeri gosterir)
    ///   ad UZADIYSA    -> soldan klasor atilir (yol GORELI hale gelir),
    ///                     sonra gerekirse ".\" ile tam uzunluga doldurulur
    ///
    /// BURADA OLCULEMEZ: SOLIDWORKS dolgulu/goreli yolu kabul ediyor mu.
    /// araclar/DeneyUretici'nin ikinci turu bunu soruyor.
    /// </summary>
    public static string? AdDegisimi(string? eskiYol, string? yeniAd)
    {
        if (string.IsNullOrEmpty(eskiYol) || string.IsNullOrEmpty(yeniAd))
        {
            return null;
        }

        string klasor = WindowsYolu.Klasor(eskiYol);
        string aday = klasor.Length == 0 ? yeniAd : WindowsYolu.Birlestir(klasor, yeniAd);
        return Ayarla(eskiYol.Length, aday);
    }

    /// <summary>
    /// <paramref name="aday"/> yolunu TAM OLARAK
    /// <paramref name="hedefUzunluk"/> karaktere getirir; yapamazsa null.
    ///
    ///   kisaysa -> adin onune ".\" eklenir (yol ayni yeri gosterir)
    ///   uzunsa  -> soldan klasor atilir; yol GORELI olur
    ///
    /// <paramref name="kirpmayaIzinVer"/> false ise uzun aday REDDEDILIR.
    /// TASIMADA bu sart: soldan kirpilmis bir yol ebeveynin yanini gosterir,
    /// oysa dosya baska klasore gitti - yani kirpma yanlis yeri isaret ederdi.
    /// Ad degisiminde ise dosya ebeveynin yaninda kaliyor ve kirpma zararsiz
    /// (olculdu: acildi).
    /// </summary>
    public static string? Ayarla(int hedefUzunluk, string? aday, bool kirpmayaIzinVer = true)
    {
        if (string.IsNullOrEmpty(aday) || hedefUzunluk <= 0)
        {
            return null;
        }

        string ad = WindowsYolu.DosyaAdi(aday);
        string klasor = WindowsYolu.Klasor(aday);
        string simdiki = aday;

        while (simdiki.Length > hedefUzunluk)
        {
            if (!kirpmayaIzinVer || klasor.Length == 0)
            {
                return null;
            }

            klasor = SoldanBirParcaAt(klasor);
            simdiki = klasor.Length == 0 ? ad : WindowsYolu.Birlestir(klasor, ad);
        }

        return Doldur(klasor, ad, hedefUzunluk - simdiki.Length);
    }

    /// <summary>Yolun EN SOLDAKI parcasini atar; kalmadiysa bos doner.</summary>
    private static string SoldanBirParcaAt(string klasor)
    {
        int i = klasor.IndexOf(WindowsYolu.Ayirici);
        return i < 0 ? string.Empty : klasor[(i + 1)..];
    }

    /// <summary>
    /// Dosya adinin onune, yolu ayni yeri gosterecek sekilde
    /// <paramref name="eksik"/> karakter ekler.
    ///
    /// ".\" iki karakter; TEK karakter gerekiyorsa ayirici cift yazilir
    /// ("a\\b"), Windows bunu tek ayirici sayar.
    /// </summary>
    private static string? Doldur(string klasor, string yeniAd, int eksik)
    {
        if (eksik < 0)
        {
            return null;
        }

        var dolgu = new System.Text.StringBuilder();
        if (eksik % 2 == 1)
        {
            dolgu.Append(WindowsYolu.Ayirici);
            eksik--;
        }

        for (int i = 0; i < eksik / 2; i++)
        {
            dolgu.Append('.').Append(WindowsYolu.Ayirici);
        }

        string taban = klasor.Length == 0
            ? string.Empty
            : klasor + WindowsYolu.Ayirici;

        return taban + dolgu + yeniAd;
    }

    /// <summary>
    /// TASIMA icin yeni yazili deger: once ebeveyne GORELI, sigmazsa MUTLAK.
    /// KIRPMA YOK - kirpilmis bir yol ebeveynin yanini gosterir ve dosya
    /// orada degil (ad degisiminden farki tam olarak bu).
    /// </summary>
    public static string? Tasima(string yazilan, string yeniTamYol, string? ebeveynKlasoru)
    {
        int hedef = yazilan.Length;

        string? goreli = WindowsYolu.Goreli(ebeveynKlasoru, yeniTamYol);
        if (goreli is not null)
        {
            string? ayarli = Ayarla(hedef, goreli, kirpmayaIzinVer: false);
            if (ayarli is not null)
            {
                return ayarli;
            }
        }

        return Ayarla(hedef, yeniTamYol, kirpmayaIzinVer: false);
    }
}
