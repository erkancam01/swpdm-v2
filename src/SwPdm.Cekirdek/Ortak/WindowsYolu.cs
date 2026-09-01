using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// Windows yollarini ayristirir. <see cref="System.IO.Path"/> YERINE kullanilir.
///
/// ===================== NEDEN VAR (CLAUDE.md 4) =====================
/// Path'in yol parcalama uyeleri Linux'ta Windows yolunu YANLIS parcaliyor;
/// Linux'ta ters bolu ayirici SAYILMIYOR:
///
///   Path.GetExtension(@"C:\Proje 2.0\parca")  ->  ".0\parca"   (dogrusu "")
///   Path.GetFileName(@"C:\a\b.SLDPRT")        ->  yolun TAMAMI (dogrusu "b.SLDPRT")
///   Path.GetDirectoryName(@"C:\a\b.SLDPRT")   ->  ""           (dogrusu "C:\a")
///
/// Testler Linux'ta kosuyorsa test YANLIS sonucu dogrular ve hata kullanicinin
/// makinesine kalir. Bu sinif iki platformda AYNI cevabi verir.
///
/// ===================== TEK KOPYA (CLAUDE.md 8) =====================
/// v1'de "yolun son parcasi" mantigi DOKUZ yerde elle yazilmisti ve UC ayri
/// bicimde ayrismisti:
///   - ucu bos girdide NullReferenceException atiyordu   -> burada bos girdi "" doner
///   - bir kismi sondaki ayiriciyi kirpmiyordu           -> burada kirpilir
///   - ikisi egik bolu tanimiyordu                       -> burada / de ayiricidir
/// Ikinci kopyasi YAZILMAYACAK.
/// ===================================================================
/// </summary>
public static partial class WindowsYolu
{
    /// <summary>Windows'un asil ayiricisi.</summary>
    public const char Ayirici = '\\';

    /// <summary>Windows egik boluyu da kabul ediyor; biz de kabul ediyoruz.</summary>
    public const char EgikAyirici = '/';


    /// <summary>Verilen karakter bir yol ayiricisi mi.</summary>
    public static bool AyiriciMi(char karakter) => karakter == Ayirici || karakter == EgikAyirici;

    /// <summary>
    /// Bu yolun KENDI ayiricisi. Icinde ters bolu yoksa ama egik bolu varsa
    /// egik bolu, yoksa ters bolu.
    ///
    /// TEK KOPYA (CLAUDE.md 8): bu karar once <see cref="Birlestir"/> icinde
    /// yaziliydi, sonra ayni karar bir baska yerde ELLE tekrar yazildi ve
    /// AYRISTI - "kendi altina tasima" denetimi Linux'ta yanlis cevap verdi,
    /// testi kirdi. Karar burada durur, herkes buraya sorar.
    /// </summary>
    public static char Ayiricisi(string? yol)
        => yol is not null && yol.IndexOf(Ayirici) < 0 && yol.IndexOf(EgikAyirici) >= 0
            ? EgikAyirici
            : Ayirici;

    /// <summary>
    /// Yolun son parcasi (dosya ya da klasor adi). Kok verilirse bos doner.
    /// Bos ya da null girdide ISTISNA ATMAZ, bos doner.
    /// </summary>
    public static string DosyaAdi(string? yol)
    {
        if (string.IsNullOrEmpty(yol))
        {
            return string.Empty;
        }

        int kok = KokUzunlugu(yol);
        int son = SondakiAyiricilariKirp(yol, kok);
        if (son <= kok)
        {
            return string.Empty;
        }

        int bas = ParcaBasi(yol, kok, son);
        return yol[bas..son];
    }

