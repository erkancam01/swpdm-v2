using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Dosya simgelerini WINDOWS KABUGUNDAN alir - yani Gezgin'de gorunen
/// simgenin ta kendisini.
///
/// NEDEN BOYLE: SOLIDWORKS kurulu bir makinede .SLDPRT/.SLDASM/.SLDDRW
/// simgeleri zaten kabuga KAYITLI. Kendi cizdigimiz taklitleri koymak yerine
/// gerceklerini istiyoruz; ustelik kullanicinin SOLIDWORKS surumu degisince
/// simgeler de kendiliginden dogru kalir.
///
/// SHGFI_USEFILEATTRIBUTES sayesinde dosyanin VAR OLMASI gerekmiyor: yalnizca
/// uzantiya bakiliyor, diske dokunulmuyor.
///
/// Basarisiz olursa null doner ve cagiran <see cref="Simgeler"/> icindeki
/// cizilmis yedege duser. SOLIDWORKS kurulu OLMAYAN bir makinede (ornegin
/// calistirma kapisinin Wine ortami) kabuk genel bir simge dondurur; bu da
/// Gezgin'in gosterdiginin aynisidir, yani yine tutarlidir.
/// </summary>
internal static class KabukSimgeleri
{
    private const uint SimgeAl = 0x000000100;          // SHGFI_ICON
    private const uint KucukSimge = 0x000000001;       // SHGFI_SMALLICON
    private const uint OznitelikleriKullan = 0x000000010; // SHGFI_USEFILEATTRIBUTES

    private const uint NormalDosya = 0x00000080;       // FILE_ATTRIBUTE_NORMAL
    private const uint Dizin = 0x00000010;             // FILE_ATTRIBUTE_DIRECTORY

    private static byte[]? _bilinmeyeninSimgesi;
    private static bool _bilinmeyenOlculdu;

    /// <summary>
    /// Uzantiya kayitli kabuk simgesi. Ornek: ".SLDPRT".
    ///
    /// OLCULDU (27.08.2026): SOLIDWORKS kurulu OLMAYAN bir makinede kabuk
    /// .SLDPRT/.SLDASM/.SLDDRW icin ayni GENEL bos sayfa simgesini donduruyor.
    /// O simgeyi almak, uc dosya turunu birbirinden ayirt EDILEMEZ yapiyordu -
    /// yani "gercek simge" istegi, gercekte simgesizlik uretiyordu.
    ///
    /// Bu yuzden gelen simge, kabukta KESINLIKLE kayitli olmayan uydurma bir
    /// uzantinin simgesiyle piksel piksel karsilastiriliyor. Ayniysa kabukta
    /// kayit YOK demektir; null donulur ve cizilmis yedege dusulur.
    /// </summary>
    internal static Bitmap? Dosya(string uzanti)
    {
        Bitmap? simge = Getir("ornek" + uzanti, NormalDosya);
        if (simge is null)
        {
            return null;
        }

        if (KabuktaKayitliDegil(simge))
        {
            simge.Dispose();
            return null;
        }

        return simge;
    }

    private static bool KabuktaKayitliDegil(Bitmap simge)
    {
        if (!_bilinmeyenOlculdu)
        {
            _bilinmeyenOlculdu = true;
            using Bitmap? bilinmeyen = Getir("ornek.bu-uzanti-hicbir-yerde-kayitli-degil", NormalDosya);
            _bilinmeyeninSimgesi = bilinmeyen is null ? null : Pikseller(bilinmeyen);
        }

        if (_bilinmeyeninSimgesi is null)
        {
            return false;   // karsilastiracak sey yok; geleni oldugu gibi kabul et
        }

        byte[]? bunun = Pikseller(simge);
        return bunun is not null && bunun.AsSpan().SequenceEqual(_bilinmeyeninSimgesi);
    }

    private static byte[]? Pikseller(Bitmap resim)
    {
        BitmapData? veri = null;
        try
        {
            veri = resim.LockBits(
                new Rectangle(0, 0, resim.Width, resim.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            int bayt = Math.Abs(veri.Stride) * resim.Height;
            var tampon = new byte[bayt];
            Marshal.Copy(veri.Scan0, tampon, 0, bayt);
            return tampon;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        finally
        {
            if (veri is not null)
            {
                resim.UnlockBits(veri);
            }
        }
    }

    /// <summary>Kabugun klasor simgesi.</summary>
    internal static Bitmap? Klasor() => Getir("klasor", Dizin);

    private static Bitmap? Getir(string sahteYol, uint oznitelik)
    {
        var bilgi = default(SHFILEINFOW);
        IntPtr sonuc;

        try
        {
            sonuc = SHGetFileInfoW(
                sahteYol,
                oznitelik,
                ref bilgi,
                (uint)Marshal.SizeOf<SHFILEINFOW>(),
                SimgeAl | KucukSimge | OznitelikleriKullan);
        }
        catch (DllNotFoundException)
        {
            return null;    // kabuk yok: cizilmis yedege dusulur
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }

        if (sonuc == IntPtr.Zero || bilgi.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using Icon simge = Icon.FromHandle(bilgi.hIcon);
            return simge.ToBitmap();
        }
        catch (ArgumentException)
        {
            return null;
        }
        finally
        {
            // Tutamak BIZE ait; birakilmazsa her simgede bir GDI nesnesi sizar.
            DestroyIcon(bilgi.hIcon);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFOW
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfoW(
        string pszPath, uint dwFileAttributes, ref SHFILEINFOW psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
