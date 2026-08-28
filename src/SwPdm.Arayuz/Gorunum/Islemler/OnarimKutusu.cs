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

    /// <summary>Yalnizca adi degistir; referanslar kirilsin.</summary>
    OnarmadanDegistir,
}

/// <summary>
/// AD DEGISIMINDE NE OLACAGINI SORAR - metnin tamami burada (CLAUDE.md 1b).
///
/// Bu kutu bir "emin misiniz?" degil. Uc ayri gercegi birden soyluyor:
///   1. KIM etkileniyor - sayiyla degil ADLARIYLA
///   2. Onarim OLCULMUS bir yoldan mi gidiyor
///   3. Cevap GUVENILIR mi (indeks tam mi)
/// Ucunu de soylemek sart: kullanici bu ekrana bakip dosya adi degistiriyor
/// ve yanlis karar montaji bozar (CLAUDE.md 3).
/// </summary>
internal static class OnarimKutusu
{
    /// <summary>Kutuda en fazla kac ebeveyn adi sayilir.</summary>
    private const int EnFazlaAd = 12;

    /// <summary>
    /// Plani gosterir ve karari alir. Ebeveyni olmayan ve guvenilir bir
    /// planda HIC SORMAZ - sorulacak bir sey yoktur.
    /// </summary>
    internal static OnarimKarari Sor(IWin32Window sahip, OnarimPlani plan, string eskiAd)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Engeller.Count > 0)
        {
            MessageBox.Show(
                sahip, Engeller(plan), "Adı değiştirilemiyor",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return OnarimKarari.Vazgec;
        }

        if (plan.Ebeveynler.Count == 0)
        {
            // Guvenilir bir taramada "kullanan yok" demek; sormaya gerek yok.
            // Guvenilir DEGILSE bunu soylemek sart - bos liste "yok" demek
            // degildir ve sessizce gecmek kullaniciyi yaniltir.
            return plan.Guvenilir
                ? OnarimKarari.OnarmadanDegistir
                : Bilinmiyor(sahip, eskiAd);
        }

        DialogResult cevap = MessageBox.Show(
            sahip, Metin(plan, eskiAd),
            plan.OlculmusGuvenli ? "Kullanan dosyalar onarılsın mı?" : "Bu ad farklı uzunlukta",
            MessageBoxButtons.YesNoCancel,
            plan.OlculmusGuvenli ? MessageBoxIcon.Question : MessageBoxIcon.Warning,
            plan.OlculmusGuvenli ? MessageBoxDefaultButton.Button1 : MessageBoxDefaultButton.Button3);

        return cevap switch
        {
            DialogResult.Yes => OnarimKarari.Onar,
            DialogResult.No => OnarimKarari.OnarmadanDegistir,
            _ => OnarimKarari.Vazgec,
        };
    }

    private static string Engeller(OnarimPlani plan)
    {
        var metin = new StringBuilder();
        metin.AppendLine("Bu ad değişikliği şu an yapılamaz:");
        metin.AppendLine();

        foreach (string e in plan.Engeller)
        {
            metin.AppendLine("  • " + e);
        }

        metin.AppendLine();
        metin.AppendLine("Hiçbir şeye dokunulmadı.");
        return metin.ToString();
    }

    /// <summary>
    /// Tarama yapilmamisken sorulan soru. "Kullanan yok" DEMIYOR - bilmedigini
    /// soyluyor (CLAUDE.md 3'un en sert kurali).
    /// </summary>
    private static OnarimKarari Bilinmiyor(IWin32Window sahip, string eskiAd)
        => MessageBox.Show(
            sahip,
            $"\"{eskiAd}\" dosyasını KİMİN kullandığını bilmiyoruz.\n\n"
            + "Bu kök henüz taranmadı; boş bir liste \"kimse kullanmıyor\" demek "
            + "DEĞİLDİR. Adı şimdi değiştirirseniz, onu kullanan bir montaj varsa "
            + "parçayı bulamaz ve bunu onaramam.\n\n"
            + "Önce Ctrl+Shift+R ile referansları taramanız önerilir.\n\n"
            + "Yine de adı değiştirilsin mi?",
            "Kimin kullandığı bilinmiyor",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK
            ? OnarimKarari.OnarmadanDegistir
            : OnarimKarari.Vazgec;

    private static string Metin(OnarimPlani plan, string eskiAd)
    {
        var metin = new StringBuilder();
        metin.AppendLine($"\"{eskiAd}\" dosyasını {plan.Ebeveynler.Count} dosya kullanıyor:");
        metin.AppendLine();
        Adlari(metin, plan.Ebeveynler);
        metin.AppendLine();

        if (plan.OlculmusGuvenli)
        {
            metin.AppendLine("EVET  — adı değiştir VE bu dosyaları onar (önerilen)");
            metin.AppendLine("HAYIR — yalnızca adı değiştir; yukarıdakiler parçayı");
            metin.AppendLine("        bulamayacak");
        }
        else
        {
            // OLCULEN SART: yazilan yolun karakter sayisi degismemeli. Yeni ad
            // farkli uzunluktaysa fark klasor kismindan karsilaniyor ve BU
            // HENUZ DOGRULANMADI - soylemeden uygulamak yalan olur.
            metin.AppendLine($"DİKKAT: \"{plan.YeniAd}\" eski addan farklı sayıda harf içeriyor.");
            metin.AppendLine();
            metin.AppendLine("Onarımın ölçülmüş hâli, yazılan yolun karakter sayısının");
            metin.AppendLine("değişmemesini gerektiriyor. Farklı uzunlukta bir ad için");
            metin.AppendLine("yolu doldurarak aynı uzunlukta tutuyorum, ama bu yolun");
            metin.AppendLine("SOLIDWORKS tarafından kabul edildiği HENÜZ DOĞRULANMADI.");
            metin.AppendLine();
            metin.AppendLine("En güvenlisi: eski adla AYNI SAYIDA HARF içeren bir ad seçin.");
            metin.AppendLine();
            metin.AppendLine("EVET  — yine de dene (doğrulanmamış)");
            metin.AppendLine("HAYIR — yalnızca adı değiştir; onarma");
        }

        metin.AppendLine("VAZGEÇ — hiçbir şey yapma");

        if (!plan.Guvenilir)
        {
            metin.AppendLine();
            metin.AppendLine("NOT: tarama tam değil; bu liste EKSİK olabilir.");
        }

        return metin.ToString();
    }

    private static void Adlari(StringBuilder metin, IReadOnlyList<string> yollar)
    {
        int yazilan = 0;
        foreach (string y in yollar)
        {
            if (yazilan == EnFazlaAd)
            {
                metin.AppendLine($"  … ve {yollar.Count - EnFazlaAd} tane daha");
                return;
            }

            metin.AppendLine("  • " + WindowsYolu.DosyaAdi(y));
            yazilan++;
        }
    }
}
