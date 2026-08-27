using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace SwPdm.Cekirdek;

/// <summary>Paketteki bir akisin yeri ve boyutlari.</summary>
/// <param name="Ad">Akis adi, ornegin "Header2" ya da "Contents/Config-0".</param>
/// <param name="VeriBaslangici">Sikistirilmis verinin dosyadaki ofseti.</param>
/// <param name="SikisikBoyut">Diskteki bayt sayisi.</param>
/// <param name="AcilmisBoyut">Acildiginda cikacak bayt sayisi.</param>
public sealed record SwAkis(string Ad, long VeriBaslangici, int SikisikBoyut, int AcilmisBoyut);

/// <summary>
/// SOLIDWORKS DOSYA KABI (2022'de olculdu).
///
/// SOLIDWORKS 2022 dosyalari OLE Bilesik Belge DEGIL - bu ONCE yanlis
/// varsayildi ve olcumle duzeltildi (CLAUDE.md 5). Kap, ardarda dizilmis
/// ADLANDIRILMIS akislardan olusuyor:
///
///   uint32 sikisikBoyut
///   uint32 acilmisBoyut
///   uint32 adUzunlugu
///   byte[adUzunlugu] ad          &lt;-- her baytin NIBBLE'lari takas edilmis ASCII
///   byte[sikisikBoyut] veri      &lt;-- ham deflate (zlib basligi YOK)
///
/// Ad cozumu: 0x34 0xF6 0xE6 0x47 -> "Cont". Yani bayt 0xAB, 'BA' olarak
/// okunuyor. Tahmin degil: yedi gercek dosyada butun akis adlari bu kuralla
/// okunabilir ASCII cikti ("Header2", "PreviewPNG", "[Content_Types].xml").
///
/// NEDEN BOYLE OKUNUYOR - HIZ:
/// Akislarin VERISI okunmuyor, uzerinden ATLANIYOR (Seek). Yalnizca basliklar
/// okunur; istenen akis sonradan tek tek acilir. Olculdu: dosya basina ~66 KB
/// okunuyor ve bu sayi DOSYA BOYUTUNDAN BAGIMSIZ - 50 KB'lik parcada da,
/// buyuk bir montajda da ayni. "Binlerce parcali montajda uzun surer mi"
/// sorusunun cevabi bu.
///
/// OLCULMEDI: cok buyuk (100 MB+) bir montajda akis SAYISI artiyor mu.
/// Akislar kategoriye gore gorunuyor (parca sayisina gore degil) ama
/// dogrulanmadi; iddia edilmiyor.
/// </summary>
public sealed class SwPaket : IDisposable
{
    /// <summary>Baslik + ad icin bir seferde okunan pencere.</summary>
    private const int TamponBoyu = 1024;

    /// <summary>Basliklar arasindaki bosluk bu kadari asarsa aranmaya devam edilir.</summary>
    private const int PencerePayi = 256;

    /// <summary>Ilk akis bu kadar bayt icinde bulunamazsa dosya bu kap DEGILDIR.</summary>
    private const int IlkAkisIcinEnFazla = 64 * 1024;

    /// <summary>Sonsuz donguye karsi ust sinir.</summary>
    private const int EnFazlaAkis = 20_000;

    /// <summary>Akil disi bir "acilmis boyut" okumaya karsi ust sinir.</summary>
    private const int EnFazlaAcilmisBoyut = 512 * 1024 * 1024;

    private readonly FileStream _akis;
    private readonly List<SwAkis> _akislar;

    private SwPaket(FileStream akis, List<SwAkis> akislar)
    {
        _akis = akis;
        _akislar = akislar;
    }

    /// <summary>Pakettekiler, dosyadaki sirayla.</summary>
    public IReadOnlyList<SwAkis> Akislar => _akislar;

