using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// TASINAN/KOPYALANAN SECIME BAGIMLILARINI EKLEMEYI SORAR.
///
/// NEDEN VAR - UYGULAMANIN ASIL SOZU: bir montaji tasiyip parcalarini
/// geride birakmak referansi kirar. Dosyanin ICINE yazip yolu duzeltmek
/// bu surumde YAPILMIYOR (yazilan dosyanin acildigi dogrulanamiyor), ama
/// yazmaya GEREK DE YOK: CLAUDE.md 5'te olculdu - SOLIDWORKS, ebeveynin
/// YANINDAKI dosyayi yazili mutlak yolun onune koyuyor. Yani montaj ve
/// parcalari BIRLIKTE tasinirsa referans kendiliginden yasiyor.
///
/// ZINCIRIN TAMAMI alinir: montaj -> alt montaj -> parca. Yalnizca
/// dogrudan cocuklari almak, bir alt seviyeyi geride birakirdi.
///
/// IKI YONLU DURUSTLUK (CLAUDE.md 3):
///   - Bagimlilari EKLEMEZSEK: tasinan montaj parcalarini kaybedebilir.
///   - Bagimlilari EKLERSEK: o parcalari kullanan BASKA dosyalar etkilenir.
/// Kutuda iki sayi da yaziyor; karar kullanicinin.
///
/// INDEKS YOKSA HIC SORULMAZ. "Bagimlilik yok" demek yerine sessiz kalmak
/// dogru: tarama yapilmamisken sorulan bir soru, kullaniciyi olmayan bir
/// bilgiye guvendirirdi.
/// </summary>
internal static class BagimlilariEkle
{
    /// <summary>
    /// Gerekirse sorar ve aktarilacak son listeyi verir.
    /// null donerse kullanici VAZGECTI.
    /// </summary>
    internal static IReadOnlyList<string>? Sor(
        IWin32Window sahip, ReferansSurucusu referanslar, IReadOnlyList<string> yollar)
    {
        ArgumentNullException.ThrowIfNull(referanslar);
        ArgumentNullException.ThrowIfNull(yollar);

        if (!referanslar.Hazir || referanslar.Indeks is null)
        {
            return yollar;   // tarama yok: soru sorulmaz
        }

        List<string> eksikler = Eksikler(referanslar.Indeks, yollar);
        if (eksikler.Count == 0)
        {
            return yollar;
        }

        int etkilenen = DisaridanKullanan(referanslar.Indeks, eksikler, yollar);

        DialogResult cevap = MessageBox.Show(
            sahip, Metin(eksikler, etkilenen), "Kullandığı dosyalar da taşınsın mı?",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        if (cevap == DialogResult.Cancel)
        {
            return null;
        }

        if (cevap == DialogResult.No)
        {
            return yollar;
        }

        var hepsi = new List<string>(yollar);
        hepsi.AddRange(eksikler);
        return hepsi;
    }

    /// <summary>
    /// Secimin kullandigi ama secimde OLMAYAN dosyalar - zincirin tamami.
    /// </summary>
    private static List<string> Eksikler(ReferansIndeksi indeks, IReadOnlyList<string> yollar)
    {
        var secili = new HashSet<string>(yollar, StringComparer.OrdinalIgnoreCase);
        var eksik = new List<string>();
        var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sira = new Queue<string>(yollar);

        while (sira.Count > 0)
        {
            string suan = sira.Dequeue();
            if (!gorulen.Add(suan))
            {
                continue;
            }

            foreach ((_, Cozum cozum) in indeks.Kullandiklari(suan))
            {
                if (cozum.Durum != CozumDurumu.Bulundu || cozum.Yol is not string hedef)
                {
                    continue;   // cozulememis referans EKLENMEZ; nereye gidecegi belirsiz
                }

                if (secili.Contains(hedef) || Icinde(secili, hedef))
                {
                    sira.Enqueue(hedef);   // zaten tasiniyor ama COCUKLARINA bakilmali
                    continue;
                }

                if (!gorulen.Contains(hedef)
                    && !eksik.Exists(v => string.Equals(v, hedef, StringComparison.OrdinalIgnoreCase)))
                {
                    eksik.Add(hedef);
                }

                sira.Enqueue(hedef);
            }
        }

        return eksik;
    }

    /// <summary>Secilen bir KLASORUN altinda mi.</summary>
    private static bool Icinde(HashSet<string> secili, string yol)
    {
        foreach (string s in secili)
        {
            if (yol.StartsWith(s + WindowsYolu.Ayirici, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Eklenecek dosyalari, TASINMAYAN baska hangi dosyalar kullaniyor.
    /// Bu sayi kullaniciya soylenmeli: onlari da etkiliyoruz.
    /// </summary>
    private static int DisaridanKullanan(
        ReferansIndeksi indeks, List<string> eksikler, IReadOnlyList<string> yollar)
    {
        var tasinan = new HashSet<string>(yollar, StringComparer.OrdinalIgnoreCase);
        foreach (string e in eksikler)
        {
            tasinan.Add(e);
        }

        var etkilenen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string e in eksikler)
        {
            foreach (string kullanan in indeks.Kullananlar(e).Kullananlar)
            {
                if (!tasinan.Contains(kullanan) && !Icinde(tasinan, kullanan))
                {
                    etkilenen.Add(kullanan);
                }
            }
        }

        return etkilenen.Count;
    }

    private static string Metin(List<string> eksikler, int etkilenen)
    {
        var metin = new StringBuilder();
        metin.AppendLine($"Seçilenler {eksikler.Count} dosya daha kullanıyor:");
        metin.AppendLine();

        int yazilan = 0;
        foreach (string e in eksikler)
        {
            if (yazilan == 10)
            {
                metin.AppendLine($"  … ve {eksikler.Count - 10} tane daha");
                break;
            }

            metin.AppendLine("  • " + WindowsYolu.DosyaAdi(e));
            yazilan++;
        }

        metin.AppendLine();
        metin.AppendLine("EVET  — onları da götür (referanslar yanınızda kalır)");
        metin.AppendLine("HAYIR — yalnızca seçtiklerimi taşı");
        metin.AppendLine();
        metin.AppendLine("Not: SOLIDWORKS bir dosyayı ararken önce ebeveynin yanına");
        metin.AppendLine("bakar. Birlikte taşınırsa referans kendiliğinden yaşar.");

        if (etkilenen > 0)
        {
            metin.AppendLine();
            metin.AppendLine($"DİKKAT: bu dosyaları taşınmayan {etkilenen} dosya daha");
            metin.AppendLine("kullanıyor; onlar bu parçaları eski yerinde bulamayabilir.");
        }

        return metin.ToString();
    }
}