    /// <summary>
    /// Noktasiyla birlikte uzanti (".SLDPRT"), yoksa bos.
    /// Kural .NET'in kendi davranisindan OLCULMUSTUR:
    ///   "b."         -> ""            (sondaki tek nokta uzanti degil)
    ///   ".gitignore" -> ".gitignore"
    ///   "a..b"       -> ".b"
    /// </summary>
    public static string Uzanti(string? yol)
    {
        string ad = DosyaAdi(yol);
        int nokta = ad.LastIndexOf('.');
        if (nokta < 0 || nokta == ad.Length - 1)
        {
            return string.Empty;
        }

        return ad[nokta..];
    }

    /// <summary>Uzantisi atilmis dosya adi.</summary>
    public static string DosyaAdiUzantisiz(string? yol)
    {
        string ad = DosyaAdi(yol);
        int nokta = ad.LastIndexOf('.');
        return nokta < 0 ? ad : ad[..nokta];
    }

    /// <summary>
    /// Yolun bulundugu klasor. Ust klasor yoksa bos doner.
    ///
    /// SURUCU KOKU TUZAGI (CLAUDE.md 4): C:\a.SLDPRT icin "C:\" doner, "C:" DEGIL.
    /// "C:" donduren bir yardimci, birlestirildiginde surucuye GORELI "C:x"
    /// uretir - yani bambaska bir yere yazar.
    /// </summary>
    public static string Klasor(string? yol)
    {
        if (string.IsNullOrEmpty(yol))
        {
            return string.Empty;
        }

        int kok = KokUzunlugu(yol);
        int son = SondakiAyiricilariKirp(yol, kok);
        if (son <= kok)
        {
            return string.Empty;   // yolun kendisi zaten kok
        }

        int bas = ParcaBasi(yol, kok, son);
        if (bas <= kok)
        {
            return yol[..kok];     // ust = kok. "C:\a.SLDPRT" -> "C:\"
        }

        int ust = bas - 1;
        while (ust > kok && AyiriciMi(yol[ust - 1]))
        {
            ust--;
        }

        return yol[..ust];
    }

    /// <summary>
    /// Klasor ile adi birlestirir. Klasor zaten ayiriciyla bitiyorsa ikincisini
    /// eklemez.
    ///
    /// Ayirici, yolun KENDI kullandigi ayiricidir: icinde ters bolu yoksa ama
    /// egik bolu varsa egik bolu konur. Sebep somut: dosya islemleri Linux'ta
    /// GERCEK klasorlerle test ediliyor ve orada "/tmp/x" + "y" birlesimi
    /// "/tmp/x\y" olsaydi tek parcali, adinda ters bolu olan bir dosya
    /// olusurdu. Windows yollarinda davranis aynen eskisi gibi.
    /// </summary>
    public static string Birlestir(string? klasor, string? ad)
    {
        if (string.IsNullOrEmpty(klasor))
        {
            return ad ?? string.Empty;
        }

        if (string.IsNullOrEmpty(ad))
        {
            return klasor;
        }

        if (AyiriciMi(klasor[^1]))
        {
            return klasor + ad;
        }

        return klasor + Ayiricisi(klasor) + ad;
    }

