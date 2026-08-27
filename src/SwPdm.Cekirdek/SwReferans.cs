using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SwPdm.Cekirdek;

/// <summary>
/// Bir SOLIDWORKS belgesinin ICINDE YAZAN referanslar.
/// </summary>
/// <param name="Dogrudan">
/// Belgenin DOGRUDAN kullandigi belgelerin yollari (montajin parcalari,
/// teknik resmin modeli). Kendi yolu bu listede YOKTUR.
/// </param>
/// <param name="KendiYolu">Belgenin en son kaydedildigi yol; bilinmiyorsa null.</param>
/// <param name="Okundu">Referanslar gercekten okunabildi mi.</param>
/// <param name="Sebep">Okunamadiysa SEBEP - bos liste "referans yok" DEMEK DEGIL.</param>
public sealed record SwReferanslar(
    IReadOnlyList<string> Dogrudan,
    string? KendiYolu,
    bool Okundu,
    string? Sebep)
{
    /// <summary>Okunamamis sonuc.</summary>
    public static SwReferanslar Okunamadi(string sebep)
        => new([], null, Okundu: false, sebep);
}

/// <summary>
/// SOLIDWORKS REFERANSLARINI OKUR - SOLIDWORKS KURULU OLMADAN.
///
/// OLCULDU (28.08.2026, SOLIDWORKS 2022, yedi gercek dosya):
///
///   Montaj1.SLDASM  -> Parça1.SLDPRT · Yeni klasör\Parça2.SLDPRT
///   Montaj2.SLDASM  -> Montaj1.SLDASM            (alt montaj)
///   Parça1.SLDDRW   -> Parça1.SLDPRT             (teknik resim -> model)
///   Montaj2.SLDDRW  -> Montaj2.SLDASM
///   Parça1/2.SLDPRT -> (yok, yaprak)
///
/// HANGI AKIS: "Header2". Bu, DOGRUDAN referanslari tutuyor. Ayrimi yapan
/// sey bu: "Contents/DisplayLists" ve "Contents/Definition" akislari butun
/// AGACI (torunlari da) yaziyor, "Header2" yalnizca dogrudan kullanilani.
/// Once yollari akis ayrimi yapmadan taramak denendi ve dolayli olanlari
/// dogrudan gibi gosteriyordu; akis adiyla ayirmak bunu cozdu.
///
/// DIZE BICIMI: MFC'nin Unicode CString'i -
///   FF FE FF  &lt;uzunluk: 1 bayt&gt;  &lt;uzunluk adet UTF-16LE karakter&gt;
/// Yedi dosyadaki 52 yolun 52'sinde tuttu. SOLIDWORKS bir MFC uygulamasi,
/// yani okudugumuz sey onlarin kendi serilestirme bicimi.
///
/// OLCULMEDI - UZUN YOL: uzunluk oneki 1 bayt, yani en fazla 254 karakter.
/// MFC daha uzun dizeleri kacisla yaziyor; o bicim BURADA GORULMEDI (ornek
/// yollarin hepsi kisaydi). Uzun bir ag yolu (\\sunucu\ortak\...) bunu
/// asabilir. Kod kacis baytini GORURSE o dizeyi atlar ve sonucu EKSIK
/// isaretler - sessizce yanlis cevap vermez (CLAUDE.md 3).
///
/// YOL BAYAT OLABILIR: butun yollar MUTLAK yaziliyor (C:\...). Klasor
/// tasininca dosyanin icindeki yol eskir ama SOLIDWORKS yanindaki kopyayi
/// bulup acmaya devam ediyor (CLAUDE.md 5'te olculdu). Yani buradan okunan
/// yol bir IPUCU; "dosya burada" garantisi degil.
/// </summary>
public static class SwReferans
{
    /// <summary>Referanslarin durdugu akis.</summary>
    private const string DogrudanAkis = "Header2";

    /// <summary>Belgenin kendi yolunun durdugu akis.</summary>
    private const string GecmisAkis = "_MO_VERSION_15000/Biography";

    /// <summary>MFC Unicode CString isareti.</summary>
    private static readonly byte[] DizeIsareti = [0xFF, 0xFE, 0xFF];

    /// <summary>Uzunluk oneki bu ise MFC kacis kullanmistir; bicimi olculmedi.</summary>
    private const byte KacisOneki = 0xFF;

    /// <summary>Bu uzantilardan biriyle biten dizeler yol sayilir.</summary>
    private static readonly string[] Uzantilar = [".SLDPRT", ".SLDASM", ".SLDDRW"];

