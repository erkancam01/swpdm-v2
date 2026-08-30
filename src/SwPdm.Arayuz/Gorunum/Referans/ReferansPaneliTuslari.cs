using System;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// REFERANS PANELI ODAKTAYKEN KLAVYE. Panelin tus davranisinin TAMAMI burada
/// (CLAUDE.md 1b): bir tusu kaldirmak = buradan bir satir silmek.
///
/// NEDEN GEREKLI - OLCULDU: panelin tek olayi CIFT TIKTI. Klavyeyle
/// gidilemiyor, satirdaki yol hicbir yere kopyalanamiyordu (depoda
/// "Clipboard" cagrisi hic yoktu) ve panel odaktayken hicbir kisayol
/// calismiyordu - AnaForm butun kisayollari "_agac.Focused" sartina
/// bagliyor.
///
/// CTRL+C BURADA "YOLU KOPYALA", AGACTA "DOSYAYI KOPYALA" - ve bu karisiklik
/// degil: iki ayri odak, iki ayri secim. Panelde secili olan sey bir DOSYA
/// degil bir REFERANS satiri; onu "kes/yapistir" panosuna koymak anlamsiz
/// olurdu. Kopyalanan metin durum cubugunda GORUNUYOR ki kullanici neyin
/// kopyalandigini bilsin (CLAUDE.md 3).
/// </summary>
internal static class ReferansPaneliTuslari
{
    /// <summary>
    /// Bu tusu panel SAHIPLENIYOR mu.
    ///
    /// NEDEN GEREKLI: <c>Ctrl+C</c> burada "yolu kopyala"dir, oysa
    /// <c>KopyalaIslemi</c> de Ctrl+C kullaniyor. Panelin sag tik menusu
    /// bunu sorup o ogenin kisayolunu YAZMIYOR - "Kopyala  Ctrl+C" yazmak
    /// yalan olurdu (CLAUDE.md 3). Liste TEK YERDE: asagidaki switch ile
    /// bu metot ayni tuslari sayar; biri degisirse oteki de degismeli.
    /// </summary>
    internal static bool Sahiplenir(Keys tus)
        => tus is Keys.Enter or (Keys.Control | Keys.C);

    /// <summary>
    /// Tusu isler. Doner: is gorulduyse true (cagiran tusu yutar).
    /// </summary>
    /// <param name="git">Verilen hedefe gider; null hedefte sebebini yazar.</param>
    /// <param name="bildir">Durum cubuguna yazar.</param>
    internal static bool Isle(
        Keys tus, ReferansListesi liste, Action<string?> git, Action<string> bildir)
    {
        ArgumentNullException.ThrowIfNull(liste);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(bildir);

        switch (tus)
        {
            case Keys.Enter:
                // Cift tikin klavye karsiligi. Hedefi olmayan satirda "git"
                // zaten sebebini yaziyor; burada sessiz kalinmiyor.
                git(liste.SeciliHedef);
                return true;

            case Keys.Control | Keys.C:
                Kopyala(liste, bildir);
                return true;

            default:
                return false;
        }
    }

    private static void Kopyala(ReferansListesi liste, Action<string> bildir)
    {
        if (liste.SeciliMetin is not string metin || metin.Length == 0)
        {
            bildir("Kopyalanacak satır seçili değil.");
            return;
        }

        try
        {
            Clipboard.SetText(metin);
            bildir("Panoya kopyalandı: " + metin);
        }
        catch (Exception hata) when (hata is System.Runtime.InteropServices.ExternalException
                                         or ArgumentException)
        {
            // Pano BASKA BIR SUREC tarafindan kilitlenmis olabilir. Sessizce
            // gecmek, kullanicinin kopyaladigini SANIP yanlis bir yol
            // yapistirmasina yol acardi (CLAUDE.md 3).
            bildir("Panoya kopyalanamadı: " + hata.Message);
        }
    }
}
