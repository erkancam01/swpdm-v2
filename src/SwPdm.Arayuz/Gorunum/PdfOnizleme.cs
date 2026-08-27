using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Bir PDF'in ILK SAYFASINI cizer.
///
/// NEDEN GEREKLI (olculdu): Erkan'in makinesinde Gezgin PDF'lerin ilk sayfasini
/// gostermiyor - yani kabukta kayitli bir PDF kucuk resim saglayicisi YOK.
/// Kabuk yolu PDF icin hicbir zaman resim dondurmeyecek; cizmek bize kaliyor.
///
/// NEDEN Windows.Data.Pdf: Windows 10/11'in ICINDE zaten var, pakete TEK BAYT
/// eklemiyor (paket 136 KB kaliyor). Alternatifi PDFium gibi yerli bir
/// kutuphaneydi: paket ~10 MB'a cikardi ve depoya ikili dosya girerdi.
/// Erkan'in olcutu "fiyat performans" idi.
///
/// BEDELI: hedef cerceve net8.0-windows10.0.19041.0 olmak zorunda. Bu
/// degisikligin Linux derlemesini ve uc kapiyi KIRMADIGI once olculdu
/// (CLAUDE.md 11); kirsaydi bu dosya hic yazilmayacakti.
///
/// ================== BURADA OLCULEMEYEN ==================
/// Wine WinRT TASIMIYOR. Yani bu sinifin GERCEKTEN cizip cizmedigi bu
/// ortamda gorulemez - yalnizca WinRT yokken COKMEDIGI olculebiliyor.
/// Cizim yalnizca Erkan'in makinesinde dogrulanabilir (CLAUDE.md 10).
/// ========================================================
/// </summary>
internal static class PdfOnizleme
{
    /// <summary>Akistan yuklemeye duserken bellege alinacak en buyuk dosya.</summary>
    private const long BellegeAlmaSiniri = 128L * 1024 * 1024;

    /// <summary>
    /// PDF'in ilk sayfasini dondurur. Cizemezse null ve <paramref name="sebep"/> dolar.
    /// Istisna ATMAZ.
    /// </summary>
    internal static Bitmap? Al(string yol, Size boyut, out string? sebep)
    {
        try
        {
            // WinRT eszamansiz. Cagiran STA is parcacigi; orada dogrudan
            // beklemek, tamamlanmalar mesaj pompasi isterse KILITLENEBILIR.
            // Is havuzunda kosturup burada bekliyoruz.
            (Bitmap? Resim, string? Sebep) sonuc =
                Task.Run(() => Ciz(yol, boyut)).GetAwaiter().GetResult();

            sebep = sonuc.Sebep;
            return sonuc.Resim;
        }
        catch (Exception hata) when (WinRtYok(hata))
        {
            // Bu ortamda WinRT hic yok (ornegin Wine). Cokmek yerine soyluyoruz.
            sebep = "Bu sistemde Windows PDF motoru yok (REGDB_E_CLASSNOTREG).";
            return null;
        }
        catch (Exception hata) when (hata is AggregateException or COMException
                                         or IOException or UnauthorizedAccessException
                                         or ArgumentException or InvalidOperationException
                                         or NotSupportedException or OutOfMemoryException)
        {
            sebep = "PDF önizlemesi alınamadı: " + Kok(hata).Message;
            return null;
        }
    }

