using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Kurulan sahnenin sonucu.</summary>
/// <param name="AcilacakYol">Acilacak dosyanin sahnedeki yolu; kurulamadiysa null.</param>
/// <param name="Dizilen">Yanina dizilen COCUK sayisi.</param>
/// <param name="Bugunku">
/// Dizilen cocuklardan kacinin O GUNKU degil BUGUNKU hali kullanildi.
/// AYRI SAYILIYOR (CLAUDE.md 3): "o gunku hali" denip bugunku parcayi
/// dizmek sessiz bir yalan olurdu; sayisi ekranda soylenir.
/// </param>
/// <param name="Atlanan">Dizilemeyenler ve sebepleri; bos olabilir.</param>
/// <param name="Sebep">Sahne hic kurulamadiysa sebebi; kurulduysa null.</param>
public sealed record SahneSonucu(
    string? AcilacakYol,
    int Dizilen,
    int Bugunku,
    IReadOnlyList<string> Atlanan,
    string? Sebep);

/// <summary>
/// VERSIYON SAHNESI - bir arsiv kopyasini ACILABILIR hale getirir.
///
/// ============ NEDEN VAR - OLCULDU (Erkan, 02.09.2026) ============
///
/// "oluşturulan versiyon açılıyor ama içi boş geliyor (montaj ve teknik resim)"
///
/// Sebep zinciri:
///   1. Arsiv kopyasi izole bir klasorde duruyor
///      (.SwPdmSurum\...\&lt;ad&gt;\vN\&lt;ad&gt;) ve cift tikta ORADAN aciliyordu.
///   2. O klasorde HICBIR KOMSU yok.
///   3. Montajin icindeki cocuk yollari ise KOMSULUGA BAGLI:
///      <see cref="YazilacakYol"/> once GORELI yol yaziyor; parcalar ayni
///      klasordeyse bu CIPLAK AD oluyor ("parça.SLDPRT") - yani "yanima bak".
///   4. Yaninda kimse olmayinca SOLIDWORKS hicbirini cozemiyor -> BOS acilir.
///
/// ============ COZUM: YOLU DUZELTME, DUZENI KUR ============
///
/// Gercek PDM'ler (SOLIDWORKS PDM, Vault) bir versiyonu ARSIVDEN ACMAZ:
/// secilen versiyonu yerel kasa gorunumunde KENDI NORMAL YOLUNA yazar ve
/// dosya her zaman olagan klasor duzeninde acilir. Dosyanin ICINDEKI yollari
/// hicbiri yamalamaz.
///
/// Burada da ayni sey yapiliyor: gecici bir klasorde KOKUN KLASOR YAPISI
/// taklit ediliyor, versiyonun dosyasi ve cocuklari kendi GORELI yerlerine
/// diziliyor, acma oradan yapiliyor.
///
/// Kazanci buyuk: ciplak ad da, "..\" de, mutlak yol da dogru cozuluyor VE
/// dosyanin icine HIC dokunulmuyor - yani "yazilan dizenin uzunlugu
/// degisirse SOLIDWORKS acmiyor" mayin tarlasina (<see cref="YazilacakYol"/>)
/// hic girilmiyor.
///
/// ============ IKI SAHNE TURU ============
///
///   <see cref="Kur"/>          - BUGUNKU parcalarla (indeksten).
///   <see cref="KurBilesimle"/> - O GUNKU parcalarla (bilesim kaydindan).
///
/// Ikisi de ayni dizme koduna dusuyor; fark YALNIZCA hangi dosyanin
/// kopyalandigi (CLAUDE.md 1b: tek is, tek yer).
///
/// ============ SALT-OKUNUR ============
///
/// Sahnedeki her dosya salt-okunur yaziliyor. Arsiv kopyasi zaten oyle;
/// cocuklar da oyle olmali, cunku sahnede kazayla kaydedilen bir degisiklik
/// kullaniciya "gecmise baktim" derken bugunku isini bozdurmus olurdu
/// (CLAUDE.md 1a).
/// </summary>
public static class SurumSahnesi
{
    /// <summary>Gecici sahnelerin toplandigi klasorun adi.</summary>
    private const string TabanAd = "SwPdmSahne";

    /// <summary>Bu yastan eski sahneler yeni sahne kurulurken silinir.</summary>
    private static readonly TimeSpan Omur = TimeSpan.FromDays(1);

    /// <summary>Sahneye dizilecek TEK dosya: nereye ve neyin kopyasi.</summary>
    /// <param name="GercekYol">Dosyanin kok icindeki gercek yolu (hedefi bu belirler).</param>
    /// <param name="Yol">Kopyalanacak dosya; arsiv kopyasi ya da bugunku hali.</param>
    /// <param name="BugunkuHali">Kaynak, o gunku degil BUGUNKU dosya mi.</param>
    private sealed record Kaynak(string GercekYol, string Yol, bool BugunkuHali);

