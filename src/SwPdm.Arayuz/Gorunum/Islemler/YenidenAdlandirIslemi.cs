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
                baglam.Sahip, rapor.Sebep ?? "Bilinmeyen sebep.",
                "Adı değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            baglam.Bildir("Adı değiştirilemedi — " + eskiAd);
            return;
        }

        if (rapor.YeniYol is string yeniYol)
        {
            GeriAlDefteri.Kaydet(GeriAlmasi(yeniYol, eskiAd, eskiAd, yeniAd));
        }

        baglam.Tazele(rapor.YeniYol);
        baglam.Bildir($"{eskiAd} → {yeniAd}");

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
                baglam.Sahip, sonuc.Sebep ?? "Bilinmeyen sebep.",
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
            $"{eskiAd} → {yeniAd} · onu kullanan {sonuc.Onarilanlar.Count} dosya onarıldı");
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
                    olmayan.Add(yeniAd + " — " + (geri.Sebep ?? "bilinmeyen sebep"));
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
            Ters: () => GeriAlmasi(
                WindowsYolu.Birlestir(WindowsYolu.Klasor(yol), hedefAd),
                WindowsYolu.DosyaAdi(yol), eskiAd, yeniAd),
            Uygula: baglam =>
            {
                var olmayan = new List<string>();
                IslemRaporu rapor = DosyaIslemleri.YenidenAdlandir(yol, hedefAd);
                if (!rapor.Oldu)
                {
                    olmayan.Add(hedefAd + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
                }

                return olmayan;
            });
}
