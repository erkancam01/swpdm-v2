using System;
using System.Collections.Generic;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// AKTARMANIN GERI ALINMASI - tasima ve kopyalama icin Ctrl+Z adimlari.
///
/// NEDEN AYRI DOSYA: aktarma motoru (TasiIslemi.cs) boyut kapisini asti
/// (607 > 600) ve kapi dogru davrandi - "geri alma" kendi basina bir konu.
/// Ileri yon orada, GERI yon burada; ikisi de tek yerde (CLAUDE.md 1b).
///
/// BURADAKI ORTAK KURAL: geri alma islemin BUTUN etkisini geri almalidir.
/// "Degistir" ile uzerine yazilan dosya cope gidiyor; onu geride birakan bir
/// geri alma, kullaniciya "geri alindi" der ve dosyayi copte unutur
/// (CLAUDE.md 3).
/// </summary>
internal static class AktarmaGeriAlma
{
    /// <summary>
    /// Tasinanlari eski klasorlerine geri gonderir - VE onarimi da.
    ///
    /// CIFTLER TASINIYOR, iki ayri liste degil: onceden "kaynaklar" ve
    /// "olan" listeleri ayni indisle eslestiriliyordu ve bir oge
    /// tasinamayinca hizalama KAYIYORDU - yani geri alma dosyayi YANLIS
    /// klasore gonderebilirdi. Cift tutmak bunu imkansiz kiliyor.
    ///
    /// ONARIM DA GERI ALINIR: yalnizca dosyalar geri tasinsaydi, ebeveynler
    /// yeni yola bakar halde kalir ve GERI ALMA referansi KIRARDI.
    /// </summary>
    internal static GeriAlinabilir TasimayiGeriAl(
        IReadOnlyList<(string Eski, string Yeni)> ciftler,
        IReadOnlyList<OnarimPlani> onarilanPlanlar,
        IReadOnlyList<string> copeGidenler,
        string? cop)
    {
        var kopya = new List<(string Eski, string Yeni)>(ciftler);
        var planlar = new List<OnarimPlani>(onarilanPlanlar);
        var kurtarilanlar = new List<string>(copeGidenler);

        return new GeriAlinabilir(
            $"{kopya.Count} öğenin taşınması",
            // ILERI ALMA yalnizca "Degistir" KULLANILMADIYSA verilir.
            // Sebebi somut: uzerine yazilan dosya geri alma sirasinda
            // copten geri geldi; ileri alirken onu YENIDEN cope gondermek
            // gerekirdi ve o dosya bu arada degismis olabilir. Tahmin
            // etmektense ileri almayi HIC teklif etmiyoruz (CLAUDE.md 1a);
            // Ctrl+Y sebebini soyluyor.
            Ters: kurtarilanlar.Count > 0
                ? null
                : () => TasimayiYineYap(kopya, planlar, cop),
            Uygula: baglam =>
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

                // TASINANLAR GERI GITTIKTEN SONRA: hedef klasor artik bos,
                // yani "Degistir" ile uzerine yazilan eski dosya kendi ADIYLA
                // geri gelebilir. Once yapilsaydi ad cakisir ve dosya
                // "(2)" olarak donerdi.
                CoptenGeriAl(cop, kurtarilanlar, olmayan);

                var dokunulan = new List<string>();
                foreach ((string eski, _) in kopya)
                {
                    dokunulan.Add(eski);
                }

                baglam.Referanslar.Tazele(dokunulan);
                return olmayan;
            });
    }

    /// <summary>
    /// ILERI ALMA: tasimayi yeniden yapar - dosyalari yeni yerine gonderir
    /// ve onarimi yeniden uygular.
    ///
    /// GERI ALMANIN AYNASI: orada once onarim geri alinip sonra dosyalar
    /// tasiniyordu; burada once dosyalar tasinir, SONRA onarim uygulanir -
    /// cunku yama her iki halde de dosya HEDEFTEYKEN yazilmali.
    /// </summary>
    private static GeriAlinabilir TasimayiYineYap(
        IReadOnlyList<(string Eski, string Yeni)> ciftler,
        IReadOnlyList<OnarimPlani> planlar,
        string? cop)
    {
        var kopya = new List<(string Eski, string Yeni)>(ciftler);
        var kopyaPlanlar = new List<OnarimPlani>(planlar);

        return new GeriAlinabilir(
            $"{kopya.Count} öğenin taşınması",
            Ters: () => TasimayiGeriAl(kopya, kopyaPlanlar, [], cop),
            Uygula: baglam =>
            {
                var olmayan = new List<string>();

                foreach ((string eski, string yeni) in kopya)
                {
                    IslemRaporu rapor = DosyaIslemleri.Tasi(eski, WindowsYolu.Klasor(yeni));
                    if (!rapor.Oldu)
                    {
                        olmayan.Add(WindowsYolu.DosyaAdi(eski)
                            + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
                    }
                }

                // Dosyalar yeni yerinde: yamalar yeniden yazilabilir.
                ReferansOnarimi.YenidenOnar(kopyaPlanlar);

                var dokunulan = new List<string>();
                foreach ((_, string yeni) in kopya)
                {
                    dokunulan.Add(yeni);
                }

                baglam.Referanslar.Tazele(dokunulan);
                return olmayan;
            });
    }

    /// <summary>
    /// Panoyu YALNIZCA hak edince bosaltir.
    ///
    /// OLCULDU (29.08.2026): eskiden her aktarmadan sonra kosulsuz
    /// bosaliyordu. Sonucu: "kopyala, sonra iki ayri klasore yapistir"
    /// CALISMIYORDU ve sebebi hicbir yerde yazmiyordu; iptal edilen ya da
    /// tamamen basarisiz bir yapistirma da panoyu siliyordu.
    ///
    /// Kes'te bosaltmak DOGRU: kesilen ogeler artik eski yerinde yok.
    /// Kopyala'da pano DURUR - Gezgin de boyle yapar.
    /// </summary>
    internal static void Panoyu(AktarmaKipi kip, bool enAzBirOldu)
    {
        if (kip == AktarmaKipi.Tasi && enAzBirOldu)
        {
            Pano.Bosalt();
        }
    }

    /// <summary>
    /// "Degistir" ile cope alinan dosyalari geri yukler.
    ///
    /// NEDEN GERI ALMANIN PARCASI: yalnizca kendi tasidigimizi geri koymak,
    /// hedef klasordeki ESKI dosyayi copte birakiyordu - ve kullanici
    /// "geri alindi" mesajini goruyordu (CLAUDE.md 3). Bulunamayan ya da
    /// adi degisen her dosya tek tek SAYILIYOR.
    /// </summary>
    internal static void CoptenGeriAl(
        string? cop, IReadOnlyList<string> yollar, List<string> olmayan)
    {
        if (yollar.Count == 0)
        {
            return;
        }

        if (cop is null)
        {
            foreach (string yol in yollar)
            {
                olmayan.Add(WindowsYolu.DosyaAdi(yol)
                    + " — üzerine yazılan eski dosya çöpte kaldı (çöp kutusu bulunamadı).");
            }

            return;
        }

        CopDurumu durum = Cop.Oku(cop);
        if (!durum.Guvenilir)
        {
            foreach (string yol in yollar)
            {
                olmayan.Add(WindowsYolu.DosyaAdi(yol)
                    + " — üzerine yazılan eski dosya çöpte kaldı: " + durum.Okunamadi);
            }

            return;
        }

        foreach (string yol in yollar)
        {
            CopOgesi? oge = null;
            foreach (CopOgesi aday in durum.Ogeler)
            {
                if (string.Equals(aday.EskiYol, yol, StringComparison.OrdinalIgnoreCase))
                {
                    oge = aday;
                    break;
                }
            }

            if (oge is null)
            {
                olmayan.Add(WindowsYolu.DosyaAdi(yol)
                    + " — üzerine yazılan eski dosya çöp kutusunda bulunamadı.");
                continue;
            }

            IslemRaporu rapor = Cop.GeriYukle(cop, oge);
            if (!rapor.Oldu)
            {
                olmayan.Add(WindowsYolu.DosyaAdi(yol) + " — üzerine yazılan eski dosya geri "
                    + "yüklenemedi: " + (rapor.Sebep ?? "bilinmeyen sebep"));
            }
            else if (!string.Equals(rapor.YeniYol, yol, StringComparison.OrdinalIgnoreCase))
            {
                olmayan.Add(WindowsYolu.DosyaAdi(yol) + " — eski dosya geri geldi ama adı "
                    + "değişti: " + WindowsYolu.DosyaAdi(rapor.YeniYol ?? yol));
            }
        }
    }

    /// <summary>Olusan kopyalari cope gonderir - kalici silmez.</summary>
    internal static GeriAlinabilir KopyalamayiGeriAl(
        IReadOnlyList<string> yeniYollar, IReadOnlyList<string> copeGidenler, string? copKlasoru)
    {
        var yollar = new List<string>(yeniYollar);
        var kurtarilanlar = new List<string>(copeGidenler);

        return new GeriAlinabilir(
            $"{yollar.Count} öğenin kopyalanması",
            // "Degistir" kullanildiysa ileri alma yok - tasimadaki ayni
            // gerekce.
            Ters: kurtarilanlar.Count > 0
                ? null
                : () => KopyalariGeriGetir(yollar, copKlasoru),
            Uygula: baglam =>
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

                // Kopyalamada da "Degistir" secilmis olabilir; uzerine yazilan
                // eski dosya, kopyalar kalktiktan SONRA kendi adiyla geri gelir.
                CoptenGeriAl(copKlasoru, kurtarilanlar, olmayan);
                return olmayan;
            });
    }

    /// <summary>
    /// ILERI ALMA: geri alma sirasinda cope gonderilen KOPYALARI eski
    /// yerlerine dondurur. Yeniden kopyalamaktan daha dogru: kaynak dosya
    /// bu arada degismis olabilir, kullanicinin geri aldigi ise o ANKI
    /// kopyaydi.
    /// </summary>
    private static GeriAlinabilir KopyalariGeriGetir(
        IReadOnlyList<string> yollar, string? copKlasoru)
    {
        var kopya = new List<string>(yollar);

        return new GeriAlinabilir(
            $"{kopya.Count} öğenin kopyalanması",
            Ters: () => KopyalamayiGeriAl(kopya, [], copKlasoru),
            Uygula: baglam =>
            {
                var olmayan = new List<string>();
                CoptenGeriAl(baglam.Secim.CopKlasoru ?? copKlasoru, kopya, olmayan);
                return olmayan;
            });
    }
}
