using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SwPdm.Cekirdek;

/// <summary>Belge agacindaki BIR dugum - bir belgenin bir ebeveyn altindaki gorunusu.</summary>
/// <param name="Yol">
/// Cozulduyse DISKTEKI gercek yol; cozulemediyse dosyanin ICINDE YAZAN yol.
/// Yazilani gostermek sart: kullanici neyin arandigini gormeden neden
/// bulunamadigini anlayamaz (CLAUDE.md 3).
/// </param>
/// <param name="Seviye">0 = belgenin kendisi; 1 = dogrudan cocuklari.</param>
/// <param name="Bulundu">Yol diskte gercekten bulundu mu.</param>
/// <param name="Okunamadi">Bu belgenin KENDI referanslarina bakilamadi mi.</param>
/// <param name="Not">Olagan disi bir sey varsa SEBEBI; her sey duzse null.</param>
public sealed record AgacDugumu(
    string Yol, int Seviye, bool Bulundu, bool Okunamadi, string? Not)
{
    /// <summary>
    /// Bu dugum yuzunden agac EKSIK mi. Bulunamayan bir cocuk da, icine
    /// bakilamayan bir belge de eksiklik demektir; ikisi de sayilir.
    /// Not'u dolu olan her dugum sorunlu DEGILDIR: "yukarida acildi" bir
    /// aciklamadir, eksiklik degil.
    /// </summary>
    public bool Sorunlu => !Bulundu || Okunamadi;
}

/// <summary>
/// BELGE AGACINI YURUR - "bu montaj neleri kullaniyor", torunlara kadar.
///
/// TEK CAGIRANI VAR: <see cref="Surumler.Cocuklari"/> - versiyona girecek
/// dosyalari bu yuruyusten cikariyor. Yine de ayri dosya, cunku burasi bir
/// OZELLIK degil ORTAK ARAC (CLAUDE.md 1b): SOLIDWORKS'un cozme kurali.
/// Versiyon arsivi bir gun kaldirilsa bile kural burada kalir.
///
/// (Once ikinci bir cagirani daha vardi - parca listesi/BOM. Erkan
/// 31.08.2026'da onu kaldirtti: "solidworkun icinde var zaten." Yuruyus
/// KALDI, cunku asagidaki 3. adim versiyon arsivinin gercek bir hatasini
/// kapatiyor.)
///
/// COZUMLEME SOLIDWORKS'UN KENDI KURALIYLA (CLAUDE.md 5'te olculdu):
///   1. EBEVEYNIN YANINDAKI ayni adli dosya kazanir - yazili mutlak yolun
///      onune geciyor.
///   2. Yaninda yoksa yazili yol EBEVEYNE GORE cozulur.
///   3. O da tutmazsa yazili yolun SON EKLERI ebeveyne gore denenir
///      ("...\Yeni klasör\Parça2.SLDPRT" -> "<ebeveyn>\Yeni klasör\Parça2.SLDPRT").
///   4. Hicbiri tutmazsa dugum "bulunamadi" olarak KALIR; UYDURULMAZ ve
///      listeden DUSMEZ (CLAUDE.md 3).
/// Indekse ihtiyac yok: bu kural diskte dogrudan yoklanabiliyor ve cekirdegin
/// arayuz durumundan bagimsiz kalmasini sagliyor.
/// </summary>
public static class BelgeAgaci
{
    /// <summary>
    /// Ic ice montajda dip yapmamak icin derinlik siniri. Yuruyus ayni
    /// dosyayi bir kez actigi icin dongu zaten imkansiz; bu, o kuralin
    /// ikinci kilidi (CLAUDE.md 2: iki ucuz hipotezden birini secmek yerine
    /// ikisini birden kapat).
    /// </summary>
    public const int EnFazlaDerinlik = 32;

