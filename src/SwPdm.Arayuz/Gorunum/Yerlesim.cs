using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// PENCERENIN HATIRLANAN YERLESIMI: boyut, iki bolucu ve son tur suzgeci.
/// Kararin tamami burada (CLAUDE.md 1b) - kaldirmak istenirse bu dosya
/// silinir ve <c>AnaForm</c>'daki iki satir cikar.
///
/// NEDEN VAR: uygulama her acilista 572x880 aciliyor, boluculer 320/282'ye
/// donuyor ve suzgec "Tümü"ye siniyordu. Kullanicinin her oturumda ayni uc
/// ayari yeniden yapmasi gerekiyordu.
///
/// KONUM BILEREK SAKLANMIYOR: iki ekranli bir makinede kaydedilen koordinat,
/// ikinci ekran cikarilinca pencereyi GORUNMEZ bir yere acardi. Boyutta bu
/// risk yok. Ayni sebeple boyut da ekrana SIGDIRILIYOR.
/// </summary>
internal static class Yerlesim
{
    /// <summary>Bolucu yerleri hic saklanmadiysa kullanilacak degerler.</summary>
    private const int VarsayilanDikey = 320;
    private const int VarsayilanAlt = 282;

    /// <summary>Kaydedilmis yerlesimi uygular; yoksa varsayilanlar kalir.</summary>
    internal static void Uygula(
        Form pencere, SplitContainer dikey, SplitContainer alt,
        SuzgecSeridi suzgecler, Ayarlar ayarlar)
    {
        ArgumentNullException.ThrowIfNull(pencere);
        ArgumentNullException.ThrowIfNull(ayarlar);
        ArgumentNullException.ThrowIfNull(suzgecler);

        if (Boyut(ayarlar.PencereBoyutu) is Size boyut)
        {
            Rectangle ekran = Screen.FromControl(pencere).WorkingArea;
            pencere.ClientSize = new Size(
                Math.Min(boyut.Width, ekran.Width),
                Math.Min(boyut.Height, ekran.Height));
        }

        // BOLUCULER PENCERE BOYUTUNDAN SONRA: bolucunun sinirlari denetimin
        // o anki olcusune bagli; once boyutu koymazsak deger kirpilir.
        BoleniAyarla(dikey, ayarlar.DikeyBolen ?? VarsayilanDikey);
        BoleniAyarla(alt, ayarlar.AltBolen ?? VarsayilanAlt);

        suzgecler.Kur(ayarlar.Suzgec);
    }

    /// <summary>Su anki yerlesimi ayarlara yazar (diske yazmaz).</summary>
    internal static void Sakla(
        Form pencere, SplitContainer dikey, SplitContainer alt,
        SuzgecSeridi suzgecler, Ayarlar ayarlar)
    {
        ArgumentNullException.ThrowIfNull(pencere);
        ArgumentNullException.ThrowIfNull(dikey);
        ArgumentNullException.ThrowIfNull(alt);
        ArgumentNullException.ThrowIfNull(suzgecler);
        ArgumentNullException.ThrowIfNull(ayarlar);

        // SIMGE DURUMUNDAKI ya da TAM EKRAN pencerenin ClientSize'i o anki
        // gorunumu anlatir, kullanicinin sectigi boyutu DEGIL; oyle bir
        // degeri saklamak pencereyi bir dahaki acilista kucultur.
        if (pencere.WindowState == FormWindowState.Normal)
        {
            ayarlar.PencereBoyutu = pencere.ClientSize.Width.ToString(CultureInfo.InvariantCulture)
                + "x" + pencere.ClientSize.Height.ToString(CultureInfo.InvariantCulture);
        }

        ayarlar.DikeyBolen = dikey.SplitterDistance;
        ayarlar.AltBolen = alt.SplitterDistance;
        ayarlar.Suzgec = suzgecler.SeciliTur is DosyaTuru tur ? DosyaTurleri.Adi(tur) : null;
    }

    /// <summary>"572x880" -> Size. Bozuksa null; bozuk ayar varsayilana duser.</summary>
    private static Size? Boyut(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin))
        {
            return null;
        }

        string[] parcalar = metin.Split('x');
        if (parcalar.Length != 2
            || !int.TryParse(parcalar[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int g)
            || !int.TryParse(parcalar[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
            || g < 200 || y < 200)
        {
            return null;
        }

        return new Size(g, y);
    }

    /// <summary>
    /// Bolucuyu KIRPARAK ayarlar.
    ///
    /// OLCULMUS TUZAK: sinirlarin disinda bir SplitterDistance ISTISNA atiyor
    /// ve uygulama acilista oluyordu. Deger her zaman
    /// [Panel1MinSize, uzunluk - SplitterWidth - Panel2MinSize] araligina
    /// kirpiliyor; aralik ters donmusse (pencere cok kucuk) hic dokunulmuyor.
    /// </summary>
    private static void BoleniAyarla(SplitContainer bolen, int hedef)
    {
        ArgumentNullException.ThrowIfNull(bolen);

        int uzunluk = bolen.Orientation == Orientation.Horizontal ? bolen.Height : bolen.Width;
        int enBuyuk = uzunluk - bolen.SplitterWidth - bolen.Panel2MinSize;
        int enKucuk = bolen.Panel1MinSize;

        if (enBuyuk < enKucuk)
        {
            return;
        }

        bolen.SplitterDistance = Math.Clamp(hedef, enKucuk, enBuyuk);
    }
}
