using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>Aktarmanin yonu.</summary>
internal enum AktarmaKipi
{
    /// <summary>Kaynak gider, hedefe konur.</summary>
    Tasi,

    /// <summary>Kaynak kalir, hedefe kopyasi konur.</summary>
    Kopyala,
}

/// <summary>
/// AKTARMA MOTORU - tasima ve kopyalama. Ikisi ayni istir, farki tek bir
/// cagri (CLAUDE.md 8: ayni mantigin ikinci kopyasi yazilmaz). Surukle-birak,
/// "Yapistir" ve gelecekte baska bir cagiran hep buraya gelir.
///
/// ARKA PLANDA KOSAR. Sebep olculmus (CLAUDE.md 6): ilerleme cubugu, is
/// arayuz is parcacigini bloke ederse HIC CIZILMEZ - kullanici bos bir oluk
/// gorur. Islem arka planda kosuyor, ilerleme arayuze BeginInvoke ile
/// bildiriliyor.
/// </summary>
internal static class Aktar
{
    /// <summary>Ayni anda ikinci bir aktarma baslamasin.</summary>
    private static bool _kosuyor;

    /// <summary>
    /// Ogeleri hedefe aktarir. Kismi basarisizlikta duran ogeler tek tek
    /// sayilir (CLAUDE.md 3).
    /// </summary>
    internal static void Yurut(
        IslemBaglami baglam,
        IReadOnlyList<string> yollar,
        string hedefKlasor,
        AktarmaKipi kip)
    {
        if (yollar.Count == 0 || _kosuyor)
        {
            return;
        }

        if (!Onayla(baglam.Sahip, yollar, hedefKlasor, kip))
        {
            baglam.Bildir(kip == AktarmaKipi.Tasi ? "Taşıma iptal edildi." : "Kopyalama iptal edildi.");
            return;
        }

        _kosuyor = true;
        var iptal = new CancellationTokenSource();
        baglam.Ilerleme.Basladi(yollar.Count, iptal);

        // Kopya SART: arka plan is parcacigi calisirken cagiranin listesi
        // (ornegin pano) temizlenebilir.
        var kaynaklar = new List<string>(yollar);

        Task.Run(
            () => Isle(baglam, kaynaklar, hedefKlasor, kip, iptal.Token))
            .ContinueWith(
                _ =>
                {
                    _kosuyor = false;
                    iptal.Dispose();
                },
                TaskScheduler.Default);
    }