    /// <summary>
    /// Dosyayi acar ve akis tablosunu cikarir. Bu kap degilse null doner.
    /// Verinin kendisi OKUNMAZ.
    /// </summary>
    public static SwPaket? Ac(string yol)
    {
        FileStream? akis = null;
        try
        {
            akis = new FileStream(
                yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, TamponBoyu);

            List<SwAkis>? akislar = Tarayarak(akis);
            if (akislar is null)
            {
                akis.Dispose();
                return null;
            }

            return new SwPaket(akis, akislar);
        }
        catch (Exception)
        {
            akis?.Dispose();
            throw;
        }
    }

    /// <summary>Adi verilen akisi acar; yoksa ya da acilamazsa null.</summary>
    public byte[]? AkisiOku(string ad)
    {
        foreach (SwAkis a in _akislar)
        {
            if (string.Equals(a.Ad, ad, StringComparison.Ordinal))
            {
                return Coz(a);
            }
        }

        return null;
    }

    /// <summary>Verilen akisi acar; bozuksa null.</summary>
    public byte[]? Coz(SwAkis akis)
    {
        ArgumentNullException.ThrowIfNull(akis);

        try
        {
            var sikisik = new byte[akis.SikisikBoyut];
            _akis.Position = akis.VeriBaslangici;
            _akis.ReadExactly(sikisik, 0, sikisik.Length);

            var cikti = new byte[akis.AcilmisBoyut];
            using var kaynak = new MemoryStream(sikisik, writable: false);
            using var acici = new DeflateStream(kaynak, CompressionMode.Decompress);

            int toplam = 0;
            while (toplam < cikti.Length)
            {
                int okunan = acici.Read(cikti, toplam, cikti.Length - toplam);
                if (okunan <= 0)
                {
                    break;
                }

                toplam += okunan;
            }

            // KISMI ACILMA YUTULMAZ: beklenen boyut cikmadiysa icerik eksiktir
            // ve eksik icerikten "referans yok" sonucu cikarmak yalan olurdu
            // (CLAUDE.md 3). Ne kadar cikmissa o kadari donuyor.
            return toplam == cikti.Length ? cikti : cikti[..toplam];
        }
        catch (Exception hata) when (hata is IOException or InvalidDataException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _akis.Dispose();

    /// <summary>
    /// Zinciri yurur. Her adimda TEK tampon okunur, akisin verisi ATLANIR.
    /// Hicbir akis bulunamazsa null (= bu kap degil).
    /// </summary>
    private static List<SwAkis>? Tarayarak(FileStream akis)
    {
        long uzunluk = akis.Length;
        var bulunanlar = new List<SwAkis>();
        var tampon = new byte[TamponBoyu];
        long p = 0;

        while (p < uzunluk - 16 && bulunanlar.Count < EnFazlaAkis)
        {
            akis.Position = p;
            int okunan = akis.Read(tampon, 0, tampon.Length);
            if (okunan < 16)
            {
                break;
            }

            SwAkis? bulunan = TamponuTara(tampon, okunan, p, uzunluk, akis);
            if (bulunan is null)
            {
                // Ilk akis makul bir mesafede yoksa bu dosya bu kap degildir;
                // butun dosyayi bayt bayt taramanin anlami yok.
                if (bulunanlar.Count == 0 && p > IlkAkisIcinEnFazla)
                {
                    return null;
                }

                p += Math.Max(1, okunan - PencerePayi);
                continue;
            }

            bulunanlar.Add(bulunan);
            p = bulunan.VeriBaslangici + bulunan.SikisikBoyut;
        }

        return bulunanlar.Count > 0 ? bulunanlar : null;
    }

    /// <summary>
    /// Tamponda gecerli bir akis basligi arar.
    ///
    /// ADAY DOGRULANIR - ve bu SART. Dogrulamasiz ilk hali yedi dosyanin
    /// DORDUNDE zinciri kaybetti: rastgele baytlar makul gorunen bir baslik
    /// uretiyor, oraya atlaniyor ve gercek zincir bir daha bulunamiyor.
    /// Belirti sinsiydi - istisna yok, yalnizca "Header2 yok" diyordu.
    /// Dogrulama: adayin verisinin BITTIGI yerde ya dosya biter ya da BASKA
    /// bir gecerli baslik durur. Sahte adayda ikisi de olmuyor.
    /// </summary>
    private static SwAkis? TamponuTara(
        byte[] tampon, int okunan, long tamponOfseti, long uzunluk, FileStream akis)
    {
        for (int i = 0; i + 12 < okunan; i++)
        {
            uint sikisik = Oku32(tampon, i);
            uint acilmis = Oku32(tampon, i + 4);
            uint adUzunlugu = Oku32(tampon, i + 8);

            long baslikYeri = tamponOfseti + i;
            if (sikisik == 0 || sikisik > uzunluk - baslikYeri)
            {
                continue;
            }

            if (acilmis == 0 || acilmis > EnFazlaAcilmisBoyut)
            {
                continue;
            }

            if (adUzunlugu < 4 || adUzunlugu > 200 || i + 12 + adUzunlugu > okunan)
            {
                continue;
            }

            string? ad = AdiCoz(tampon, i + 12, (int)adUzunlugu);
            if (ad is null)
            {
                continue;
            }

            long veri = baslikYeri + 12 + adUzunlugu;
            if (veri + sikisik > uzunluk)
            {
                continue;
            }

            if (!SonrasiTutuyorMu(akis, veri + sikisik, uzunluk))
            {
                continue;
            }

            return new SwAkis(ad, veri, (int)sikisik, (int)acilmis);
        }

        return null;
    }

    /// <summary>
    /// Adayin verisinin bittigi yerde zincirin devam edip etmedigine bakar.
    /// Dosyanin sonuna gelinmisse de tutar.
    /// </summary>
    private static bool SonrasiTutuyorMu(FileStream akis, long yer, long uzunluk)
    {
        if (yer >= uzunluk - 16)
        {
            return true;
        }

        long eskiYer = akis.Position;
        try
        {
            var tampon = new byte[TamponBoyu];
            akis.Position = yer;
            int okunan = akis.Read(tampon, 0, tampon.Length);

            for (int i = 0; i + 12 < okunan; i++)
            {
                uint sikisik = Oku32(tampon, i);
                uint acilmis = Oku32(tampon, i + 4);
                uint adUzunlugu = Oku32(tampon, i + 8);

                long baslik = yer + i;
                if (sikisik == 0 || sikisik > uzunluk - baslik) { continue; }
                if (acilmis == 0 || acilmis > EnFazlaAcilmisBoyut) { continue; }
                if (adUzunlugu < 4 || adUzunlugu > 200 || i + 12 + adUzunlugu > okunan) { continue; }
                if (AdiCoz(tampon, i + 12, (int)adUzunlugu) is null) { continue; }
                if (baslik + 12 + adUzunlugu + sikisik > uzunluk) { continue; }

                return true;
            }

            return false;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            akis.Position = eskiYer;
        }
    }

    /// <summary>
    /// Nibble'lari takas edilmis ASCII adi cozer. Ad, akis adlarinda gorulen
    /// karakter kumesinin disina cikarsa null - yanlis alarm uretmemek icin
    /// kume DAR tutuldu (CLAUDE.md 9).
    /// </summary>
    private static string? AdiCoz(byte[] tampon, int bas, int uzunluk)
    {
        var harfler = new char[uzunluk];
        bool harfVar = false;

        for (int i = 0; i < uzunluk; i++)
        {
            byte b = tampon[bas + i];
            char c = (char)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));

            bool uygun = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')
                         or '_' or '.' or '/' or '[' or ']' or '-' or ' ';
            if (!uygun)
            {
                return null;
            }

            harfVar |= c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
            harfler[i] = c;
        }

        return harfVar ? new string(harfler) : null;
    }

    private static uint Oku32(byte[] tampon, int yer)
        => (uint)(tampon[yer] | (tampon[yer + 1] << 8) | (tampon[yer + 2] << 16) | (tampon[yer + 3] << 24));
}
