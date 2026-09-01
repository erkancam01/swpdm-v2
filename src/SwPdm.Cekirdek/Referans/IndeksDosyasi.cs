using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SwPdm.Cekirdek;

/// <summary>
/// INDEKSIN DISKTEKI HALI.
///
/// NEREDE: %APPDATA%\SwPdm\indeks\&lt;kok-ozeti&gt;.txt
///
/// AG SURUCUSUNE YAZILMIYOR - ve bu bilincli. Uygulamanin asil calisma yeri
/// ortak bir ag surucusu; oraya yazmak uc sorun dogururdu: klasor
/// salt-okunur olabilir, iki kullanici birbirinin indeksini ezer, ve
/// kullanicinin proje klasorune bizim dosyamiz girer. Indeks zaten
/// TUREVSEL bir veri - kaybolursa yeniden taranir, bir sey kaybolmaz.
///
/// BICIM: duz metin, "anahtar=deger" - Ayarlar.cs ile ayni desen (CLAUDE.md
/// 8: ikinci bir bicim icat edilmiyor). Bozuk satir ATLANIR, uygulamayi
/// dusurmez; indeks eksik kalir ve bunu tarama zaten duzeltir.
/// </summary>
public static class IndeksDosyasi
{
    /// <summary>Bir kokun indeks dosyasinin yolu.</summary>
    public static string YoluBul(string kok)
    {
        string taban = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Linux'ta (testlerde) bos donebiliyor - Ayarlar.cs ile ayni cozum.
        if (string.IsNullOrEmpty(taban))
        {
            taban = Path.GetTempPath();
        }

        string klasor = WindowsYolu.Birlestir(WindowsYolu.Birlestir(taban, "SwPdm"), "indeks");
        return WindowsYolu.Birlestir(klasor, Ozet(kok) + ".txt");
    }

    /// <summary>Kokun indeksini diskten okur; yoksa BOS indeks doner.</summary>
    public static ReferansIndeksi Oku(string kok, string? dosya = null)
    {
        var indeks = new ReferansIndeksi(kok);
        string yol = dosya ?? YoluBul(kok);

        string[] satirlar;
        try
        {
            if (!File.Exists(yol))
            {
                return indeks;
            }

            satirlar = File.ReadAllLines(yol, Encoding.UTF8);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Indeks okunamadi: bos indeksle devam. Tarama yeniden kurar.
            return indeks;
        }

        string? suanYol = null;
        long boyut = 0;
        DateTime degistirme = default;
        bool okundu = false;
        string? kendi = null;
        string? sebep = null;
        var referanslar = new List<string>();
        DateTime? zaman = null;
        bool tam = false;

        void Kaydet()
        {
            if (suanYol is not null)
            {
                indeks.Koy(new IndeksKaydi(
                    suanYol, boyut, degistirme, referanslar.ToArray(), kendi, okundu, sebep));
            }
        }

        foreach (string satir in satirlar)
        {
            int esit = satir.IndexOf('=', StringComparison.Ordinal);
            if (esit <= 0)
            {
                continue;
            }

            string anahtar = satir[..esit];
            string deger = satir[(esit + 1)..];

            switch (anahtar)
            {
                case "zaman":
                    if (long.TryParse(deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out long t))
                    {
                        zaman = new DateTime(t, DateTimeKind.Local);
                    }

                    break;

                case "tam":
                    tam = deger == "1";
                    break;

                case "dosya":
                    Kaydet();
                    suanYol = deger;
                    boyut = 0;
                    degistirme = default;
                    okundu = false;
                    kendi = null;
                    sebep = null;
                    referanslar = [];
                    break;

                case "boyut":
                    long.TryParse(deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out boyut);
                    break;

                case "degistirme":
                    if (long.TryParse(deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out long d))
                    {
                        degistirme = new DateTime(d, DateTimeKind.Local);
                    }

                    break;

                case "okundu":
                    okundu = deger == "1";
                    break;

                case "kendi":
                    kendi = deger.Length == 0 ? null : deger;
                    break;

                case "sebep":
                    sebep = deger.Length == 0 ? null : deger;
                    break;

                case "ref":
                    if (suanYol is not null && deger.Length > 0)
                    {
                        referanslar.Add(deger);
                    }

                    break;

                default:
                    // Tanimadigimiz anahtar: atlanir. Ileride eklenen bir alan
                    // eski surumu DUSURMEZ.
                    break;
            }
        }

        Kaydet();

        if (zaman is not null)
        {
            indeks.TaramayiBitir(tam, zaman.Value);
        }

        return indeks;
    }

    /// <summary>Indeksi diske yazar. Yazilamazsa SESSIZCE gecmez, false doner.</summary>
    public static bool Yaz(ReferansIndeksi indeks, string? dosya = null)
    {
        ArgumentNullException.ThrowIfNull(indeks);
        string yol = dosya ?? YoluBul(indeks.Kok);

        var satirlar = new List<string>
        {
            "kok=" + indeks.Kok,
            "zaman=" + (indeks.TaramaZamani ?? DateTime.Now).Ticks.ToString(CultureInfo.InvariantCulture),
            "tam=" + (indeks.Tam ? "1" : "0"),
        };

        foreach (IndeksKaydi k in indeks.Kayitlar)
        {
            satirlar.Add("dosya=" + k.Yol);
            satirlar.Add("boyut=" + k.Boyut.ToString(CultureInfo.InvariantCulture));
            satirlar.Add("degistirme=" + k.Degistirme.Ticks.ToString(CultureInfo.InvariantCulture));
            satirlar.Add("okundu=" + (k.Okundu ? "1" : "0"));

            if (k.KendiYolu is not null)
            {
                satirlar.Add("kendi=" + Tek(k.KendiYolu));
            }

            if (k.Sebep is not null)
            {
                satirlar.Add("sebep=" + Tek(k.Sebep));
            }

            foreach (string r in k.YazilanYollar)
            {
                satirlar.Add("ref=" + Tek(r));
            }
        }

        try
        {
            string klasor = WindowsYolu.Klasor(yol);
            if (klasor.Length > 0)
            {
                Directory.CreateDirectory(klasor);
            }

            File.WriteAllLines(yol, satirlar, Encoding.UTF8);
            return true;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Satir sonu iceren bir deger dosya bicimini bozar; tek satira indirilir.</summary>
    private static string Tek(string deger)
        => deger.Replace('\r', ' ').Replace('\n', ' ');

    private static string Ozet(string kok)
    {
        byte[] ozet = SHA256.HashData(Encoding.UTF8.GetBytes(kok.ToUpperInvariant()));
        var yazi = new StringBuilder(16);
        for (int i = 0; i < 8; i++)
        {
            yazi.Append(ozet[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return yazi.ToString();
    }
}