    /// <summary>
    /// Arsiv kopyasini BUGUNKU parcalarla acilabilir bir sahneye dizer.
    /// </summary>
    /// <param name="kok">Acik kok klasor.</param>
    /// <param name="arsivYolu">Arsivdeki kopya.</param>
    /// <param name="orijinalYol">Dosyanin kok icindeki GERCEK yolu.</param>
    /// <param name="indeks">Cocuklarin kim oldugunu bilen indeks; yoksa yalniz dosya dizilir.</param>
    public static SahneSonucu Kur(
        string? kok, string? arsivYolu, string? orijinalYol, ReferansIndeksi? indeks)
        => Diz(kok, arsivYolu, orijinalYol, BugunkuKaynaklar(indeks, orijinalYol));

    /// <summary>
    /// Arsiv kopyasini O GUNKU parcalarla dizer: hangi cocugun hangi
    /// versiyonda oldugu <paramref name="bilesim"/>'de yaziyor.
    ///
    /// Cocuk listesi INDEKSTEN DEGIL kayittan geliyor - bilerek: o gunden
    /// beri silinmis ya da adi degismis bir parca indekste YOK ama kayitta
    /// VAR; montaji eksiksiz acabilmenin tek yolu kayda bakmak.
    /// </summary>
    public static SahneSonucu KurBilesimle(
        string? kok, string? arsivYolu, string? orijinalYol, BilesimDurumu? bilesim)
        => Diz(kok, arsivYolu, orijinalYol, BilesimKaynaklari(kok, bilesim));

    /// <summary>
    /// Dosyanin kok icindeki GERCEK yolunu arsiv yolundan cikarir.
    /// Arsiv duzeni: kok\.SwPdmSurum\&lt;goreli klasor&gt;\&lt;ad&gt;\vN\&lt;ad&gt;
    /// </summary>
    public static string? OrijinalYol(string? kok, string? arsivYolu)
    {
        if (string.IsNullOrWhiteSpace(kok) || string.IsNullOrWhiteSpace(arsivYolu))
        {
            return null;
        }

        // vN klasorunun ustu = <ad> klasoru; onun ustu = goreli klasor.
        string adKlasoru = WindowsYolu.Klasor(WindowsYolu.Klasor(arsivYolu));
        string arsivKok = WindowsYolu.Birlestir(kok, Surumler.KlasorAdi);

        // DUZ ONEK KIRPMA - WindowsYolu.Goreli KULLANILMIYOR: o, dosyanin
        // ICINE yazilacak yollar icin ve ".\" susu ekliyor (Surumler.Yuvasi
        // ayni sebeple boyle yaziliyor).
        string? goreli = GoreliDuz(arsivKok, adKlasoru);
        return goreli is null ? null : WindowsYolu.Birlestir(kok, goreli);
    }

    /// <summary>
    /// Dosyanin BUTUN torunlari - genislemesine, her dosya BIR KEZ.
    /// Indeks yoksa bos (cocuk oldugunu bilmiyoruz demek, "yok" demek degil).
    /// TEK GEZINTI (CLAUDE.md 1b): sahne de, bilesim kaydi da buradan sorar.
    /// </summary>
    public static IEnumerable<string> Cocuklar(ReferansIndeksi? indeks, string? yol)
    {
        if (indeks is null || string.IsNullOrWhiteSpace(yol))
        {
            yield break;
        }

        var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { yol };
        var kuyruk = new Queue<string>();
        kuyruk.Enqueue(yol);

        while (kuyruk.Count > 0)
        {
            string suAn = kuyruk.Dequeue();
            foreach ((string _, Cozum cozum) in indeks.Kullandiklari(suAn))
            {
                if (cozum.Durum != CozumDurumu.Bulundu
                    || cozum.Yol is not string cocuk
                    || !gorulen.Add(cocuk))
                {
                    continue;
                }

                kuyruk.Enqueue(cocuk);
                yield return cocuk;
            }
        }
    }

    /// <summary>
    /// <paramref name="yol"/>'u <paramref name="kok"/>'e gore DUZ onek
    /// kirpmayla yazar; altinda degilse null. Sonucta ".\" ya da "..\" YOK.
    /// </summary>
    internal static string? GoreliDuz(string? kok, string? yol)
    {
        if (string.IsNullOrWhiteSpace(kok) || string.IsNullOrWhiteSpace(yol)
            || !WindowsYolu.AltindaMi(yol, kok))
        {
            return null;
        }

        return yol.Length > kok.Length
            ? yol[kok.Length..].Trim(WindowsYolu.Ayirici, WindowsYolu.EgikAyirici)
            : string.Empty;
    }

    private static IEnumerable<Kaynak> BugunkuKaynaklar(ReferansIndeksi? indeks, string? yol)
    {
        foreach (string cocuk in Cocuklar(indeks, yol))
        {
            yield return new Kaynak(cocuk, cocuk, BugunkuHali: true);
        }
    }