    private static void Isle(
        IslemBaglami baglam,
        List<string> yollar,
        string hedefKlasor,
        AktarmaKipi kip,
        CancellationToken belirtec)
    {
        var olan = new List<string>();
        var olmayan = new List<string>();
        bool kesildi = false;

        for (int i = 0; i < yollar.Count; i++)
        {
            if (belirtec.IsCancellationRequested)
            {
                // Iptal yalnizca ogeler ARASINDA olur; yarim dosya birakmayiz.
                kesildi = true;
                break;
            }

            string yol = yollar[i];
            string ad = WindowsYolu.DosyaAdi(yol);
            baglam.Ilerleme.Adim(i, yollar.Count, ad);

            IslemRaporu rapor = kip == AktarmaKipi.Tasi
                ? DosyaIslemleri.Tasi(yol, hedefKlasor)
                : DosyaIslemleri.Kopyala(yol, hedefKlasor);

            if (rapor.Oldu)
            {
                olan.Add(rapor.YeniYol ?? WindowsYolu.Birlestir(hedefKlasor, ad));
            }
            else
            {
                olmayan.Add(ad + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
            }
        }

        baglam.Ilerleme.Adim(yollar.Count, yollar.Count, string.Empty);
        baglam.Ilerleme.Bitti(() => Topla(baglam, yollar, olan, olmayan, kip, kesildi));
    }

    private static void Topla(
        IslemBaglami baglam,
        List<string> kaynaklar,
        List<string> olan,
        List<string> olmayan,
        AktarmaKipi kip,
        bool kesildi)
    {
        Pano.Bosalt();

        if (olan.Count > 0)
        {
            // Her ozellik KENDI geri almasini yaziyor (CLAUDE.md 1b): defter
            // hicbir islemi adiyla bilmez.
            GeriAlDefteri.Kaydet(kip == AktarmaKipi.Tasi
                ? TasimayiGeriAl(kaynaklar, olan)
                : KopyalamayiGeriAl(olan));
        }

        baglam.Tazele(null);

        string is_ = kip == AktarmaKipi.Tasi ? "taşındı" : "kopyalandı";
        string olumsuz = kip == AktarmaKipi.Tasi ? "TAŞINMADI (yerinde duruyor)" : "KOPYALANMADI";

        if (olmayan.Count > 0)
        {
            var metin = new StringBuilder();
            metin.AppendLine($"{olan.Count} öğe {is_}.");
            metin.AppendLine();
            metin.AppendLine($"{olmayan.Count} öğe {olumsuz}:");
            foreach (string satir in olmayan)
            {
                metin.AppendLine("  • " + satir);
            }

            MessageBox.Show(
                baglam.Sahip, metin.ToString(), "Bazı öğeler aktarılamadı",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        string kuyruk = kesildi ? " · iptal edildi" : string.Empty;
        baglam.Bildir(olmayan.Count == 0
            ? $"{olan.Count} öğe {is_}.{kuyruk}"
            : $"{olan.Count} {is_} · {olmayan.Count} olmadı{kuyruk}");
    }

    /// <summary>Tasinanlari eski klasorlerine geri gonderir.</summary>
    private static GeriAlinabilir TasimayiGeriAl(
        IReadOnlyList<string> eskiYollar, IReadOnlyList<string> yeniYollar)
    {
        // Eski KLASORLER yakalanip tutuluyor; geri alirken dosya adindan
        // degil, geldigi yerden gidiyoruz.
        var eskiKlasorler = new List<string>(yeniYollar.Count);
        for (int i = 0; i < yeniYollar.Count && i < eskiYollar.Count; i++)
        {
            eskiKlasorler.Add(WindowsYolu.Klasor(eskiYollar[i]));
        }

        var yollar = new List<string>(yeniYollar);

        return new GeriAlinabilir(
            $"{yollar.Count} öğenin taşınması",
            baglam =>
            {
                var olmayan = new List<string>();
                for (int i = 0; i < yollar.Count; i++)
                {
                    IslemRaporu rapor = DosyaIslemleri.Tasi(yollar[i], eskiKlasorler[i]);
                    if (!rapor.Oldu)
                    {
                        olmayan.Add(WindowsYolu.DosyaAdi(yollar[i])
                            + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
                    }
                }

                return olmayan;
            });
    }

    /// <summary>Olusan kopyalari cope gonderir - kalici silmez.</summary>
    private static GeriAlinabilir KopyalamayiGeriAl(IReadOnlyList<string> yeniYollar)
    {
        var yollar = new List<string>(yeniYollar);

        return new GeriAlinabilir(
            $"{yollar.Count} öğenin kopyalanması",
            baglam =>
            {
                var olmayan = new List<string>();
                if (baglam.Secim.Kok is not string kok)
                {
                    olmayan.Add("Kök klasör kapalı; kopyalar kaldırılamadı.");
                    return olmayan;
                }

                foreach (string yol in yollar)
                {
                    // KALICI SILME YOK: kopyalar da cope gider, oradan geri
                    // alinabilir (CLAUDE.md 1a).
                    IslemRaporu rapor = Cop.Sil(kok, yol);
                    if (!rapor.Oldu)
                    {
                        olmayan.Add(WindowsYolu.DosyaAdi(yol)
                            + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
                    }
                }

                return olmayan;
            });
    }

    private static bool Onayla(
        IWin32Window sahip,
        IReadOnlyList<string> yollar,
        string hedef,
        AktarmaKipi kip)
    {
        var metin = new StringBuilder();
        string fiil = kip == AktarmaKipi.Tasi ? "taşınacak" : "kopyalanacak";

        metin.AppendLine(yollar.Count == 1
            ? $"\"{WindowsYolu.DosyaAdi(yollar[0])}\" {fiil}:"
            : $"{yollar.Count} öğe {fiil}:");
        metin.AppendLine();
        metin.AppendLine(hedef);
        metin.AppendLine();

        if (kip == AktarmaKipi.Tasi)
        {
            // CLAUDE.md 5'te OLCULDU - oldugu gibi soyleniyor.
            metin.AppendLine("Ölçüldü: bir klasör taşındığında içindeki montaj–parça");
            metin.AppendLine("bağları YAŞIYOR. Kırılan, DIŞARIDAN bu dosyalara verilen");
            metin.AppendLine("referanslardır; onları şu an ONARAMIYORUZ.");
            metin.AppendLine();
            metin.AppendLine("Teknik resim → model bağı için bu ölçüm HENÜZ YAPILMADI.");
        }
        else
        {
            metin.AppendLine("Kopyalar, kaynak dosyanın referanslarını AYNEN taşır;");
            metin.AppendLine("yani kopya da özgün parçaları gösterir. Referans");
            metin.AppendLine("düzenlemesi (Pack and Go gibi) HENÜZ YAPILMIYOR.");
        }

        return MessageBox.Show(
            sahip, metin.ToString(), kip == AktarmaKipi.Tasi ? "Taşı" : "Kopyala",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK;
    }
}
