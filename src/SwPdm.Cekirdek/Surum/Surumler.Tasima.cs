using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>
/// VERSIYON ARSIVI DOSYAYLA BIRLIKTE TASINIR.
///
/// NEDEN (Erkan, 31.08.2026: "parçanın adını veya bağlı bulunduğu klasörün
/// adını değiştirince versiyonlar gözükmüyor, versiyon yok diyor"): yuva
/// dosyanin YOLUNDAN turetiliyor. Ad ya da klasor adi degisince yuvanin yolu
/// da degisir, eski yuva OKSUZ kalir ve panel "Versiyon yok" der. Arsiv
/// diskte duruyor ama kullanici KAYBOLDUGUNU sanir - "versiyonladim" deyip
/// dosyanin ustune yazar (CLAUDE.md 3).
///
/// KANCA ARAYUZDE DEGIL CEKIRDEKTE. Arayuzde bes ayri yol dosya tasiyor
/// (adlandir · tasi · adlandirmayi geri al · tasimayi geri al · ileri al);
/// besine ayri satir eklemek CLAUDE.md 1b'nin uyardigi "merkezi liste"
/// tuzagi - altincisi eklenince unutulur ve hata SESSIZ olur. Kanca
/// hepsinin altindan gectigi <see cref="DosyaIslemleri"/> metotlarinda.
///
/// COP KANCASIZ - bilincli: cope atilan dosyanin yuvasi yerinde kalir, geri
/// yukleme dosyayi AYNI yola koydugu icin versiyonlar kendiliginden geri
/// gelir. Yuvayi cope tasimak hicbir sey kazandirmaz, kaybetme riski katar.
/// </summary>
public static partial class Surumler
{
    /// <summary>Arsiv aranirken cikilacak en fazla ata sayisi.</summary>
    private const int EnFazlaAta = 32;

    /// <summary>
    /// Bir yol diskte tasindiktan SONRA cagrilir; versiyon yuvasini da tasir.
    ///
    /// SIRA: DOSYA ONCE, ARSIV SONRA. Tersi olsaydi dosya islemi tutmayinca
    /// yuvayi geri almak gerekirdi. Bu sirayla arsiv tasinamazsa dosya
    /// islemi gecerli kalir, sebep soylenir ve arsiv ESKI YERINDE durur -
    /// hicbir sey silinmez (CLAUDE.md 1a).
    ///
    /// KLASOR ICIN AYRI KOD YOK: <see cref="Yuvasi"/> bir klasor yolunda da
    /// dogru dugumu veriyor ("kok\55" -> ".SwPdmSurum\55"), yani tek
    /// Directory.Move klasorun icindeki BUTUN dosyalarin yuvalarini birden
    /// tasiyor.
    /// </summary>
    /// <returns>
    /// null = sorun yok (tasindi ya da tasinacak arsiv yoktu). Aksi halde
    /// SEBEP - cagiran bunu kullaniciya soylemek zorunda; sessizce gecmek,
    /// versiyonlarin kayboldugunu dusundurur (CLAUDE.md 3).
    /// </returns>
    public static string? Tasindi(string? eskiYol, string? yeniYol)
    {
        if (string.IsNullOrWhiteSpace(eskiYol) || string.IsNullOrWhiteSpace(yeniYol)
            || string.Equals(eskiYol, yeniYol, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var sebepler = new List<string>();

        // BIRDEN COK ATADA ARSIV OLABILIR: kullanici bir alt klasoru kok
        // yapip versiyon olusturduysa arsiv orada durur. Hepsi yoklanir ve
        // bu yolun yuvasini GERCEKTEN iceren tasinir.
        foreach (string arsivKoku in ArsivKokleri(eskiYol))
        {
            // BILESIM KAYITLARINA DOKUNULMAZ - bilerek. Kayittaki yol O
            // GUNKU yerdir ve arsivdeki montajin icinde yazan yollar da o
            // gunku yollardir (arsiv kopyasi salt-okunur, hic onarilmaz).
            // Bir tur once burada guncelleniyordu; YANLISTI - sahneyi
            // bugunku yere kurup arsivdeki montajin baktigi yeri bosaltirdi.
            // Icerik artik karmayla bulunuyor, yani yolun bayatlamasi
            // ARAMAYI da bozmuyor (Surumler.Bilesim.cs).

            string? eski = Yuvasi(arsivKoku, eskiYol);
            if (eski is null || !Directory.Exists(eski))
            {
                continue;
            }

            // Yeni yol ayni arsivin altinda degilse tasinamaz - arsivi baska
            // bir kokun altina goturmek, oradaki duzeni bozar.
            string? yeni = Yuvasi(arsivKoku, yeniYol);
            if (yeni is null)
            {
                sebepler.Add(
                    $"versiyon arşivi taşınamadı (yeni yol arşivin kökü altında değil): {eski}");
                continue;
            }

            if (Directory.Exists(yeni))
            {
                // Hedefte zaten yuva var (cope gitmis eski bir dosyadan
                // kalmis olabilir). USTUNE YAZMAK ikisinden birini yok
                // ederdi; ikisi de yerinde birakilir ve sebep soylenir.
                sebepler.Add(
                    $"versiyon arşivi taşınmadı — hedefte zaten arşiv var: {yeni}");
                continue;
            }

            try
            {
                string ust = WindowsYolu.Klasor(yeni);
                if (ust.Length > 0)
                {
                    Directory.CreateDirectory(ust);
                }

                Directory.Move(eski, yeni);
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                sebepler.Add($"versiyon arşivi taşınamadı ({hata.Message}): {eski}");
            }
        }

        return sebepler.Count == 0 ? null : string.Join(" · ", sebepler);
    }

    /// <summary>
    /// Yolun atalarindaki arsiv KOKLERI - yani icinde ".SwPdmSurum" olan
    /// klasorler, en yakindan uzaga.
    ///
    /// NEDEN YUKARI YURUYORUZ: <see cref="DosyaIslemleri"/> acik koku
    /// BILMIYOR ve bilmemeli (o bir arayuz kavrami). Arsiv diskte duran bir
    /// isaret; yukari yurumek onu koke bagimli olmadan buluyor - ustelik
    /// daha dogru: alt klasoru kok yapip versiyonlayan kullanicinin arsivi
    /// de bulunuyor.
    ///
    /// EnFazlaAta ile sinirli: erisilemeyen bir ag yolunda Directory.Exists
    /// uzun surebiliyor (CLAUDE.md 4), yani bu dongu sinirsiz olamaz.
    /// </summary>
    private static IEnumerable<string> ArsivKokleri(string yol)
    {
        string? klasor = WindowsYolu.Klasor(yol);

        for (int i = 0; i < EnFazlaAta && !string.IsNullOrEmpty(klasor); i++)
        {
            bool var_;
            try
            {
                var_ = Directory.Exists(WindowsYolu.Birlestir(klasor, KlasorAdi));
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                yield break;
            }

            if (var_)
            {
                yield return klasor;
            }

            string ust = WindowsYolu.Klasor(klasor);
            if (string.Equals(ust, klasor, StringComparison.OrdinalIgnoreCase))
            {
                yield break;   // surucu koku: Klasor("C:\") kendini doner
            }

            klasor = ust;
        }
    }
}
