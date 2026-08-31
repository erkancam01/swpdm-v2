using System;
using System.Collections.Generic;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// VERSIYONLAR sekmesinin icerigi - "bu dosyanin hangi versiyonlari var,
/// hangisine donulur" sorusunun arayuzdeki TEK sahibi (CLAUDE.md 1b).
/// Veriyi <see cref="Surumler"/> verir; burasi satirlari cizer ve son
/// cizilen listeyi tutar ki "Enter = bu versiyona don" sira numarasindan
/// kaydi bulabilsin.
///
/// Satir duzeni: solda "v3 — not", sagda tarih. Ipucunda (ve Ctrl+C ile
/// panoda) arsiv kopyasinin TAM YOLU - kullanici isterse arsive Gezgin'den
/// bakabilir; yolu gizlemek "arsiv nerede" sorusunu cevapsiz birakirdi
/// (CLAUDE.md 3).
/// </summary>
internal sealed class SurumBolumu
{
    private readonly List<SurumKaydi> _sonListe = [];

    /// <summary>Sekmede yazacak sayi: "yok" · "N" · "okunamadı".</summary>
    internal static string SayiMetni(string kok, string yol)
    {
        SurumDurumu durum = Surumler.Listele(kok, yol);
        if (!durum.Guvenilir)
        {
            return "okunamadı";
        }

        return durum.Ogeler.Count == 0
            ? "yok"
            : durum.Ogeler.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Bolumu doldurur; cizilen kayitlari sira icin saklar.</summary>
    internal void Doldur(ReferansListesi liste, string kok, string yol)
    {
        _sonListe.Clear();

        SurumDurumu durum = Surumler.Listele(kok, yol);
        if (!durum.Guvenilir)
        {
            Aciklama(liste, durum.Okunamadi ?? "Versiyon kaydı okunamadı.", "hata");
            return;
        }

        if (durum.Ogeler.Count == 0)
        {
            Aciklama(liste, "Versiyon yok — Ctrl+Shift+U ile başlat.", "—");
            return;
        }

        foreach (SurumKaydi kayit in durum.Ogeler)
        {
            _sonListe.Add(kayit);

            string ad = kayit.Not.Length == 0
                ? $"v{kayit.No}"
                : $"v{kayit.No} — {kayit.Not}";

            // HEDEF = ARSIV KOPYASI (Erkan, 31.08.2026: "versiyonların
            // önizlemesini görmek ve çift tıklayınca açabilmek"). Tek tik
            // boylece komsu onizleme borusuna girer: panelde o versiyonun
            // resmi, baslikta "◂ v3.SLDPRT". Cift tikin "ac" anlami
            // AnaForm'daki dallanmada; sag tik icin ReferansMenusu arsiv
            // yolunu tanir ve dosya islemlerini uygulamaz.
            liste.Ekle(
                ad,
                Zaman.Yaz(kayit.Zaman),
                simgeSirasi: -1,
                Renkler.ReferansAsagiYazi,
                hedefYol: kayit.ArsivYolu,
                tamMetin: kayit.ArsivYolu);
        }

        // KAYIP/BOZUK KAYIT SESSIZCE YUTULMAZ (CLAUDE.md 3): satiri
        // gosteremiyoruz ama VARLIGINI soyluyoruz - yoksa kullanici o
        // versiyonun hic olmadigini sanir.
        if (durum.BozukSatir > 0)
        {
            Aciklama(
                liste,
                $"{durum.BozukSatir} kayıt bozuk ya da arşiv kopyası kayıp",
                "!");
        }
    }

    /// <summary>
    /// Cift tik: versiyonu ACAR. Acilan sey her halde SALT-OKUNUR; SOLIDWORKS
    /// onu [Read-Only] acar, gecmisin ustune kaza ile kaydedilemez (1a).
    ///
    /// COCUGU OLAN BELGE ARSIVDEN ACILMIYOR - ERKAN'DA OLCULDU (31.08.2026):
    /// montaj/teknik resim versiyonuna cift tiklayinca SOLIDWORKS "dosya
    /// bozuk" diyordu. Sebep bozukluk DEGIL - agactaki guncel dosyalar
    /// sorunsuz aciliyor: arsiv kopyasi kendi klasorunde TEK BASINA duruyor,
    /// parcalari yaninda degil ve komsuluk kurali (CLAUDE.md 5) bosa cikiyor.
    /// O yuzden cocugu olan versiyon once OZGUN DOSYANIN YANINA cikariliyor.
    /// Karar TIPE gore degil OLCUME gore (Surumler.DogrudanAcilir): turetilmis
    /// bir PARCANIN da dis referansi olabilir.
    /// </summary>
    /// <returns>Durum cubuguna yazilacak cumle.</returns>
    internal static string Ac(
        System.Windows.Forms.IWin32Window sahip, SurumKaydi? kayit, string? canliYol)
    {
        if (kayit is null)
        {
            return "Bu satırda açılacak bir versiyon yok.";
        }

        if (Surumler.DogrudanAcilir(kayit.ArsivYolu))
        {
            return KlasorTarayici.DosyayiOku(kayit.ArsivYolu) is DosyaOgesi dosya
                ? DosyaAcici.Ac(sahip, dosya)
                    + "  (salt-okunur arşiv kopyası — düzenlemek için: Enter ile bu versiyona dön)"
                : "Arşiv kopyası okunamadı: " + kayit.ArsivYolu;
        }

        (string? kopya, string? sebep) =
            Surumler.GoruntulemeKopyasi(kayit.ArsivYolu, canliYol, kayit.No);

        if (kopya is null)
        {
            // Kullanici cift tikladi; bir cevap almali (CLAUDE.md 3).
            return $"v{kayit.No} açılamadı — {sebep}";
        }

        return KlasorTarayici.DosyayiOku(kopya) is DosyaOgesi ogesi
            ? DosyaAcici.Ac(sahip, ogesi)
                + $"  (salt-okunur görüntüleme kopyası: {WindowsYolu.DosyaAdi(kopya)}"
                + " — silmek serbest)"
            : "Görüntüleme kopyası okunamadı: " + kopya;
    }

    /// <summary>Cizilen siradaki versiyon kaydi; sira bir versiyon satiri degilse null.</summary>
    internal SurumKaydi? Kayit(int sira)
        => sira >= 0 && sira < _sonListe.Count ? _sonListe[sira] : null;

    private static void Aciklama(ReferansListesi liste, string cumle, string rol)
        => liste.Ekle(cumle, rol, -1, Renkler.UstBilgiYazi, hedefYol: null, tamMetin: cumle);
}
