using System;
using System.IO;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// KABUK ILETISIM KUTULARININ TEK KAPISI (klasor secme, dosya secme).
///
/// NEDEN VAR - CLAUDE.md 4, OLCULMUS TUZAK: kabuk kutulari surecin CALISMA
/// KLASORUNU kaydiriyor ve o klasor bir daha SILINEMIYOR. Bir dosya
/// yoneticisinde bu gercek bir hata: kullanicinin sildigi klasor
/// "kullanımda" diye direnir ve sebebi hicbir yerde yazmaz.
///
/// NEDEN ORTAK ARAC, OZELLIK DEGIL (CLAUDE.md 1b/8): ayni korumayi ucuncu
/// kez elle yazmak, birinde unutulmasi demekti - ve unutulani hicbir kapi
/// yakalamaz, cunku belirtisi cok sonra ve baska bir yerde cikiyor.
/// </summary>
internal static class KabukKutusu
{
    /// <summary>
    /// Kutuyu gosterir ve KAPANDIKTAN SONRA calisma klasorunu geri koyar.
    /// <see cref="CommonDialog"/> oldugu icin klasor ve dosya kutularinin
    /// ikisi de buradan geciyor.
    /// </summary>
    internal static DialogResult Goster(CommonDialog kutu, IWin32Window? sahip)
    {
        ArgumentNullException.ThrowIfNull(kutu);

        string onceki = Directory.GetCurrentDirectory();
        try
        {
            return sahip is null ? kutu.ShowDialog() : kutu.ShowDialog(sahip);
        }
        finally
        {
            GeriKoy(onceki);
        }
    }

    private static void GeriKoy(string klasor)
    {
        try
        {
            Directory.SetCurrentDirectory(klasor);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                         or DirectoryNotFoundException)
        {
            // Eski klasor artik yoksa yapacak bir sey yok; uygulamayi dusurmez.
        }
    }
}
