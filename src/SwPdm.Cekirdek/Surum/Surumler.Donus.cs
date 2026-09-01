using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Bir versiyona donerken geri yazilabilecek COCUK.</summary>
/// <param name="ArsivYolu">O gunku kopyanin arsivdeki yolu.</param>
/// <param name="CanliYol">Bugunku dosyanin yolu (olmayabilir).</param>
/// <param name="Farkli">Bugunku icerik arsivdekinden farkli mi.</param>
/// <param name="Engel">Geri yazmaya engel; yoksa null.</param>
public sealed record DonusOgesi(string ArsivYolu, string CanliYol, bool Farkli, string? Engel);

/// <summary>
/// VERSIYONA DONUS - "bu versiyona don" akisinin cekirdegi.
///
/// Ayri dosya, cunku Surumler.cs 615 satira cikti ve boyut kapisinin siniri
/// 600 (CLAUDE.md 11). Konu ayrimi da dogru: otekiler arsive YAZIYOR, burasi
/// arsivden CANLI DOSYAYA yaziyor - tek geri donusu olan islem bu.
/// </summary>
public static partial class Surumler
{
    /// <summary>
    /// Dosyayi verilen versiyonun icerigine dondurur.
    ///
    /// SIRA (CLAUDE.md 1a - hicbir icerik kaybolmaz):
    ///   1. SOLIDWORKS'te acik gorunen dosyaya DOKUNULMAZ (~$ kilidi).
    ///   2. Bugunku icerik once OTOMATIK arsivlenir ("donmeden once" notuyla).
    ///   3. Eski icerik ayni klasorde geciciye kopyalanir, boyutu dogrulanir,
    ///      File.Replace ile oturur - yarim yazma dosyayi bozamaz.
    /// </summary>
    /// <param name="cocukYollari">
    /// Ayrica geri yazilacak COCUKLARIN canli yollari; null ya da bos ise
    /// yalnizca asil dosya doner (31.08.2026 oncesi davranis). Erkan'in ilk
    /// versiyon isteginin 3. maddesi: "montajin icinde parcayi sectigimde
    /// istedigim versiyona gore guncelleyebilmeliyim" - versiyon kendi
    /// kendine yettigi icin o gunku cocuk kopyalari arsivde hazir duruyor.
    /// </param>
    public static IslemRaporu Don(
        string kok, string yol, int no, IReadOnlyList<string>? cocukYollari = null)
    {
        if (Kilit.AcikMi(yol))
        {
            return new IslemRaporu(
                IslemSonucu.Kilitli, null,
                "Dosya SOLIDWORKS'te açık görünüyor (~$ kilidi) — kapatıp yeniden deneyin.");
        }

        SurumDurumu durum = Listele(kok, yol);
        if (!durum.Guvenilir)
        {
            return new IslemRaporu(IslemSonucu.Bilinmeyen, null, durum.Okunamadi);
        }

        SurumKaydi? hedef = null;
        foreach (SurumKaydi kayit in durum.Ogeler)
        {
            if (kayit.No == no)
            {
                hedef = kayit;
                break;
            }
        }

        if (hedef is null)
        {
            return new IslemRaporu(
                IslemSonucu.Bulunamadi, null, $"v{no} arşivde yok.");
        }

        // GUARD YIGILMASI DURDURULUR - ERKAN'DA OLCULDU (31.08.2026):
        // basarisiz bir donus denemesi her seferinde yeni bir "donmeden
        // once" kopyasi yigiyordu (v5/v6/v7 ayni icerik). Bugunku icerik en
        // son versiyonun kopyasiyla BIREBIR ayniysa yeniden arsivlemek
        // hicbir seyi korumaz; atlanir ve sebep cumleye yazilir.
        string? guardNotu = null;
        if (durum.Ogeler.Count > 0 && AyniIcerik(yol, durum.Ogeler[0].ArsivYolu))
        {
            guardNotu = $" (bugünkü hâl zaten v{durum.Ogeler[0].No}'da arşivli)";
        }
        else
        {
            IslemRaporu guvence = Olustur(kok, yol, $"v{no}'a dönmeden önce", out int _);
            if (!guvence.Oldu)
            {
                // Bugunku hal saklanamadiysa donus YAPILMAZ - aksi, mevcut
                // icerigi geri donussuz silmek olurdu (CLAUDE.md 1a).
                return new IslemRaporu(
                    guvence.Sonuc, null,
                    "Dönülmedi — bugünkü hâl arşivlenemedi: " + guvence.Sebebi);
            }
        }

        long arsivBoyutu;
        try
        {
            arsivBoyutu = new FileInfo(hedef.ArsivYolu).Length;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return IslemSonuclari.HatayiCevir(hata);
        }

        string? yazmaHatasi = GeriYaz(hedef.ArsivYolu, yol);
        if (yazmaHatasi is not null)
        {
            return new IslemRaporu(IslemSonucu.Bilinmeyen, null, yazmaHatasi);
        }

        // ---- COCUKLAR. Her biri KENDI icinde hepsi-ya-hicbiri: biri
        // arsivlenemez ya da kilitliyse ATLANIR ve sebebi yazilir; otekiler
        // durmaz. Yarim donen bir montaj, hic donmemis bir montajdan daha
        // kotu degil - ama SESSIZ kalmak yalan olurdu (CLAUDE.md 3).
        int yazilanCocuk = 0;
        var atlananlar = new List<string>();

        foreach (string cocuk in cocukYollari ?? [])
        {
            string? arsivdeki = ArsivdekiEsi(hedef.ArsivYolu, cocuk);
            if (arsivdeki is null)
            {
                atlananlar.Add(WindowsYolu.DosyaAdi(cocuk) + " (arşivde yok)");
                continue;
            }

            if (Kilit.AcikMi(cocuk))
            {
                atlananlar.Add(WindowsYolu.DosyaAdi(cocuk) + " (SOLIDWORKS'te açık)");
                continue;
            }

            // ONCE BUGUNKU HALI ARSIVLE - cocugun KENDI yuvasina. Bu olmadan
            // geri yazmak, cocugun bugunku halini geri donussuz silerdi (1a).
            if (!AyniIcerik(cocuk, arsivdeki))
            {
                IslemRaporu guvence = Olustur(kok, cocuk, $"v{no}'a dönmeden önce", out int _);
                if (!guvence.Oldu)
                {
                    atlananlar.Add(
                        WindowsYolu.DosyaAdi(cocuk) + " (bugünkü hâli arşivlenemedi)");
                    continue;
                }

                if (GeriYaz(arsivdeki, cocuk) is string cocukHatasi)
                {
                    atlananlar.Add(WindowsYolu.DosyaAdi(cocuk) + " — " + cocukHatasi);
                    continue;
                }
            }

            yazilanCocuk++;
        }

        string? kayitNotu = arsivBoyutu == hedef.Boyut
            ? null
            : $" (not: kayıt {hedef.Boyut} bayt diyordu, arşiv {arsivBoyutu} — arşivdeki içerik esas alındı)";

        string cocukNotu = yazilanCocuk > 0 ? $" · {yazilanCocuk} çocuk geri yazıldı" : "";
        string atlamaNotu = atlananlar.Count > 0
            ? " · atlandı: " + string.Join(", ", atlananlar)
            : "";

        return new IslemRaporu(
            IslemSonucu.Tamam, yol,
            (guardNotu ?? "") + (kayitNotu ?? "") + cocukNotu + atlamaNotu);
    }

