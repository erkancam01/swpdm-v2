using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Cop kutusundaki bir oge.</summary>
/// <param name="No">Cop icindeki klasor adi; kimlik.</param>
/// <param name="Ad">Ozgun dosya/klasor adi.</param>
/// <param name="EskiYol">Silinmeden onceki tam yol.</param>
/// <param name="Zaman">Silinme zamani.</param>
/// <param name="Boyut">Dosyaysa boyutu; klasorse -1 (bilinmiyor).</param>
/// <param name="KlasorMu">Klasor mu.</param>
public sealed record CopOgesi(
    string No,
    string Ad,
    string EskiYol,
    DateTime Zaman,
    long Boyut,
    bool KlasorMu);

/// <summary>
/// Cop kutusunun OKUNMUS hali.
///
/// NEDEN AYRI BIR TIP: "bos" ile "okunamadi" ayni sey degil. Ikisini de bos
/// liste ile anlatmak, kullaniciya silinmis dosyalarinin kayboldugunu
/// dusundurur (CLAUDE.md 3).
/// </summary>
/// <param name="Ogeler">Diskte karsiligi bulunan ogeler, en yeni basta.</param>
/// <param name="Okunamadi">Kayit okunamadiysa sebebi; okunduysa null.</param>
/// <param name="BozukSatir">Cozulemeyen kayit satiri sayisi.</param>
public sealed record CopDurumu(
    IReadOnlyList<CopOgesi> Ogeler,
    string? Okunamadi,
    int BozukSatir)
{
    /// <summary>Kayit okunabildi mi. false ise SAYI GOSTERILMEZ.</summary>
    public bool Guvenilir => Okunamadi is null;
}

/// <summary>
/// COP KUTUSU - uygulamanin kendi cop klasoru, kokun icinde.
///
/// NEDEN WINDOWS COP KUTUSU DEGIL - olculmus gerekce:
/// Windows Cop Kutusu yalnizca yerel disklerde vardir. AG SURUCUSUNDEN
/// (\\sunucu\ortak) silinen dosya cop kutusuna GITMEZ, kalici gider. Erkan'in
/// asil calisma yeri ag surucusu; oraya "geri alinabilir" demek YALAN olurdu
/// (CLAUDE.md 3). Kendi klasorumuz ayni diskte oldugu icin tasima ANLIK ve
/// her yerde ayni davraniyor.
///
/// Yapisi:
///   &lt;kok&gt;\.SwPdmCop\kayit.txt        sekmeyle ayrilmis kayit
///   &lt;kok&gt;\.SwPdmCop\&lt;no&gt;\&lt;ozgun ad&gt;  silinen ogenin kendisi
///
/// Her oge kendi numarali klasorunde durur; boylece ad cakismasi IMKANSIZ ve
/// ozgun ad korunur. Kayit duz metin: bozulursa ELLE onarilabilir ve dosyalar
/// zaten yerinde durur.
///
/// KENDILIGINDEN HICBIR SEY SILINMEZ (CLAUDE.md 1a). Sure dolunca temizleme
/// yoktur; bosaltmayi yalnizca kullanici ister.
/// </summary>
public static class Cop
{
    /// <summary>Cop klasorunun adi. Agacta GOSTERILMEZ.</summary>
    public const string KlasorAdi = ".SwPdmCop";

    private const string KayitAdi = "kayit.txt";

    /// <summary>
    /// Cop klasorunun yolu.
    ///
    /// <paramref name="ustKlasor"/> verilmezse kokun KENDI ICI kullanilir -
    /// varsayilan budur ve en hizlisidir: ayni diskte oldugu icin silme bir
    /// TASIMA'dir, 1 GB'lik montaj kopyalanmaz.
    ///
    /// Kullanici baska bir ust klasor secebilir. O klasor BASKA BIR DISKTE
    /// ise silme kopyalamaya doner ve yavaslar; bunu secim aninda SOYLEMEK
    /// cagiranin isi (CLAUDE.md 3).
    /// </summary>
    public static string Yolu(string kok, string? ustKlasor = null)
        => WindowsYolu.Birlestir(
            string.IsNullOrWhiteSpace(ustKlasor) ? kok : ustKlasor, KlasorAdi);

    /// <summary>
    /// Iki yol ayni surucude mi. Degilse silme ANLIK olmaz, kopyalamaya doner.
    /// Bilinemiyorsa true doner - "yavas olacak" diye YANLIS uyarmak,
    /// uyarmamaktan kotudur.
    /// </summary>
    public static bool AyniSurucudeMi(string a, string b)
    {
        string kokA = SurucuKoku(a);
        string kokB = SurucuKoku(b);

        return kokA.Length == 0 || kokB.Length == 0
            || string.Equals(kokA, kokB, StringComparison.OrdinalIgnoreCase);
    }