    /// <summary>
    /// Agaci yurur ve dugumleri DERINLEMESINE SIRAYLA doner: once belgenin
    /// kendisi (seviye 0), sonra her cocugu ve onun alti.
    ///
    /// AYNI DOSYA BIRDEN COK SATIR OLABILIR - bilincli: bir parca iki ayri
    /// montajin altinda geciyorsa iki satir cikar ve "kac yerde geciyor"
    /// sayilabilir. Ama ALTI bir kez acilir: ikinci gorunusun notu bunu
    /// soyler, sessiz kalmaz.
    /// </summary>
    /// <param name="kok">Agacin tepesindeki belge.</param>
    /// <param name="belirtec">
    /// Iptal. Buyuk bir montajda yuruyus dakikalar surebiliyor; iptal edilen
    /// yuruyus O ANA KADARKI dugumleri doner - cagiran bunu "tam liste" diye
    /// gostermemeli (CLAUDE.md 3).
    /// </param>
    public static IReadOnlyList<AgacDugumu> Yur(string? kok, CancellationToken belirtec = default)
    {
        var dugumler = new List<AgacDugumu>();
        if (string.IsNullOrWhiteSpace(kok))
        {
            return dugumler;
        }

        if (!Diskte(kok))
        {
            dugumler.Add(new AgacDugumu(kok, 0, Bulundu: false, Okunamadi: false, "Dosya bulunamadı."));
            return dugumler;
        }

        var acilan = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { kok };
        Yurut(kok, 0, dugumler, acilan, belirtec);
        return dugumler;
    }

    private static void Yurut(
        string yol, int seviye, List<AgacDugumu> dugumler, HashSet<string> acilan,
        CancellationToken belirtec)
    {
        if (belirtec.IsCancellationRequested)
        {
            return;
        }

        SwReferanslar referanslar = SwReferans.Oku(yol);
        if (!referanslar.Okundu)
        {
            // OKUNAMAYAN BELGE "referansi yok" DEMEK DEGIL (CLAUDE.md 3):
            // dugum kalir ve sebebini yazar.
            dugumler.Add(new AgacDugumu(
                yol, seviye, Bulundu: true, Okunamadi: true,
                "İçine bakılamadı — " + (referanslar.Sebep ?? "sebep bilinmiyor")));
            return;
        }

        dugumler.Add(new AgacDugumu(yol, seviye, Bulundu: true, Okunamadi: false, Not: null));

        foreach (string yazilan in referanslar.Dogrudan)
        {
            string? cocuk = Coz(yazilan, yol);
            if (cocuk is null)
            {
                dugumler.Add(new AgacDugumu(
                    yazilan, seviye + 1, Bulundu: false, Okunamadi: false,
                    "Diskte bulunamadı — dosyanın içinde yazan yol."));
                continue;
            }

            if (seviye + 1 >= EnFazlaDerinlik)
            {
                dugumler.Add(new AgacDugumu(
                    cocuk, seviye + 1, Bulundu: true, Okunamadi: true,
                    $"Ağaç {EnFazlaDerinlik} kat derinleşti — altına inilmedi."));
                continue;
            }

            if (!acilan.Add(cocuk))
            {
                dugumler.Add(new AgacDugumu(
                    cocuk, seviye + 1, Bulundu: true, Okunamadi: false,
                    "Yukarıda bir kez açıldı — alt ağacı orada yazıyor."));
                continue;
            }

            Yurut(cocuk, seviye + 1, dugumler, acilan, belirtec);
        }
    }

