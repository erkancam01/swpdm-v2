using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// WindowsYolu'nun ASIL olcumu: Windows'ta, isletim sisteminin kendi
/// ayristirmasiyla (System.IO.Path) karsilastirilir.
///
/// Bu olcum Linux'ta YAPILAMAZ - orada Path'in kendisi bozuk. O yuzden bu
/// sinif Windows disinda SEBEBIYLE atlanir ve CI'in Windows isinde kosar.
/// Linux tarafinda ise Path'in bozuklugu ayrica BELGELENIR: bir gun .NET bunu
/// degistirirse test bize haber verir.
/// </summary>
public class PathIleKarsilastirma
{
    private static readonly string[] Yollar =
    [
        @"C:\a\b.SLDPRT",
        @"C:\a.SLDPRT",
        @"C:\Proje 2.0\parca",
        @"C:\Proje 2.0\parca.SLDPRT",
        @"C:\a\b\",
        @"C:\a\alt klasor\montaj.SLDASM",
        @"\\10.34.1.250\ortak\montaj.SLDASM",
        @"D:\ÜRÜNLER\Parça1.SLDPRT",
        "b.SLDPRT",
    ];

    [WindowsOlgusu]
    public void DosyaAdi_WindowsuntPathIleAYNI()
    {
        foreach (string yol in Yollar)
        {
            Assert.Equal(Path.GetFileName(yol), WindowsYolu.DosyaAdi(yol));
        }
    }

    [WindowsOlgusu]
    public void Uzanti_WindowsunPathIleAYNI()
    {
        foreach (string yol in Yollar)
        {
            Assert.Equal(Path.GetExtension(yol), WindowsYolu.Uzanti(yol));
        }
    }

    [WindowsOlgusu]
    public void Klasor_WindowsunPathIleAYNI()
    {
        foreach (string yol in Yollar)
        {
            string beklenen = Path.GetDirectoryName(yol) ?? string.Empty;
            Assert.Equal(beklenen, WindowsYolu.Klasor(yol));
        }
    }

    [WindowsOlgusu]
    public void GecersizKarakterListesi_WindowsunListesiniKAPSAR()
    {
        // Elle yazilan liste, isletim sisteminin bildirdigi her karakteri
        // icermeli. Eksik kalirsa gecersiz bir ad kabul edilir.
        foreach (char karakter in Path.GetInvalidFileNameChars())
        {
            Assert.Contains(karakter, WindowsYolu.GecersizAdKarakterleri);
        }
    }

    // ---------------------------------------------------------------
    // Windows DISINDA: Path'in bozuklugunu belgeleyen olcum.
    // CLAUDE.md 4 bunlari cikti olarak yaziyor; burasi onun kosan hali.
    // Bu test KIRILIRSA .NET davranisini degistirmis demektir - iyi haber,
    // ama CLAUDE.md guncellenmeli.
    // ---------------------------------------------------------------

    [WindowsDisiOlgusu]
    public void LinuxtePathHALA_TersBoluyuAyiriciSAYMIYOR()
    {
        Assert.Equal(@".0\parca", Path.GetExtension(@"C:\Proje 2.0\parca"));
        Assert.Equal(@"C:\a\b.SLDPRT", Path.GetFileName(@"C:\a\b.SLDPRT"));
        Assert.Equal(string.Empty, Path.GetDirectoryName(@"C:\a\b.SLDPRT"));
        Assert.Equal(2, Path.GetInvalidFileNameChars().Length);

        // Ayni girdilerde WindowsYolu DOGRU cevabi veriyor:
        Assert.Equal(string.Empty, WindowsYolu.Uzanti(@"C:\Proje 2.0\parca"));
        Assert.Equal("b.SLDPRT", WindowsYolu.DosyaAdi(@"C:\a\b.SLDPRT"));
        Assert.Equal(@"C:\a", WindowsYolu.Klasor(@"C:\a\b.SLDPRT"));
    }
}
