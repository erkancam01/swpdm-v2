using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>Bir klasordeki kilit dosyalarinin cozulmus hali.</summary>
/// <param name="Gosterilecek">
/// Agacta gorunecek dosyalar: sahibi bulunan kilitler CIKARILMIS, sahibi
/// bulunamayan kilitler ICERIDE.
/// </param>
/// <param name="AcikYollar">Yaninda kilidi olan (yani ACIK gorunen) dosyalarin yollari.</param>
/// <param name="GizlenenSayisi">Kac kilit gizlendi. Durum cubugunda soylenir.</param>
public sealed record KilitDurumu(
    IReadOnlyList<DosyaOgesi> Gosterilecek,
    IReadOnlySet<string> AcikYollar,
    int GizlenenSayisi);

/// <summary>
/// SOLIDWORKS KILIT DOSYALARI ("~$...") - hepsi burada (CLAUDE.md 1b).
///
/// NE OLDUGU (CLAUDE.md 5, olculdu): SOLIDWORKS her actigi belge icin ayni
/// klasore gizli bir "~$&lt;ad&gt;" dosyasi yaziyor ve temiz kapanmazsa geride
/// birakiyor. Gezgin onlari gostermiyor; kullanici goremiyor ama Windows
/// "dizin bos degil" diyor.
///
/// KURAL - TEK CUMLE:
///   Sahibi AYNI KLASORDE varsa kilit GIZLENIR ve sahibi "acik" isaretlenir.
///   Sahibi YOKSA kilit GORUNUR ve "sahipsiz" diye etiketlenir.
///
/// NEDEN IKINCI YARISI SART: aciklayamadigimiz bir seyi gizlemek CLAUDE.md
/// 3'un ta kendisi olur - kullanici klasoru bos sanir, silmeye calisir,
/// Windows "dizin bos degil" der ve ekranda hicbir sebep yoktur. Zaten
/// klasor silinmesini engelleyen sey cogu zaman TAM OLARAK o dosyadir (4).
///
/// NEDEN GIZLEMEK DEGIL CEVIRMEK: "~$X.SLDPRT" bir belge degil, X.SLDPRT'nin
/// bir DURUMU - o belge su anda acik. Ag surucusunde calisan bir PDM'de bu
/// gurultu degil BILGI.
///
/// OLCULMEMIS BIR BILINMEYENE KARSI GUVENLI: kilit adinin her zaman tam
/// olarak "~$" + sahip adi oldugu yalnizca KISA adlarda olculdu. Office uzun
/// adlari kirpiyor; SOLIDWORKS de kirpiyorsa sahip BULUNAMAZ ve dosya
/// GIZLENMEZ. Yanlis tarafa dusmuyor.
///
/// NEDEN CEKIRDEKTE: arayuz kodunda birim testi yazilamiyor. Karar burada
/// oldugu icin Linux'ta test ediliyor; arayuz yalnizca ciziyor (CLAUDE.md 7).
/// <see cref="KlasorTarayici"/> DEGISMIYOR - disk okuyucusu her seyi
/// dondurmeye devam eder, degisen yalnizca CIZIM karari.
/// </summary>
public static class Kilit
{
    /// <summary>Kilit dosyasi adinin basindaki isaret.</summary>
    public const string Onek = "~$";

    /// <summary>Acik gorunen dosyanin adinin yanina yazilan.</summary>
    public const string AcikIsareti = "• açık";

    /// <summary>Sahibi bulunamayan kilidin adinin yanina yazilan.</summary>
    public const string SahipsizIsareti = "• sahipsiz kilit";

    /// <summary>Sahipsiz kilidin ipucu.</summary>
    public const string SahipsizIpucu =
        "SOLIDWORKS kilit dosyası; sahibi bu klasörde yok.\n"
        + "Büyük ihtimalle SOLIDWORKS temiz kapanmadığı için kalmış.\n"
        + "Gezgin'de görünmez ve klasörün silinmesini engelleyebilir.";

    /// <summary>Acik gorunen dosyanin ipucu.</summary>
    public static string AcikIpucu(string kilitAdi) =>
        $"Şu anda açık görünüyor: yanında \"{kilitAdi}\" kilit dosyası var.\n"
        + "Başka biri açmış olabilir; SOLIDWORKS temiz kapanmadıysa kalıntı da olabilir.\n"
        + "Kapatılmadan taşımak ya da silmek Windows tarafından engellenebilir.";

    /// <summary>Bu ad bir kilit dosyasinin adi mi.</summary>
    public static bool KilitMi(string? dosyaAdi)
        => dosyaAdi is not null
            && dosyaAdi.Length > Onek.Length
            && dosyaAdi.StartsWith(Onek, StringComparison.Ordinal);

    /// <summary>Kilit adindan sahibinin adi; kilit degilse null.</summary>
    public static string? SahibininAdi(string? dosyaAdi)
        => KilitMi(dosyaAdi) ? dosyaAdi![Onek.Length..] : null;

    /// <summary>
    /// Hangi dosya gorunecek, hangisi acik isaretlenecek.
    ///
    /// ESLESME KLASOR BAZINDA: liste birden cok klasoru kapsayabiliyor (arama
    /// sonucu). Yalnizca ada bakan bir eslesme, A klasorundeki kilidi B
    /// klasorundeki dosyayla eslestirir ve YANLIS dosyayi gizlerdi.
    /// </summary>
    public static KilitDurumu Coz(IReadOnlyList<DosyaOgesi>? dosyalar)
    {
        if (dosyalar is null || dosyalar.Count == 0)
        {
            return new KilitDurumu([], new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
        }

        // Anahtar: klasor + ad. Windows'ta dosya adlari buyuk/kucuk harf
        // duyarsiz - Ordinal karsilastirma AYNI dosyayi kacirirdi.
        var adaGore = new Dictionary<string, DosyaOgesi>(StringComparer.OrdinalIgnoreCase);
        foreach (DosyaOgesi dosya in dosyalar)
        {
            adaGore[dosya.Yol] = dosya;
        }

        var gosterilecek = new List<DosyaOgesi>(dosyalar.Count);
        var acikYollar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int gizlenen = 0;

        foreach (DosyaOgesi dosya in dosyalar)
        {
            string? sahipAdi = SahibininAdi(dosya.Ad);
            if (sahipAdi is null)
            {
                gosterilecek.Add(dosya);
                continue;
            }

            string sahipYolu = WindowsYolu.Birlestir(WindowsYolu.Klasor(dosya.Yol), sahipAdi);
            if (adaGore.ContainsKey(sahipYolu))
            {
                acikYollar.Add(sahipYolu);
                gizlenen++;
                continue;   // aciklayabiliyoruz: bu bir dosya degil, bir DURUM
            }

            // Sahibini bulamadik. GIZLEMIYORUZ (CLAUDE.md 3).
            gosterilecek.Add(dosya);
        }

        return new KilitDurumu(gosterilecek, acikYollar, gizlenen);
    }
}
