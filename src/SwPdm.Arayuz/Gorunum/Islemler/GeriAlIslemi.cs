using System;
using System.Collections.Generic;
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
/// <param name="Ters">
/// BU ADIMIN TERSI - yani "ileri alma" (Ctrl+Y). Bir FABRIKA, hazir bir adim
/// degil: ters adimin da kendi tersi olsun ki kullanici Ctrl+Z ve Ctrl+Y
/// arasinda istedigi kadar gidip gelebilsin.
///
/// NULL BIRAKILABILIR ve bu bir eksiklik degil BIR KARARDIR: tersi
/// simetrik olmayan bir islemi "ileri aldim" diye kosturmak yanlis dosyaya
/// dokunur (CLAUDE.md 1a). Null ise Ctrl+Y sebebini SOYLER, sessizce
/// hicbir sey yapmaz (CLAUDE.md 3).
/// </param>
internal sealed record GeriAlinabilir(
    string Aciklama,
    Func<IslemBaglami, List<string>> Uygula,
    Func<GeriAlinabilir>? Ters = null);

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

    /// <summary>
    /// ILERI ALMA yigini (Ctrl+Y). Geri alinan her adimin TERSI buraya
    /// girer; yeni bir islem yapilinca gecersiz olur ve bosaltilir - metin
    /// duzenleyicilerin kurali, ve dogrusu: geri alip sonra baska bir sey
    /// yapmissan eski "ileri" zinciri artik baska bir dunyaya aitti.
    /// </summary>
    private static readonly LinkedList<GeriAlinabilir> Ileri = new();

    /// <summary>Geri alinacak bir sey var mi.</summary>
    internal static bool Var => Yigin.Count > 0;

    /// <summary>Ileri alinacak bir sey var mi.</summary>
    internal static bool IleriVar => Ileri.Count > 0;

    /// <summary>Sirada geri alinacak islemin adi; yoksa null.</summary>
    internal static string? Sonraki => Yigin.First?.Value.Aciklama;

    /// <summary>Sirada ileri alinacak islemin adi; yoksa null.</summary>
    internal static string? SonrakiIleri => Ileri.First?.Value.Aciklama;

    /// <summary>Kac adim tutuldugu - kullaniciya soylenebilsin diye.</summary>
    internal static int EnFazlaAdim => Sinir;

    /// <summary>Bir adim biraktir. YENI bir islem: ileri zinciri gecersizdir.</summary>
    internal static void Kaydet(GeriAlinabilir adim)
    {
        Ileri.Clear();
        Yigin.AddFirst(adim);
        while (Yigin.Count > Sinir)
        {
            Yigin.RemoveLast();
        }
    }

    /// <summary>
    /// Bir adim BASARIYLA geri alindi: tersi ileri yigina girer.
    ///
    /// Yarim kalan bir geri almadan sonra ileri alma TEKLIF EDILMEZ - diskin
    /// hali artik ne "once" ne "sonra"; oradan ileri gitmek tahmin olurdu.
    /// </summary>
    internal static void GeriAlindi(GeriAlinabilir adim)
    {
        ArgumentNullException.ThrowIfNull(adim);

        if (adim.Ters is null)
        {
            Ileri.Clear();
            return;
        }

        Ileri.AddFirst(adim.Ters());
        while (Ileri.Count > Sinir)
        {
            Ileri.RemoveLast();
        }
    }

    /// <summary>Siradaki ileri adimini cikarir.</summary>
    internal static GeriAlinabilir? IleriAl()
    {
        if (Ileri.First is null)
        {
            return null;
        }

        GeriAlinabilir adim = Ileri.First.Value;
        Ileri.RemoveFirst();
        return adim;
    }

    /// <summary>
    /// Bir adim BASARIYLA ileri alindi: tersi geri yigina girer.
    /// Ileri yigin BOSALTILMAZ - yoksa Ctrl+Y bir kez calisip zinciri
    /// kendisi kirardi.
    /// </summary>
    internal static void IleriAlindi(GeriAlinabilir adim)
    {
        ArgumentNullException.ThrowIfNull(adim);

        if (adim.Ters is null)
        {
            return;
        }

        Yigin.AddFirst(adim.Ters());
        while (Yigin.Count > Sinir)
        {
            Yigin.RemoveLast();
        }
    }

    /// <summary>
    /// Defteri bosaltir. Kok degisince SART: kayitli yollar artik baska bir
    /// agacin yollari olur ve geri alma yanlis yere dokunur (CLAUDE.md 1a).
    /// </summary>
    internal static void Temizle()
    {
        Yigin.Clear();
        Ileri.Clear();
    }

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
            nedenOlmaz = $"Geri alınacak bir işlem yok (en fazla "
                + $"{GeriAlDefteri.EnFazlaAdim} adım tutulur).";
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

        List<string> olmayan = Adimi.Kostur(adim, baglam, geriMi: true);
        baglam.Tazele(null);

        if (olmayan.Count > 0)
        {
            // Sebep ve liste Adimi.Kostur'da yazildi; burada YALNIZCA
            // "ileri alma teklif etme" karari kaliyor.
            return;
        }

        // TERSI ILERI YIGINA GIRIYOR - yalnizca TAM basarili geri almadan
        // sonra (yukaridaki dal buraya gelmeden donuyor).
        GeriAlDefteri.GeriAlindi(adim);

        baglam.Bildir("Geri alındı: " + adim.Aciklama
            + (adim.Ters is null ? " (bu işlem ileri alınamaz)" : " · Ctrl+Y ile ileri al"));

        // Geri alma dosyalari degistirdi; indeks arka planda tazelenir.
        ReferansTazeleme.Sonra(baglam);
    }
}

