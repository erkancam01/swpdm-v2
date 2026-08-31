using System;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// "BU VERSIYONA DON" AKISI - VERSIYONLAR sekmesinde secili satira Enter.
///
/// IAgacIslemi DEGIL: islem listesindekiler agactaki secimle calisir; bunun
/// hedefi ise paneldeki VERSIYON SATIRIDIR (hangi dosya + hangi numara).
/// AnaForm yalnizca baglar; akisin karari burada (CLAUDE.md 1b).
///
/// GUVENLIK (CLAUDE.md 1a) cekirdekte: <see cref="Surumler.Don"/> once
/// bugunku hali otomatik arsivler, SOLIDWORKS'te acik dosyaya dokunmaz.
/// Buradaki onay kutusu iki seyi ACIKCA soyler: hicbir icerik kaybolmaz,
/// ve bu parcayi kullanan BUTUN montajlar donulen icerigi gorur (ayni ad =
/// tek icerik - Erkan'in sectigi model).
/// </summary>
internal static class SurumeDonusu
{
    /// <summary>
    /// Akisi kosturur. Doner: tus kullanildi mi (satir versiyon degilse
    /// false doner ki Enter'in olagan anlami bozulmasin).
    /// </summary>
    internal static bool Calistir(
        IWin32Window sahip,
        SurumKaydi? kayit,
        SecimBaglami secim,
        Action belgeyiBirak,
        Action<string?> tazele,
        Action<string> bildir)
    {
        ArgumentNullException.ThrowIfNull(secim);
        ArgumentNullException.ThrowIfNull(belgeyiBirak);
        ArgumentNullException.ThrowIfNull(tazele);
        ArgumentNullException.ThrowIfNull(bildir);

        if (kayit is null)
        {
            return false;   // secili satir bir versiyon degil (aciklama satiri vb.)
        }

        if (secim.TekOge is not DosyaOgesi dosya || secim.Kok is not string kok)
        {
            bildir("Önce ağaçta tek bir dosya seçin.");
            return true;
        }

        bool onay = OnayKutusu.Sor(
            sahip,
            "Versiyona dön",
            $"\"{dosya.Ad}\" v{kayit.No} içeriğine dönecek.\n\n"
            + "Bugünkü hâl önce otomatik arşivlenir — hiçbir içerik kaybolmaz.\n"
            + "Bu dosyayı kullanan BÜTÜN montajlar dönülen içeriği görür.",
            tehlikeli: true);

        if (!onay)
        {
            bildir("Versiyona dönme iptal edildi.");
            return true;
        }

        // 3B onizleme belgeyi kilitli tutabilir (eDrawings); islemden once
        // birakilir - AgacMenusu.IslemOncesi'nin buradaki karsiligi.
        belgeyiBirak();

        IslemRaporu rapor = Surumler.Don(kok, dosya.Yol, kayit.No);

        if (!rapor.Oldu)
        {
            // CLAUDE.md 6: gorulmeden gecilemeyecek bir HATA - kutu mesru.
            MessageBox.Show(
                sahip, rapor.Sebebi,
                "Versiyona dönülemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            bildir("Versiyona dönülemedi — " + dosya.Ad);
            return true;
        }

        // Cekirdegin notlari (kayit-boyut farki, "zaten arsivli") GIZLENMEZ -
        // ozellikle kayit farki, kok sebep avinda Erkan'in gorecegi tek iz.
        bildir(
            $"{dosya.Ad} → v{kayit.No} içeriğine dönüldü."
            + (rapor.Sebep is { Length: > 0 } not_ ? not_ : " (önceki hâl arşivde)"));
        tazele(dosya.Yol);
        return true;
    }
}
