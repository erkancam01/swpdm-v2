using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// AGACI KAPAT - butun dallari kapatir ve koke doner.
///
/// Erkan: "bu dallanan dosya agacinin dallanmalarini kapatsin, bazen geri
/// gelmek zulum olabiliyor." Derin bir dalda kaybolunca tek tek kapatmak
/// yerine bir hamlede basa donmek icin.
///
/// KOK ACIK KALIR: her seyi kapatip tek satirlik bir agac birakmak,
/// kullaniciya "klasor bosaldi" hissi verirdi.
/// </summary>
internal sealed class AgaciKapatIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Ağacı kapat";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.K;

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
        baglam.AgaciKapat();
        baglam.Bildir("Ağaç kapatıldı.");
    }
}