/// <summary>
/// ILERI AL (Ctrl+Y) - geri alinan bir islemi yeniden yapar.
///
/// NEDEN VAR: Ctrl+Z vardi, donusu YOKTU. Yanlislikla geri alan kullanicinin
/// tek caresi islemi elle tekrar yapmakti - ve bir tasimayi elle tekrarlamak
/// tam da bu uygulamanin onlemeye calistigi seydir.
///
/// ILERI ALMA BIR TAHMIN DEGIL: her adim kendi TERSINI kendi dosyasinda
/// yaziyor (GeriAlinabilir.Ters). Tersi olmayan bir adimda bu islem
/// SEBEBINI SOYLER, is yapmis gibi davranmaz (CLAUDE.md 3).
/// </summary>
internal sealed class IleriAlIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad =>
        GeriAlDefteri.SonrakiIleri is string ad ? "İleri al: " + ad : "İleri al";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Y;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (!GeriAlDefteri.IleriVar)
        {
            nedenOlmaz = "İleri alınacak bir işlem yok — önce Ctrl+Z ile geri alın.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        GeriAlinabilir? adim = GeriAlDefteri.IleriAl();
        if (adim is null)
        {
            return;
        }

        List<string> olmayan = Adimi.Kostur(adim, baglam, geriMi: false);
        if (olmayan.Count > 0)
        {
            return;
        }

        GeriAlDefteri.IleriAlindi(adim);
        baglam.Bildir("İleri alındı: " + adim.Aciklama);
        ReferansTazeleme.Sonra(baglam);
    }
}

/// <summary>
/// Geri alma ve ileri almanin ORTAK govdesi: adimi kostur, agaci tazele,
/// yarim kaldiysa NE OLMADIGINI tek tek yaz.
///
/// Tek kopya (CLAUDE.md 8): ayni kismi basarisizlik metnini iki yerde
/// yazmak, ikisinin zamanla ayrismasi demekti.
/// </summary>
internal static class Adimi
{
    /// <summary>Doner: olmayanlarin listesi (bos ise her sey oldu).</summary>
    internal static List<string> Kostur(
        GeriAlinabilir adim, IslemBaglami baglam, bool geriMi)
    {
        ArgumentNullException.ThrowIfNull(adim);
        ArgumentNullException.ThrowIfNull(baglam);

        List<string> olmayan = adim.Uygula(baglam);
        baglam.Tazele(null);

        if (olmayan.Count == 0)
        {
            return olmayan;
        }

        string is_ = geriMi ? "geri alınamadı" : "ileri alınamadı";
        MaddeKutusu.Goster(
            baglam.Sahip,
            geriMi ? "Geri alma yarım kaldı" : "İleri alma yarım kaldı",
            $"{adim.Aciklama} tam {is_}.\n",
            olmayan);

        baglam.Bildir((geriMi ? "Geri alma" : "İleri alma") + " yarım kaldı — " + adim.Aciklama);
        return olmayan;
    }
}