    private static string SurucuKoku(string yol)
    {
        // "C:\..." -> "C:"   ·   "\\sunucu\ortak\..." -> "\\sunucu\ortak"
        if (yol.Length >= 2 && yol[1] == ':')
        {
            return yol[..2];
        }

        if (yol.Length > 2 && WindowsYolu.AyiriciMi(yol[0]) && WindowsYolu.AyiriciMi(yol[1]))
        {
            int birinci = yol.IndexOfAny([WindowsYolu.Ayirici, WindowsYolu.EgikAyirici], 2);
            if (birinci < 0)
            {
                return yol;
            }

            int ikinci = yol.IndexOfAny(
                [WindowsYolu.Ayirici, WindowsYolu.EgikAyirici], birinci + 1);
            return ikinci < 0 ? yol : yol[..ikinci];
        }

        return string.Empty;
    }

    /// <summary>Bir ogeyi cope tasir.</summary>
    public static IslemRaporu Sil(string cop, string yol)
    {
        bool klasorMu = Directory.Exists(yol);
        if (!klasorMu && !File.Exists(yol))
        {
            return new IslemRaporu(IslemSonucu.Bulunamadi, null, "Bulunamadı: " + yol);
        }

        // Cop klasorunun kendisi cope atilamaz.
        if (klasorMu && DosyaIslemleri.KendiAltindaMi(yol, cop))
        {
            return new IslemRaporu(
                IslemSonucu.KendiAltina, null, "Çöp klasörü çöpe atılamaz.");
        }

        string no = YeniNo(cop);
        string kutu = WindowsYolu.Birlestir(cop, no);
        string ad = WindowsYolu.DosyaAdi(yol);
        long boyut = klasorMu ? -1 : YeniBoyut(yol);

        string hedef = WindowsYolu.Birlestir(kutu, ad);

        try
        {
            Directory.CreateDirectory(kutu);

            if (klasorMu)
            {
                Directory.Move(yol, hedef);
            }
            else
            {
                File.Move(yol, hedef, overwrite: false);
            }
        }
        catch (Exception hata)
        {
            TemizlemeyeCalis(kutu);
            return DosyaIslemleri.HatayiCevir(hata);
        }

        // BURADAN SONRASI AYRI BIR TRY - VE SEBEBI OLCULDU:
        // kayit tasimadan SONRA yazilir (tasima tutmadiysa kayitta olmayan
        // bir oge birakmayiz). Ama ikisi AYNI try icindeyken, tasima olup
        // kayit yazilamadiginda ortaya SESSIZ BIR KAYIP cikiyordu:
        // dosya cop klasorunun icinde duruyor, kayit.txt'te olmadigi icin
        // Listele onu GOSTERMIYOR, cagiran da "SILINEMEDI" diyor. Yani
        // kullaniciya gore dosya yok olmus oluyor (CLAUDE.md 3).
        // File.AppendAllText ag surucusunde en olasi hata noktasi.
        try
        {
            KayitEkle(cop, new CopOgesi(no, ad, yol, DateTime.Now, boyut, klasorMu));
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return KayitYazilamadi(kutu, hedef, yol, ad, klasorMu, hata);
        }

        return IslemRaporu.Basarili(hedef);
    }

    /// <summary>
    /// Tasima OLDU ama kayit yazilamadi. Once dosya YERINE geri konur -
    /// boylece kullanicinin gordugu ile diskteki hal ayni kalir. Geri de
    /// konamiyorsa dosyanin NEREDE oldugu yazilir; "silinemedi" deyip
    /// susmak, kullaniciya dosyayi kaybettirir (CLAUDE.md 3).
    /// </summary>
    private static IslemRaporu KayitYazilamadi(
        string kutu, string hedef, string eskiYol, string ad, bool klasorMu, Exception hata)
    {
        try
        {
            if (klasorMu)
            {
                Directory.Move(hedef, eskiYol);
            }
            else
            {
                File.Move(hedef, eskiYol, overwrite: false);
            }

            TemizlemeyeCalis(kutu);
            return new IslemRaporu(
                IslemSonucu.Bilinmeyen, null,
                $"\"{ad}\" silinmedi: çöp kaydı yazılamadı ({hata.Message}). "
                + "Dosya yerinde bırakıldı.");
        }
        catch (Exception geri) when (geri is IOException or UnauthorizedAccessException)
        {
            return new IslemRaporu(
                IslemSonucu.Bilinmeyen, null,
                $"\"{ad}\" çöp kutusuna taşındı ama kaydı yazılamadı "
                + $"({hata.Message}) ve geri de konamadı ({geri.Message}). "
                + $"Dosya şurada: {hedef}");
        }
    }

