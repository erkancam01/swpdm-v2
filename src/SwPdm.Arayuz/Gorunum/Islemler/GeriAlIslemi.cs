using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Geri alinabilir bir islem.
/// </summary>
/// <param name="Aciklama">
/// Kullaniciya gosterilecek ad - "3 öğenin taşınması" gibi. Ipucunda ve durum
/// cubugunda gorunur; kullanici NEYI geri aldigini bilmeli (CLAUDE.md 3).
/// </param>
/// <param name="Uygula">
/// Geri almayi yapar. Doner deger: OLMAYANLARIN listesi (bos ise her sey oldu).
/// </param>
internal sealed record GeriAlinabilir(string Aciklama, Func<IslemBaglami, List<string>> Uygula);

/// <summary>
/// GERI ALMA DEFTERI - yalnizca bir yigin. Hicbir islemi ADIYLA bilmez;
/// her ozellik KENDI geri almasini kendi dosyasinda yazip buraya birakir
/// (CLAUDE.md 1b: hicbir ozellik baska bir ozelligin dosyasina satir ekletmez).
/// </summary>
internal static class GeriAlDefteri
{
    /// <summary>
    /// Kac adim geri alinabilir. Sinirsiz degil: her adim eski yollari
    /// tutuyor ve kok degistiginde zaten hepsi gecersiz oluyor.
    /// </summary>
    private const int Sinir = 20;

    private static readonly LinkedList<GeriAlinabilir> Yigin = new();

    /// <summary>Geri alinacak bir sey var mi.</summary>
    internal static bool Var => Yigin.Count > 0;

    /// <summary>Sirada geri alinacak islemin adi; yoksa null.</summary>
    internal static string? Sonraki => Yigin.First?.Value.Aciklama;

    /// <summary>Bir adim biraktir.</summary>
    internal static void Kaydet(GeriAlinabilir adim)
    {
        Yigin.AddFirst(adim);
        while (Yigin.Count > Sinir)
        {
            Yigin.RemoveLast();
        }
    }

    /// <summary>
    /// Defteri bosaltir. Kok degisince SART: kayitli yollar artik baska bir
    /// agacin yollari olur ve geri alma yanlis yere dokunur (CLAUDE.md 1a).
    /// </summary>
    internal static void Temizle() => Yigin.Clear();

    /// <summary>Sirdaki adimi cikarir.</summary>
    internal static GeriAlinabilir? Al()
    {
        if (Yigin.First is null)
        {
            return null;
        }

        GeriAlinabilir adim = Yigin.First.Value;
        Yigin.RemoveFirst();
        return adim;
    }
}

/// <summary>
/// GERI AL. Arac cubugunda cop kutusunun yaninda, sag tik menusunde ve
/// Ctrl+Z'de.
/// </summary>
internal sealed class GeriAlIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => GeriAlDefteri.Sonraki is string ad ? "Geri al: " + ad : "Geri al";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Z;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (!GeriAlDefteri.Var)
        {
            nedenOlmaz = "Geri alınacak bir işlem yok.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        GeriAlinabilir? adim = GeriAlDefteri.Al();
        if (adim is null)
        {
            return;
        }

        List<string> olmayan = adim.Uygula(baglam);
        baglam.Tazele(null);

        if (olmayan.Count > 0)
        {
            // CLAUDE.md 3: geri alma YARIM kaldiysa ne olmadigi tek tek yazilir.
            // "Geri alindi" deyip gecmek, kullanicinin dosyalari eski yerinde
            // sanmasina yol acar.
            var metin = new StringBuilder();
            metin.AppendLine(adim.Aciklama + " tam geri alınamadı.");
            metin.AppendLine();
            foreach (string satir in olmayan)
            {
                metin.AppendLine("  • " + satir);
            }

            MessageBox.Show(
                baglam.Sahip, metin.ToString(), "Geri alma yarım kaldı",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            baglam.Bildir("Geri alma yarım kaldı — " + adim.Aciklama);
            return;
        }

        baglam.Bildir("Geri alındı: " + adim.Aciklama);

        // Geri alma dosyalari degistirdi; indeks arka planda tazelenir.
        ReferansTazeleme.Sonra(baglam);
    }
}
