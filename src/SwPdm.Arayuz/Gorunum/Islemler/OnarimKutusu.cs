using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>Kullanicinin ad degisiminde verdigi karar.</summary>
internal enum OnarimKarari
{
    /// <summary>Hicbir sey yapilmasin.</summary>
    Vazgec,

    /// <summary>Adi degistir VE onu kullanan dosyalari onar.</summary>
    Onar,

    /// <summary>Onarilacak bir sey yok; yalnizca adi degistir.</summary>
    OnarmadanDegistir,
}

/// <summary>
/// AD DEGISIMINDE NE OLACAGINI SORAR - metnin tamami burada (CLAUDE.md 1b).
///
/// Bu kutu bir "emin misiniz?" degil. Uc ayri gercegi birden soyluyor:
///   1. KIM etkileniyor - sayiyla degil ADLARIYLA
///   2. Cevap GUVENILIR mi (indeks tam mi)
/// Ikisini de soylemek sart: kullanici bu ekrana bakip dosya adi degistiriyor
/// ve yanlis karar montaji bozar (CLAUDE.md 3).
/// </summary>
internal static class OnarimKutusu
{
    /// <summary>
    /// Plani gosterir ve karari alir. Ebeveyni olmayan ve guvenilir bir
    /// planda HIC SORMAZ - sorulacak bir sey yoktur.
    /// </summary>
    internal static OnarimKarari Sor(
        IWin32Window sahip, OnarimPlani plan, string eskiAd, string? uzantiUyarisi)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Engeller.Count > 0)
        {
            MessageBox.Show(
                sahip, Engeller(plan), "Adı değiştirilemiyor",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return OnarimKarari.Vazgec;
        }

        // SORULACAK BIR SEY YOKSA KUTU CIKMAZ: guvenilir bir taramada
        // "kullanan yok" demektir. Guvenilir DEGILSE bunu soylemek sart -
        // bos liste "yok" demek degildir (CLAUDE.md 3).
        if (plan.Ebeveynler.Count == 0 && uzantiUyarisi is null)
        {
            return plan.Guvenilir ? OnarimKarari.OnarmadanDegistir : Bilinmiyor(sahip, eskiAd);
        }

        return OnayKutusu.Sor(sahip, "Adı değiştir", Metin(plan, eskiAd, uzantiUyarisi))
            ? (plan.Ebeveynler.Count > 0 ? OnarimKarari.Onar : OnarimKarari.OnarmadanDegistir)
            : OnarimKarari.Vazgec;
    }

    private static string Engeller(OnarimPlani plan)
        => MaddeKutusu.Metin("Bu ad değişikliği şu an yapılamaz:\n", plan.Engeller)
            + "\n\nHiçbir şeye dokunulmadı.";

    /// <summary>
    /// Tarama yapilmamisken sorulan soru. "Kullanan yok" DEMIYOR - bilmedigini
    /// soyluyor (CLAUDE.md 3'un en sert kurali).
    /// </summary>
    private static OnarimKarari Bilinmiyor(IWin32Window sahip, string eskiAd)
        => OnayKutusu.Sor(
            sahip, "Kimin kullandığı bilinmiyor",
            $"\"{eskiAd}\" dosyasını KİMİN kullandığını bilmiyoruz.\n\n"
            + "Bu kök henüz taranmadı; boş bir liste \"kimse kullanmıyor\" demek DEĞİLDİR.\n"
            + "Adı şimdi değiştirirseniz onu kullanan bir montaj varsa parçayı\n"
            + "bulamaz ve bunu onaramam.\n\n"
            + "Önce Ctrl+Shift+R ile taramanız önerilir.",
            tehlikeli: true)
            ? OnarimKarari.OnarmadanDegistir
            : OnarimKarari.Vazgec;

    private static string Metin(OnarimPlani plan, string eskiAd, string? uzantiUyarisi)
    {
        var metin = new StringBuilder();

        // UZANTI UYARISI AYRI KUTU DEGIL (28.08.2026): once uzanti kutusu,
        // hemen ardindan onarim kutusu cikiyordu - ust uste iki kutu.
        if (uzantiUyarisi is not null)
        {
            metin.AppendLine(uzantiUyarisi);
            metin.AppendLine();
        }

        if (plan.Ebeveynler.Count > 0)
        {
            var adlar = new List<string>(plan.Ebeveynler.Count);
            foreach (string y in plan.Ebeveynler)
            {
                adlar.Add(WindowsYolu.DosyaAdi(y));
            }

            // Madde listesi ve kirpma TEK yerden (MaddeKutusu, CLAUDE.md 8).
            metin.AppendLine(MaddeKutusu.Metin(
                $"\"{eskiAd}\" dosyasını {plan.Ebeveynler.Count} dosya kullanıyor:\n", adlar));
            metin.AppendLine();
            metin.AppendLine("Adı değiştirilecek ve bu dosyalar onarılacak.");

            if (!plan.Guvenilir)
            {
                metin.AppendLine();
                metin.AppendLine("NOT: tarama tam değil; bu liste EKSİK olabilir.");
            }
        }

        return metin.ToString().TrimEnd();
    }

}