    private static IEnumerable<Kaynak> BilesimKaynaklari(string? kok, BilesimDurumu? bilesim)
    {
        if (kok is null || bilesim is null)
        {
            yield break;
        }

        foreach (BilesimOgesi oge in bilesim.Ogeler)
        {
            string gercek = WindowsYolu.Birlestir(kok, oge.GoreliYol);

            // O GUNKU KOPYA YOKSA (versiyon elle silinmis olabilir) bugunku
            // hali dizilir ve BU SAYILIR - eksik bir montaj actirmaktansa
            // dogru sayiyi soylemek yeglenir (CLAUDE.md 3).
            string? arsiv = Surumler.BilesimArsivi(kok, oge);
            yield return arsiv is not null
                ? new Kaynak(gercek, arsiv, BugunkuHali: false)
                : new Kaynak(gercek, gercek, BugunkuHali: true);
        }
    }

    private static SahneSonucu Diz(
        string? kok, string? arsivYolu, string? orijinalYol, IEnumerable<Kaynak> cocuklar)
    {
        if (string.IsNullOrWhiteSpace(kok)
            || string.IsNullOrWhiteSpace(arsivYolu)
            || string.IsNullOrWhiteSpace(orijinalYol))
        {
            return new SahneSonucu(null, 0, 0, [], "Sahne için kök ve dosya yolu gerekli.");
        }

        if (!File.Exists(arsivYolu))
        {
            return new SahneSonucu(null, 0, 0, [], "Arşiv kopyası bulunamadı.");
        }

        string? sahne = SahneKlasoru();
        if (sahne is null)
        {
            return new SahneSonucu(null, 0, 0, [], "Geçici klasör açılamadı.");
        }

        var atlanan = new List<string>();

        string? hedef = Kopyala(sahne, kok, orijinalYol, arsivYolu, atlanan);
        if (hedef is null)
        {
            return new SahneSonucu(null, 0, 0, atlanan, "Versiyon kopyası sahneye dizilemedi.");
        }

        int dizilen = 0;
        int bugunku = 0;

        foreach (Kaynak kaynak in cocuklar)
        {
            if (Kopyala(sahne, kok, kaynak.GercekYol, kaynak.Yol, atlanan) is null)
            {
                continue;
            }

            dizilen++;
            if (kaynak.BugunkuHali)
            {
                bugunku++;
            }
        }

        return new SahneSonucu(hedef, dizilen, bugunku, atlanan, null);
    }

    /// <summary>
    /// Bir dosyayi sahneye, KOKE GORE ayni goreli yere kopyalar.
    /// Doner: sahnedeki yol; olmadiysa null (sebebi listeye eklenir).
    /// </summary>
    private static string? Kopyala(
        string sahne, string kok, string gercekYol, string kaynak, List<string> atlanan)
    {
        string? goreli = GoreliDuz(kok, gercekYol);
        if (string.IsNullOrEmpty(goreli))
        {
            atlanan.Add(WindowsYolu.DosyaAdi(gercekYol) + " — kökün dışında");
            return null;
        }

        string hedef = WindowsYolu.Birlestir(sahne, goreli);

        try
        {
            Directory.CreateDirectory(WindowsYolu.Klasor(hedef));
            File.Copy(kaynak, hedef, overwrite: true);

            // SALT-OKUNUR: sahnede yapilan bir kayit bugunku isi bozmasin.
            var bilgi = new FileInfo(hedef);
            if (!bilgi.IsReadOnly)
            {
                bilgi.IsReadOnly = true;
            }

            return hedef;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            atlanan.Add(WindowsYolu.DosyaAdi(gercekYol) + " — " + hata.Message);
            return null;
        }
    }

    /// <summary>
    /// Yeni bir sahne klasoru; ayrica ESKILERI temizler.
    ///
    /// Temizlik BURADA cunku uygulamanin kapanisina guvenilemez (cokme,
    /// oturum kapanmasi). Silinemeyen bir sahne SESSIZCE atlanir: acik bir
    /// SOLIDWORKS belgesi tutuyor olabilir ve onu zorlamak kullanicinin
    /// acik dosyasini bozardi (CLAUDE.md 1a).
    /// </summary>
    private static string? SahneKlasoru()
    {
        try
        {
            string taban = WindowsYolu.Birlestir(Path.GetTempPath(), TabanAd);
            Directory.CreateDirectory(taban);
            EskileriTemizle(taban);

            string yeni = WindowsYolu.Birlestir(taban, Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(yeni);
            return yeni;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void EskileriTemizle(string taban)
    {
        DateTime sinir = DateTime.UtcNow - Omur;

        foreach (string klasor in Directory.GetDirectories(taban))
        {
            try
            {
                if (Directory.GetCreationTimeUtc(klasor) > sinir)
                {
                    continue;
                }

                foreach (string dosya in Directory.EnumerateFiles(
                             klasor, "*", SearchOption.AllDirectories))
                {
                    var bilgi = new FileInfo(dosya);
                    if (bilgi.IsReadOnly)
                    {
                        bilgi.IsReadOnly = false;
                    }
                }

                Directory.Delete(klasor, recursive: true);
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                // Tutan bir sahne varsa DOKUNULMAZ; sessizce gecilir.
            }
        }
    }
}
