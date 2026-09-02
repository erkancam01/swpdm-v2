using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YENI VERSIYON OLUSTUR (Erkan, 31.08.2026: "bu versiyon olayı çok ciddi").
/// O anki icerigi <see cref="Surumler"/> arsivine kopyalar; ilk cagri v0'i
/// yaratir - mevcut dosyalar v0 sayilir, onceden hazirlik gerekmez.
///
/// YALNIZ O DOSYA kopyalanir - montaj ve teknik resim dahil (Erkan'in karari,
/// 01.09.2026). Bir tur once montajin o gunku cocuklari da BU dosyanin
/// arsivine giriyordu ve tek bir teknik resim "5 dosya", bir parca "162
/// dosya" suruklyordu.
///
/// MONTAJ/TEKNIK RESIMDE AYRICA BILESIM (02.09.2026): cocuklarin o gunku
/// icerigi GIZLI icerik deposuna konur ve <see cref="Surumler.BilesimYaz"/>
/// ile kaydedilir. Cocuklarin KENDI versiyon listelerine hicbir sey
/// eklenmez (Erkan: "montajın versiyonunu oluştur dediğimde içindeki tüm
/// parçaların versiyonunu oluşturuyor") - "versiyon = yalniz o dosya"
/// kurali gorunurde de, gercekte de duruyor. Ayni icerik diskte tek kopya.
///
/// Iki giris kapisindan BIRINCISI bu (Erkan'in secimi "ikisi birden"):
/// sag tik / Ctrl+Shift+U her an. Ikincisi - belge kapaninca tek soru -
/// sonraki asamada (SIRADAKI.md).
/// </summary>
internal sealed class SurumOlusturIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Yeni versiyon oluştur…";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.U;

    /// <inheritdoc/>
    public bool Yazar => false;   // arsive KOPYALAR; bitmis isin kendisini degistirmez

    /// <inheritdoc/>
    /// <remarks>
    /// ERKAN'DA GORULEN HATA (31.08.2026): burasi "Sahip" diyordu, yani
    /// panelde bir PARCAYA sag tiklansa bile versiyon AGACTA SECILI montaja
    /// aciliyordu - kullanici yanlis dosyayi versiyonluyor ve bunu ancak o
    /// versiyona donmek isteyince anliyordu (CLAUDE.md 3).
    ///
    /// Duz "Satir" de olmaz: VERSIYONLAR sekmesindeki satir bir arsiv
    /// kopyasidir, dosyaya cozulmez ve Ctrl+Shift+U orada GRI kalirdi.
    /// </remarks>
    public IslemHedefi Hedef => IslemHedefi.SatirYoksaSahip;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        ArgumentNullException.ThrowIfNull(secim);

        if (secim.TekOge is not DosyaOgesi dosya)
        {
            nedenOlmaz = "Tek bir dosya seçin.";
            return false;
        }

        // ASAMA 1 YALNIZ SOLIDWORKS DOSYALARI: VERSIYONLAR sekmesi de ayni
        // kumeyi gosteriyor; burada genis, orada dar olsa sekme "yok" derken
        // kisayol arsive yazardi - iki yuz ayrisirdi (CLAUDE.md 8).
        if (!SwReferans.TasiyabilirMi(dosya.Yol))
        {
            nedenOlmaz = "Bu tür için versiyon tutulmuyor (yalnız SOLIDWORKS dosyaları).";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        ArgumentNullException.ThrowIfNull(baglam);

        if (baglam.Secim.TekOge is not DosyaOgesi dosya || baglam.Secim.Kok is not string kok)
        {
            baglam.Bildir("Önce bir dosya seçin.");
            return;
        }

        // ============ MONTAJ/TEKNIK RESIM: BILESIM DE KAYDEDILIR ============
        //
        // VERSIYON = YALNIZ O DOSYA (Erkan, 01.09.2026: "versiyon olusturma
        // o parcanin bir kopyasini olusturma degil mi, ne alaka dosyalari
        // arsivleme") - BU KURAL DURUYOR: cocuklar bu dosyanin arsivine
        // KOPYALANMIYOR.
        //
        // Ama versiyonun "o gunku hali" ile acilabilmesi icin cocuklarin o
        // gunku ICERIGININ bir yerde durmasi sart. O yer, parcanin versiyon
        // listesi DEGIL: gizli, icerik-adresli depo (Surumler.BilesimYaz).
        // Ayni icerik ikinci kez yazilmaz - anahtar icerigin kendisi.
        DosyaTuru tur = DosyaTurleri.Tani(dosya.Yol);
        bool cocukluTur = tur == DosyaTuru.Montaj || tur == DosyaTuru.TeknikResim;

        List<string> cocuklar = cocukluTur
            ? [.. SurumSahnesi.Cocuklar(baglam.Referanslar.Indeks, dosya.Yol)]
            : [];

        string soru = cocuklar.Count > 0
            ? $"\"{dosya.Ad}\" şimdiki hâliyle arşivlenecek.\r\n"
              + $"İçindeki {cocuklar.Count} dosyanın o günkü hâli de saklanacak "
              + "— böylece bu versiyon ileride o günkü parçalarıyla açılabilir. "
              + "Parçaların kendi versiyon listeleri DEĞİŞMEZ.\r\n\r\n"
              + "Not (isteğe bağlı):"
            : $"\"{dosya.Ad}\" şimdiki hâliyle arşivlenecek. Not (isteğe bağlı):";

        string? not = SurumNotuKutusu.Sor(baglam.Sahip, "Yeni versiyon", soru);

        if (not is null)
        {
            baglam.Bildir("Versiyon oluşturma iptal edildi.");
            return;
        }

        IslemRaporu rapor = Surumler.Olustur(kok, dosya.Yol, not, out int no);

        if (!rapor.Oldu)
        {
            MessageBox.Show(
                baglam.Sahip, rapor.Sebebi,
                "Versiyon oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            baglam.Bildir("Versiyon oluşturulamadı — " + dosya.Ad);
            return;
        }

        string cumle = no == 0
            ? $"v0 arşivlendi (ilk versiyon): {dosya.Ad}"
            : $"v{no} oluşturuldu: {dosya.Ad}";

        if (cocukluTur)
        {
            cumle += "  · " + BilesimiYaz(kok, dosya.Yol, no, cocuklar);
        }

        baglam.Bildir(cumle);

        // Panel tazelensin ki VERSIYONLAR sekmesindeki sayi hemen artsin;
        // yol verilerek secim ayni dosyada kalir.
        baglam.Tazele(dosya.Yol);
    }

    /// <summary>
    /// Bilesim kaydini yazar ve DURUM CUBUGUNA yazilacak cumleyi doner.
    ///
    /// Bilesim yazilamazsa versiyon GERI ALINMAZ: dosyanin kendi arsiv
    /// kopyasi saglam ve "bu versiyona don" ondan calisiyor. Eksilen tek
    /// sey "o gunku parcalarla ac" secenegi - ve o SOYLENIYOR, sessizce
    /// yutulmuyor (CLAUDE.md 3).
    /// </summary>
    private static string BilesimiYaz(string kok, string yol, int no, List<string> cocuklar)
    {
        if (cocuklar.Count == 0)
        {
            return "bileşim boş (indeks taranmamış ya da çocuğu yok) — "
                   + "bu versiyon bugünkü parçalarla açılır";
        }

        IslemRaporu rapor = Surumler.BilesimYaz(
            kok, yol, no, cocuklar, out int yeniSaklanan, out IReadOnlyList<string> atlanan);

        if (!rapor.Oldu)
        {
            return "bileşim yazılamadı (" + rapor.Sebebi
                   + ") — bu versiyon bugünkü parçalarla açılır";
        }

        string cumle = $"{cocuklar.Count} parçanın bileşimi kaydedildi";
        cumle += yeniSaklanan > 0
            ? $" ({yeniSaklanan} parçanın o günkü hâli saklandı)"
            : " (hepsi zaten depoda, yeni kopya yazılmadı)";

        if (atlanan.Count > 0)
        {
            cumle += $" · {atlanan.Count} parça kaydedilemedi: " + string.Join("; ", atlanan);
        }

        return cumle;
    }
}

