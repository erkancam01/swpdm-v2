using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YENI KLASOR. Bu islemin butun karari burada: nereye acilir, adi ne olur,
/// cakisirsa ne yapar, hata olursa ne yazar (CLAUDE.md 1b).
/// </summary>
internal sealed class YeniKlasorIslemi : IAgacIslemi
{
    private const string VarsayilanAd = "Yeni klasör";

    /// <inheritdoc/>
    public string Ad => "Yeni klasör";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.N;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (secim.AramaKipinde)
        {
            // Arama sonucu duz bir listedir; "burasi" diye bir yer yok.
            nedenOlmaz = "Arama sonucunda klasör açılamaz — önce aramayı temizleyin.";
            return false;
        }

        if (secim.EtkinKlasor is null)
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
        string? ust = baglam.Secim.EtkinKlasor;
        if (ust is null)
        {
            return;
        }

        // Gezgin gibi: cakisirsa "Yeni klasör (2)". Kullaniciya soru sormaz.
        string ad = DosyaIslemleri.BosAdBul(ust, VarsayilanAd);
        IslemRaporu rapor = DosyaIslemleri.KlasorOlustur(ust, ad);

        if (!rapor.Oldu)
        {
            // CLAUDE.md 3: sebep EKRANDA, yalnizca gunlukte degil.
            MessageBox.Show(
                baglam.Sahip,
                rapor.Sebep ?? "Bilinmeyen sebep.",
                "Klasör açılamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            baglam.Bildir("Klasör açılamadı — " + ad);
            return;
        }

        baglam.Tazele(rapor.YeniYol);
        baglam.Bildir("Klasör açıldı: " + ad);
    }
}
