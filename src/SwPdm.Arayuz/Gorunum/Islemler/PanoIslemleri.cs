using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// PANO - "Kes", "Kopyala" ve "Yapistir" AYNI ozelliktir, o yuzden ayni
/// dosyada (CLAUDE.md 1b). Uygulama icidir; Windows panosuna DOKUNMAZ.
///
/// Windows panosuna yazmamak bilincli: oraya yazsaydik Gezgin'den de
/// yapistirilabilir olurdu ve bizim referans uyarilarimiz DEVRE DISI kalirdi.
/// </summary>
internal static class Pano
{
    private static readonly List<string> Yollar = [];

    /// <summary>Panodaki oge sayisi.</summary>
    internal static int Adet => Yollar.Count;

    /// <summary>Panodakiler tasinacak mi kopyalanacak mi.</summary>
    internal static AktarmaKipi Kip { get; private set; } = AktarmaKipi.Tasi;

    /// <summary>Panonun bir kopyasi.</summary>
    internal static IReadOnlyList<string> Icerik => [.. Yollar];

    /// <summary>Panoyu doldurur.</summary>
    internal static void Doldur(IEnumerable<object> ogeler, AktarmaKipi kip)
    {
        Yollar.Clear();
        Kip = kip;

        foreach (object oge in ogeler)
        {
            if (SecimBaglami.Yolu(oge) is string yol)
            {
                Yollar.Add(yol);
            }
        }
    }

    /// <summary>Panoyu bosaltir.</summary>
    internal static void Bosalt() => Yollar.Clear();
}

/// <summary>Secilenleri TASINMAK uzere panoya koyar.</summary>
internal sealed class KesIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Kes";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.X;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
        => PanoyaKoy.Olur(secim, "taşınacak", out nedenOlmaz);

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        Pano.Doldur(baglam.Secim.Ogeler, AktarmaKipi.Tasi);
        baglam.Bildir($"{Pano.Adet} öğe kesildi — hedef klasörü seçip Ctrl+V.");
    }
}

/// <summary>Secilenleri KOPYALANMAK uzere panoya koyar.</summary>
internal sealed class KopyalaIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Kopyala";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.C;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
        => PanoyaKoy.Olur(secim, "kopyalanacak", out nedenOlmaz);

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        Pano.Doldur(baglam.Secim.Ogeler, AktarmaKipi.Kopyala);
        baglam.Bildir($"{Pano.Adet} öğe kopyalandı — hedef klasörü seçip Ctrl+V.");
    }
}

/// <summary>Panodakileri etkin klasore aktarir.</summary>
internal sealed class YapistirIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => Pano.Adet == 0
        ? "Yapıştır"
        : $"Yapıştır ({Pano.Adet} öğe {(Pano.Kip == AktarmaKipi.Tasi ? "taşınacak" : "kopyalanacak")})";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.V;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (Pano.Adet == 0)
        {
            nedenOlmaz = "Panoda bir şey yok — önce Kes (Ctrl+X) ya da Kopyala (Ctrl+C).";
            return false;
        }

        if (secim.AramaKipinde)
        {
            nedenOlmaz = "Arama sonucuna yapıştırılamaz — önce aramayı temizleyin.";
            return false;
        }

        if (secim.EtkinKlasor is null)
        {
            nedenOlmaz = "Hedef klasörü seçin.";
            return false;
        }

        // AYNI KLASORE TASIMA ONDEN REDDEDILIR (29.08.2026). Eskiden buraya
        // kadar geliyordu ve cekirdek "zaten bu klasorde" deyip ZatenVar
        // donuyordu; aktarma motoru bunu AD CAKISMASI sanip dosyayi
        // KENDISIYLE karsilastiran bir cakisma kutusu aciyordu - ve hangi
        // secenek secilirse secilsin sonuc "TASINMADI" oluyordu.
        //
        // Kopyalamada ayni klasor MESRUDUR: cogaltma demektir, numaralanir.
        if (Pano.Kip == AktarmaKipi.Tasi && HepsiAyniKlasorde(secim.EtkinKlasor))
        {
            nedenOlmaz = "Kesilen öğeler zaten bu klasörde.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <summary>Panodakilerin TAMAMI verilen klasorde mi.</summary>
    private static bool HepsiAyniKlasorde(string klasor)
    {
        foreach (string yol in Pano.Icerik)
        {
            if (!string.Equals(
                WindowsYolu.Klasor(yol), klasor, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return Pano.Adet > 0;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        if (baglam.Secim.EtkinKlasor is string hedef)
        {
            Aktar.Yurut(baglam, Pano.Icerik, hedef, Pano.Kip);
        }
    }
}

/// <summary>Kes ve Kopyala'nin ORTAK on kosulu - tek kopya (CLAUDE.md 8).</summary>
internal static class PanoyaKoy
{
    internal static bool Olur(SecimBaglami secim, string fiil, out string nedenOlmaz)
    {
        if (secim.Ogeler.Count == 0)
        {
            nedenOlmaz = $"Önce {fiil} öğeleri seçin.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }
}
