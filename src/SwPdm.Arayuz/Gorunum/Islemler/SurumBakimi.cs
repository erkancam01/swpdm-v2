using System;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// VERSIYON SATIRININ BAKIMI - F2 notu duzeltir, Delete versiyonu siler
/// (Erkan, 31.08.2026: "versiyon silme ve not düzenlemeyi ekle").
///
/// SurumeDonusu ile ayni kalip: IAgacIslemi DEGIL, cunku hedefi agactaki
/// secim degil PANELDEKI VERSIYON SATIRIDIR. AnaForm yalnizca tusu iletir;
/// hangi tus ne yapar, hangi metin cikar, neye onay sorulur - hepsi burada
/// (CLAUDE.md 1b: kaldirmak = bu dosyayi sil + AnaForm'da bir blogu sil).
///
/// AYNI TUSLAR PANELDE ZATEN VAR ve BASKA SEY YAPIYOR: F2/Delete normalde
/// satirin DOSYASINA gider (ReferansMenusu). Arsiv kopyasinda o yol zaten
/// kapali ("dosya işlemleri uygulanmaz"), ama sira yine de onemli - bu
/// blok AnaForm'da ondan ONCE denenir, yoksa versiyon satirinda F2 hicbir
/// sey yapmayan bir tus olarak kalirdi.
/// </summary>
internal static class SurumBakimi
{
    /// <summary>
    /// Tusu isler. Doner: tus kullanildi mi (satir versiyon degilse ya da
    /// tus baskaysa false - o zaman olagan yolundan devam eder).
    /// </summary>
    internal static bool Calistir(
        IWin32Window sahip,
        Keys tuslar,
        SurumKaydi? kayit,
        SecimBaglami secim,
        Action<string?> tazele,
        Action<string> bildir)
    {
        ArgumentNullException.ThrowIfNull(secim);
        ArgumentNullException.ThrowIfNull(tazele);
        ArgumentNullException.ThrowIfNull(bildir);

        if (kayit is null || (tuslar != Keys.F2 && tuslar != Keys.Delete))
        {
            return false;
        }

        if (secim.TekOge is not DosyaOgesi dosya || secim.Kok is not string kok)
        {
            bildir("Önce ağaçta tek bir dosya seçin.");
            return true;
        }

        return tuslar == Keys.F2
            ? NotuDuzelt(sahip, kayit, kok, dosya, tazele, bildir)
            : Sil(sahip, kayit, kok, dosya, tazele, bildir);
    }

    private static bool NotuDuzelt(
        IWin32Window sahip, SurumKaydi kayit, string kok, DosyaOgesi dosya,
        Action<string?> tazele, Action<string> bildir)
    {
        string? yeni = SurumNotuKutusu.Sor(
            sahip,
            $"v{kayit.No} notu",
            $"\"{dosya.Ad}\" v{kayit.No} için not (boş bırakılabilir):",
            baslangic: kayit.Not);

        if (yeni is null)
        {
            bildir("Not değiştirme iptal edildi.");
            return true;
        }

        IslemRaporu rapor = Surumler.NotDegistir(kok, dosya.Yol, kayit.No, yeni);

        if (!rapor.Oldu)
        {
            MessageBox.Show(
                sahip, rapor.Sebebi,
                "Not değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            bildir("Not değiştirilemedi — v" + kayit.No);
            return true;
        }

        bildir(
            yeni.Length == 0
                ? $"v{kayit.No} notu silindi."
                : $"v{kayit.No} notu: {yeni}");
        tazele(dosya.Yol);
        return true;
    }

    private static bool Sil(
        IWin32Window sahip, SurumKaydi kayit, string kok, DosyaOgesi dosya,
        Action<string?> tazele, Action<string> bildir)
    {
        // GERI ALINAMAYAN ISLEM = ONAY KUTUSU (CLAUDE.md 6'daki iki
        // sebepten biri). Cumle iki seyi ACIKCA soyluyor: kalici, ve
        // CANLI DOSYAYA dokunulmuyor - kullanicinin en cok korktugu sey bu.
        bool onay = OnayKutusu.Sor(
            sahip,
            "Versiyonu sil",
            $"\"{dosya.Ad}\" v{kayit.No} arşivden KALICI olarak silinecek.\n\n"
            + "Çöp kutusuna gitmez, geri alınamaz.\n"
            + "Dosyanın kendisine dokunulmaz — yalnız bu versiyon kaydı silinir.",
            tehlikeli: true);

        if (!onay)
        {
            bildir("Versiyon silme iptal edildi.");
            return true;
        }

        IslemRaporu rapor = Surumler.Sil(kok, dosya.Yol, kayit.No);

        if (!rapor.Oldu)
        {
            MessageBox.Show(
                sahip, rapor.Sebebi,
                "Versiyon silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            bildir("Versiyon silinemedi — v" + kayit.No);
            return true;
        }

        bildir($"v{kayit.No} silindi: {dosya.Ad}");
        tazele(dosya.Yol);
        return true;
    }
}
