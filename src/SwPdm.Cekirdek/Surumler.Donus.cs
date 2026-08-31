using System;
using System.IO;

namespace SwPdm.Cekirdek;

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
    public static IslemRaporu Don(string kok, string yol, int no)
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

        string gecici = yol + ".swpdm-don";
        long arsivBoyutu;
        try
        {
            arsivBoyutu = new FileInfo(hedef.ArsivYolu).Length;
            File.Copy(hedef.ArsivYolu, gecici, overwrite: true);

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
            if (kopya != arsivBoyutu)
            {
                File.Delete(gecici);
                return new IslemRaporu(
                    IslemSonucu.Bilinmeyen, null,
                    $"Kopya doğrulanamadı ({kopya} ≠ {arsivBoyutu} bayt) — dosyaya dokunulmadı.");
            }

            File.Replace(gecici, yol, destinationBackupFileName: null);
        }
        catch (Exception hata)
        {
            TemizlemeyeCalis(gecici);
            return IslemSonuclari.HatayiCevir(hata);
        }

        string? kayitNotu = arsivBoyutu == hedef.Boyut
            ? null
            : $" (not: kayıt {hedef.Boyut} bayt diyordu, arşiv {arsivBoyutu} — arşivdeki içerik esas alındı)";

        return new IslemRaporu(IslemSonucu.Tamam, yol, (guardNotu ?? "") + (kayitNotu ?? ""));
    }
}
