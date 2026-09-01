using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// DOSYA ACMANIN TEK KAPISI - cift tiklaninca ne olacagi burada.
///
/// Hata metni de, kutu da burada: ozelligin butun karari kendi dosyasinda
/// (CLAUDE.md 1b). Cagiran yalnizca donen cumleyi durum cubuguna yazar.
/// </summary>
internal static class DosyaAcici
{
    /// <summary>
    /// YOLDAN acar: once diskten okur, sonra <see cref="Ac"/>. Ayni uc satir
    /// once SurumBolumu'nde, sonra referans panelinde gerekti - ucuncusunu
    /// yazmamak icin buraya alindi (CLAUDE.md 8).
    /// </summary>
    /// <returns>Durum cubuguna yazilacak cumle; yol okunamazsa SEBEBI.</returns>
    internal static string YoluAc(IWin32Window sahip, string? yol)
    {
        if (yol is null)
        {
            return "Bu satırda açılacak bir dosya yok.";
        }

        return KlasorTarayici.DosyayiOku(yol) is DosyaOgesi dosya
            ? Ac(sahip, dosya)
            : "Dosya okunamadı: " + yol;
    }

    /// <summary>
    /// Dosyayi Windows'un varsayilan uygulamasiyla acar - Gezgin'de cift
    /// tiklamakla ayni.
    ///
    /// CalismaKlasoru BILEREK verilmiyor: cocuk surec bir klasoru calisma
    /// klasoru yaparsa o klasor bir daha silinemez (CLAUDE.md 5'te SOLIDWORKS
    /// icin olculmus tuzagin ta kendisi) ve bu bir dosya yoneticisi icin
    /// dogrudan zarar olurdu.
    /// </summary>
    /// <returns>Durum cubuguna yazilacak cumle. CLAUDE.md 3: her istek bir
    /// YANIT alir - sessizce hicbir sey olmasi kullaniciya ikinci kez
    /// tiklatir.</returns>
    internal static string Ac(IWin32Window sahip, DosyaOgesi dosya)
    {
        try
        {
            using Process? surec = Process.Start(new ProcessStartInfo(dosya.Yol)
            {
                UseShellExecute = true,
            });

            return dosya.Ad + " açılıyor…";
        }
        catch (Exception hata) when (hata is Win32Exception or InvalidOperationException
                                         or FileNotFoundException or ObjectDisposedException)
        {
            string sebep = hata is Win32Exception
                ? hata.Message + "\n\nBu uzantı için kayıtlı bir uygulama olmayabilir."
                : hata.Message;

            // Durum cubugu KISA kalir - uzun hata metni cubugu tasiriyordu.
            // Ayrinti kutuda; sebep yine de EKRANDA (CLAUDE.md 3).
            MessageBox.Show(
                sahip,
                dosya.Yol + "\n\n" + sebep,
                "Dosya açılamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return "Açılamadı — " + dosya.Ad;
        }
    }
}
