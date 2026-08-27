using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Windows'un KENDI onizlemesini alir - Gezgin'in onizleme bolmesinde ne
/// gorunuyorsa o. SOLIDWORKS kurulu bir makinede parcanin/montajin gercek
/// goruntusu gelir, cunku onizlemeyi ureten SOLIDWORKS'un kendi saglayicisi.
///
/// Erkan: "eski uygulamada Windows'taki onizleme araci ne goruyorsa biz de
/// onu goruyorduk." v1'in yolu buydu.
///
/// ================== OLCULMUS TUZAK (CLAUDE.md 4) ==================
/// Kabuk onizleme saglayicilari STA ISTIYOR. ThreadPool (MTA) icinden
/// cagirinca E_FAIL (0x80004005) doner ve sebep hicbir yerde yazmaz.
/// Bu yuzden burada apartman durumu ONDEN denetleniyor: yanlis is
/// parcacigindan cagrilirsa sessizce bos donmek yerine SEBEBINI soyluyor.
/// ==================================================================
///
/// SIIGBF_THUMBNAILONLY bilerek: kabuk onizleme uretemiyorsa SIMGE dondurur
/// ve bir simgeyi onizleme diye gostermek yalan olur (CLAUDE.md 3).
/// </summary>
internal static class KabukOnizleme
{
    private const int OlcegeSigdir = 0x00000000;   // SIIGBF_RESIZETOFIT
    private const int YalnizcaKucukResim = 0x00000008;   // SIIGBF_THUMBNAILONLY

    private static readonly Guid ResimUreticisi = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    /// <summary>
    /// Onizlemeyi dondurur. Yoksa null ve <paramref name="sebep"/> dolar.
    /// </summary>
    internal static Bitmap? Al(string yol, Size boyut, out string? sebep)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            // Bunu sessizce "onizleme yok" diye gostermek, aylarca surecek bir
            // yanlis teshise yol acardi.
            sebep = "Kabuk önizlemesi STA iş parçacığı istiyor; buradan çağrılamaz.";
            return null;
        }

        IShellItemImageFactory? uretici = null;
        IntPtr tutamak = IntPtr.Zero;

        try
        {
            Guid arayuz = ResimUreticisi;
            int sonuc = SHCreateItemFromParsingName(yol, IntPtr.Zero, ref arayuz, out uretici);
            if (sonuc != 0 || uretici is null)
            {
                sebep = $"Kabuk öğesi açılamadı (0x{sonuc:X8}).";
                return null;
            }

            sonuc = uretici.GetImage(
                new BOYUT { cx = boyut.Width, cy = boyut.Height },
                OlcegeSigdir | YalnizcaKucukResim,
                out tutamak);

            if (sonuc != 0 || tutamak == IntPtr.Zero)
            {
                // WTS_E_FAILEDEXTRACTION ve akrabalari: bu tur icin kayitli bir
                // onizleme saglayicisi yok demektir. Bilgi, hata degil.
                sebep = null;
                return null;
            }

            sebep = null;
            Bitmap? resim = BitEslemeyeCevir(tutamak);

            // OLCULDU: Wine'in kabugu S_OK donduruyor ama bit eslem TAMAMEN
            // SAYDAM - yani gercekte onizleme yok. Bunu "var" saymak iki kez
            // yanlis olurdu: bos bir kutu onizleme diye gosterilir VE dosyanin
            // icindeki gomulu onizlemeye hic gecilmez.
            if (resim is not null && TumuylaSaydamMi(resim))
            {
                resim.Dispose();
                return null;
            }

            return resim;
        }
        catch (Exception hata) when (hata is COMException or ArgumentException
                                         or ExternalException or NotSupportedException)
        {
            sebep = "Önizleme alınamadı: " + hata.Message;
            return null;
        }
        finally
        {
            if (tutamak != IntPtr.Zero)
            {
                DeleteObject(tutamak);   // GDI nesnesi BIZE ait; birakilmazsa sizar
            }

            if (uretici is not null)
            {
                Marshal.ReleaseComObject(uretici);
            }
        }
    }

    /// <summary>
    /// Kabugun verdigi HBITMAP'i yonetilen bir bit eslemeye cevirir.
    ///
    /// Image.FromHbitmap ALFA KANALINI YOK SAYIYOR. Kabuk ise kucuk resimleri
    /// 32 bit ONCARPIMLI ALFA ile donduruyor; FromHbitmap ile alinan resimde
    /// saydam kisimlar cop piksele donuyor. Wine altinda bu gri bir gradyan
    /// olarak goruldu - Windows'ta genelde siyah kose olarak cikar.
    ///
    /// Bu yuzden 32 bitlik bit eslemeler DOGRUDAN, oncarpimli bicimiyle
    /// kopyalanıyor; digerleri icin FromHbitmap yeterli.
    /// </summary>
    private static Bitmap? BitEslemeyeCevir(IntPtr tutamak)
    {
        var bilgi = default(BITESLEME);
        if (GetObject(tutamak, Marshal.SizeOf<BITESLEME>(), ref bilgi) == 0)
        {
            return null;
        }

        if (bilgi.bmBitsPixel != 32 || bilgi.bmBits == IntPtr.Zero)
        {
            return Image.FromHbitmap(tutamak);
        }

        // Ust satirdan basliyor: bmWidthBytes pozitif kabul ediliyor.
        using var kaynak = new Bitmap(
            bilgi.bmWidth, bilgi.bmHeight, bilgi.bmWidthBytes,
            PixelFormat.Format32bppPArgb, bilgi.bmBits);

        // Kaynak, kabugun belleğine bakiyor; tutamak birakildiktan sonra
        // gecersiz olur. Kendi kopyamizi aliyoruz.
        return new Bitmap(kaynak);
    }

    /// <summary>
    /// Resmin her pikseli saydam mi. Oyleyse gosterilecek bir sey yok demektir.
    /// Alfasi olmayan bicimlerde her zaman false doner.
    /// </summary>
    private static bool TumuylaSaydamMi(Bitmap resim)
    {
        if (resim.PixelFormat is not (PixelFormat.Format32bppArgb or PixelFormat.Format32bppPArgb))
        {
            return false;
        }

        BitmapData? veri = null;
        try
        {
            veri = resim.LockBits(
                new Rectangle(0, 0, resim.Width, resim.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                for (int y = 0; y < resim.Height; y++)
                {
                    byte* satir = (byte*)veri.Scan0 + (y * veri.Stride);
                    for (int x = 3; x < resim.Width * 4; x += 4)
                    {
                        if (satir[x] != 0)
                        {
                            return false;   // saydam olmayan piksel bulundu
                        }
                    }
                }
            }

            return true;
        }
        catch (Exception hata) when (hata is ArgumentException or InvalidOperationException)
        {
            return false;
        }
        finally
        {
            if (veri is not null)
            {
                resim.UnlockBits(veri);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITESLEME
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BOYUT
    {
        public int cx;
        public int cy;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(BOYUT boyut, int bayraklar, out IntPtr hbitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string yol, IntPtr baglam, ref Guid arayuz, out IShellItemImageFactory? sonuc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr nesne);

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetObject(IntPtr nesne, int boyut, ref BITESLEME hedef);
}
