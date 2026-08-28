using System;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// AGAC ODAKTAYKEN GEZINME TUSLARI - islem kisayollarindan (F2, Delete…)
/// ayri: bunlar hicbir sey degistirmez, yalnizca gezdirir ve acar.
///
/// NEDEN AYRI DOSYA: islem kisayollari <c>AgacIslemleri</c> listesinden
/// URETILIYOR; gezinme tuslarinin orada isi yok (menude "Enter: aç" diye bir
/// oge olmayacak). Ikisini ayni yere koymak, birini silmek isteyene otekini
/// de okutur (CLAUDE.md 1b).
///
/// NEDEN GEREKLI - OLCULDU: dosya acmanin TEK yolu cift tiklamaydi; klavyeyle
/// dosya acilamiyordu. Ust klasore cikmanin da kisayolu yoktu.
/// </summary>
internal static class AgacTuslari
{
    /// <summary>
    /// Tusu isler. Doner: is gorulduyse true (cagiran tusu yutar).
    /// </summary>
    /// <param name="ac">Secili dosyayi acar.</param>
    /// <param name="bildir">Durum cubuguna yazar.</param>
    internal static bool Isle(
        Keys tus, SecimliAgac agac, Action<DosyaOgesi> ac, Action<string> bildir)
    {
        ArgumentNullException.ThrowIfNull(agac);
        ArgumentNullException.ThrowIfNull(ac);
        ArgumentNullException.ThrowIfNull(bildir);

        return tus switch
        {
            Keys.Enter => Acmayi_Dene(agac, ac, bildir),
            Keys.Back => UsteCik(agac, bildir),
            _ => false,
        };
    }

    /// <summary>
    /// Enter: dosyaysa ACAR, klasorse dali acip kapatir.
    ///
    /// Klasorde "hicbir sey yapmamak" yerine ac/kapa secildi: kullanicinin
    /// Enter'dan bekledigi sey "bunu göster"; sag/sol oklarla ayni is.
    /// </summary>
    private static bool Acmayi_Dene(SecimliAgac agac, Action<DosyaOgesi> ac, Action<string> bildir)
    {
        TreeNode? dugum = agac.SelectedNode;
        if (dugum is null)
        {
            bildir("Önce bir dosya seçin.");
            return true;
        }

        switch (AgacDoldurucu.Etiket(dugum))
        {
            case DosyaOgesi dosya:
                ac(dosya);
                return true;

            case KlasorOgesi:
                if (dugum.IsExpanded)
                {
                    dugum.Collapse();
                }
                else
                {
                    dugum.Expand();
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Backspace: bir ust klasore cikar ve orayi secer.
    ///
    /// KOKTE SESSIZ KALINMAZ: "en ustesin" denir, yoksa tusun calismadigi
    /// sanilir (CLAUDE.md 3).
    /// </summary>
    private static bool UsteCik(SecimliAgac agac, Action<string> bildir)
    {
        TreeNode? ust = agac.SelectedNode?.Parent;
        if (ust is null)
        {
            bildir("Zaten en üst klasördesiniz.");
            return true;
        }

        agac.YalnizSec(ust);
        ust.EnsureVisible();
        return true;
    }
}
