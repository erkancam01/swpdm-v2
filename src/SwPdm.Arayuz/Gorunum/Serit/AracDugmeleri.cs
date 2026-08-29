using System;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// ARAC CUBUGU DUGMELERININ DURUM KURALLARI - cop dugmesinin yazisi/ipucu
/// ve geri-al dugmesinin ipucu.
///
/// NEDEN AYRI DOSYA (29.08.2026 §1b denetimi): bu kurallar AnaForm'da
/// yasiyordu - "kayit okunamadiysa sayi yazilmaz, (?) yazilir" gibi bir
/// DURUSTLUK karari (CLAUDE.md 3) baglama dosyasinin icine gomulmustu.
/// AnaForm yalnizca baglar; karar tasiyan her sey kendi dosyasinda durur.
/// </summary>
internal static class AracDugmeleri
{
    /// <summary>Cop dugmesinin yazisini ve durumunu tazeler.</summary>
    internal static void CopuTazele(ToolStripButton dugme, string? kok, Ayarlar ayarlar)
    {
        ArgumentNullException.ThrowIfNull(dugme);
        ArgumentNullException.ThrowIfNull(ayarlar);

        if (kok is null)
        {
            dugme.Enabled = false;
            dugme.Text = "Çöp";
            dugme.ToolTipText = "Çöp kutusu — önce bir klasör açın";
            return;
        }

        CopDurumu durum = Cop.Oku(Cop.Yolu(kok, ayarlar.CopUstKlasoru));
        dugme.Enabled = true;

        // KAYIT OKUNAMADIYSA SAYI YAZILMAZ. "Çöp kutusu" yazip gecmek,
        // okunamayan bir kutuyu BOS gibi gosterirdi (CLAUDE.md 3).
        if (!durum.Guvenilir)
        {
            dugme.Text = "Çöp kutusu (?)";
            dugme.ToolTipText = durum.Okunamadi;
            return;
        }

        int adet = durum.Ogeler.Count;
        dugme.Text = adet == 0 ? "Çöp kutusu" : $"Çöp kutusu ({adet})";
        dugme.ToolTipText = "Silinenleri gör ve geri yükle";
    }

    /// <summary>
    /// Geri-al dugmesini tazeler. Ipucu NEYIN geri alinacagini yazar -
    /// kullanici neye bastigini bilmeli (CLAUDE.md 3).
    /// </summary>
    internal static void GeriAliTazele(ToolStripButton dugme)
    {
        ArgumentNullException.ThrowIfNull(dugme);

        dugme.Enabled = GeriAlDefteri.Var;
        dugme.ToolTipText = GeriAlDefteri.Sonraki is string ad
            ? $"Geri al: {ad}  (Ctrl+Z)"
            : "Geri al — geri alınacak bir işlem yok";
    }
}
