using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// Bir SOLIDWORKS dosyasinin ICINDEKI onizleme resmini cikarir - SOLIDWORKS
/// kurulu olmadan, dosyayi acmadan, diske hicbir sey yazmadan.
///
/// NE OLCULDU, NE OLCULMEDI (CLAUDE.md 10):
///   OLCULDU   : bilesik belge bicimi dogru cozuluyor (mini akis ve normal
///               sektor yollari, bagimsiz bir okuyucuyla capraz dogrulandi).
///   OLCULMEDI : GERCEK bir SOLIDWORKS dosyasindaki akisin ADI. Surumden
///               surume degisebiliyor ve elimizde gercek dosya YOK.
/// Bu yuzden tek bir ada baglanmiyoruz: adinda "preview" gecen her akis
/// deneniyor ve gelen bayt IMZASINDAN resim mi diye bakiliyor. Ad tutmazsa
/// kabuk onizlemesine dusulur (arayuz tarafi).
/// </summary>
public static class OnizlemeOkuyucu
{
    /// <summary>Once denenecek adlar; sonra adinda "preview" gecen her akis.</summary>
    private static readonly string[] OncelikliAdlar = ["PreviewPNG", "Preview", "PreviewBitmap"];

    /// <summary>
    /// Onizleme baytlarini dondurur (PNG/JPEG/BMP). Bulunamazsa null.
    /// Istisna ATMAZ: bozuk dosya da null doner.
    /// </summary>
    public static byte[]? Oku(string? dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu))
        {
            return null;
        }

        using BilesikDosya? bilesik = BilesikDosya.Ac(dosyaYolu);
        if (bilesik is null)
        {
            return null;
        }

        foreach (string ad in Adaylar(bilesik.AkisAdlari))
        {
            byte[]? ham = bilesik.AkisiOku(ad);
            if (ham is null || ham.Length < 8)
            {
                continue;
            }

            byte[]? resim = ResmeCevir(ham);
            if (resim is not null)
            {
                return resim;
            }
        }

        return null;
    }

    /// <summary>
    /// Ham baytlari gosterilebilir bir resme cevirir. Tanimadigi bicimde null.
    /// DIB (dosya basligi olmayan bitmap) ise basligi eklenir.
    /// </summary>
    public static byte[]? ResmeCevir(byte[] ham)
    {
        if (ham.Length >= 8 && ham[0] == 0x89 && ham[1] == 0x50 && ham[2] == 0x4E && ham[3] == 0x47)
        {
            return ham;   // PNG
        }

        if (ham.Length >= 3 && ham[0] == 0xFF && ham[1] == 0xD8 && ham[2] == 0xFF)
        {
            return ham;   // JPEG
        }

        if (ham.Length >= 2 && ham[0] == 0x42 && ham[1] == 0x4D)
        {
            return ham;   // BMP (dosya basligi zaten var)
        }

        return DibiSar(ham);
    }

    private static IEnumerable<string> Adaylar(IReadOnlyCollection<string> akisAdlari)
    {
        foreach (string tercih in OncelikliAdlar)
        {
            foreach (string ad in akisAdlari)
            {
                if (ad.Equals(tercih, StringComparison.OrdinalIgnoreCase))
                {
                    yield return ad;
                }
            }
        }

        foreach (string ad in akisAdlari)
        {
            if (ad.Contains("preview", StringComparison.OrdinalIgnoreCase)
                && Array.IndexOf(OncelikliAdlar, ad) < 0)
            {
                yield return ad;
            }
        }
    }

    /// <summary>
    /// Cipiplak DIB'e 14 baytlik BMP dosya basligi ekler. Pano ve OLE ozet
    /// bilgisindeki kucuk resimler bu bicimde saklaniyor.
    /// </summary>
    private static byte[]? DibiSar(byte[] dib)
    {
        if (dib.Length < 40)
        {
            return null;
        }

        uint basBoyu = BinaryPrimitives.ReadUInt32LittleEndian(dib);
        if (basBoyu is not (40 or 52 or 56 or 108 or 124))
        {
            return null;   // BITMAPINFOHEADER ailesinden degil
        }

        ushort bitSayisi = BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(14, 2));
        uint sikistirma = BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(16, 4));
        uint kullanilanRenk = BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(32, 4));

        long paletBayt = bitSayisi <= 8
            ? (kullanilanRenk == 0 ? 1L << bitSayisi : kullanilanRenk) * 4
            : 0;

        if (sikistirma == 3 && basBoyu == 40)
        {
            paletBayt += 12;   // BI_BITFIELDS maskeleri
        }

        long veriBasi = 14 + basBoyu + paletBayt;
        if (veriBasi > dib.Length + 14L)
        {
            return null;
        }

        var sonuc = new byte[14 + dib.Length];
        sonuc[0] = 0x42;   // 'B'
        sonuc[1] = 0x4D;   // 'M'
        BinaryPrimitives.WriteUInt32LittleEndian(sonuc.AsSpan(2, 4), (uint)sonuc.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(sonuc.AsSpan(10, 4), (uint)veriBasi);
        dib.CopyTo(sonuc, 14);
        return sonuc;
    }
}
