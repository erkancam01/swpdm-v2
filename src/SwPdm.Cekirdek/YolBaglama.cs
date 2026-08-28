using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>
/// BIR EBEVEYNIN ICINDEKI YAZILI YOLU GERCEK DOSYAYA BAGLAR.
///
/// Iki musterisi var ve IKISI DE AYNI ISI yapiyor - o yuzden tek dosya
/// (CLAUDE.md 1b):
///   TOPLU (<see cref="BayatlariOnar"/>) : indeksteki butun bayat yollari
///          kendi buldugu hedefe baglar.
///   ELLE  (<see cref="Bagla"/>)         : hedefi KULLANICI secer.
///
/// NEDEN ELLE SECIM DE GEREKLI: bizim cozucumuz dosyayi ADA gore ariyor
/// (CLAUDE.md 5). Dosya baska bir programla YENIDEN ADLANDIRILDIYSA ortada
/// eslesecek bir ad yoktur ve otomatik onarimin bakacagi bir hedef kalmaz -
/// "BULUNAMADI" satiri boyle dogar. O bagi ancak dosyayi bilen kisi kurabilir.
///
/// AD DEGISIMINDEN FARKI (<see cref="ReferansOnarimi"/>): orada COCUGU biz
/// tasiyoruz ve BUTUN ebeveynler ya hep ya hic onarilir. Burada dosyaya
/// dokunulmuyor; yalnizca TEK bir ebeveynin icindeki TEK bir yol duzeliyor.
/// Bu yuzden "hepsi ya da hicbiri" kurali burada yok: her yol kendi basina
/// gecer, tutmayanin sebebi tek tek yazilir.
///
/// SIRA HER ZAMAN AYNI - CLAUDE.md 3: KOPYALA -> YAMA -> DOGRULA -> DEGISTIR.
/// Yama bir YAN DOSYAYA yaziliyor; dogrulama <see cref="SwYazici"/> icinde
/// diskten YENIDEN OKUYARAK yapiliyor. Tutmazsa asil dosya hic degismiyor.
/// </summary>
public static class YolBaglama
{
    /// <summary>Yamalanmis dosyanin gecici uzantisi.</summary>
    private const string YeniUzanti = ".swpdm-yeni";

    /// <summary>Degistirilmeden once asilin alindigi gecici uzanti.</summary>
    private const string YedekUzanti = ".swpdm-eski";

    /// <summary>
    /// BAYAT YOLLARI TOPLUCA ONARIR - gecmiste kirilmis baglari toparlar.
    ///
    /// NEDEN GEREKLI: bu surumden onceki tasimalarda (ve baska bir programla
    /// yapilan tasimalarda) dosyanin icindeki yol eskidi. Dosya duruyor, biz
    /// onu buluyoruz, ama SOLIDWORKS acamiyor. Ileriye donuk onarim bunu
    /// ONLUYOR; gecmiste olani ancak bu duzeltir.
    ///
    /// HER EBEVEYN ICIN AYRI ISLEM: bir ebeveynde birden cok bayat yol
    /// olabilir, her biri kendi icinde KOPYALA -> YAMA -> DOGRULA -> DEGISTIR
    /// olarak gecer. Biri tutmazsa otekiler DURMAZ; tutmayanin sebebi yazilir.
    ///
    /// ACIK DOSYAYA DOKUNULMAZ (yaninda "~$" kilidi olan) - sebebi yazilir.
    /// </summary>
    public static OnarimOzeti BayatlariOnar(ReferansIndeksi? indeks)
    {
        int onarilan = 0;
        var hatalar = new List<string>();
        var dokunulan = new List<string>();

        if (indeks is null)
        {
            return new OnarimOzeti(0, ["Referans indeksi yok."], []);
        }

        foreach (IndeksKaydi kayit in new List<IndeksKaydi>(indeks.Kayitlar))
        {
            foreach (string yazilan in kayit.YazilanYollar)
            {
                Cozum cozum = indeks.Coz(kayit, yazilan);
                if (!ReferansIndeksi.BayatMi(kayit.Yol, yazilan, cozum)
                    || cozum.Yol is not string gercek)
                {
                    continue;
                }

                string? hata = Bagla(kayit.Yol, WindowsYolu.DosyaAdi(yazilan), gercek);
                if (hata is null)
                {
                    onarilan++;
                    if (!Iceriyor(dokunulan, kayit.Yol))
                    {
                        dokunulan.Add(kayit.Yol);
                    }
                }
                else
                {
                    hatalar.Add(WindowsYolu.DosyaAdi(kayit.Yol) + " — " + hata);
                }
            }
        }

        return new OnarimOzeti(onarilan, hatalar, dokunulan);
    }

