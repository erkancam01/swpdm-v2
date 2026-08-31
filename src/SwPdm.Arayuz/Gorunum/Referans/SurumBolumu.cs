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

            liste.Ekle(
                ad,
                Zaman.Yaz(kayit.Zaman),
                simgeSirasi: -1,
                Renkler.ReferansAsagiYazi,
                hedefYol: null,
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

    /// <summary>Cizilen siradaki versiyon kaydi; sira bir versiyon satiri degilse null.</summary>
    internal SurumKaydi? Kayit(int sira)
        => sira >= 0 && sira < _sonListe.Count ? _sonListe[sira] : null;

    private static void Aciklama(ReferansListesi liste, string cumle, string rol)
        => liste.Ekle(cumle, rol, -1, Renkler.UstBilgiYazi, hedefYol: null, tamMetin: cumle);
}
