using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// WINDOWS AD VE YOL DOGRULAMA - "bu ad diske yazilabilir mi".
///
/// NEDEN AYRI DOSYA: WindowsYolu 645 satira cikti ve boyut kapisi (600)
/// yakaladi. Kesme yeri satir sayisina gore DEGIL KONUYA gore (CLAUDE.md
/// 1b): burada YALNIZCA "gecerli mi" sorusu var - gecersiz karakterler,
/// ayrilmis aygit adlari, uzunluk sinirlari. Parcalama ve cozumleme
/// WindowsYolu.cs'te kaldi.
///
/// CLAUDE.md 4'UN EN PAHALI MADDESI BURADA: Path.GetInvalidFileNameChars()
/// Linux'ta yalnizca "/" ve "\0" donduruyor, yani Windows'ta GECERSIZ bir
/// adi buradaki testler KABUL EDER. Liste bu yuzden ELLE yazili.
/// </summary>
public static partial class WindowsYolu
{
    // CLAUDE.md 4: Path.GetInvalidFileNameChars() Linux'ta yalnizca '/' ve '\0'
    // donduruyor, yani Windows'ta gecersiz bir adi testler KABUL EDER.
    // Bu yuzden liste ELLE yazilmistir. Windows'ta gercek listeye karsi
    // dogrulanmasi icin testler klasorunde ayri bir olcum var.
    private static readonly char[] Gecersizler = OlusturGecersizler();

    private static char[] OlusturGecersizler()
    {
        var liste = new List<char> { '"', '<', '>', '|', ':', '*', '?', '\\', '/' };
        for (char k = (char)0; k < (char)32; k++)
        {
            liste.Add(k);
        }

        return liste.ToArray();
    }

    // Windows'ta AYRILMIS aygit adlari. Bir dosya bunlardan biri olamaz -
    // uzantili hali de olamaz ("CON.SLDPRT" da yasak).
    private static readonly string[] AyrilmisAdlar =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Windows'ta gecersiz sayilan dosya adi karakterleri.</summary>
    public static IReadOnlyList<char> GecersizAdKarakterleri => Gecersizler;
    /// <summary>Windows'ta bir dosya/klasor adinin en fazla karakteri.</summary>
    public const int EnUzunAd = 255;

    /// <summary>
    /// Uzun yol destegi acik degilken Windows'un tam yol siniri. 260, sonu
    /// bitiren karakter dahil; kullanilabilir uzunluk 259.
    /// </summary>
    public const int EnUzunYol = 259;

    /// <summary>
    /// Ad gecerli mi VE o klasorde olusacak tam yol sinira siginiyor mu.
    ///
    /// Ayri bir uye, cunku ad tek basina gecerli olsa bile derin bir klasorde
    /// yol sinirini asabilir - ve o hata ancak diske yazarken cikardi.
    /// </summary>
    public static bool YolGecerliMi(string klasor, string? ad, out string sebep)
    {
        if (!AdGecerliMi(ad, out sebep))
        {
            return false;
        }

        int uzunluk = Birlestir(klasor, ad!).Length;
        if (uzunluk > EnUzunYol)
        {
            sebep = $"Tam yol çok uzun ({uzunluk} karakter); Windows sınırı {EnUzunYol}. "
                + "Daha kısa bir ad verin ya da daha üstteki bir klasöre koyun.";
            return false;
        }

        sebep = string.Empty;
        return true;
    }

    public static bool AdGecerliMi(string? ad, out string sebep)
    {
        if (string.IsNullOrWhiteSpace(ad))
        {
            sebep = "Ad boş olamaz.";
            return false;
        }

        // UZUNLUK SINIRI - burada HIC YOKTU (29.08.2026). Sonucu: Windows
        // PathTooLongException atiyordu, HatayiCevir haritasinda karsiligi
        // olmadigi icin kullaniciya .NET'in ham Ingilizce mesaji cikiyordu.
        // Sinir onden konur ve sebep TURKCE yazilir (CLAUDE.md 3).
        if (ad.Length > EnUzunAd)
        {
            sebep = $"Ad çok uzun ({ad.Length} karakter); en fazla {EnUzunAd} olabilir.";
            return false;
        }

        // Bastaki bosluk: Windows'ta acilabiliyor ama Gezgin'de gorunmez bir
        // fark yaratiyor - "Parca" ile " Parca" ayni gorunup ayri dosya olur.
        if (ad[0] == ' ')
        {
            sebep = "Ad boşlukla başlayamaz.";
            return false;
        }

        foreach (char karakter in ad)
        {
            if (Array.IndexOf(Gecersizler, karakter) >= 0)
            {
                sebep = karakter < ' '
                    ? "Ad, yazdırılamayan bir karakter içeriyor."
                    : $"Ad şu karakteri içeremez: {karakter}";
                return false;
            }
        }

        if (ad[^1] == '.' || ad[^1] == ' ')
        {
            sebep = "Ad nokta veya boşlukla bitemez.";
            return false;
        }

        // Uzantili hali de yasak: "CON.SLDPRT" da acilamiyor.
        // Ordinal karsilastirma SART: Turkce yerelinde ToUpper() noktali I
        // uretiyor ve kulture bagli karsilastirma sasiyor.
        //
        // GOVDE KIRPILIYOR: "CON .SLDPRT" gibi bir adda govde "CON " olur ve
        // kirpilmadan bakan bir denetim bunu KACIRIR; Windows sondaki boslugu
        // atip aygit adi sayar.
        int nokta = ad.IndexOf('.');
        string govde = (nokta < 0 ? ad : ad[..nokta]).TrimEnd(' ', '.');
        foreach (string ayrilmis in AyrilmisAdlar)
        {
            if (govde.Equals(ayrilmis, StringComparison.OrdinalIgnoreCase))
            {
                sebep = $"\"{ayrilmis}\" Windows'ta ayrılmış bir aygıt adı, dosya adı olarak kullanılamaz.";
                return false;
            }
        }

        sebep = string.Empty;
        return true;
    }
}