    /// <summary>
    /// Tek bir ebeveyndeki tek bir yazili yolu, verilen GERCEK dosyaya baglar.
    ///
    /// <paramref name="yazilanDosyaAdi"/> ebeveynin icinde su an YAZAN addir
    /// (eslesme ada gore yapiliyor - dosyalarin icindeki yollar yazarin
    /// makinesine ait, tam yol eslesmesi calismaz; CLAUDE.md 5).
    /// <paramref name="gercekYol"/> baglanacak dosyanin diskteki TAM yolu;
    /// adi farkli olabilir - ad degisimi olarak yazilir ve OLCULDU
    /// (28.08.2026, Erkan'in makinesi): SOLIDWORKS boyle bir dosyayi aciyor
    /// ve parcalari yerinde getiriyor.
    /// </summary>
    /// <returns>Hata sebebi; tuttuysa null (CLAUDE.md 3: sessiz basari yok).</returns>
    public static string? Bagla(string ebeveyn, string yazilanDosyaAdi, string gercekYol)
    {
        if (string.IsNullOrWhiteSpace(ebeveyn) || string.IsNullOrWhiteSpace(yazilanDosyaAdi))
        {
            return "Onarılacak referans belirtilmedi.";
        }

        if (string.IsNullOrWhiteSpace(gercekYol) || !File.Exists(gercekYol))
        {
            return "Seçilen dosya bulunamadı.";
        }

        if (!File.Exists(ebeveyn))
        {
            return "Onarılacak dosya bulunamadı.";
        }

        // ACIK DOSYAYA YAZILMAZ: SOLIDWORKS'te acik bir belgenin yaninda "~$"
        // kilidi durur (CLAUDE.md 5) ve oturumdaki hal diskteki halin onune
        // gecer - yazsak bile kaydedildiginde uzerine yazilirdi.
        if (Kilit.AcikMi(ebeveyn))
        {
            return "SOLIDWORKS'te açık görünüyor; önce kapatın.";
        }

        string yeni = ebeveyn + YeniUzanti;
        YamaSonucu s = SwYazici.YoluDegistir(
            ebeveyn, yeni, yazilanDosyaAdi, gercekYol, WindowsYolu.Klasor(ebeveyn));

        if (!s.Oldu)
        {
            DosyaIslemleri.GeciciyiSil(yeni);
            return s.Sebep;
        }

        string yedek = ebeveyn + YedekUzanti;
        try
        {
            File.Move(ebeveyn, yedek);
            File.Move(yeni, ebeveyn);
            DosyaIslemleri.GeciciyiSil(yedek);
            return null;
        }
        catch (Exception hata) when (DosyaIslemleri.DiskHatasi(hata))
        {
            DosyaIslemleri.GeciciyiSil(yeni);
            return hata.Message;
        }
    }

    /// <summary>
    /// Bir ebeveynin ICINDE yazan, ama ELLE baglanabilecek yollar: cozulemeyen
    /// (BULUNAMADI/BELIRSIZ) ve bayat olanlar.
    ///
    /// COZULMUS VE YERINDE olanlar listeye GIRMEZ: calisan bir bagi elle
    /// degistirmek icin bir sebep yok ve listeyi kalabalik yapmak yanlis
    /// olani secme ihtimalini artirir (CLAUDE.md 1a).
    /// </summary>
    public static IReadOnlyList<(string YazilanYol, Cozum Cozum)> BaglanabilirYollar(
        ReferansIndeksi? indeks, string? ebeveyn)
    {
        var sonuc = new List<(string, Cozum)>();
        if (indeks is null || string.IsNullOrWhiteSpace(ebeveyn))
        {
            return sonuc;
        }

        foreach ((string yazilan, Cozum cozum) in indeks.Kullandiklari(ebeveyn))
        {
            if (cozum.Durum != CozumDurumu.Bulundu
                || ReferansIndeksi.BayatMi(ebeveyn, yazilan, cozum))
            {
                sonuc.Add((yazilan, cozum));
            }
        }

        return sonuc;
    }

    private static bool Iceriyor(List<string> liste, string aranan)
    {
        foreach (string v in liste)
        {
            if (string.Equals(v, aranan, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
