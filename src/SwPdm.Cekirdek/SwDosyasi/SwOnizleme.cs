using System;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>
/// SOLIDWORKS BELGESININ ICINDEKI ONIZLEME.
///
/// OLCULDU (28.08.2026): SOLIDWORKS 2022 paketinde "PreviewPNG" akisi
/// GERCEK bir PNG - baytlari 89 50 4E 47 ile basliyor. Yani cozulecek bir
/// sey yok, akis oldugu gibi bir resim.
///
/// NEDEN ONEMLI: bugun onizleme yalnizca Windows kabugundan geliyor
/// (KabukOnizleme). SOLIDWORKS kurulu olmayan bir makinede kabuk .SLDPRT
/// icin resim vermiyor ve onizleme kutusu BOS kaliyor. Ayrica Wine'da kabuk
/// saglayicisi hic yok, yani bu alan BURADA HIC OLCULEMIYORDU. Bu akisla
/// ikisi de cozuluyor: onizleme her makinede cikiyor ve Linux'ta test
/// edilebiliyor.
///
/// Var olan "dosyanin icindeki gomulu onizleme" yedeginin (OnizlemeOkuyucu,
/// OLE bilesik belgeler icin) yeni bicime genisletilmesi bu. Uygulamanin
/// "Windows ne gosteriyorsa onu goster" sozu bozulmuyor: gosterilen sey
/// yine dosyanin KENDI icindeki onizleme, disaridan bir motor degil.
///
/// "Preview" diye ikinci bir akis daha var; o ham DIB (BITMAPINFOHEADER ile
/// basliyor). Kullanilmiyor - PNG hazir ve tek parca. Gerekirse buraya
/// eklenir, baska hicbir dosya degismez.
/// </summary>
public static class SwOnizleme
{
    /// <summary>Onizlemenin durdugu akis.</summary>
    private const string Akis = "PreviewPNG";

    /// <summary>PNG imzasi - gelen seyin gercekten resim oldugu DOGRULANIR.</summary>
    private static readonly byte[] PngImzasi = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Belgenin icindeki onizleme resmini (PNG) verir; yoksa null.
    ///
    /// CLAUDE.md 3: "bir seyler dondu" yetmez. Gelen baytlar PNG imzasini
    /// tasimiyorsa null donuyor - bozuk bir resmi onizleme diye gostermek
    /// kullaniciya bos bir kutu gosterip sebebini saklamak olurdu.
    /// </summary>
    public static byte[]? Oku(string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu) || !SwReferans.TasiyabilirMi(dosyaYolu))
        {
            return null;
        }

        try
        {
            using SwPaket? paket = SwPaket.Ac(dosyaYolu);
            byte[]? resim = paket?.AkisiOku(Akis);

            return PngMi(resim) ? resim : null;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool PngMi(byte[]? veri)
    {
        if (veri is null || veri.Length < PngImzasi.Length)
        {
            return false;
        }

        for (int i = 0; i < PngImzasi.Length; i++)
        {
            if (veri[i] != PngImzasi[i])
            {
                return false;
            }
        }

        return true;
    }
}
