using System;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YENI VERSIYON OLUSTUR (Erkan, 31.08.2026: "bu versiyon olayı çok ciddi").
/// O anki icerigi <see cref="Surumler"/> arsivine kopyalar; ilk cagri v0'i
/// yaratir - mevcut dosyalar v0 sayilir, onceden hazirlik gerekmez.
///
/// Iki giris kapisindan BIRINCISI bu (Erkan'in secimi "ikisi birden"):
/// sag tik / Ctrl+Shift+U her an. Ikincisi - belge kapaninca tek soru -
/// Asama 2'de gelecek (SIRADAKI.md).
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

        // KAC DOSYA ARSIVLENECEGI KUTUDA YAZIYOR - ayri bir uyari kutusu
        // DEGIL (CLAUDE.md 6). Versiyon artik kendi kendine yetiyor: montajin
        // o gunku cocuklari da kopyalaniyor, yani disk maliyeti var ve
        // kullanicinin gozu onunde olmali (CLAUDE.md 3).
        CocukKumesi cocuklar = Surumler.Cocuklari(dosya.Yol);
        string kapsam = cocuklar.Yollar.Count == 0
            ? $"\"{dosya.Ad}\" şimdiki hâliyle arşivlenecek."
            : $"\"{dosya.Ad}\" ve kullandığı {cocuklar.Yollar.Count} dosya "
              + "şimdiki hâlleriyle arşivlenecek.";

        // EKSIK VERSIYON UYARISI KUTUNUN ICINDE (CLAUDE.md 3/6): durum
        // cubuguna yazilan uyari Erkan'da GOZDEN KACTI ve eksik arsivlenen
        // montaj "dosya bozuk" diye geri dondu. Kullanici karari kutuda
        // gorup versiyonu yine de olusturabilir - engel degil, bilgi.
        if (cocuklar.Cozulemeyen > 0)
        {
            kapsam += $"\nDİKKAT: {cocuklar.Cozulemeyen} referans bulunamadı — "
                + "versiyon EKSİK arşivlenecek.";
        }

        string? not = SurumNotuKutusu.Sor(
            baglam.Sahip, "Yeni versiyon", kapsam + " Not (isteğe bağlı):");

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

        baglam.Bildir(
            (no == 0
                ? $"v0 arşivlendi (ilk versiyon): {dosya.Ad}"
                : $"v{no} oluşturuldu: {dosya.Ad}")
            + (cocuklar.Yollar.Count > 0 ? $" · {cocuklar.Yollar.Count} referansıyla" : "")
            // Cekirdek "N referans bulunamadi" diyorsa YUTULMAZ: eksik
            // cocukla arsivlenen versiyon eksik acilir (CLAUDE.md 3).
            + (rapor.Sebep is { Length: > 0 } uyari ? " · " + uyari : ""));

        // Panel tazelensin ki VERSIYONLAR sekmesindeki sayi hemen artsin;
        // yol verilerek secim ayni dosyada kalir.
        baglam.Tazele(dosya.Yol);
    }
}