    /// <summary>
    /// Coptekiler, en yeni once.
    ///
    /// Diskte KARSILIGI OLMAYAN kayitlar atlanir - kullaniciya var olmayan bir
    /// dosyayi "geri yukleyebilirsin" diye gostermek yalan olur.
    /// </summary>
    public static CopDurumu Oku(string cop)
    {
        string kayit = WindowsYolu.Birlestir(cop, KayitAdi);

        if (!File.Exists(kayit))
        {
            // Ayni adda bir KLASOR duruyorsa kayit okunamiyor demektir;
            // "hic silinmemis" ile karistirilmaz (CLAUDE.md 3).
            return Directory.Exists(kayit)
                ? new CopDurumu([], "Çöp kaydı okunamadı: " + kayit + " bir klasör.", 0)
                : new CopDurumu([], null, 0);   // hic silinmemis: GERCEKTEN bos
        }

        string[] satirlar;
        try
        {
            satirlar = File.ReadAllLines(kayit);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // BOS LISTE "YOK" DEMEK DEGILDIR (CLAUDE.md 3). Burasi eskiden
            // sessizce [] donuyordu ve ekranda "Çöp kutusu boş." yaziyordu:
            // kullanici okunamayan bir kutuya bakip "silinenlerim gitmis"
            // saniyordu. Sebep artik cagirana kadar gidiyor.
            return new CopDurumu([], "Çöp kaydı okunamadı: " + hata.Message, 0);
        }

        var sonuc = new List<CopOgesi>(satirlar.Length);
        int bozuk = 0;
        foreach (string satir in satirlar)
        {
            CopOgesi? oge = Coz(satir);
            if (oge is null)
            {
                // Bozuk satir uygulamayi dusurmez - ama SAYILIR: sessizce
                // atlanan bir satir, listede gorunmeyen bir dosya demek.
                if (satir.Trim().Length > 0)
                {
                    bozuk++;
                }

                continue;
            }

            if (Var(IcerdekiYol(cop, oge)))
            {
                sonuc.Add(oge);
            }
        }

        sonuc.Reverse();
        return new CopDurumu(sonuc, null, bozuk);
    }

    /// <summary>
    /// Bir ogeyi eski yerine geri koyar.
    ///
    /// CAKISMA KARARI DISARIDAN GELIR (29.08.2026). Once burasi her zaman
    /// numaralandiriyordu: kullaniciya "uzerine yaz / atla / ikisini de tut"
    /// hic sorulmuyordu, oysa uygulamada tam bu is icin bir cakisma kutusu
    /// var. Varsayilan hala numaralamak - yani hicbir cagiran sessizce
    /// uzerine yazmaz.
    /// </summary>
    public static IslemRaporu GeriYukle(
        string cop, CopOgesi oge, Cakisma cakisma = Cakisma.IkisiniDeTut)
    {
        string kaynak = IcerdekiYol(cop, oge);

        if (!Var(kaynak))
        {
            return new IslemRaporu(IslemSonucu.Bulunamadi, null, "Çöpte bulunamadı: " + oge.Ad);
        }

        string ustKlasor = WindowsYolu.Klasor(oge.EskiYol);

        try
        {
            // Eski klasor silinmis olabilir; yeniden acilir. Aksi halde geri
            // yukleme "olmaz" derdi ve dosya copte mahsur kalirdi.
            Directory.CreateDirectory(ustKlasor);

            // Ayni adda bir sey geri gelmisse USTUNE YAZILMAZ; numaralanir ve
            // bu cagirana SOYLENIR (rapor.YeniYol'a bakilir).
            string dogrudan = WindowsYolu.Birlestir(ustKlasor, oge.Ad);
            string hedef;

            if (!DosyaIslemleri.Var(dogrudan))
            {
                hedef = dogrudan;   // cakisma yok
            }
            else if (cakisma == Cakisma.Atla)
            {
                return new IslemRaporu(IslemSonucu.Atlandi, null, $"\"{oge.Ad}\" atlandı.");
            }
            else if (cakisma == Cakisma.Degistir)
            {
                // Uzerine yazilan YOK EDILMEZ; o da cope gider (CLAUDE.md 1a).
                if (!Sil(cop, dogrudan).Oldu)
                {
                    return new IslemRaporu(
                        IslemSonucu.ZatenVar, null,
                        $"\"{oge.Ad}\" değiştirilemedi: eskisi çöp kutusuna alınamadı.");
                }

                hedef = dogrudan;
            }
            else if (DosyaIslemleri.BosAdBul(ustKlasor, oge.Ad) is string ad)
            {
                hedef = WindowsYolu.Birlestir(ustKlasor, ad);
            }
            else
            {
                return new IslemRaporu(
                    IslemSonucu.ZatenVar, null,
                    $"\"{oge.Ad}\" geri yüklenemedi: eski klasöründe boş bir ad bulunamadı.");
            }

            if (oge.KlasorMu)
            {
                Directory.Move(kaynak, hedef);
            }
            else
            {
                File.Move(kaynak, hedef, overwrite: false);
            }

            KayitCikar(cop, oge.No);
            TemizlemeyeCalis(WindowsYolu.Birlestir(cop, oge.No));
            return IslemRaporu.Basarili(hedef);
        }
        catch (Exception hata)
        {
            return DosyaIslemleri.HatayiCevir(hata);
        }
    }

