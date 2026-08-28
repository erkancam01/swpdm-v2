using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Bir ad degisiminin referanslara etkisi - HICBIR SEY DEGISTIRMEDEN.</summary>
/// <param name="EskiYol">Adi degisecek dosya.</param>
/// <param name="YeniAd">Yeni dosya adi.</param>
/// <param name="Ebeveynler">Bu dosyayi kullanan ve onarilmasi gereken dosyalar.</param>
/// <param name="Engeller">Onarimi imkansiz kilan sebepler; bos degilse UYGULANMAZ.</param>
/// <param name="Guvenilir">Indeks tam mi - degilse ebeveyn listesi EKSIK olabilir.</param>
public sealed record OnarimPlani(
    string EskiYol,
    string YeniAd,
    IReadOnlyList<string> Ebeveynler,
    IReadOnlyList<string> Engeller,
    bool Guvenilir);

/// <summary>Onarimin sonucu.</summary>
/// <param name="Oldu">Ad degisti VE butun ebeveynler onarildi mi.</param>
/// <param name="Onarilanlar">Onarilan ebeveynlerin yollari.</param>
/// <param name="Sebep">Olmadiysa sebep. Bos donmek yasak (CLAUDE.md 3).</param>
public sealed record OnarimSonucu(
    bool Oldu, IReadOnlyList<string> Onarilanlar, string? Sebep)
{
    /// <summary>Hicbir sey degismedi.</summary>
    public static OnarimSonucu Olmadi(string sebep) => new(false, [], sebep);
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
public static class ReferansOnarimi
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
        if (indeks is null)
        {
            return new OnarimPlani(
                eskiYol, yeniAd, [], ["Referans indeksi yok; kimin kullandığı bilinmiyor."], false);
        }

        KullanimSonucu kullanim = indeks.Kullananlar(eskiYol);
        return Kur(eskiYol, yeniAd, kullanim.Kullananlar, kullanim.Guvenilir);
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
        IReadOnlyList<string> ebeveynler, string eskiYol, string yeniAd)
        => Kur(eskiYol, yeniAd, ebeveynler, guvenilir: true);

    private static OnarimPlani Kur(
        string eskiYol, string yeniAd, IReadOnlyList<string> adaylar, bool guvenilir)
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

        return new OnarimPlani(eskiYol, yeniAd, ebeveynler, engeller, guvenilir);
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
        string yeniYol = WindowsYolu.Birlestir(WindowsYolu.Klasor(plan.EskiYol), plan.YeniAd);

        if (File.Exists(yeniYol) || Directory.Exists(yeniYol))
        {
            return OnarimSonucu.Olmadi($"\"{plan.YeniAd}\" bu klasörde zaten var.");
        }

        // ---- 1) HER ebeveyn YAN DOSYAYA yamalanir; asillara dokunulmaz.
        var yamalar = new List<(string Ebeveyn, string Yeni)>();
        foreach (string ebeveyn in plan.Ebeveynler)
        {
            string yeni = ebeveyn + YeniUzanti;
            YamaSonucu s = SwYazici.AdiDegistir(ebeveyn, yeni, eskiAd, plan.YeniAd);
            if (!s.Oldu)
            {
                Temizle(yamalar);
                Sil(yeni);
                return OnarimSonucu.Olmadi(
                    $"{WindowsYolu.DosyaAdi(ebeveyn)} onarılamadı: {s.Sebep} "
                    + "Hiçbir şey değiştirilmedi.");
            }

            yamalar.Add((ebeveyn, yeni));
        }

        // ---- 2) Cocugun adi degisir.
        try
        {
            File.Move(plan.EskiYol, yeniYol);
        }
        catch (Exception hata) when (Dosya(hata))
        {
            Temizle(yamalar);
            return OnarimSonucu.Olmadi("Adı değiştirilemedi: " + hata.Message);
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
                GeriAl(degisenler, yeniYol, plan.EskiYol);
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

        return new OnarimSonucu(true, [.. plan.Ebeveynler], null);
    }

    /// <summary>Yaninda "~$" kilidi olan, yani SOLIDWORKS'te acik gorunenler.</summary>
    private static IEnumerable<string> AcikOlanlar(IReadOnlyList<string> yollar)
    {
        foreach (string yol in yollar)
        {
            string kilit = WindowsYolu.Birlestir(
                WindowsYolu.Klasor(yol), Kilit.Onek + WindowsYolu.DosyaAdi(yol));
            if (File.Exists(kilit))
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
        List<(string Ebeveyn, string Yedek)> degisenler, string yeniYol, string eskiYol)
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
            if (File.Exists(yeniYol))
            {
                File.Move(yeniYol, eskiYol);
            }
        }
        catch (Exception hata) when (Dosya(hata))
        {
            // ad geri alinamadi; cagirana donen sebep zaten islemin
            // yapilmadigini soyluyor
        }
    }

    private static void Sil(string yol)
    {
        try
        {
            if (File.Exists(yol))
            {
                File.Delete(yol);
            }
        }
        catch (Exception hata) when (Dosya(hata))
        {
            // silinemeyen gecici dosya isi bozmaz
        }
    }

    private static bool Dosya(Exception hata)
        => hata is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException;
}