    /// <summary>
    /// Arsivdeki bir kopyayi canli dosyanin uzerine oturtur:
    /// KOPYALA -> DOGRULA -> Replace. Doner: hata sebebi, olduysa null.
    ///
    /// TEK KOPYA (CLAUDE.md 8): ayni sira hem asil dosya hem cocuklar icin
    /// gecerli; ikinci bir yazma yolu acmak, birinin dogrulamasiz kalmasi
    /// demekti.
    /// </summary>
    private static string? GeriYaz(string arsivYolu, string canliYol)
    {
        string gecici = canliYol + ".swpdm-don";
        try
        {
            long kaynak = new FileInfo(arsivYolu).Length;
            File.Copy(arsivYolu, gecici, overwrite: true);

            // File.Copy OZNITELIGI DE kopyaliyor: arsiv salt-okunur, gecici
            // de salt-okunur dogar. Temizlenmezse Replace sonrasi CANLI
            // dosya salt-okunur kalir ve SOLIDWORKS kaydedemez olur.
            File.SetAttributes(gecici, FileAttributes.Normal);

            // DOGRULAMA KAYNAGA GORE - ERKAN'DA OLCULDU (31.08.2026):
            // once kopya KAYITTAKI boyutla karsilastiriliyordu; kayitla
            // dosyasi ayrismis bir versiyonda (68197 ≠ 62729) donus her
            // denemede reddediliyor ve kullanici TIKALI kaliyordu. Kopya
            // sadakati kopyalanan KAYNAGA gore olculur; kayit farki ise
            // gizlenmez, asagida cumleye yazilir (CLAUDE.md 3).
            long kopya = new FileInfo(gecici).Length;
            if (kopya != kaynak)
            {
                File.Delete(gecici);
                return $"Kopya doğrulanamadı ({kopya} ≠ {kaynak} bayt) — dosyaya dokunulmadı.";
            }

            File.Replace(gecici, canliYol, destinationBackupFileName: null);
            return null;
        }
        catch (Exception hata)
        {
            TemizlemeyeCalis(gecici);
            return IslemSonuclari.HatayiCevir(hata).Sebebi;
        }
    }