    private static async Task<(Bitmap? Resim, string? Sebep)> Ciz(string yol, Size boyut)
    {
        PdfDocument? belge = await BelgeyiAc(yol).ConfigureAwait(false);
        if (belge is null)
        {
            return (null, "PDF açılamadı (parola korumalı ya da bozuk olabilir).");
        }

        if (belge.PageCount == 0)
        {
            return (null, "PDF'te sayfa yok.");
        }

        using PdfPage sayfa = belge.GetPage(0);

        // Sayfa oranini koruyarak kutuya sigdir.
        double sayfaGenislik = Math.Max(sayfa.Size.Width, 1);
        double sayfaYukseklik = Math.Max(sayfa.Size.Height, 1);
        double olcek = Math.Min(boyut.Width / sayfaGenislik, boyut.Height / sayfaYukseklik);
        uint hedefGenislik = (uint)Math.Max(1, Math.Round(sayfaGenislik * olcek));

        using var cikti = new InMemoryRandomAccessStream();
        await sayfa.RenderToStreamAsync(cikti, new PdfPageRenderOptions
        {
            DestinationWidth = hedefGenislik,
        });

        byte[] baytlar = await BaytlariAl(cikti).ConfigureAwait(false);

        // CLAUDE.md 4: Image.FromStream akisi SAHIPLENIYOR - akis kapaninca
        // resim sessizce cizilmiyor. Bagimsiz kopya sart.
        using var bellek = new MemoryStream(baytlar, writable: false);
        using Image gecici = Image.FromStream(bellek);
        return (new Bitmap(gecici), null);
    }

    private static async Task<PdfDocument?> BelgeyiAc(string yol)
    {
        // 1) Dogrudan dosyadan: dosyayi bellege ALMAZ.
        try
        {
            StorageFile dosya = await StorageFile.GetFileFromPathAsync(yol);
            return await PdfDocument.LoadFromFileAsync(dosya);
        }
        catch (Exception hata) when (hata is COMException or ArgumentException
                                         or UnauthorizedAccessException
                                         or FileNotFoundException or IOException)
        {
            // StorageFile ag yollarinda (\\sunucu\pay) takilabiliyor; akistan
            // yuklemeye dusuyoruz.
        }

        // 2) Akistan: bedeli dosyayi bellege almak, o yuzden sinirli.
        var bilgi = new FileInfo(yol);
        if (!bilgi.Exists || bilgi.Length > BellegeAlmaSiniri)
        {
            return null;
        }

        var tampon = new InMemoryRandomAccessStream();
        try
        {
            byte[] icerik = await File.ReadAllBytesAsync(yol).ConfigureAwait(false);
            using (var yazici = new DataWriter(tampon))
            {
                yazici.WriteBytes(icerik);
                await yazici.StoreAsync();
                await yazici.FlushAsync();
                yazici.DetachStream();
            }

            tampon.Seek(0);
            return await PdfDocument.LoadFromStreamAsync(tampon);
        }
        catch (Exception hata) when (hata is COMException or IOException
                                         or UnauthorizedAccessException or ArgumentException)
        {
            tampon.Dispose();
            return null;
        }
    }

    private static async Task<byte[]> BaytlariAl(IRandomAccessStream akis)
    {
        var baytlar = new byte[(int)akis.Size];
        using var okuyucu = new DataReader(akis.GetInputStreamAt(0));
        await okuyucu.LoadAsync((uint)baytlar.Length);
        okuyucu.ReadBytes(baytlar);
        return baytlar;
    }

    /// <summary>REGDB_E_CLASSNOTREG - calisma zamani sinifi bu sistemde kayitli degil.</summary>
    private const int SinifKayitliDegil = unchecked((int)0x80040154);

    /// <summary>
    /// Hata, WinRT'nin hic bulunmamasindan mi kaynaklaniyor. Wine'da butun
    /// Windows.* tipleri yok; bunu "PDF bozuk" diye raporlamak yanlis olurdu.
    ///
    /// OLCULDU (Wine, 27.08.2026): eksiklik TypeLoadException olarak degil,
    /// COMException 0x80040154 (REGDB_E_CLASSNOTREG) olarak geliyor. Yani
    /// yalnizca tip yukleme hatalarina bakmak YETMIYOR - once oyle yazmistim
    /// ve kullaniciya ham HRESULT gorunuyordu.
    /// </summary>
    private static bool WinRtYok(Exception hata)
    {
        Exception k = Kok(hata);
        return k is TypeLoadException or DllNotFoundException
            or EntryPointNotFoundException or PlatformNotSupportedException
            || (k is COMException com && com.HResult == SinifKayitliDegil)
            || (k is FileNotFoundException eksik
                && eksik.Message.Contains("Windows", StringComparison.OrdinalIgnoreCase));
    }

    private static Exception Kok(Exception hata)
        => hata is AggregateException toplu ? toplu.GetBaseException() : hata;
}
