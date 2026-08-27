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

        // ONCE bagimliliklar sorulur, SONRA onay: onay kutusunda gorunen
        // sayi gercekten aktarilacak sayi olsun (CLAUDE.md 3 - kullanici
        // "3 oge" onaylayip 7 oge tasinmis bulmasin).
        IReadOnlyList<string>? tam = BagimlilariEkle.Sor(baglam.Sahip, baglam.Referanslar, yollar);
        if (tam is null)
        {
            baglam.Bildir(kip == AktarmaKipi.Tasi ? "Taşıma iptal edildi." : "Kopyalama iptal edildi.");
            return;
        }

        yollar = tam;

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
        var atlanan = new List<string>();
        bool kesildi = false;

        // "Hepsine uygula" isaretlenirse kalan cakismalar sorulmadan bu
        // kararla gecer.
        Cakisma hepsiIcin = Cakisma.Sor;

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

            IslemRaporu rapor = Uygula(baglam, yol, hedefKlasor, kip, hepsiIcin);

            // Cakisma var ve daha once "hepsine" denmedi: KULLANICIYA SOR.
            if (rapor.Sonuc == IslemSonucu.ZatenVar && hepsiIcin == Cakisma.Sor)
            {
                CakismaKarari karar = SorArayuzde(
                    baglam, yol, WindowsYolu.Birlestir(hedefKlasor, ad));

                if (karar.Vazgecti)
                {
                    kesildi = true;
                    break;
                }

                if (karar.Hepsine)
                {
                    hepsiIcin = karar.Karar;
                }

                rapor = Uygula(baglam, yol, hedefKlasor, kip, karar.Karar);
            }

            if (rapor.Oldu)
            {
                olan.Add(rapor.YeniYol ?? WindowsYolu.Birlestir(hedefKlasor, ad));
            }
            else if (rapor.Sonuc == IslemSonucu.Atlandi)
            {
                atlanan.Add(ad);
            }
            else
            {
                olmayan.Add(ad + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
            }
        }

        baglam.Ilerleme.Adim(yollar.Count, yollar.Count, string.Empty);
        baglam.Ilerleme.Bitti(() => Topla(baglam, yollar, olan, olmayan, atlanan, kip, kesildi));
    }

    /// <summary>Tek bir ogeyi verilen cakisma karariyla aktarir.</summary>
    private static IslemRaporu Uygula(
        IslemBaglami baglam,
        string yol,
        string hedefKlasor,
        AktarmaKipi kip,
        Cakisma cakisma)
    {
        // "Degistir" secilirse uzerine yazilan dosya YOK EDILMEZ, once cope
        // tasinir. Kurtarma tutmazsa cekirdek islemi YAPMAZ (CLAUDE.md 1a).
        bool Kurtar(string eskisi)
            => baglam.Secim.CopKlasoru is string cop && Cop.Sil(cop, eskisi).Oldu;

        return kip == AktarmaKipi.Tasi
            ? DosyaIslemleri.Tasi(yol, hedefKlasor, cakisma, Kurtar)
            : DosyaIslemleri.Kopyala(yol, hedefKlasor, cakisma, Kurtar);
    }

    /// <summary>
    /// Cakismayi ARAYUZ is parcaciginda sorar ve cevabi bekler. Is arka
    /// planda kosuyor; oradan pencere acmak coker.
    /// </summary>
    private static CakismaKarari SorArayuzde(IslemBaglami baglam, string kaynak, string hedef)
    {
        CakismaKarari karar = default;
        using var bekle = new ManualResetEventSlim(false);

        // Pencere kapandiysa kutu HIC acilmaz; o zaman beklemek sonsuz
        // askida kalmak olurdu (CLAUDE.md 3). Kuyruga girmediyse "vazgecildi".
        bool kuyrukta = baglam.Ilerleme.Arayuzde(() =>
        {
            try
            {
                karar = CakismaKutusu.Sor(baglam.Sahip, kaynak, hedef);
            }
            finally
            {
                bekle.Set();
            }
        });

        if (!kuyrukta)
        {
            return new CakismaKarari(Cakisma.Atla, Hepsine: true, Vazgecti: true);
        }

        // Kuyruga girdi ama pencere cevap gelmeden kapanabilir: mesaj pompasi
        // durdugunda delege HIC kosmaz. O yuzden beklemek KOSULLU.
        while (!bekle.Wait(200))
        {
            if (baglam.Sahip is Control sahip && (sahip.IsDisposed || !sahip.IsHandleCreated))
            {
                return new CakismaKarari(Cakisma.Atla, Hepsine: true, Vazgecti: true);
            }
        }

        return karar;
    }

    private static void Topla(
        IslemBaglami baglam,
        List<string> kaynaklar,
        List<string> olan,
        List<string> olmayan,
        List<string> atlanan,
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
        if (atlanan.Count > 0)
        {
            kuyruk = $" · {atlanan.Count} atlandı" + kuyruk;
        }

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
                if (baglam.Secim.CopKlasoru is not string cop)
                {
                    olmayan.Add("Kök klasör kapalı; kopyalar kaldırılamadı.");
                    return olmayan;
                }

                foreach (string yol in yollar)
                {
                    // KALICI SILME YOK: kopyalar da cope gider, oradan geri
                    // alinabilir (CLAUDE.md 1a).
                    IslemRaporu rapor = Cop.Sil(cop, yol);
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
