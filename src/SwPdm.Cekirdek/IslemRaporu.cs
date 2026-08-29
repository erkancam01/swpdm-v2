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

    /// <summary>Kullanici bu ogeyi atlamayi secti. Hata DEGIL.</summary>
    Atlandi,

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
/// ISLEM SONUCU VE HATA CEVIRISI - "ne oldu" ve "Windows'un hatasi Turkce
/// nasil soylenir" sorulari.
///
/// NEDEN AYRI DOSYA: DosyaIslemleri boyut kapisini asti (626 > 600) ve kapi
/// dogru davrandi - sonuc tipleri ile hata cevirisi kendi basina bir konu:
/// yalniz dosya islemleri degil, cop kutusu, yamalama ve onarim da ayni
/// raporu ve ayni ceviriyi kullaniyor (CLAUDE.md 8).
/// </summary>
public static class IslemSonuclari
{
    /// <summary>
    /// Istisna DISK kaynakli mi - "catch when" suzgeci icin. TEK KOPYA:
    /// yamalama ve onarim da bunu kullaniyor, yoksa listeler ayrisir ve
    /// biri gunun birinde bir istisnayi yutar (CLAUDE.md 8).
    /// </summary>
    public static bool DiskHatasi(Exception hata)
        => hata is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException;


    /// <summary>
    /// Istisnayi ayirt edilebilir bir sebebe cevirir.
    ///
    /// CLAUDE.md 4 - OLCULDU: Windows bir klasoru UC ayri sebeple sildirmiyor
    /// ve ucunun cozumu farkli; ex.Message bunlari AYIRT EDEMIYOR cunku metin
    /// yerellestirilmis. Win32 kodu HResult'in dusuk 16 bitinde duruyor.
    /// </summary>
    public static IslemRaporu HatayiCevir(Exception hata)
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
                PathTooLongException => IslemSonucu.GecersizAd,
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
        IslemSonucu.GecersizAd => $"Yol Windows sınırından ({WindowsYolu.EnUzunYol} karakter) "
            + "uzun. " + hata.Message,
        _ => hata.Message,
    };
}
