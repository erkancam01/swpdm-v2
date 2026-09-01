using System;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YENI VERSIYON OLUSTUR (Erkan, 31.08.2026: "bu versiyon olayı çok ciddi").
/// O anki icerigi <see cref="Surumler"/> arsivine kopyalar; ilk cagri v0'i
/// yaratir - mevcut dosyalar v0 sayilir, onceden hazirlik gerekmez.
///
/// YALNIZ O DOSYA kopyalanir - montaj ve teknik resim dahil (Erkan'in karari,
/// 01.09.2026). Bir tur once montajin o gunku cocuklari da giriyordu ve tek
/// bir teknik resim "5 dosya", bir parca "162 dosya" suruklyordu.
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

        // VERSIYON = YALNIZ O DOSYA (Erkan, 01.09.2026: "versiyon olusturma
        // o parcanin bir kopyasini olusturma degil mi, ne alaka dosyalari
        // arsivleme"). Kutu artik tek cumle; sayilacak bir sey yok, o yuzden
        // "Listeyi goster..." dugmesi de kalkti (CLAUDE.md 6: kutuda yalnizca
        // kararin gerektirdigi kadari durur).
        string? not = SurumNotuKutusu.Sor(
            baglam.Sahip, "Yeni versiyon",
            $"\"{dosya.Ad}\" şimdiki hâliyle arşivlenecek. Not (isteğe bağlı):");

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

        baglam.Bildir(no == 0
            ? $"v0 arşivlendi (ilk versiyon): {dosya.Ad}"
            : $"v{no} oluşturuldu: {dosya.Ad}");

        // Panel tazelensin ki VERSIYONLAR sekmesindeki sayi hemen artsin;
        // yol verilerek secim ayni dosyada kalir.
        baglam.Tazele(dosya.Yol);
    }
}
