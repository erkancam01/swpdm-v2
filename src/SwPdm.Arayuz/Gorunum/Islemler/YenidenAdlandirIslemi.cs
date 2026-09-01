using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YENIDEN ADLANDIR. Uzanti uyarisi ve referans uyarisi burada; ad kutusunun
/// KENDISI ortak arac (Islemler/AdKutusu.cs) cunku "yeni klasor" de onu
/// kullaniyor - CLAUDE.md 1b'nin 3. kurali: ortak arac, ozellik degil.
/// </summary>
internal sealed class YenidenAdlandirIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Yeniden adlandır";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.F2;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (secim.Ogeler.Count == 0)
        {
            nedenOlmaz = "Önce bir öğe seçin.";
            return false;
        }

        if (secim.Ogeler.Count > 1)
        {
            nedenOlmaz = "Aynı anda tek bir öğenin adı değiştirilebilir.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
        // ONCE TARA: kimin kullandigi bilinmeden onarim yapilamaz ve kutudaki
        // sayi yanlis olur. Tarama iptal edilirse ad da degismez.
        => ReferansTazeleme.Once(baglam, () => Adlandir(baglam));

    private static void Adlandir(IslemBaglami baglam)
    {
        object? oge = baglam.Secim.TekOge;
        string? yol = SecimBaglami.Yolu(oge);
        if (oge is null || yol is null)
        {
            return;
        }

        string eskiAd = SecimBaglami.Adi(oge);

        // AD KUTUSU ORTAK ARAC (Islemler/AdKutusu.cs): dogrulama, uzunluk
        // siniri, cakisma uyarisi ve UZANTI KILIDI orada tek kopya. Uzanti
        // artik ayri ve kilitli geliyor - kaza ile degismesi imkansiz.
        string? yeniAd = AdKutusu.Sor(
            baglam.Sahip, "Yeniden adlandır", eskiAd,
            WindowsYolu.Klasor(yol), oge is DosyaOgesi);

        if (yeniAd is null || yeniAd == eskiAd)
        {
            return;
        }

        // UZANTI UYARISI ARTIK AYRI KUTU DEGIL: onarim kutusunun icine
        // bir satir olarak giriyor (28.08.2026). Iki kutu ust uste
        // cikiyordu ve ikincisi birincinin uzerini ortuyordu.
        string? uzantiUyarisi =
            oge is DosyaOgesi
            && !string.Equals(WindowsYolu.Uzanti(eskiAd), WindowsYolu.Uzanti(yeniAd),
                StringComparison.OrdinalIgnoreCase)
                ? $"DİKKAT: uzantı değişiyor ({WindowsYolu.Uzanti(eskiAd)} → "
                  + $"{WindowsYolu.Uzanti(yeniAd)}). Dosya kullanılamaz hale gelebilir."
                : null;

        // REFERANS ONARIMI. Bir SOLIDWORKS dosyasinin adi degisince onu
        // kullanan montaj/teknik resim ESKI ADI arar; komsuluk kurali da
        // kurtarmiyor (CLAUDE.md 5). Tek cozum ebeveynin ICINE yazmak - ve
        // bunun calistigi Erkan'in makinesinde OLCULDU (28.08.2026).
        if (SwReferans.TasiyabilirMi(yol))
        {
            OnarimPlani plan = ReferansOnarimi.Planla(baglam.Referanslar.Indeks, yol, yeniAd);
            switch (OnarimKutusu.Sor(baglam.Sahip, plan, eskiAd, uzantiUyarisi))
            {
                case OnarimKarari.Vazgec:
                    baglam.Bildir("Ad değiştirme iptal edildi.");
                    return;

                case OnarimKarari.Onar:
                    Onar(baglam, plan, eskiAd, yeniAd);
                    return;

                default:
                    break;   // onarmadan devam
            }
        }

        // SOLIDWORKS dosyasi degilse onarim kutusu hic acilmaz; uzanti
        // uyarisi yine de gosterilmeli.
        if (!SwReferans.TasiyabilirMi(yol) && uzantiUyarisi is not null
            && !OnayKutusu.Sor(baglam.Sahip, "Adı değiştir", uzantiUyarisi, tehlikeli: true))
        {
            baglam.Bildir("Ad değiştirme iptal edildi.");
            return;
        }

        IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(yol, yeniAd);

        if (!rapor.Oldu)
        {
            MessageBox.Show(
                baglam.Sahip, rapor.Sebebi,
                "Adı değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            baglam.Bildir("Adı değiştirilemedi — " + eskiAd);
            return;
        }

        // ============ KLASOR ADI DEGISINCE DISARIDAKI EBEVEYNLER ============
        //
        // ERKAN'DA KIRILDI (31.08.2026, "aynı sorun devam ediyor"): 55\
        // klasoru 56 yapilinca, disaridaki Montaj1.SLDASM'in icinde yazili
        // "..\55\...Parça1.SLDPRT" oldugu gibi kaldi ve referans BAYATLADI.
        //
        // Klasor adi degistirmek, icindeki HER dosyayi tasimaktir - o yuzden
        // tasima motorunun makinesi kullaniliyor (CLAUDE.md 8, ikinci kopya
        // yok): TasimaPlanlari klasor ciftini dosya ciftlerine ACAR, birlikte
        // giden ic ebeveynleri ELER (SOLIDWORKS once komsuya bakiyor ve
        // goreli yolun derinligi degismedi - CLAUDE.md 5), disarida kalanlari
        // yamalar. Plan RENAME'DEN SONRA kurulmak zorunda: acilim yeni
        // klasoru diskten sayarak yapiliyor.
        int onarilan = 0;
        IReadOnlyList<string> onarimHatalari = [];
        IReadOnlyList<OnarimPlani> tutan = [];
        string? onarimSebebi = null;

        if (oge is not DosyaOgesi && rapor.YeniYol is string yeniKlasor)
        {
            (IReadOnlyList<OnarimPlani> planlar, onarimSebebi) =
                ReferansOnarimi.TasimaPlanlari(
                    baglam.Referanslar.Indeks, [(yol, yeniKlasor)], [yol]);
            (onarilan, onarimHatalari, tutan) = ReferansOnarimi.Onar(planlar);
        }

        if (rapor.YeniYol is string yeniYol)
        {
            // Onarim varsa geri alma IKISINI birden cozmeli: yalniz adi geri
            // dondurmek, ebeveynleri yeni ada bakar halde birakirdi. Tasima
            // geri almasi tam bunu yapiyor (once GeriOnar, sonra Move).
            GeriAlDefteri.Kaydet(tutan.Count > 0
                ? AktarmaGeriAlma.TasimayiGeriAl(
                    [(yol, yeniYol)], tutan, [], baglam.Secim.CopKlasoru)
                : GeriAlmasi(yeniYol, eskiAd, eskiAd, yeniAd));
        }

        // ONARIM HATASI KUTUYLA soylenir (CLAUDE.md 6'nin 2. sebebi): durum
        // cubugunda kalsa kullanici klasoru adlandirdim sanir, referanslar
        // sessizce kirik kalirdi - tam da bugunku sikayet.
        if (onarimHatalari.Count > 0)
        {
            MessageBox.Show(
                baglam.Sahip,
                "Klasör adlandı ama şu dosyalar onarılamadı:\n\n  "
                + string.Join("\n  ", onarimHatalari)
                + "\n\nKlasörün adı DEĞİŞTİ; bu dosyaların içine yazılamadı."
                + "\nCtrl+Shift+L ile elle bağlayabilirsiniz.",
                "Referanslar onarılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        baglam.Tazele(rapor.YeniYol);
        baglam.Bildir(
            $"{eskiAd} → {yeniAd}"
            + (onarilan > 0 ? $" · dışarıdan kullanan {onarilan} dosya onarıldı" : "")
            + (onarimSebebi is not null ? $" · onarım yapılamadı: {onarimSebebi}" : "")
            // Cekirdek versiyon arsivini de tasidi; tasiyamadiysa SEBEBI
            // burada gorunur (CLAUDE.md 3 - sessiz gecilmez).
            + (rapor.Sebep is { Length: > 0 } arsiv ? " · " + arsiv : ""));

        // Dokunulan iki yol biliniyor; butun kok taranmiyor. Klasor
        // adlandirildiysa Sonra kendisi tam taramaya duser.
        ReferansTazeleme.Sonra(baglam, [yol, rapor.YeniYol ?? yol]);
    }

    /// <summary>
    /// Adi degistirir VE onu kullanan dosyalari onarir - hepsi ya da hicbiri.
    /// Sonuc SAYIYLA yaziliyor; "oldu" demek yetmez (CLAUDE.md 10).
    /// </summary>
    private static void Onar(IslemBaglami baglam, OnarimPlani plan, string eskiAd, string yeniAd)
    {
        OnarimSonucu sonuc = ReferansOnarimi.Uygula(plan);
        if (!sonuc.Oldu)
        {
            MessageBox.Show(
                baglam.Sahip, sonuc.Sebebi,
                "Onarılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            baglam.Bildir("Onarılamadı — " + eskiAd);
            return;
        }

        string yeniYol = WindowsYolu.Birlestir(WindowsYolu.Klasor(plan.EskiYol), yeniAd);

        // INDEKS TAZELENIYOR: yoksa referans paneli artik olmayan bir adi
        // "kullaniyor" diye gosterir (CLAUDE.md 3).
        baglam.Referanslar.Tazele([yeniYol, .. sonuc.Onarilanlar]);

        GeriAlDefteri.Kaydet(OnarimiGeriAl(yeniYol, plan.EskiYol, eskiAd, yeniAd, sonuc.Onarilanlar));

        baglam.Tazele(yeniYol);
        baglam.Bildir(
            $"{eskiAd} → {yeniAd} · onu kullanan {sonuc.Onarilanlar.Count} dosya onarıldı"
            + (sonuc.Sebep is { Length: > 0 } arsiv ? " · " + arsiv : ""));
        ReferansTazeleme.Sonra(baglam, [plan.EskiYol, yeniYol, .. sonuc.Onarilanlar]);
    }

    /// <summary>
    /// Onarimi GERI ALIR: adi eskiye dondurur VE ebeveynleri geri onarir.
    ///
    /// EBEVEYN LISTESI BURADA TASINIYOR, indekse yeniden sorulmuyor: indeks
    /// ad degisiminden sonra yeni adi bilmez ve sifir ebeveyn dondururdu -
    /// yani geri alma dosyayi eski adina dondurup ebeveynleri YENI ada bakar
    /// halde birakirdi. Referansi geri alma KIRARDI.
    /// </summary>
    private static GeriAlinabilir OnarimiGeriAl(
        string yeniYol, string eskiYol, string eskiAd, string yeniAd,
        IReadOnlyList<string> ebeveynler)
        => new(
            $"\"{eskiAd}\" → \"{yeniAd}\" adlandırması ve {ebeveynler.Count} onarım",
            // DOSYANIN IKI HALI DE + ICINE YAZILACAK EBEVEYNLER: onarim
            // ebeveynlerin ICINE yaziyor, yani onlar da dokunulan yol.
            Yollar: [eskiYol, yeniYol, .. ebeveynler],
            // ILERI ALMA: ayni sey ters yone. Onarim planinin kendisi
            // simetrik (PlanlaBilinenlerle iki yolu da aliyor), o yuzden
            // yalnizca yollar takas ediliyor.
            Ters: () => OnarimiGeriAl(eskiYol, yeniYol, eskiAd, yeniAd, ebeveynler),
            Uygula: baglam =>
            {
                var olmayan = new List<string>();
                OnarimSonucu geri = ReferansOnarimi.Uygula(
                    ReferansOnarimi.PlanlaBilinenlerle(ebeveynler, yeniYol, eskiYol));

                if (!geri.Oldu)
                {
                    olmayan.Add(yeniAd + " — " + geri.Sebebi);
                    return olmayan;
                }

                baglam.Referanslar.Tazele([eskiYol, .. ebeveynler]);
                return olmayan;
            });

    /// <summary>
    /// Geri alma: adi <paramref name="hedefAd"/>'a dondurur.
    ///
    /// TERSI AYNI FONKSIYON, ARGUMANLARI TAKAS EDILMIS: adlandirma kendi
    /// tersini tasiyan bir islem. Bu yuzden ileri alma (Ctrl+Y) burada
    /// ayri bir kod istemiyor - ve ayrismasi da imkansiz (CLAUDE.md 8).
    /// </summary>
    private static GeriAlinabilir GeriAlmasi(string yol, string hedefAd, string eskiAd, string yeniAd)
        => new(
            $"\"{eskiAd}\" → \"{yeniAd}\" adlandırması",
            Yollar: [yol, WindowsYolu.Birlestir(WindowsYolu.Klasor(yol), hedefAd)],
            Ters: () => GeriAlmasi(
                WindowsYolu.Birlestir(WindowsYolu.Klasor(yol), hedefAd),
                WindowsYolu.DosyaAdi(yol), eskiAd, yeniAd),
            Uygula: baglam =>
            {
                var olmayan = new List<string>();
                IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(yol, hedefAd);
                if (!rapor.Oldu)
                {
                    olmayan.Add(hedefAd + " — " + rapor.Sebebi);
                }

                return olmayan;
            });
}