    /// <summary>
    /// Yazili bir referansin diskteki karsiligi: once EBEVEYNIN YANI, sonra
    /// yazili yol EBEVEYNE GORE cozulerek. SOLIDWORKS'un sirasi bu (CLAUDE.md 5).
    ///
    /// EBEVEYNE GORE COZMEK SART - ERKAN'DA OLCULDU (31.08.2026): onarimin
    /// kendi yazdigi yollar GORELI ("..\3157\.\...\Ad.SLDPRT"). Ilk halde
    /// buraya duz File.Exists(yazilan) konmustu; goreli yol calisma klasorune
    /// gore bakiliyor, hicbir zaman bulunmuyor ve montajin COCUKLARI HIC
    /// ARSIVLENMIYORDU - v0'da montaj tek basina kaldi, SOLIDWORKS "dosya
    /// bozuk" dedi. Kendi yazdigimiz yolu kendi toplayicimiz okuyamiyordu.
    /// </summary>
    public static string? Coz(string yazilanYol, string ebeveynYolu)
    {
        string ad = WindowsYolu.DosyaAdi(yazilanYol);
        if (ad.Length == 0)
        {
            return null;
        }

        try
        {
            string ebeveynKlasoru = WindowsYolu.Klasor(ebeveynYolu);
            string komsu = WindowsYolu.Birlestir(ebeveynKlasoru, ad);
            if (Diskte(komsu))
            {
                return komsu;
            }

            string? cozulen = EbeveyneGoreCoz(ebeveynKlasoru, yazilanYol);
            if (cozulen is not null && Diskte(cozulen))
            {
                return cozulen;
            }

            return SonEkiDene(ebeveynKlasoru, yazilanYol);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Yazili yolu ebeveynin klasorune gore GERCEK bir diske-yola cevirir.
    ///
    /// WindowsYolu.Cozumle KULLANILMIYOR - bilincli: o, iki yolu KIYASLAMAK
    /// icin var ve sonucu hep "\" ile birlestiriyor; testler Linux'ta
    /// kosuyor ve File.Exists oyle bir yolu bulamazdi. Burada tabandan
    /// Klasor/Birlestir ile yuruyoruz: taban GERCEK bir yol oldugundan
    /// Birlestir ayiriciyi ondan seciyor ve sonuc her iki isletim
    /// sisteminde de aranabilir kaliyor.
    /// </summary>
    private static string? EbeveyneGoreCoz(string temel, string yazilan)
    {
        // Mutlak yol (surucu ya da UNC) oldugu gibi denenir.
        if ((yazilan.Length > 1 && yazilan[1] == ':')
            || (yazilan.Length > 1 && Ayirici(yazilan[0]) && Ayirici(yazilan[1])))
        {
            return yazilan;
        }

        string suan = temel;
        foreach (string parca in yazilan.Split(
                     new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (parca == ".")
            {
                continue;   // onarimin uzunluk dolgusu: ".\" yolu degistirmez
            }

            if (parca == "..")
            {
                suan = WindowsYolu.Klasor(suan);
                if (suan.Length == 0)
                {
                    return null;   // kokun ustune cikti; yol gecersiz
                }

                continue;
            }

            suan = WindowsYolu.Birlestir(suan, parca);
        }

        return suan;

        static bool Ayirici(char c) => c is '\\' or '/';
    }

    /// <summary>
    /// Yazili yolun SON EKLERINI ebeveynin klasorune gore dener - en UZUN
    /// ekten baslayarak.
    ///
    /// NEDEN VAR - KAPI OLCTU (31.08.2026): ornek montajin icinde
    /// "C:\Users\PC\Desktop\tertemiz\Yeni klasör\Parça2.SLDPRT" yaziyor.
    /// Parca gercekte montajin yanindaki "Yeni klasör" altinda duruyor ama
    /// mutlak yol baska bir makineye ait; komsuluk kurali da yalniz dosya
    /// ADINA baktigi icin ALT KLASORDEKI dosyayi gormuyordu. Sonuc: parca
    /// listesi diskte DURAN bir parcayi "bulunamadi" diye gosteriyordu -
    /// yani listeye bakan biri o parcayi fiyatlamazdi (CLAUDE.md 3). Ayni
    /// delik versiyon arsivinde de vardi: o parca arsive HIC girmiyordu.
    ///
    /// UYDURMA DEGIL: bir yol ancak DISKTE VARSA doner ve en uzun ek once
    /// denenir - yani en OZGUL eslesme kazanir. Komsuluk kurali (1. adim)
    /// hala once geliyor; bu yalnizca "hicbir sey bulunamadi" halinin
    /// yerine geciyor.
    /// </summary>
    private static string? SonEkiDene(string temel, string yazilan)
    {
        string[] parcalar = yazilan.Split(
            new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        // bas = 1: surucu harfi/sunucu adi atlanir (o hicbir zaman ebeveynin
        // altinda olmaz). Son parca tek basina zaten komsuluk adiminda
        // denendi, o yuzden en az IKI parca kaliyor.
        for (int bas = 1; bas <= parcalar.Length - 2; bas++)
        {
            string aday = temel;
            for (int i = bas; i < parcalar.Length; i++)
            {
                if (parcalar[i] == "." || parcalar[i] == "..")
                {
                    aday = string.Empty;
                    break;
                }

                aday = WindowsYolu.Birlestir(aday, parcalar[i]);
            }

            if (aday.Length > 0 && Diskte(aday))
            {
                return aday;
            }
        }

        return null;
    }

    /// <summary>
    /// File.Exists TEK KAPIDAN geciyor: CLAUDE.md 4'te olculdu, erisilemeyen
    /// bir yolda uzun sure bloklanabiliyor. Bir gun zaman asimi konacaksa
    /// degisecek yer burasi olsun (CLAUDE.md 8).
    /// </summary>
    private static bool Diskte(string yol) => File.Exists(yol);
}
