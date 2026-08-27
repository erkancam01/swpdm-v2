using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YENILE - agaci diskten bastan okur.
///
/// Gerekcesi somut: dosyalar ortak ag surucusunde duruyor ve baskasi bir
/// klasoru degistirdiginde agac bunu KENDILIGINDEN gormuyor. Yenile, gordugun
/// seyin gercekten diskte olani oldugunu garanti eder.
///
/// Acik dallar ve secim KORUNUR - tazelemek, kullanicinin yerini kaybetmesi
/// demek degildir.
/// </summary>
internal sealed class YenileIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Yenile";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.F5;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (secim.Kok is null)
        {
            nedenOlmaz = "Önce bir klasör açın.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        baglam.Tazele(null);
        baglam.Bildir("Yenilendi.");
    }
}
