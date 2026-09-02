using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Bir ad degisiminin referanslara etkisi - HICBIR SEY DEGISTIRMEDEN.</summary>
/// <param name="EskiYol">Adi degisecek dosya.</param>
/// <param name="YeniYol">Dosyanin yeni TAM yolu (ad ve/veya klasor degismis).</param>
/// <param name="CocuguTasi">
/// true: dosyayi bu sinif tasiyacak (ad degistirme). false: dosya ZATEN
/// tasindi (Kes/Yapistir, suruklemek) ve yalnizca ebeveynler onarilacak.
/// </param>
/// <param name="Ebeveynler">Bu dosyayi kullanan ve onarilmasi gereken dosyalar.</param>
/// <param name="Engeller">Onarimi imkansiz kilan sebepler; bos degilse UYGULANMAZ.</param>
/// <param name="Guvenilir">Indeks tam mi - degilse ebeveyn listesi EKSIK olabilir.</param>
public sealed record OnarimPlani(
    string EskiYol,
    string YeniYol,
    bool CocuguTasi,
    IReadOnlyList<string> Ebeveynler,
    IReadOnlyList<string> Engeller,
    bool Guvenilir)
{
    /// <summary>Dosyanin yeni adi.</summary>
    public string YeniAd => WindowsYolu.DosyaAdi(YeniYol);

    /// <summary>Klasor degisti mi - yani bu bir TASIMA mi.</summary>
    public bool KlasorDegisti => !string.Equals(
        WindowsYolu.Klasor(EskiYol), WindowsYolu.Klasor(YeniYol), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Toplu onarimin ozeti.</summary>
/// <param name="Onarilan">Kac yazili yol duzeltildi.</param>
/// <param name="Hatalar">Tutmayanlar ve sebepleri. Bos donmek yasak (CLAUDE.md 3).</param>
/// <param name="Dokunulan">Degistirilen dosyalar - indeks bunlardan tazelenir.</param>
/// <param name="AtlananKilitli">
/// KILITLI klasorde oldugu icin HIC DOKUNULMAYAN dosya sayisi.
///
/// AYRI ALAN OLMASI SART (CLAUDE.md 3): bunlar "hata" degil, kullanicinin
/// KENDI koydugu kilidin geregi - hata listesine karistirmak "onarim
/// basarisiz" dedirtirdi. Ama sessizce yutmak da yalan olurdu: kullanici
/// "hepsi duzeldi" sanip kilitli klasoru duzelmis kabul eder.
/// </param>
public sealed record OnarimOzeti(
    int Onarilan, IReadOnlyList<string> Hatalar, IReadOnlyList<string> Dokunulan,
    int AtlananKilitli = 0);

/// <summary>Onarimin sonucu.</summary>
/// <param name="Oldu">Ad degisti VE butun ebeveynler onarildi mi.</param>
/// <param name="Onarilanlar">Onarilan ebeveynlerin yollari.</param>
/// <param name="Sebep">Olmadiysa sebep. Bos donmek yasak (CLAUDE.md 3).</param>
public sealed record OnarimSonucu(
    bool Oldu, IReadOnlyList<string> Onarilanlar, string? Sebep)
{
    /// <summary>Hicbir sey degismedi.</summary>
    public static OnarimSonucu Olmadi(string sebep) => new(false, [], sebep);

    /// <summary>Sebep, yoksa "bilinmeyen sebep" - IslemRaporu.Sebebi'nin esi.</summary>
    public string Sebebi => Sebep ?? "bilinmeyen sebep";
}

/// <summary>
/// AD DEGISINCE EBEVEYNLERI ONARIR - bu uygulamanin varlik sebebi.
///
/// NEDEN GEREKLI: bir parcanin adi degisince onu kullanan montaj ve teknik
/// resim ESKI ADI arar. Tasimada durum farkli (SOLIDWORKS once ebeveynin
/// yanina bakiyor, CLAUDE.md 5) ama ad degisiminde komsuluk kurali da
/// kurtarmiyor. Tek cozum ebeveynin ICINE yazmak.
///
/// OLCULDU (Erkan'in makinesi, 28.08.2026): yol yazan butun akislar
/// degistirilince SOLIDWORKS dosyayi ACIYOR ve parcalar yerinde geliyor.
/// Ilk turda daha uzun bir ad HATA vermisti; sebebi yazilan dizenin
/// UZAMASIYDI. Ikinci turda fark klasor kismindan karsilandi
/// (<see cref="SwYazici.UzunlukKorunanYol"/>) ve KISA da UZUN da ad CALISTI.
///
/// ============ HEPSI YA DA HICBIRI ============
///
/// Yarim onarim en kotu sonuctur: bir montaj duzelir, otekinde parca kaybolur
/// ve kullanici hangisinin dogru oldugunu bilemez. Sira:
///
///   1. HER ebeveyn icin yama bir YAN DOSYAYA yazilir (asillara dokunulmaz)
///   2. biri bile tutmazsa: yan dosyalar silinir, HICBIR SEY DEGISMEZ
///   3. cocugun adi degistirilir
///   4. asillar yedege alinir, yan dosyalar yerine gecer
///   5. ortada bir yerde tutmazsa: HEPSI geri alinir (ad dahil)
///
/// CLAUDE.md 3: KOPYALA -> ONAR -> DOGRULA -> SIL. Dogrulama
/// <see cref="SwYazici"/> icinde, diskten YENIDEN OKUYARAK yapiliyor.
/// </summary>
public static partial class ReferansOnarimi
{
    /// <summary>Yamalanmis dosyanin gecici uzantisi.</summary>
    private const string YeniUzanti = ".swpdm-yeni";

    /// <summary>Degistirilmeden once asilin alindigi gecici uzanti.</summary>
    private const string YedekUzanti = ".swpdm-eski";

    /// <summary>
    /// Ne olacagini HESAPLAR; diske dokunmaz. Cagiran once bunu gosterir.
    /// </summary>
    public static OnarimPlani Planla(ReferansIndeksi? indeks, string eskiYol, string yeniAd)
    {
        string yeniYol = WindowsYolu.Birlestir(WindowsYolu.Klasor(eskiYol), yeniAd);
        return Planla(indeks, eskiYol, yeniYol, cocuguTasi: true, harictut: null);
    }

    /// <summary>
    /// TASIMA icin: dosya ZATEN yeni yerinde. Yalnizca DISARIDA KALAN
    /// ebeveynler onarilir.
    ///
    /// BIRLIKTE TASINANLARA DOKUNULMAZ (<paramref name="harictut"/>): olculdu
    /// (CLAUDE.md 5) - SOLIDWORKS once ebeveynin yanina bakiyor, yani birlikte
    /// tasinan aile kendiliginden calisiyor. Calisani onarmak bos risktir (1a).
    /// </summary>
    public static OnarimPlani TasimaPlani(
        ReferansIndeksi? indeks, string eskiYol, string yeniYol, IReadOnlyList<string>? harictut)
        => Planla(indeks, eskiYol, yeniYol, cocuguTasi: false, harictut);

    private static OnarimPlani Planla(
        ReferansIndeksi? indeks, string eskiYol, string yeniYol, bool cocuguTasi,
        IReadOnlyList<string>? harictut)
    {
        if (indeks is null)
        {
            return new OnarimPlani(
                eskiYol, yeniYol, cocuguTasi, [],
                ["Referans indeksi yok; kimin kullandığı bilinmiyor."], false);
        }

        KullanimSonucu kullanim = indeks.Kullananlar(eskiYol);
        var adaylar = new List<string>();
        foreach (string k in kullanim.Kullananlar)
        {
            if (harictut is null || !Icinde(harictut, k))
            {
                adaylar.Add(k);
            }
        }

        return Kur(eskiYol, yeniYol, cocuguTasi, adaylar, kullanim.Guvenilir);
    }

    /// <summary>
    /// Bir TASIMA islemi icin butun onarim planlari. KLASORLERI ACAR:
    /// bir klasor tasindiginda icindeki her SOLIDWORKS dosyasi tasinmistir
    /// ve disaridan ona isaret eden ebeveynler kirilir.
    ///
    /// Olculdu (CLAUDE.md 5): klasor tasininca ICERIDEKI referanslar yasiyor.
    /// O yuzden yalnizca DISARIDA kalan ebeveynler planlanir - tasinan her
    /// sey <paramref name="harictut"/> ile eleniyor.
    ///
    /// Ebeveyni olmayan dosya icin plan URETILMEZ; bos is yapilmaz.
    /// </summary>
    /// <returns>
    /// Planlar ve - onarim YAPILAMAYACAKSA - SEBEBI. Sebep null degilse
    /// cagiran bunu SOYLEMEK zorunda: sessizce onarmadan gecmek, kullaniciya
    /// referansi saglam sanip dosya actirir (CLAUDE.md 3). Bu delik gercekte
    /// acildi ve Erkan'in dosyasini kirdi (28.08.2026).
    /// </returns>
    public static (IReadOnlyList<OnarimPlani> Planlar, string? Sebep) TasimaPlanlari(
        ReferansIndeksi? indeks,
        IReadOnlyList<(string Eski, string Yeni)>? ciftler,
        IReadOnlyList<string>? harictut)
    {
        var planlar = new List<OnarimPlani>();
        if (indeks is null)
        {
            return (planlar, "referans indeksi yok");
        }

        if (ciftler is null)
        {
            return (planlar, null);
        }

        bool guvenilir = true;

        foreach ((string eski, string yeni) in ciftler)
        {
            foreach ((string e, string y) in Ac(eski, yeni))
            {
                OnarimPlani plan = TasimaPlani(indeks, e, y, harictut);
                guvenilir &= plan.Guvenilir;
                if (plan.Ebeveynler.Count > 0)
                {
                    planlar.Add(plan);
                }

                // TASINAN DOSYANIN KENDI YOLLARI DA ONARILIR - eksik olan
                // adim buydu (Erkan, 02.09.2026: "başka klasöre taşıdığımda
                // içindekiler ve kullananlar kısmı yok diyor, kırık diyor").
                planlar.AddRange(KendiYollariPlanlari(indeks, e, y, harictut));
            }
        }

        return (planlar, guvenilir ? null : "tarama tam değil; kimin kullandığı eksik bilinebilir");
    }

    /// <summary>
    /// Klasor ciftini ICINDEKI dosya ciftlerine acar; dosyaysa kendisini verir.
    /// Yalnizca referans TASIYABILEN turler (parca/montaj/teknik resim).
    /// </summary>
    private static IEnumerable<(string Eski, string Yeni)> Ac(string eski, string yeni)
    {
        if (!Directory.Exists(yeni))
        {
            if (SwReferans.TasiyabilirMi(eski))
            {
                yield return (eski, yeni);
            }

            yield break;
        }

        foreach (string y in Directory.EnumerateFiles(yeni, "*", SearchOption.AllDirectories))
        {
            if (!SwReferans.TasiyabilirMi(y))
            {
                continue;
            }

            string kuyruk = y[yeni.Length..].TrimStart(WindowsYolu.Ayirici, WindowsYolu.EgikAyirici);
            yield return (WindowsYolu.Birlestir(eski, kuyruk), y);
        }
    }

    /// <summary>
    /// Planlari uygular. Doner: onarilan DOSYA sayisi ve tutmayanlarin sebebi.
    /// Biri tutmazsa otekiler DURMAZ - her plan kendi icinde hepsi-ya-hicbiri.
    /// </summary>
    /// <param name="planlar">Uygulanacak planlar.</param>
    /// <returns>
    /// Onarilan dosya sayisi, tutmayanlarin sebebi, ve TUTAN PLANLAR.
    /// Sonuncusu GERI ALMA icin sart: geri alirken indekse sormak yanlis
    /// olur (indeks yeni yollari bilmez), ebeveyn listesi tasinmali.
    /// </returns>
    public static (int Onarilan, IReadOnlyList<string> Hatalar, IReadOnlyList<OnarimPlani> Tutanlar)
        Onar(IReadOnlyList<OnarimPlani>? planlar)
    {
        int onarilan = 0;
        var hatalar = new List<string>();
        var tutanlar = new List<OnarimPlani>();

        foreach (OnarimPlani plan in planlar ?? [])
        {
            OnarimSonucu s = Uygula(plan);
            if (s.Oldu)
            {
                onarilan += s.Onarilanlar.Count;
                tutanlar.Add(plan);
            }
            else
            {
                hatalar.Add(WindowsYolu.DosyaAdi(plan.EskiYol) + " — " + s.Sebebi);
            }
        }

        return (onarilan, hatalar, tutanlar);
    }

    /// <summary>
    /// Uygulanmis onarimlari GERI ALIR: yazili yollar eski hedefe doner.
    /// Dosyalarin kendisini geri tasimak cagiranin isi.
    /// </summary>
    public static void GeriOnar(IReadOnlyList<OnarimPlani>? tutanlar)
        => Yonlendir(tutanlar, geriye: true);

    /// <summary>
    /// Geri alinmis onarimlari YENIDEN uygular - "ileri alma" (Ctrl+Y) icin.
    /// <see cref="GeriOnar"/>'in aynasi; dosyalarin kendisini yeni yerine
    /// tasimak yine cagiranin isi.
    /// </summary>
    public static void YenidenOnar(IReadOnlyList<OnarimPlani>? tutanlar)
        => Yonlendir(tutanlar, geriye: false);

    /// <summary>
    /// Iki yonun TEK govdesi: fark yalnizca hangi yolun kaynak sayildigi.
    /// Ayri iki dongu yazmak, birinin zamanla otekinden ayrilmasi demekti
    /// (CLAUDE.md 8).
    /// </summary>
    private static void Yonlendir(IReadOnlyList<OnarimPlani>? tutanlar, bool geriye)
    {
        foreach (OnarimPlani plan in tutanlar ?? [])
        {
            Uygula(PlanlaBilinenlerle(
                plan.Ebeveynler,
                geriye ? plan.YeniYol : plan.EskiYol,
                geriye ? plan.EskiYol : plan.YeniYol,
                cocuguTasi: false));
        }
    }

    /// <summary>Yol, listedeki bir dosya ya da KLASORUN ALTINDA mi.</summary>
    private static bool Icinde(IReadOnlyList<string> kume, string yol)
    {
        foreach (string k in kume)
        {
            // "Altinda mi" TEK kopyadan sorulur (CLAUDE.md 8).
            if (WindowsYolu.AltindaMi(yol, k))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ebeveynler DISARIDAN veriliyor - GERI ALMA icin.
    ///
    /// NEDEN SART: onarimdan sonra indeks eski adi bilir, yenisini bilmez.
    /// Geri alirken indekse sormak SIFIR ebeveyn dondururdu ve geri alma
    /// dosyayi eski adina dondururken ebeveynleri YENI ada bakar halde
    /// birakirdi - yani geri alma referansi KIRARDI.
    /// </summary>
    public static OnarimPlani PlanlaBilinenlerle(
        IReadOnlyList<string> ebeveynler, string eskiYol, string yeniYol, bool cocuguTasi = true)
        => Kur(eskiYol, yeniYol, cocuguTasi, ebeveynler, guvenilir: true);

    private static OnarimPlani Kur(
        string eskiYol, string yeniYol, bool cocuguTasi,
        IReadOnlyList<string> adaylar, bool guvenilir)
    {
        var engeller = new List<string>();
        var ebeveynler = new List<string>();

        foreach (string ebeveyn in adaylar)
        {
            if (!File.Exists(ebeveyn))
            {
                engeller.Add(WindowsYolu.DosyaAdi(ebeveyn) + " — dosya bulunamadı.");
                continue;
            }

            ebeveynler.Add(ebeveyn);
        }

        // ACIK DOSYAYA YAZILMAZ. SOLIDWORKS'te acik bir belgenin yaninda "~$"
        // kilidi durur (CLAUDE.md 5); onu degistirmek acik oturumla catisir.
        foreach (string acik in AcikOlanlar(ebeveynler))
        {
            engeller.Add(
                WindowsYolu.DosyaAdi(acik) + " — SOLIDWORKS'te açık görünüyor; önce kapatın.");
        }

        return new OnarimPlani(eskiYol, yeniYol, cocuguTasi, ebeveynler, engeller, guvenilir);
    }

    /// <summary>
    /// Plani UYGULAR: once butun ebeveynler yamalanir, sonra ad degisir,
    /// sonra yamalar yerine gecer. Herhangi biri tutmazsa HEPSI geri alinir.
    /// </summary>
    public static OnarimSonucu Uygula(OnarimPlani? plan)
    {
        if (plan is null)
        {
            return OnarimSonucu.Olmadi("Plan yok.");
        }

        if (plan.Engeller.Count > 0)
        {
            return OnarimSonucu.Olmadi("Engeller var: " + string.Join(" · ", plan.Engeller));
        }

        string eskiAd = WindowsYolu.DosyaAdi(plan.EskiYol);
        string yeniYol = plan.YeniYol;

        if (plan.CocuguTasi && (File.Exists(yeniYol) || Directory.Exists(yeniYol)))
        {
            return OnarimSonucu.Olmadi($"\"{plan.YeniAd}\" bu klasörde zaten var.");
        }

        // ---- 1) HER ebeveyn YAN DOSYAYA yamalanir; asillara dokunulmaz.
        var yamalar = new List<(string Ebeveyn, string Yeni)>();
        var dokunulmayan = new List<string>();
        foreach (string ebeveyn in plan.Ebeveynler)
        {
            string yeni = ebeveyn + YeniUzanti;

            // YAZILAN YOL HER ZAMAN EBEVEYNE GORE KURULUR - ad degisiminde de.
            //
            // ONCE BOYLE DEGILDI ve ERKAN'DA KIRILDI (31.08.2026): ad
            // degisiminde yazili KLASOR korunuyor, yalniz ad degisiyordu.
            // Dayanagi "cocuk ebeveynin yaninda kalir" varsayimiydi - teknik
            // resim 1\, parca 3\ olunca varsayim COKUYOR. Ustune uzunlugu
            // korumak icin soldan klasor atilinca ortaya
            // "3\.\.\...\11-Parça1.SLDPRT" gibi bir yol cikiyor ve
            // ebeveynin klasorune gore cozulunce "1\3\..." ediyordu: referans
            // KAYBOLUYOR, hicbir sey patlamiyor (CLAUDE.md 3).
            //
            // Tek dogru soru "bu dosya ebeveynden nasil gorunur": once
            // EBEVEYNE GORELI yol, sigmazsa MUTLAK, ikisi de sigmazsa YAZILMAZ
            // ve sebebi soylenir. Yan kazanc: dogrulama da ada degil
            // COZUMLEMEYE bakiyor (SwYazici.YoluDogrula) - yani yanlis yeri
            // gosteren bir yama artik kabul edilmiyor (CLAUDE.md 2).
            YamaSonucu s = SwYazici.YoluDegistir(
                ebeveyn, yeni, eskiAd, yeniYol, WindowsYolu.Klasor(ebeveyn));
            if (!s.Oldu)
            {
                Temizle(yamalar);
                Sil(yeni);
                return OnarimSonucu.Olmadi(
                    $"{WindowsYolu.DosyaAdi(ebeveyn)} onarılamadı: {s.Sebep} "
                    + "Hiçbir şey değiştirilmedi.");
            }

            // DEGISIKLIK GEREKMEDI (yazici DegisenAkis = 0 diyor): ebeveynin
            // yazdigi deger zaten gecerli - tipik hali CIPLAK AD, konum
            // belirtmiyor ve klasor degisince anlami degismiyor (Erkan'da
            // olculdu, 31.08.2026). Yan dosya yok; ASILA HIC DOKUNULMAZ ve
            // "onarilan" sayisina da girmez - sayi dogru kalmali (CLAUDE.md 3).
            if (s.DegisenAkis == 0)
            {
                dokunulmayan.Add(ebeveyn);
                continue;
            }

            yamalar.Add((ebeveyn, yeni));
        }

        // ---- 2) Cocugun adi degisir. TASIMADA bu adim YOK: dosya zaten
        //         yeni yerinde, tasima motoru goturdu.
        string? arsivUyarisi = null;
        if (plan.CocuguTasi)
        {
            try
            {
                File.Move(plan.EskiYol, yeniYol);

                // VERSIYON ARSIVI DA TASINIR. Bu yol DosyaIslemleri'nden
                // gecmiyor (yama sirasi yuzunden ad burada degisiyor), o
                // yuzden kanca ayrica gerekli - Erkan'in kullandigi ASIL
                // yol bu: onarimli ad degisimi. Tasinamazsa SEBEP asagida
                // basari raporuna giriyor; sessiz gecmiyor (CLAUDE.md 3).
                arsivUyarisi = Surumler.Tasindi(plan.EskiYol, yeniYol);
            }
            catch (Exception hata) when (Dosya(hata))
            {
                Temizle(yamalar);
                return OnarimSonucu.Olmadi("Adı değiştirilemedi: " + hata.Message);
            }
        }

        // ---- 3) Yamalar yerine gecer; ortada tutmazsa HEPSI geri alinir.
        var degisenler = new List<(string Ebeveyn, string Yedek)>();
        foreach ((string ebeveyn, string yeni) in yamalar)
        {
            string yedek = ebeveyn + YedekUzanti;
            try
            {
                File.Move(ebeveyn, yedek);
                File.Move(yeni, ebeveyn);
                degisenler.Add((ebeveyn, yedek));
            }
            catch (Exception hata) when (Dosya(hata))
            {
                GeriAl(degisenler, plan.CocuguTasi ? yeniYol : null, plan.EskiYol);
                Temizle(yamalar);
                return OnarimSonucu.Olmadi(
                    $"{WindowsYolu.DosyaAdi(ebeveyn)} değiştirilemedi: {hata.Message} "
                    + "Yapılan her şey geri alındı.");
            }
        }

        foreach ((_, string yedek) in degisenler)
        {
            Sil(yedek);
        }

        // ONARILANLAR = GERCEKTEN YAZILANLAR. Dokunulmayan ebeveyn "onarildi"
        // diye sayilmaz; sayi yalan soylemez (CLAUDE.md 3/10).
        var onarilanlar = new List<string>();
        foreach (string ebeveyn in plan.Ebeveynler)
        {
            if (!dokunulmayan.Contains(ebeveyn))
            {
                onarilanlar.Add(ebeveyn);
            }
        }

        return new OnarimSonucu(true, onarilanlar, arsivUyarisi);
    }

    /// <summary>Yaninda "~$" kilidi olan, yani SOLIDWORKS'te acik gorunenler.</summary>
    private static IEnumerable<string> AcikOlanlar(IReadOnlyList<string> yollar)
    {
        foreach (string yol in yollar)
        {
            if (Kilit.AcikMi(yol))
            {
                yield return yol;
            }
        }
    }

    /// <summary>Yarim kalan yamalari siler; disk temiz kalir.</summary>
    private static void Temizle(List<(string Ebeveyn, string Yeni)> yamalar)
    {
        foreach ((_, string yeni) in yamalar)
        {
            Sil(yeni);
        }
    }

    /// <summary>
    /// Degistirilmis ebeveynleri ve ad degisikligini GERI ALIR.
    /// Burada bir hata olursa yutuluyor: cagirana zaten "geri alindi" degil,
    /// asil hatanin sebebi doner ve yedek dosyalar diskte KALIR - kullanici
    /// onlari gorebilsin diye (sessizce silmek kanit yok eder).
    /// </summary>
    private static void GeriAl(
        List<(string Ebeveyn, string Yedek)> degisenler, string? yeniYol, string eskiYol)
    {
        foreach ((string ebeveyn, string yedek) in degisenler)
        {
            try
            {
                Sil(ebeveyn);
                File.Move(yedek, ebeveyn);
            }
            catch (Exception hata) when (Dosya(hata))
            {
                // yedek diskte kaliyor; asagida ad da geri alinacak
            }
        }

        try
        {
            if (yeniYol is not null && File.Exists(yeniYol))
            {
                File.Move(yeniYol, eskiYol);
                Surumler.Tasindi(yeniYol, eskiYol);
            }
        }
        catch (Exception hata) when (Dosya(hata))
        {
            // ad geri alinamadi; cagirana donen sebep zaten islemin
            // yapilmadigini soyluyor
        }
    }

    private static void Sil(string yol) => DosyaIslemleri.GeciciyiSil(yol);

    private static bool Dosya(Exception hata) => IslemSonuclari.DiskHatasi(hata);
}