    /// <summary>
    /// Ad Windows'ta gecerli bir dosya adi mi. Degilse <paramref name="sebep"/>
    /// EKRANDA gosterilebilecek bir cumle doner (CLAUDE.md 3: sebep gizlenmez).
    /// </summary>
    /// <summary>
    /// <paramref name="hedef"/>'i <paramref name="temel"/> klasorune GORE
    /// yazar. Ayni surucude degillerse ya da hesaplanamiyorsa null.
    ///
    /// NEDEN VAR: referans onariminda ebeveynin icine yazilacak yol.
    /// Goreli yol iki sebeple tercih ediliyor:
    ///   1. KISA - yazilan dizenin uzunlugu korunmak zorunda (CLAUDE.md 5),
    ///      kisa yol daha sik siginiyor
    ///   2. SOLIDWORKS'un kendi davranisiyla ayni yonde - once ebeveynin
    ///      yanina bakiyor; goreli yol bunun dogal uzantisi
    ///
    /// Windows'un kendi Path.GetRelativePath'i KULLANILMIYOR: Linux'ta
    /// ters bolu ayirici sayilmiyor ve yanlis sonuc veriyor (CLAUDE.md 4).
    /// </summary>
    public static string? Goreli(string? temel, string? hedef)
    {
        if (string.IsNullOrWhiteSpace(temel) || string.IsNullOrWhiteSpace(hedef))
        {
            return null;
        }

        string[] t = Parcala(temel);
        string[] h = Parcala(hedef);
        if (t.Length == 0 || h.Length == 0)
        {
            return null;
        }

        // Ayni kok (surucu ya da sunucu) degilse goreli yol YAZILAMAZ.
        if (!string.Equals(t[0], h[0], StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int ortak = 0;
        while (ortak < t.Length && ortak < h.Length - 1
               && string.Equals(t[ortak], h[ortak], StringComparison.OrdinalIgnoreCase))
        {
            ortak++;
        }

        var parcalar = new List<string>();
        for (int i = ortak; i < t.Length; i++)
        {
            parcalar.Add("..");
        }

        for (int i = ortak; i < h.Length; i++)
        {
            parcalar.Add(h[i]);
        }

        // Ayni klasordeyse ".\ad" seklinde yaziliyor: cikplak bir ad da
        // gecerli ama nokta onu ACIKCA goreli yapiyor.
        return parcalar.Count == h.Length - ortak
            ? "." + Ayirici + string.Join(Ayirici, parcalar)
            : string.Join(Ayirici, parcalar);
    }

    /// <summary>
    /// Goreli bir yolu GERCEK bir taban klasore gore cozer - sonuc DISKTE
    /// ARANABILIR kalir.
    ///
    /// <see cref="Cozumle"/>'DEN FARKI VE NEDEN IKISI DE VAR: Cozumle iki
    /// yolu KIYASLAMAK icin ve sonucu hep "\" ile birlestiriyor; testler
    /// Linux'ta kosuyor ve File.Exists oyle bir yolu bulamaz. Burada tabandan
    /// Klasor/Birlestir ile yuruyoruz, yani ayirici GERCEK yoldan geliyor ve
    /// sonuc her iki isletim sisteminde de aranabiliyor.
    ///
    /// TEK KOPYA (CLAUDE.md 8): ayni yuruyus once belge agacinda yazildi,
    /// sonra klasor kilidinde ikinci kez gerekti. Ucuncusunu yazmamak icin
    /// yol yardimcilarinin yanina alindi - "bu soruyu soran herkes buraya
    /// gelir".
    /// </summary>
    /// <returns>
    /// Cozulmus yol; taban bos ya da yol kokun ustune cikiyorsa null.
    /// Mutlak yol (surucu ya da UNC) oldugu gibi doner.
    /// </returns>
    public static string? TabandanCoz(string? taban, string? yol)
    {
        if (string.IsNullOrWhiteSpace(taban) || string.IsNullOrWhiteSpace(yol))
        {
            return null;
        }

        // Mutlak yol (surucu ya da UNC): TEK BICIME indirilerek donuyor.
        //
        // HAM DONMESI BIR HATAYDI - OLCULDU (01.09.2026, Erkan'in gercek
        // agacinda): onarimin uzunluk dolgusu mutlak yollara ".\" ve cift
        // ayirici yaziyor (YazilacakYol). Ayni dosya bir yerden
        // "C:\a\b\Ad.SLDPRT", baska yerden "C:\a\b\.\.\Ad.SLDPRT" olarak
        // cozuluyordu; iki dize esit olmadigi icin belge agaci onu IKI AYRI
        // DOSYA sayiyor ve alt agacini IKI KEZ yuruyordu. Versiyon kutusunda
        // "241 dosya" derken bir kismi buydu.
        if ((yol.Length > 1 && yol[1] == ':')
            || (yol.Length > 1 && AyiriciMi(yol[0]) && AyiriciMi(yol[1])))
        {
            return Duzlestir(yol);
        }

        string suan = taban;
        foreach (string parca in yol.Split(
                     new[] { Ayirici, EgikAyirici }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (parca == ".")
            {
                continue;   // onarimin uzunluk dolgusu: ".\" yolu degistirmez
            }

            if (parca == "..")
            {
                suan = Klasor(suan);
                if (suan.Length == 0)
                {
                    return null;   // kokun ustune cikti; yol gecersiz
                }

                continue;
            }

            suan = Birlestir(suan, parca);
        }

        return suan;
    }

    /// <summary>
    /// Goreli bir yolu <paramref name="temel"/> klasorune gore COZER ve
    /// "." / ".." parcalarini duzlestirir. Yol zaten mutlaksa yalnizca
    /// duzlestirilir. Cozulemezse null.
    ///
    /// <see cref="Goreli"/>'nin tersi. Onarimin DOGRULAMASI icin gerekli:
    /// dosyanin icine yazdigimiz deger goreli olabiliyor, "dogru yeri
    /// gosteriyor mu" sorusu ancak cozulunce cevaplanir.
    /// </summary>
    public static string? Cozumle(string? temel, string? yol)
    {
        if (string.IsNullOrWhiteSpace(yol))
        {
            return null;
        }

        bool mutlak = (yol.Length > 1 && yol[1] == ':')
                   || (yol.Length > 1 && AyiriciMi(yol[0]) && AyiriciMi(yol[1]));

        string tam = mutlak ? yol : (string.IsNullOrWhiteSpace(temel) ? yol : temel + Ayirici + yol);
        string[] parcalar = Parcala(tam);

        var yigin = new List<string>();
        foreach (string parca in parcalar)
        {
            if (parca == ".")
            {
                continue;
            }

            if (parca == ".." && yigin.Count > 1)
            {
                yigin.RemoveAt(yigin.Count - 1);
                continue;
            }

            yigin.Add(parca);
        }

        if (yigin.Count == 0)
        {
            return null;
        }

        string sonuc = string.Join(Ayirici, yigin);
        return mutlak && yol.Length > 1 && AyiriciMi(yol[0]) && AyiriciMi(yol[1])
            ? Ayirici.ToString() + Ayirici + sonuc
            : sonuc;
    }

    /// <summary>Yolu ayiricilardan bolup bos parcalari atar.</summary>
    private static string[] Parcala(string yol)
        => yol.Split(new[] { Ayirici, EgikAyirici }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// <paramref name="yol"/>, <paramref name="klasor"/>'un KENDISI mi ya da
    /// ALTINDA mi (buyuk-kucuk harf duyarsiz, iki ayirici da taninir).
    ///
    /// NEDEN TEK KOPYA - OLCULDU (29.08.2026): bu soru bes yerde uc farkli
    /// bicimde elle yazilmisti ve ikisi HATALIYDI - ayirici eklemeyen
    /// StartsWith, "C:\Kok2"yi "C:\Kok"un ici sayiyordu. CLAUDE.md 8'in
    /// "yolun son parcasi dokuz yerdeydi" dersinin aynisi, bu kez
    /// "altinda mi" sorusunda. Ikinci kopya YASAK; bu soruyu soran herkes
    /// buraya gelir.
    /// </summary>
    public static bool AltindaMi(string? yol, string? klasor)
    {
        if (string.IsNullOrWhiteSpace(yol) || string.IsNullOrWhiteSpace(klasor))
        {
            return false;
        }

        if (string.Equals(yol, klasor, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Surucu koku ("C:\") zaten ayiriciyla bitiyor; ona bir ayirici daha
        // aramak her yolu disarida birakirdi (CLAUDE.md 4'un kok tuzagi).
        if (AyiriciMi(klasor[^1]))
        {
            return yol.StartsWith(klasor, StringComparison.OrdinalIgnoreCase);
        }

        // Ayirici, klasor adinin BITTIGI yerde olmali: "C:\Kok2\a" yolunun
        // 6. karakteri '2' oldugu icin "C:\Kok"un altina girmez.
        return yol.Length > klasor.Length
            && AyiriciMi(yol[klasor.Length])
            && yol.StartsWith(klasor, StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// MUTLAK yolu tek bicime indirir: "." adimlarini atar, ".."yi geri
    /// yurur, ust uste ayiricilari teke indirir. Kok ("C:\", "\\sunucu\pay")
    /// oldugu gibi kalir.
    ///
    /// NEDEN GEREKLI: ayni dosyanin iki farkli yazimi, dizeye bakan her
    /// tekillemeyi (HashSet, sozluk, "gorduk mu") ATLATIYOR. Goreli yollar
    /// icin bu is zaten TabandanCoz'da yapiliyordu; mutlak yol dali
    /// yapmiyordu ve fark Erkan'in agacinda sayiyi sisirdi.
    ///
    /// SURUCU KOKU TUZAGI (CLAUDE.md 4): "C:" donduren bir yol,
    /// Birlestir ile SURUCUYE GORELI bir yol uretir. Kok hic bozulmuyor.
    /// </summary>
    public static string Duzlestir(string? yol)
    {
        if (string.IsNullOrWhiteSpace(yol))
        {
            return string.Empty;
        }

        int kokUzunlugu = KokUzunlugu(yol);
        if (kokUzunlugu == 0)
        {
            return yol;   // koksuz (goreli) yol: burada isimiz yok
        }

        string kok = yol[..kokUzunlugu];
        var parcalar = new List<string>();

        foreach (string parca in yol[kokUzunlugu..].Split(
                     new[] { Ayirici, EgikAyirici }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (parca == ".")
            {
                continue;
            }

            if (parca == "..")
            {
                if (parcalar.Count > 0)
                {
                    parcalar.RemoveAt(parcalar.Count - 1);
                }

                continue;   // kokun ustune CIKILMAZ; orada durur
            }

            parcalar.Add(parca);
        }

        string sonuc = kok;
        foreach (string parca in parcalar)
        {
            sonuc = Birlestir(sonuc, parca);
        }

        return sonuc;
    }

    /// <summary>
    /// Yolun kok uzunlugu: "C:\" -> 3, "C:" -> 2, "\" -> 1,
    /// "\\sunucu\pay" -> pay'in sonu, koksuz -> 0.
    /// Kokun altina INILMEZ; butun kirpmalar burada durur.
    /// </summary>
    private static int KokUzunlugu(string yol)
    {
        int uzunluk = yol.Length;

        // UNC: \\sunucu\pay  - CAD atolyesinde yaygin, ag surucusu.
        if (uzunluk >= 2 && AyiriciMi(yol[0]) && AyiriciMi(yol[1]))
        {
            int i = 2;
            while (i < uzunluk && !AyiriciMi(yol[i]))
            {
                i++;   // sunucu adi
            }

            if (i < uzunluk)
            {
                i++;   // ayirici
            }

            while (i < uzunluk && !AyiriciMi(yol[i]))
            {
                i++;   // pay adi
            }

            return i;
        }

        if (SurucuOnEkiVarMi(yol))
        {
            return uzunluk >= 3 && AyiriciMi(yol[2]) ? 3 : 2;
        }

        if (uzunluk >= 1 && AyiriciMi(yol[0]))
        {
            return 1;
        }

        return 0;
    }

    private static bool SurucuOnEkiVarMi(string yol)
        => yol.Length >= 2 && yol[1] == ':' && char.IsLetter(yol[0]);

    private static int SondakiAyiricilariKirp(string yol, int kok)
    {
        int son = yol.Length;
        while (son > kok && AyiriciMi(yol[son - 1]))
        {
            son--;
        }

        return son;
    }

    private static int ParcaBasi(string yol, int kok, int son)
    {
        int bas = son;
        while (bas > kok && !AyiriciMi(yol[bas - 1]))
        {
            bas--;
        }

        return bas;
    }
}