    /// <summary>
    /// Canli bir cocugun ARSIVDEKI esi: ayni versiyon klasorunde AYNI ADLA
    /// duran kopya. Ad esitligi yeter - cocuklar arsive gercek adlariyla,
    /// duz olarak konuyor (bkz. Olustur).
    /// </summary>
    private static string? ArsivdekiEsi(string asilArsivYolu, string canliCocuk)
    {
        string klasor = WindowsYolu.Klasor(asilArsivYolu);
        string aday = WindowsYolu.Birlestir(klasor, WindowsYolu.DosyaAdi(canliCocuk));
        return File.Exists(aday) ? aday : null;
    }

    /// <summary>
    /// Bir versiyona donerken GERI YAZILABILECEK cocuklarin listesi.
    ///
    /// Kullanicinin karar verebilmesi icin her satir uc seyi soyluyor:
    /// bugunku dosya NEREDE, arsivdekiyle FARKLI mi, ve bir ENGEL var mi.
    /// Karari arayuz veriyor; cekirdek yalniz olcuyor (CLAUDE.md 1b).
    /// </summary>
    public static IReadOnlyList<DonusOgesi> DonusListesi(string kok, string yol, int no)
    {
        var liste = new List<DonusOgesi>();

        SurumDurumu durum = Listele(kok, yol);
        SurumKaydi? hedef = null;
        foreach (SurumKaydi kayit in durum.Ogeler)
        {
            if (kayit.No == no)
            {
                hedef = kayit;
                break;
            }
        }

        if (hedef is null)
        {
            return liste;
        }

        string asilAdi = WindowsYolu.DosyaAdi(hedef.ArsivYolu);
        string canliKlasor = WindowsYolu.Klasor(yol);

        try
        {
            foreach (string arsivdeki in Directory.GetFiles(WindowsYolu.Klasor(hedef.ArsivYolu)))
            {
                string ad = WindowsYolu.DosyaAdi(arsivdeki);
                if (string.Equals(ad, asilAdi, StringComparison.OrdinalIgnoreCase))
                {
                    continue;   // asil dosya listede degil; o zaten donuyor
                }

                // BUGUNKU KARSILIGI: once ebeveynin yani (SOLIDWORKS'un
                // kurali, CLAUDE.md 5), sonra kokun altinda ayni ad.
                string canli = WindowsYolu.Birlestir(canliKlasor, ad);
                string? engel = File.Exists(canli)
                    ? (Kilit.AcikMi(canli) ? "SOLIDWORKS'te açık" : null)
                    : "bugün bu klasörde yok";

                liste.Add(new DonusOgesi(
                    arsivdeki, canli, engel is null && !AyniIcerik(canli, arsivdeki), engel));
            }
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Arsiv okunamiyorsa liste bos doner; "dön" yine asil dosyayi
            // yazar - eksigi arayuz soyler.
        }

        return liste;
    }
}