    /// <summary>Bu dosya turu referans tasiyabilir mi.</summary>
    public static bool TasiyabilirMi(string? yol)
    {
        string uzanti = WindowsYolu.Uzanti(yol);
        foreach (string u in Uzantilar)
        {
            if (string.Equals(uzanti, u, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Belgenin icindeki dogrudan referanslari okur.
    /// Basarisizlikta bos liste DEGIL, sebebi olan bir sonuc doner.
    /// </summary>
    public static SwReferanslar Oku(string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu))
        {
            return SwReferanslar.Okunamadi("Yol boş.");
        }

        try
        {
            using SwPaket? paket = SwPaket.Ac(dosyaYolu);
            if (paket is null)
            {
                return SwReferanslar.Okunamadi(
                    "Dosya SOLIDWORKS paketi gibi görünmüyor (biçim tanınmadı).");
            }

            byte[]? basliklar = paket.AkisiOku(DogrudanAkis);
            if (basliklar is null)
            {
                return SwReferanslar.Okunamadi(
                    $"\"{DogrudanAkis}\" akışı okunamadı; referanslar bilinmiyor.");
            }

            string? kendi = KendiYolunuBul(paket, dosyaYolu);
            string kendiAdi = WindowsYolu.DosyaAdi(dosyaYolu);

            var yollar = new List<string>();
            var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool kacisGoruldu = Dizeler(basliklar, dize =>
            {
                if (!YolMu(dize))
                {
                    return;
                }

                // Belge kendi yolunu da yaziyor; o bir referans DEGIL.
                if (string.Equals(WindowsYolu.DosyaAdi(dize), kendiAdi, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (gorulen.Add(dize))
                {
                    yollar.Add(dize);
                }
            });

            return new SwReferanslar(
                yollar,
                kendi,
                Okundu: true,
                kacisGoruldu ? "Bazı yollar 254 karakterden uzun; onlar okunamadı." : null);
        }
        catch (FileNotFoundException)
        {
            return SwReferanslar.Okunamadi("Dosya bulunamadı.");
        }
        catch (DirectoryNotFoundException)
        {
            return SwReferanslar.Okunamadi("Klasör bulunamadı.");
        }
        catch (UnauthorizedAccessException)
        {
            return SwReferanslar.Okunamadi("Dosya okunamadı: erişim reddedildi.");
        }
        catch (IOException hata)
        {
            return SwReferanslar.Okunamadi("Dosya okunamadı: " + hata.Message);
        }
    }

    /// <summary>Belgenin en son kaydedildigi yolu bulur; yoksa null.</summary>
    private static string? KendiYolunuBul(SwPaket paket, string dosyaYolu)
    {
        byte[]? gecmis = paket.AkisiOku(GecmisAkis);
        if (gecmis is null)
        {
            return null;
        }

        string kendiAdi = WindowsYolu.DosyaAdi(dosyaYolu);
        string? bulunan = null;

        Dizeler(gecmis, dize =>
        {
            if (bulunan is null
                && YolMu(dize)
                && string.Equals(WindowsYolu.DosyaAdi(dize), kendiAdi, StringComparison.OrdinalIgnoreCase))
            {
                bulunan = dize;
            }
        });

        return bulunan;
    }

    /// <summary>
    /// Tampondaki MFC Unicode dizelerini tek tek verir.
    /// Doner deger: olculmemis KACIS bicimi goruldu mu (o dizeler atlandi).
    /// </summary>
    private static bool Dizeler(byte[] tampon, Action<string> her)
    {
        bool kacis = false;

        for (int i = 0; i + 4 < tampon.Length; i++)
        {
            if (tampon[i] != DizeIsareti[0]
                || tampon[i + 1] != DizeIsareti[1]
                || tampon[i + 2] != DizeIsareti[2])
            {
                continue;
            }

            byte uzunluk = tampon[i + 3];
            if (uzunluk == KacisOneki)
            {
                // MFC burada WORD/DWORD uzunluga geciyor. BICIM OLCULMEDI:
                // tahminle cozmek yanlis yol uretebilir, o yuzden atlaniyor
                // ve cagirana "eksik" oldugu SOYLENIYOR.
                kacis = true;
                continue;
            }

            if (uzunluk == 0)
            {
                continue;
            }

            int bas = i + 4;
            int bayt = uzunluk * 2;
            if (bas + bayt > tampon.Length)
            {
                continue;
            }

            string dize = Encoding.Unicode.GetString(tampon, bas, bayt);
            if (YazdirilabilirMi(dize))
            {
                her(dize);
            }

            i = bas + bayt - 1;
        }

        return kacis;
    }

    private static bool YazdirilabilirMi(string dize)
    {
        foreach (char c in dize)
        {
            if (c < ' ' && c != '\t')
            {
                return false;
            }
        }

        return true;
    }

    private static bool YolMu(string dize)
    {
        foreach (string u in Uzantilar)
        {
            if (dize.EndsWith(u, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
