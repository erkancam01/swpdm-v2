using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// KLASORU KILITLE / KILIDI KALDIR (Erkan, 31.08.2026: "bitmis isleri
/// tikleyeyim, kilitleyeyim, ondan sonra rahat rahat calisayim").
///
/// Kilitli klasor agacta GORUNUR ama ACILMAZ - "+" cikmaz, icindeki dosya
/// hic gorunmez, yani kazayla secilip degistirilemez. Karar ve kural
/// cekirdekte (<see cref="KlasorKilidi"/>); burasi yalnizca secimi ona
/// veriyor.
///
/// TEK OGE, IKI YON: ayni komut kilitliyi acar, aciyi kilitler (secilenlerin
/// hepsi kilitliyse "kaldir" sayilir). Iki ayri menu satiri koymak,
/// ikisinden birinin HER ZAMAN gri durmasi demekti (CLAUDE.md 6: menu az ve
/// dogru olsun). Menudeki yazi sabit, cunku IAgacIslemi.Ad secimi gormuyor;
/// ne oldugunu durum cubugu SAYIYLA soyluyor.
///
/// YAZAR = false: bu islem kilitli klasorde de calismali - yoksa kilidi
/// KALDIRMANIN yolu kapanirdi. Kendi kendini kilitleyen bir ozellik olurdu.
/// </summary>
internal sealed class KlasorKilidiIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Klasörü kilitle / kilidi kaldır";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.Q;

    /// <inheritdoc/>
    public bool Yazar => false;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (string.IsNullOrWhiteSpace(secim.Kok))
        {
            nedenOlmaz = "Önce bir klasör açın.";
            return false;
        }

        if (secim.AramaKipinde)
        {
            // Arama sonucundaki satirin agactaki yeri baska; kilidi oradan
            // koymak kullanicinin gormedigi bir klasoru kilitlerdi.
            nedenOlmaz = "Arama sonucundayken kilitlenemez — önce aramadan çıkın (Esc).";
            return false;
        }

        if (Klasorler(secim).Count == 0)
        {
            nedenOlmaz = "Önce bir klasör seçin — kilit yalnızca klasöre konur.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        List<string> klasorler = Klasorler(baglam.Secim);
        if (klasorler.Count == 0 || baglam.Secim.Kok is not string kok)
        {
            return;
        }

        bool kilitle = !HepsiKilitli(baglam.Secim, klasorler);
        IslemRaporu rapor = KlasorKilidi.Degistir(kok, klasorler, kilitle);

        if (!rapor.Oldu)
        {
            // SEBEP EKRANDA (CLAUDE.md 3): kilit konmadiysa kullanici bunu
            // bilmeli, yoksa "kilitledim" sanip rahat calisir.
            baglam.Bildir(rapor.Sebebi);
            return;
        }

        baglam.Bildir(
            (kilitle
                ? $"{klasorler.Count} klasör kilitlendi — açılmaz; kilidi kaldırmak için yine {Kisayolu()}"
                : $"{klasorler.Count} klasörün kilidi kaldırıldı")
            + (rapor.Sebep is { Length: > 0 } not_ ? " · " + not_ : string.Empty));

        // Agac yeniden kuruluyor: "+" kutusunun varligi kilide bagli.
        baglam.Tazele(null);
    }

    private static string Kisayolu() => "Ctrl+Shift+Q";

    private static List<string> Klasorler(SecimBaglami secim)
    {
        var sonuc = new List<string>();
        foreach (object oge in secim.Ogeler)
        {
            if (oge is KlasorOgesi klasor)
            {
                sonuc.Add(klasor.Yol);
            }
        }

        return sonuc;
    }

    private static bool HepsiKilitli(SecimBaglami secim, List<string> klasorler)
    {
        if (secim.Kilitler is null)
        {
            return false;
        }

        foreach (string klasor in klasorler)
        {
            if (!secim.Kilitler.KendisiKilitli(klasor))
            {
                return false;
            }
        }

        return true;
    }
}
