using System;
using System.IO;

namespace SwPdm.Cekirdek;


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
    public static string? BosAdBul(string klasor, string istenenAd)
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

        // TUKENDI. Eskiden burasi ISTENEN ADI donduruyordu ve yorumda
        // "cagiran ZatenVar alacak" yaziyordu - ama OLCULDU (29.08.2026):
        // dogru degildi. Kopyalamada hedef VAR OLAN dosyaya/klasore esitlenip
        // kopyalama patliyor, ardindan YarimKalaniSil o VAR OLAN ogeyi
        // siliyordu (klasorde recursive). Yani "sessizce ustune yazmaz"
        // yerine "sessizce SILER" oluyordu. Artik null: cagiran karar verir.
        return null;
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
            return IslemSonuclari.HatayiCevir(hata);
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
            return IslemSonuclari.HatayiCevir(hata);
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
    public static IslemRaporu Tasi(
        string kaynak,
        string hedefKlasor,
        Cakisma cakisma = Cakisma.Sor,
        Func<string, bool>? eskisiniKurtar = null)
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

        IslemRaporu? karar = CakismayiCoz(
            hedefKlasor, ad, klasorMu, cakisma, eskisiniKurtar,
            out string hedef, out bool eskisiCopeAlindi);
        if (karar is not null)
        {
            return karar;
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
            return EskisiniSoyle(IslemSonuclari.HatayiCevir(hata), eskisiCopeAlindi, ad);
        }
    }

    /// <summary>
    /// Bir dosya ya da klasoru hedef klasore KOPYALAR. Kaynak yerinde kalir.
    ///
    /// AYNI klasore kopyalamak mesrudur ve "cogaltma"dir: ad cakisacagi icin
    /// numaralanir ("Parca (2).SLDPRT"). BASKA bir klasore kopyalarken ad
    /// cakisiyorsa USTUNE YAZILMAZ - islem yapilmaz ve sebebi soylenir,
    /// cunku oradaki dosya baska bir dosyadir (CLAUDE.md 3).
    /// </summary>
    public static IslemRaporu Kopyala(
        string kaynak,
        string hedefKlasor,
        Cakisma cakisma = Cakisma.Sor,
        Func<string, bool>? eskisiniKurtar = null)
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
        bool ayniKlasor = string.Equals(kaynakKlasoru, hedefKlasor, StringComparison.OrdinalIgnoreCase);

        if (klasorMu && KendiAltindaMi(kaynak, hedefKlasor))
        {
            return new IslemRaporu(
                IslemSonucu.KendiAltina, null, $"\"{ad}\" kendi içine kopyalanamaz.");
        }

        // AYNI klasore kopyalamak COGALTMADIR: ad zaten cakisacagi icin
        // dogrudan numaralanir, kullaniciya sorulmaz.
        Cakisma etkin = ayniKlasor ? Cakisma.IkisiniDeTut : cakisma;

        IslemRaporu? karar = CakismayiCoz(
            hedefKlasor, ad, klasorMu, etkin, eskisiniKurtar,
            out string hedef, out bool eskisiCopeAlindi);
        if (karar is not null)
        {
            return karar;
        }

        // HEDEF ONCEDEN VAR MIYDI: yarim kalani silerken tek olcut bu.
        // Cakisma cozuldukten sonra hedefin var OLMAMASI gerekir; yine de
        // olculuyor, cunku burada yanilmanin bedeli VAR OLAN BIR KLASORU
        // icerigiyle silmek (CLAUDE.md 1a).
        bool hedefVardi = Var(hedef);

        try
        {
            if (klasorMu)
            {
                KlasoruKopyala(kaynak, hedef);
            }
            else
            {
                File.Copy(kaynak, hedef, overwrite: false);
            }

            return IslemRaporu.Basarili(hedef);
        }
        catch (Exception hata)
        {
            // Yarim kalan kopya BIRAKILMAZ: kullanici onu tam sanip
            // kaynagi silebilir (CLAUDE.md 1a). Ama BIZIM olusturmadigimiz
            // bir hedefe DOKUNULMAZ.
            if (!hedefVardi)
            {
                YarimKalaniSil(hedef, klasorMu);
            }
            return EskisiniSoyle(IslemSonuclari.HatayiCevir(hata), eskisiCopeAlindi, ad);
        }
    }

    /// <summary>
    /// "Degistir" secilip hedefteki eski dosya cope alindiktan SONRA islem
    /// patarsa, o dosya artik hedef klasorde DEGILDIR.
    ///
    /// OLCULDU (29.08.2026): ekranda "TASINMADI (yerinde duruyor)" yaziyordu
    /// ve bu, hedef klasordeki dosyanin da yerinde oldugunu dusunduruyordu.
    /// Kaynak yerindeydi, hedefteki dosya copteydi ve bunu HICBIR SATIR
    /// soylemiyordu (CLAUDE.md 3).
    /// </summary>
    private static IslemRaporu EskisiniSoyle(IslemRaporu rapor, bool eskisiCopeAlindi, string ad)
        => eskisiCopeAlindi
            ? rapor with
            {
                Sebep = (rapor.Sebep ?? "Bilinmeyen sebep.")
                    + $" Hedefteki eski \"{ad}\" çöp kutusuna alınmıştı; "
                    + "oradan geri yükleyebilirsiniz.",
            }
            : rapor;

    private static void KlasoruKopyala(string kaynak, string hedef)
    {
        Directory.CreateDirectory(hedef);

        foreach (string dosya in Directory.GetFiles(kaynak))
        {
            // SOLIDWORKS'un "~$" KILIT DOSYALARI KOPYALANMAZ. Kopyalanirsa
            // kopyanin icinde SAHIPSIZ kilitler olusuyor: agacta oyle
            // gorunuyorlar ve klasorun sonradan silinmesini engelleyebiliyorlar
            // (CLAUDE.md 4 - "dizin bos degil"in gorunmez sebebi).
            if (Kilit.KilitMi(WindowsYolu.DosyaAdi(dosya)))
            {
                continue;
            }

            File.Copy(dosya, WindowsYolu.Birlestir(hedef, WindowsYolu.DosyaAdi(dosya)),
                overwrite: false);
        }

        foreach (string alt in Directory.GetDirectories(kaynak))
        {
            KlasoruKopyala(alt, WindowsYolu.Birlestir(hedef, WindowsYolu.DosyaAdi(alt)));
        }
    }

    private static void YarimKalaniSil(string hedef, bool klasorMu)
    {
        try
        {
            if (klasorMu && Directory.Exists(hedef))
            {
                Directory.Delete(hedef, recursive: true);
            }
            else if (!klasorMu && File.Exists(hedef))
            {
                File.Delete(hedef);
            }
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Temizlik tutmazsa asil hatanin sebebini gizlemeyiz.
        }
    }

    /// <summary>
    /// Ad cakismasini karara baglar.
    ///
    /// Doner deger null ise islem SURECEK ve <paramref name="hedef"/> kullanilir.
    /// Doner deger doluysa islem yapilmayacak; rapor cagirana gider.
    /// </summary>
    private static IslemRaporu? CakismayiCoz(
        string hedefKlasor,
        string ad,
        bool klasorMu,
        Cakisma cakisma,
        Func<string, bool>? eskisiniKurtar,
        out string hedef,
        out bool eskisiCopeAlindi)
    {
        hedef = WindowsYolu.Birlestir(hedefKlasor, ad);
        eskisiCopeAlindi = false;

        if (!Var(hedef))
        {
            return null;   // cakisma yok
        }

        switch (cakisma)
        {
            case Cakisma.Atla:
                return new IslemRaporu(IslemSonucu.Atlandi, null, $"\"{ad}\" atlandı.");

            case Cakisma.IkisiniDeTut:
                if (BosAdBul(hedefKlasor, ad) is not string bosAd)
                {
                    return new IslemRaporu(
                        IslemSonucu.ZatenVar, null,
                        $"\"{ad}\" için boş bir ad bulunamadı (1000 kopya denendi).");
                }

                hedef = WindowsYolu.Birlestir(hedefKlasor, bosAd);
                return null;

            case Cakisma.Degistir:
                // KLASOR DEGISTIRILMEZ. Bir klasoru "degistirmek" icini silmek
                // demektir ve birlestirme kurallari sinsi: kullanicinin
                // gormedigi alt dosyalar yok olur. Yalnizca dosyada mesru.
                if (klasorMu)
                {
                    return new IslemRaporu(
                        IslemSonucu.ZatenVar, null,
                        $"\"{ad}\" bir klasör; klasörün üzerine yazılmaz.");
                }

                // Var olan dosya YOK EDILMEZ, once kurtarilir (cope tasinir).
                // Kurtarma tutmazsa islem YAPILMAZ - CLAUDE.md 1a.
                if (eskisiniKurtar is null || !eskisiniKurtar(hedef))
                {
                    return new IslemRaporu(
                        IslemSonucu.ZatenVar, null,
                        $"\"{ad}\" değiştirilemedi: eskisi çöp kutusuna alınamadı.");
                }

                // BURADAN SONRA HEDEFTEKI DOSYA ARTIK ORADA DEGIL. Islem
                // patlarsa cagiranin bunu SOYLEMESI gerekiyor; yoksa mesaj
                // "yerinde duruyor" deyip hedefteki dosyanin cope gittigini
                // gizler (CLAUDE.md 3).
                eskisiCopeAlindi = true;
                return null;

            default:
                return new IslemRaporu(IslemSonucu.ZatenVar, null, $"Hedefte \"{ad}\" zaten var.");
        }
    }

    /// <summary>
    /// Hedef klasor, kaynak klasorun kendi altinda mi. Karari
    /// <see cref="WindowsYolu.AltindaMi"/> verir - "altinda mi" sorusunun
    /// TEK kopyasi orada (CLAUDE.md 8); buradaki ad yalnizca niyeti tasiyor.
    /// </summary>
    public static bool KendiAltindaMi(string kaynakKlasor, string hedefKlasor)
        => WindowsYolu.AltindaMi(hedefKlasor, kaynakKlasor);

    /// <summary>
    /// Bir klasoru YALNIZCA BOSSA siler; icinde bir sey varsa DOKUNMAZ ve
    /// sebebini soyler.
    ///
    /// NEDEN BURADA (29.08.2026 denetimi): "yeni klasor"un geri almasi bu isi
    /// KENDI dosyasinda Directory.Delete ile yapiyordu - yani bir silme yolu
    /// "diskteki dosya islemleri tek kapidan" kuralinin (CLAUDE.md 11
    /// tablosu) DISINDAN geciyordu ve kendi istisna yakalamasini tasiyordu.
    /// </summary>
    public static IslemRaporu BosKlasoruSil(string yol)
    {
        try
        {
            if (!Directory.Exists(yol))
            {
                // Zaten yok: silinmek istenen sey ortada olmadigina gore
                // islem AMACINA ulasmis sayilir; "bulunamadi" hatasi vermek
                // geri almayi bosuna yarim gosterirdi.
                return new IslemRaporu(IslemSonucu.Tamam, null, null);
            }

            if (Directory.GetFileSystemEntries(yol).Length > 0)
            {
                return new IslemRaporu(
                    IslemSonucu.Dolu, null,
                    $"\"{WindowsYolu.DosyaAdi(yol)}\" içine bir şeyler konmuş, silinmedi.");
            }

            Directory.Delete(yol);
            return new IslemRaporu(IslemSonucu.Tamam, null, null);
        }
        catch (Exception hata)
        {
            return IslemSonuclari.HatayiCevir(hata);
        }
    }

    /// <summary>Bir yolun kutuda gosterilecek ozeti.</summary>
    /// <param name="KlasorMu">Klasor mu.</param>
    /// <param name="Boyut">Dosyaysa boyutu; klasorse ya da okunamadiysa null.</param>
    /// <param name="Degistirme">Son degistirme; okunamadiysa null.</param>
    public sealed record YolOzeti(bool KlasorMu, long? Boyut, DateTime? Degistirme);

    /// <summary>
    /// Yolun boyut/tarih ozetini OKUR. Okunamayan alan null doner - uydurma
    /// deger gosterilmez (CLAUDE.md 3).
    ///
    /// NEDEN BURADA: cakisma kutusu bu bilgiyi diskten KENDISI okuyordu
    /// (FileInfo + GetLastWriteTime + kendi istisna yakalamasi). Diske giden
    /// her yol tek kapidan gecer.
    /// </summary>
    public static YolOzeti Ozet(string yol)
    {
        bool klasorMu = Directory.Exists(yol);

        try
        {
            if (klasorMu)
            {
                return new YolOzeti(true, null, Directory.GetLastWriteTime(yol));
            }

            var bilgi = new FileInfo(yol);
            return bilgi.Exists
                ? new YolOzeti(false, bilgi.Length, bilgi.LastWriteTime)
                : new YolOzeti(false, null, null);
        }
        catch (Exception hata) when (IslemSonuclari.DiskHatasi(hata))
        {
            return new YolOzeti(klasorMu, null, null);
        }
    }

    /// <summary>
    /// Yarim kalmis gecici dosyayi SESSIZCE siler. Silinememesi isi bozmaz -
    /// dosya diskte kalir ve kullanici onu gorur; sessizce yutulan sey burada
    /// yalnizca SILME hatasi, islemin kendi sonucu degil (CLAUDE.md 3).
    /// </summary>
    /// <summary>
    /// Bu yolda bir sey var mi - dosya ya da klasor.
    ///
    /// ACIK (public) cunku ad kutusu da cakismayi ONDEN gostermek icin
    /// soruyor; iki ayri "var mi" mantigi yazilmaz (CLAUDE.md 8).
    /// </summary>
    public static bool Var(string yol) => File.Exists(yol) || Directory.Exists(yol);

    public static void GeciciyiSil(string yol)
    {
        try
        {
            if (File.Exists(yol))
            {
                File.Delete(yol);
            }
        }
        catch (Exception hata) when (IslemSonuclari.DiskHatasi(hata))
        {
            // silinemeyen gecici dosya isi bozmaz
        }
    }
}
