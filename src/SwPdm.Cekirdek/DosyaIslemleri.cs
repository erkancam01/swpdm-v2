using System;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Bir dosya isleminin nasil bittigi.</summary>
public enum IslemSonucu
{
    /// <summary>Oldu.</summary>
    Tamam,

    /// <summary>Ad Windows'ta gecerli degil.</summary>
    GecersizAd,

    /// <summary>Hedefte ayni adda bir sey zaten var.</summary>
    ZatenVar,

    /// <summary>Kaynak bulunamadi.</summary>
    Bulunamadi,

    /// <summary>Izin yok ya da salt-okunur (Win32 5).</summary>
    Erisim,

    /// <summary>Baska bir program acik tutuyor (Win32 32).</summary>
    Kilitli,

    /// <summary>Klasorun ici bos degil (Win32 145) - GIZLI dosyalar dahil.</summary>
    Dolu,

    /// <summary>Hedef, kaynagin kendi altinda.</summary>
    KendiAltina,

    /// <summary>Ayirt edilemedi; sebep metni yine de verilir.</summary>
    Bilinmeyen,
}

/// <summary>
/// Bir islemin raporu. <see cref="Sebep"/> her zaman EKRANDA gosterilebilecek
/// bir cumledir (CLAUDE.md 3: sebep gizlenmez).
/// </summary>
public sealed record IslemRaporu(IslemSonucu Sonuc, string? YeniYol, string? Sebep)
{
    /// <summary>Islem oldu mu.</summary>
    public bool Oldu => Sonuc == IslemSonucu.Tamam;

    /// <summary>Basarili rapor.</summary>
    public static IslemRaporu Basarili(string yeniYol) => new(IslemSonucu.Tamam, yeniYol, null);
}

/// <summary>
/// DISK ISLEMLERININ TEK KAPISI: klasor olustur · yeniden adlandir · tasi.
/// Arayuz BILMEZ; hedef net8.0 oldugu icin bu mantik Linux'ta GERCEK
/// klasorlerle test ediliyor.
///
/// SILME BURADA YOK - bilerek. Silme cop kutusuna gidiyor ve o Windows
/// kabugunun isi; burada olsaydi ya test edilemezdi ya da kalici silme
/// yazmak zorunda kalirdik (CLAUDE.md 3: geri alinamayan islem yazilmaz).
/// </summary>
public static class DosyaIslemleri
{
    /// <summary>
    /// Klasorde cakismayan bir ad bulur: "Yeni klasör", "Yeni klasör (2)"...
    /// Gezgin'in davranisi. 1000'de durur - sonsuz donguye girmez.
    /// </summary>
    public static string BosAdBul(string klasor, string istenenAd)
    {
        if (!Var(WindowsYolu.Birlestir(klasor, istenenAd)))
        {
            return istenenAd;
        }

        string govde = istenenAd;
        string uzanti = string.Empty;

        // Uzantiyi koru: "Parca.SLDPRT" -> "Parca (2).SLDPRT", "Parca.SLDPRT (2)" DEGIL.
        string bulunanUzanti = WindowsYolu.Uzanti(istenenAd);
        if (bulunanUzanti.Length > 0)
        {
            govde = istenenAd[..^bulunanUzanti.Length];
            uzanti = istenenAd[^bulunanUzanti.Length..];
        }

        for (int sira = 2; sira <= 1000; sira++)
        {
            string aday = $"{govde} ({sira}){uzanti}";
            if (!Var(WindowsYolu.Birlestir(klasor, aday)))
            {
                return aday;
            }
        }

        return istenenAd;   // cagiran ZatenVar alacak; sessizce ustune yazmaz
    }