    /// <summary>Bir ogeyi KALICI siler. Geri donusu yoktur.</summary>
    public static IslemRaporu KaliciSil(string cop, CopOgesi oge)
    {
        try
        {
            string kutu = WindowsYolu.Birlestir(cop, oge.No);
            if (Directory.Exists(kutu))
            {
                Directory.Delete(kutu, recursive: true);
            }

            KayitCikar(cop, oge.No);
            return IslemRaporu.Basarili(kutu);
        }
        catch (Exception hata)
        {
            return DosyaIslemleri.HatayiCevir(hata);
        }
    }

    /// <summary>
    /// Ogenin cop kutusunun ICINDEKI yolu.
    ///
    /// ACIK (public): cakisma kutusu "hangi dosya hangisiyle carpisiyor"
    /// diye ikisinin de boyut/tarihini okuyor.
    /// </summary>
    public static string IcerdekiYolu(string cop, CopOgesi oge) => IcerdekiYol(cop, oge);

    private static string IcerdekiYol(string cop, CopOgesi oge)
        => WindowsYolu.Birlestir(WindowsYolu.Birlestir(cop, oge.No), oge.Ad);

    private static bool Var(string yol) => File.Exists(yol) || Directory.Exists(yol);

    private static long YeniBoyut(string yol)
    {
        try
        {
            return new FileInfo(yol).Length;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return -1;
        }
    }

    /// <summary>Kullanilmayan bir numara. Var olan klasorlere bakar.</summary>
    private static string YeniNo(string cop)
    {
        long taban = DateTime.Now.Ticks;
        for (int i = 0; i < 1000; i++)
        {
            string aday = (taban + i).ToString(CultureInfo.InvariantCulture);
            if (!Directory.Exists(WindowsYolu.Birlestir(cop, aday)))
            {
                return aday;
            }
        }

        return taban.ToString(CultureInfo.InvariantCulture);
    }

    private static void KayitEkle(string cop, CopOgesi oge)
    {
        string satir = string.Join(
            '\t',
            oge.No,
            oge.Ad,
            oge.EskiYol,
            oge.Zaman.ToString("O", CultureInfo.InvariantCulture),
            oge.Boyut.ToString(CultureInfo.InvariantCulture),
            oge.KlasorMu ? "K" : "D");

        File.AppendAllText(WindowsYolu.Birlestir(cop, KayitAdi), satir + Environment.NewLine);
    }

    private static void KayitCikar(string cop, string no)
    {
        string kayit = WindowsYolu.Birlestir(cop, KayitAdi);
        if (!File.Exists(kayit))
        {
            return;
        }

        var kalan = new List<string>();
        foreach (string satir in File.ReadAllLines(kayit))
        {
            if (!satir.StartsWith(no + "\t", StringComparison.Ordinal))
            {
                kalan.Add(satir);
            }
        }

        File.WriteAllLines(kayit, kalan);
    }

    /// <summary>Bozuk satir null doner; cagiran atlar, uygulama dusmez.</summary>
    private static CopOgesi? Coz(string satir)
    {
        string[] p = satir.Split('\t');
        if (p.Length != 6)
        {
            return null;
        }

        if (!DateTime.TryParse(p[3], CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime zaman)
            || !long.TryParse(p[4], CultureInfo.InvariantCulture, out long boyut))
        {
            return null;
        }

        return new CopOgesi(p[0], p[1], p[2], zaman, boyut, p[5] == "K");
    }

    private static void TemizlemeyeCalis(string klasor)
    {
        try
        {
            if (Directory.Exists(klasor) && Directory.GetFileSystemEntries(klasor).Length == 0)
            {
                Directory.Delete(klasor);
            }
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Bos kutu kalirsa zarari yok.
        }
    }
}
