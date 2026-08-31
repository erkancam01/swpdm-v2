using System;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>
/// VERSIYONU ACILABILIR HALE GETIRME - "goruntuleme kopyasi".
///
/// NEDEN (Erkan, 31.08.2026: "part dosyasının versiyonlarını açabiliyorum ama
/// montaj ve teknik resim dosyalarının versiyonlarını açamıyorum, açınca hata
/// veriyor"): arsiv kopyasi ".SwPdmSurum\&lt;dosya&gt;\v3.SLDASM" icinde TEK
/// BASINA durur. SOLIDWORKS once ebeveynin yanina bakiyor (CLAUDE.md 5) ve
/// arsiv klasorunde parca yok; yazili goreli yol da arsiv klasorune gore
/// cozulup bosa cikiyor. Parcada gorulmuyor cunku parcanin cocugu yok.
///
/// COZUM: arsiv kopyasi, OZGUN DOSYANIN KENDI KLASORUNE gecici bir adla
/// cikariliyor ve o aciliyor - parcalar yaninda oldugu icin komsuluk kurali
/// isliyor. (Erkan'in secimi, 31.08.2026.)
///
/// KARAR TIPE GORE DEGIL OLCUME GORE: "montaj/teknik resimse kopyala" demek
/// yanlis olurdu - turetilmis (derived) bir PARCANIN da dis referansi olabilir.
/// Tek soru soruluyor: bu belgenin dogrudan referansi var mi.
/// </summary>
public static partial class Surumler
{
    /// <summary>
    /// Arsiv kopyasi OLDUGU YERDEN acilabilir mi - yani cocugu yok mu.
    ///
    /// Okunamayan bir belgede TRUE doner: kopya cikarmak da onu okumayi
    /// gerektirmiyor ve bugunku davranis (dogrudan ac) korunuyor; yanlis
    /// tarafta hata yapmak gereksiz dosya uretirdi.
    /// </summary>
    public static bool DogrudanAcilir(string? arsivYolu)
    {
        if (string.IsNullOrWhiteSpace(arsivYolu))
        {
            return true;
        }

        try
        {
            return SwReferans.Oku(arsivYolu).Dogrudan.Count == 0;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// Versiyonu OZGUN DOSYANIN YANINA cikarir ve o kopyanin yolunu doner.
    ///
    /// Ad: "&lt;taban&gt; ~v&lt;no&gt;&lt;uzanti&gt;" - kendini anlatiyor,
    /// kullanici agacta gorup silebilir. GIZLENMIYOR (CLAUDE.md 3): diske
    /// yazdigimiz her seyi kullanici gormeli.
    ///
    /// UC KURAL:
    ///   - Kopya SALT-OKUNUR birakilir: gecmisin ustune kaza ile kaydedilemez.
    ///   - Ayni ad zaten VARSA ve arsivle BIREBIR aynıysa yeniden yazilmaz,
    ///     o kopya kullanilir (ikinci, ucuncu tik dosya yigmaz).
    ///   - Ayni adda YABANCI bir dosya varsa USTUNE YAZILMAZ: numarali ada
    ///     dusulur ("Montaj1 ~v3 (2).SLDASM"). Kullanicinin dosyasini yok
    ///     etmektense ikinci bir kopya birakmak yeglenir (CLAUDE.md 1a).
    /// </summary>
    /// <returns>Kopyanin yolu; olmadiysa Yol null ve SEBEP dolu.</returns>
    public static (string? Yol, string? Sebep) GoruntulemeKopyasi(
        string? arsivYolu, string? canliYol, int no)
    {
        if (string.IsNullOrWhiteSpace(arsivYolu) || !File.Exists(arsivYolu))
        {
            return (null, "Arşiv kopyası bulunamadı.");
        }

        if (string.IsNullOrWhiteSpace(canliYol))
        {
            return (null, "Dosyanın kendi yolu bilinmiyor.");
        }

        string klasor = WindowsYolu.Klasor(canliYol);
        string ad = WindowsYolu.DosyaAdi(canliYol);
        string uzanti = WindowsYolu.Uzanti(ad);
        string taban = uzanti.Length > 0 ? ad[..^uzanti.Length] : ad;
        string istenen = $"{taban} ~v{no}{uzanti}";
        string hedef = WindowsYolu.Birlestir(klasor, istenen);

        // ONCEKI KOPYA AYNIYSA YENIDEN YAZMA. AyniIcerik tek kopya (CLAUDE.md 8);
        // ayrica salt-okunur bir dosyanin uzerine File.Copy zaten dusmez.
        if (File.Exists(hedef) && AyniIcerik(hedef, arsivYolu))
        {
            return (hedef, null);
        }

        if (File.Exists(hedef) || Directory.Exists(hedef))
        {
            // Yabanci dosya: Gezgin'in kalibiyla numaralanir. BosAdBul tek
            // kopya - "Yeni klasör (2)" mantiginin ayni yeri.
            string? bos = DosyaIslemleri.BosAdBul(klasor, istenen);
            if (bos is null)
            {
                return (null, $"\"{istenen}\" için boş ad bulunamadı.");
            }

            hedef = WindowsYolu.Birlestir(klasor, bos);
        }

        try
        {
            File.Copy(arsivYolu, hedef, overwrite: false);

            // File.Copy OZNITELIGI DE KOPYALIYOR (arsiv salt-okunur), yani
            // kopya da salt-okunur dogar - istenen bu. Yine de ACIKCA
            // konuyor: arsivin oznitelik korumasi bir gun duserse burasi
            // sessizce yazilabilir olmasin (CLAUDE.md 1a).
            File.SetAttributes(hedef, FileAttributes.ReadOnly);
        }
        catch (Exception hata)
        {
            return (null, IslemSonuclari.HatayiCevir(hata).Sebebi);
        }

        return (hedef, null);
    }
}