    /// <summary>Yeni bir alt klasor olusturur.</summary>
    public static IslemRaporu KlasorOlustur(string ustKlasor, string ad)
    {
        if (!WindowsYolu.AdGecerliMi(ad, out string sebep))
        {
            return new IslemRaporu(IslemSonucu.GecersizAd, null, sebep);
        }

        string hedef = WindowsYolu.Birlestir(ustKlasor, ad);
        if (Var(hedef))
        {
            return new IslemRaporu(IslemSonucu.ZatenVar, null, $"\"{ad}\" zaten var.");
        }

        try
        {
            Directory.CreateDirectory(hedef);
            return IslemRaporu.Basarili(hedef);
        }
        catch (Exception hata)
        {
            return Cevir(hata);
        }
    }

    /// <summary>
    /// Bir dosya ya da klasorun adini degistirir.
    /// Yalnizca ADI degisir; yer degismez.
    /// </summary>
    public static IslemRaporu YenidenAdlandir(string yol, string yeniAd)
    {
        if (!WindowsYolu.AdGecerliMi(yeniAd, out string sebep))
        {
            return new IslemRaporu(IslemSonucu.GecersizAd, null, sebep);
        }

        bool klasorMu = Directory.Exists(yol);
        if (!klasorMu && !File.Exists(yol))
        {
            return new IslemRaporu(IslemSonucu.Bulunamadi, null, "Kaynak bulunamadı: " + yol);
        }

        string ustKlasor = WindowsYolu.Klasor(yol);
        string hedef = WindowsYolu.Birlestir(ustKlasor, yeniAd);

        // Yalnizca BUYUK-KUCUK harf degisiyorsa hedef "zaten var" gorunur ama
        // istenen sey mesru. Ordinal karsilastirma SART: Turkce yerelinde
        // noktali/noktasiz I yuzunden kulture bagli karsilastirma sasar.
        bool sadeceHarfBoyu = string.Equals(yol, hedef, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(yol, hedef, StringComparison.Ordinal);

        if (!sadeceHarfBoyu && Var(hedef))
        {
            return new IslemRaporu(IslemSonucu.ZatenVar, null, $"\"{yeniAd}\" zaten var.");
        }

        try
        {
            if (klasorMu)
            {
                Directory.Move(yol, hedef);
            }
            else
            {
                File.Move(yol, hedef, overwrite: false);
            }

            return IslemRaporu.Basarili(hedef);
        }
        catch (Exception hata)
        {
            return Cevir(hata);
        }
    }

    /// <summary>
    /// Bir dosya ya da klasoru baska bir klasore tasir.
    ///
    /// CLAUDE.md 5'te OLCULDU: klasor tasininca SOLIDWORKS'un IC referanslari
    /// yasiyor - "ebeveynin yanindaki dosya" kurali yazili mutlak yolun onune
    /// geciyor. Kirilan yalnizca DISARIDAN verilen referanslar. Bu yuzden
    /// Directory.Move mesru bir hizli yol.
    /// </summary>
    public static IslemRaporu Tasi(string kaynak, string hedefKlasor)
    {
        bool klasorMu = Directory.Exists(kaynak);
        if (!klasorMu && !File.Exists(kaynak))
        {
            return new IslemRaporu(IslemSonucu.Bulunamadi, null, "Kaynak bulunamadı: " + kaynak);
        }

        if (!Directory.Exists(hedefKlasor))
        {
            return new IslemRaporu(IslemSonucu.Bulunamadi, null, "Hedef klasör yok: " + hedefKlasor);
        }

        string ad = WindowsYolu.DosyaAdi(kaynak);
        string kaynakKlasoru = WindowsYolu.Klasor(kaynak);

        if (string.Equals(kaynakKlasoru, hedefKlasor, StringComparison.OrdinalIgnoreCase))
        {
            // Ayni yere tasimak bir sey yapmaz; "oldu" demek yaniltici olur.
            return new IslemRaporu(IslemSonucu.ZatenVar, null, $"\"{ad}\" zaten bu klasörde.");
        }

        if (klasorMu && KendiAltindaMi(kaynak, hedefKlasor))
        {
            return new IslemRaporu(
                IslemSonucu.KendiAltina, null,
                $"\"{ad}\" kendi içine taşınamaz.");
        }

        string hedef = WindowsYolu.Birlestir(hedefKlasor, ad);
        if (Var(hedef))
        {
            return new IslemRaporu(IslemSonucu.ZatenVar, null, $"Hedefte \"{ad}\" zaten var.");
        }

        try
        {
            if (klasorMu)
            {
                Directory.Move(kaynak, hedef);
            }
            else
            {
                // File.Move DEGIL kopyala-sil de degil: ayni surucude Move
                // atomik ve hizli. Farkli surucude .NET kendisi kopyalayip
                // siliyor ve kopyalama yarim kalirsa KAYNAK DURUYOR - yani
                // CLAUDE.md 3'un "kismi basarisizlikta eski dosyayi koru"
                // kurali zaten saglaniyor.
                File.Move(kaynak, hedef, overwrite: false);
            }

            return IslemRaporu.Basarili(hedef);
        }
        catch (Exception hata)
        {
            return Cevir(hata);
        }
    }

    /// <summary>Hedef klasor, kaynak klasorun kendi altinda mi.</summary>
    public static bool KendiAltindaMi(string kaynakKlasor, string hedefKlasor)
    {
        string kaynak = SonuAyiriciyaGetir(kaynakKlasor);
        string hedef = SonuAyiriciyaGetir(hedefKlasor);
        return hedef.StartsWith(kaynak, StringComparison.OrdinalIgnoreCase);
    }

    private static string SonuAyiriciyaGetir(string yol)
        => yol.Length > 0 && WindowsYolu.AyiriciMi(yol[^1])
            ? yol
            : yol + WindowsYolu.Ayiricisi(yol);

    private static bool Var(string yol) => File.Exists(yol) || Directory.Exists(yol);

    /// <summary>
    /// Istisnayi ayirt edilebilir bir sebebe cevirir.
    ///
    /// CLAUDE.md 4 - OLCULDU: Windows bir klasoru UC ayri sebeple sildirmiyor
    /// ve ucunun cozumu farkli; ex.Message bunlari AYIRT EDEMIYOR cunku metin
    /// yerellestirilmis. Win32 kodu HResult'in dusuk 16 bitinde duruyor.
    /// </summary>
    private static IslemRaporu Cevir(Exception hata)
    {
        int win32 = hata.HResult & 0xFFFF;

        IslemSonucu sonuc = win32 switch
        {
            5 => IslemSonucu.Erisim,        // ERROR_ACCESS_DENIED
            32 => IslemSonucu.Kilitli,      // ERROR_SHARING_VIOLATION
            145 => IslemSonucu.Dolu,        // ERROR_DIR_NOT_EMPTY
            80 or 183 => IslemSonucu.ZatenVar,
            2 or 3 => IslemSonucu.Bulunamadi,
            _ => hata switch
            {
                UnauthorizedAccessException => IslemSonucu.Erisim,
                FileNotFoundException or DirectoryNotFoundException => IslemSonucu.Bulunamadi,
                _ => IslemSonucu.Bilinmeyen,
            },
        };

        return new IslemRaporu(sonuc, null, Anlat(sonuc, hata));
    }

    private static string Anlat(IslemSonucu sonuc, Exception hata) => sonuc switch
    {
        IslemSonucu.Erisim => "İzin yok ya da salt-okunur. " + hata.Message,
        IslemSonucu.Kilitli => "Başka bir program bu dosyayı açık tutuyor "
            + "(SOLIDWORKS açıksa kapatın). " + hata.Message,
        IslemSonucu.Dolu => "Klasörün içi boş değil. SOLIDWORKS'ün gizli \"~$\" "
            + "kilit dosyaları Gezgin'de görünmez ama klasörü doldurur. " + hata.Message,
        IslemSonucu.Bulunamadi => "Bulunamadı. " + hata.Message,
        IslemSonucu.ZatenVar => "Hedefte aynı adda bir şey var. " + hata.Message,
        _ => hata.Message,
    };
}
