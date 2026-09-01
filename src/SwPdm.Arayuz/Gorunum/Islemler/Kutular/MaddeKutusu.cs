using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// MADDELI METIN - "N öğe …:" ustune "  • satır" listesi.
///
/// NEDEN VAR - OLCULDU (29.08.2026 §1b denetimi): bu kalip YEDI dosyada
/// ON IKI kez elle kurulmustu (StringBuilder + dongu + kirpma) ve coktan
/// ayrismisti: kirpma esigi bir yerde 8, bir yerde 10, ayni dosyada 12;
/// madde imi bir yerde "•", bir yerde "!". CLAUDE.md 8'in tarif ettigi
/// hastaliğin ta kendisi. Kalibin TAMAMI artik burada; esik ve im tek yerde.
///
/// ORTAK ARAC, ozellik degil (CLAUDE.md 1b kural 3): silinecek bir sey
/// degil, kutu kuran herkesin kullandigi tek kopya.
/// </summary>
internal static class MaddeKutusu
{
    /// <summary>
    /// Bir listeden en fazla kac madde yazilir; kalani "… ve N tane daha"
    /// olur. TEK sabit - once 8/10/12 karisikti ve hicbiri gerekceliydi.
    /// </summary>
    internal const int EnFazlaMadde = 10;

    /// <summary>
    /// Maddeli blok kurar: istege bagli bas cumle + kirpilmis madde listesi.
    /// MessageBox CAGIRMAZ - cagiran bloklari istedigi gibi birlestirir
    /// (tasima kutusu gibi bilesik kutular var).
    /// </summary>
    internal static string Metin(string? bas, IReadOnlyList<string> maddeler, string im = "•")
    {
        var yazi = new StringBuilder();
        if (!string.IsNullOrEmpty(bas))
        {
            yazi.AppendLine(bas);
        }

        int yazilan = 0;
        foreach (string madde in maddeler)
        {
            if (yazilan == EnFazlaMadde)
            {
                yazi.AppendLine($"  … ve {maddeler.Count - EnFazlaMadde} tane daha");
                break;
            }

            yazi.AppendLine($"  {im} {madde}");
            yazilan++;
        }

        return yazi.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// KULLANICININ KENDI ISTEDIGI liste - uyari DEGIL, bilgi.
    ///
    /// Ayri metot cunku ikon YALAN SOYLEMEMELI (CLAUDE.md 3/6): "Listeyi
    /// göster" dedigi icin acilan bir kutuda uyari ucgeni, ortada bir sorun
    /// varmis gibi okunur. Kirpma esigi ve metin kurali Goster ile AYNI
    /// yerden geliyor - ikinci kopya yok (CLAUDE.md 8).
    /// </summary>
    internal static void Listele(
        IWin32Window sahip, string baslik, string bas, IReadOnlyList<string> maddeler)
        => MessageBox.Show(
            sahip, Metin(bas, maddeler), baslik,
            MessageBoxButtons.OK, MessageBoxIcon.None);

    /// <summary>
    /// En sik hal: baslik + bas cumle + maddeler, uyari ikonlu kutu.
    /// </summary>
    internal static void Goster(
        IWin32Window sahip, string baslik, string bas, IReadOnlyList<string> maddeler)
        => MessageBox.Show(
            sahip, Metin(bas, maddeler), baslik,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
