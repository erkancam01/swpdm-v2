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

        // BAGIMLILIK KUTUSU KALKTI (28.08.2026). Onarim geldigi icin
        // parcalari birlikte goturmek referans acisindan SART DEGIL; onay
        // kutusundaki tek satir onun yerini aliyor.
        if (!Onayla(baglam.Sahip, baglam.Referanslar, yollar, hedefKlasor, kip))
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
        // ONCE TARAMA (Erkan, 28.08.2026). Indeks hazir degilse kimin
        // kullandigi bilinmez ve tasima sonrasi ONARIM YAPILAMAZ - bu delik
        // gercekte acildi: dosya tasindi, uygulama sessizce onarmadi ve
        // SOLIDWORKS parcayi bulamadi.
        //
        // IPTAL EDILIRSE TASIMA DA YAPILMAZ: yarim bilgiyle onarmaktansa
        // hic dokunmamak dogru (CLAUDE.md 1a).
        if (kip == AktarmaKipi.Tasi && !baglam.Referanslar.Hazir
            && !Tara(baglam, belirtec))
        {
            baglam.Ilerleme.Bitti(() => baglam.Bildir("Taşıma iptal edildi — tarama yarım kaldı."));
            return;
        }

        var olan = new List<string>();
        var olmayan = new List<string>();
        var atlanan = new List<string>();
        var ciftler = new List<(string Eski, string Yeni)>();
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
                string yeniYol = rapor.YeniYol ?? WindowsYolu.Birlestir(hedefKlasor, ad);
                olan.Add(yeniYol);
                ciftler.Add((yol, yeniYol));
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

        // REFERANS ONARIMI - yalnizca TASIMADA.
        //
        // Kopyalamada gerekmez: ozgun dosyalar yerinde kaliyor, yani onlarin
        // ebeveynleri etkilenmiyor. (Kopyanin ozgun parcalari kullanmasi ayri
        // bir konu - Pack and Go - ve bu surumde YAPILMIYOR, onay kutusunda
        // yaziyor.)
        //
        // BIRLIKTE TASINANLAR ELENIR: olculdu (CLAUDE.md 5) - SOLIDWORKS once
        // ebeveynin yanina bakiyor, yani birlikte giden aile kendiliginden
        // calisiyor. Calisani onarmak bos risktir (1a).
        int onarilan = 0;
        IReadOnlyList<string> onarimHatalari = [];
        IReadOnlyList<OnarimPlani> tutan = [];
        string? onarimSebebi = null;

        if (kip == AktarmaKipi.Tasi)
        {
            (IReadOnlyList<OnarimPlani> planlar, onarimSebebi) =
                ReferansOnarimi.TasimaPlanlari(baglam.Referanslar.Indeks, ciftler, yollar);
            (onarilan, onarimHatalari, tutan) = ReferansOnarimi.Onar(planlar);
        }

        if (onarilan > 0)
        {
            // Indeks tazelenmezse referans paneli eski yolu gostermeye devam
            // eder (CLAUDE.md 3).
            baglam.Referanslar.Tazele(olan);
        }

        baglam.Ilerleme.Bitti(() => Topla(
            baglam, ciftler, olan, olmayan, atlanan, kip, kesildi,
            onarilan, onarimHatalari, tutan, onarimSebebi));
    }

    /// <summary>
    /// Tasimadan once referans taramasi. Doner: devam edilebilir mi.
    ///
    /// CAGIRAN TEK YER BURASI ama TARAMANIN KENDISI ReferansSurucusu'nda -
    /// ikinci bir tarama kopyasi yazilmiyor (CLAUDE.md 8).
    /// </summary>
    private static bool Tara(IslemBaglami baglam, CancellationToken belirtec)
    {
        baglam.Bildir("Referanslar taranıyor — taşımadan önce gerekli…");
        TaramaSonucu? sonuc = baglam.Referanslar.Tara(
            belirtec, (yapilan, toplam, ad) => baglam.Ilerleme.Adim(yapilan, toplam, ad));

        return sonuc is not null && !sonuc.Iptal;
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
        List<(string Eski, string Yeni)> ciftler,
        List<string> olan,
        List<string> olmayan,
        List<string> atlanan,
        AktarmaKipi kip,
        bool kesildi,
        int onarilan,
        IReadOnlyList<string> onarimHatalari,
        IReadOnlyList<OnarimPlani> onarilanPlanlar,
        string? onarimSebebi)
    {
        Pano.Bosalt();

        if (olan.Count > 0)
        {
            // Her ozellik KENDI geri almasini yaziyor (CLAUDE.md 1b): defter
            // hicbir islemi adiyla bilmez.
            GeriAlDefteri.Kaydet(kip == AktarmaKipi.Tasi
                ? TasimayiGeriAl(ciftler, onarilanPlanlar)
                : KopyalamayiGeriAl(olan));
        }

        baglam.Tazele(null);

        string is_ = kip == AktarmaKipi.Tasi ? "taşındı" : "kopyalandı";
        string olumsuz = kip == AktarmaKipi.Tasi ? "TAŞINMADI (yerinde duruyor)" : "KOPYALANMADI";

        // IKI AYRI HATA KUTUSU BIRLESTI (28.08.2026): once "Bazi ogeler
        // aktarilamadi" ve "Referans onarilamadi" ust uste iki kutu
        // cikabiliyordu. Ikisi de ayni islemin sonucu; tek kutu yeter.
        if (olmayan.Count > 0 || onarimHatalari.Count > 0 || onarimSebebi is not null)
        {
            var metin = new StringBuilder();
            metin.AppendLine($"{olan.Count} öğe {is_}.");

            if (olmayan.Count > 0)
            {
                metin.AppendLine();
                metin.AppendLine($"{olmayan.Count} öğe {olumsuz}:");
                foreach (string satir in olmayan)
                {
                    metin.AppendLine("  • " + satir);
                }
            }

            // SESSIZ ATLAMA YOK (CLAUDE.md 3): onarim yapilamadiysa SEBEBI
            // yazilir. Once bu delik acikti ve dosya sessizce kirildi.
            if (onarimSebebi is not null)
            {
                metin.AppendLine();
                metin.AppendLine("Referanslar ONARILAMADI — " + onarimSebebi + ".");
                metin.AppendLine("Ctrl+Shift+R ile tarayıp taşımayı tekrar deneyin.");
            }

            if (onarimHatalari.Count > 0)
            {
                metin.AppendLine();
                metin.AppendLine($"{onarimHatalari.Count} dosyanın referansı onarılamadı:");
                foreach (string satir in onarimHatalari)
                {
                    metin.AppendLine("  • " + satir);
                }

                metin.AppendLine();
                metin.AppendLine("Bunları kullanan belgeler parçayı bulamayabilir.");
                metin.AppendLine("Ctrl+Z ile geri alabilirsiniz.");
            }

            MessageBox.Show(
                baglam.Sahip, metin.ToString(), "Bazı işlemler tamamlanmadı",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        string kuyruk = kesildi ? " · iptal edildi" : string.Empty;
        if (onarilan > 0)
        {
            kuyruk = $" · {onarilan} dosya onarıldı" + kuyruk;
        }
        if (atlanan.Count > 0)
        {
            kuyruk = $" · {atlanan.Count} atlandı" + kuyruk;
        }

        baglam.Bildir(olmayan.Count == 0
            ? $"{olan.Count} öğe {is_}.{kuyruk}"
            : $"{olan.Count} {is_} · {olmayan.Count} olmadı{kuyruk}");
    }

    /// <summary>Tasinanlari eski klasorlerine geri gonderir.</summary>
    /// <summary>
    /// Tasimayi geri alir - VE onarimi da.
    ///
    /// CIFTLER TASINIYOR, iki ayri liste degil: onceden "kaynaklar" ve
    /// "olan" listeleri ayni indisle eslestiriliyordu ve bir oge
    /// tasinamayinca hizalama KAYIYORDU - yani geri alma dosyayi YANLIS
    /// klasore gonderebilirdi. Cift tutmak bunu imkansiz kiliyor.
    ///
    /// ONARIM DA GERI ALINIR: yalnizca dosyalar geri tasinsaydi, ebeveynler
    /// yeni yola bakar halde kalir ve GERI ALMA referansi KIRARDI.
    /// </summary>
    private static GeriAlinabilir TasimayiGeriAl(
        IReadOnlyList<(string Eski, string Yeni)> ciftler,
        IReadOnlyList<OnarimPlani> onarilanPlanlar)
    {
        var kopya = new List<(string Eski, string Yeni)>(ciftler);
        var planlar = new List<OnarimPlani>(onarilanPlanlar);

        return new GeriAlinabilir(
            $"{kopya.Count} öğenin taşınması",
            baglam =>
            {
                var olmayan = new List<string>();

                // ONCE onarim geri alinir: dosyalar hala yeni yerindeyken
                // yamalar okunabiliyor ve yazilabiliyor.
                ReferansOnarimi.GeriOnar(planlar);

                foreach ((string eski, string yeni) in kopya)
                {
                    IslemRaporu rapor = DosyaIslemleri.Tasi(yeni, WindowsYolu.Klasor(eski));
                    if (!rapor.Oldu)
                    {
                        olmayan.Add(WindowsYolu.DosyaAdi(yeni)
                            + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
                    }
                }

                var dokunulan = new List<string>();
                foreach ((string eski, _) in kopya)
                {
                    dokunulan.Add(eski);
                }

                baglam.Referanslar.Tazele(dokunulan);
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

    /// <summary>
    /// Aktarma onayi. TEK KUTU ve KISA (Erkan, 28.08.2026).
    ///
    /// Eski metin ARTIK YANLISTI: "onlari su an ONARAMIYORUZ" ve "teknik
    /// resim -> model bagi icin bu olcum HENUZ YAPILMADI" diyordu. Ikisi de
    /// gecersiz - onariyoruz ve olcum yapildi. Bayat uyari, gurultuden once
    /// bir DURUSTLUK sorunudur (CLAUDE.md 3).
    /// </summary>
    private static bool Onayla(
        IWin32Window sahip,
        ReferansSurucusu referanslar,
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

        // TEK SATIRLIK GERCEK BILGI - eski bagimlilik kutusunun yerine.
        // Sayi yalnizca VARSA yaziliyor; "0 dosya" satiri gurultudur.
        if (referanslar.Indeks is ReferansIndeksi indeks)
        {
            int eksik = indeks.ZincirdekiEksikler(yollar).Count;
            if (eksik > 0)
            {
                metin.AppendLine();
                metin.AppendLine(kip == AktarmaKipi.Tasi
                    ? $"Kullandığı {eksik} dosya taşınmıyor; referansları onarılacak."
                    : $"Kopya, kullandığı {eksik} dosyanın özgün hâlini göstermeye devam eder.");
            }
        }

        return OnayKutusu.Sor(sahip, kip == AktarmaKipi.Tasi ? "Taşı" : "Kopyala", metin.ToString());
    }
}
