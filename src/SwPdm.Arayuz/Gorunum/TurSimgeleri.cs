using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Agacin ve listelerin kullandigi ortak <see cref="ImageList"/> ve icindeki
/// siralar. IKISI DE ayni kaynaktan - <see cref="DosyaTurleri.Turler"/> -
/// uretiliyor, yani kayamazlar (CLAUDE.md 1b).
///
/// Once boyle degildi: bir yerde elle yazilmis simge listesi, baska bir yerde
/// elle yazilmis sabitler vardi ve ayni sirada olmalarini yalnizca bir yorum
/// satiri sagliyordu.
///
/// Yeni bir tur eklemek icin BU DOSYAYA DOKUNMAK GEREKMEZ. Turu cekirdekteki
/// listeye eklemek yeter; simgesi Windows kabugundan gelir, kabuk vermezse
/// genel dosya simgesi cizilir. Ozel bir cizim isteniyorsa
/// <see cref="Cizimler"/>'e bir satir eklenir - o kadar.
/// </summary>
internal static class TurSimgeleri
{
    /// <summary>Klasor simgesinin sirasi. Her zaman ilk.</summary>
    internal const int Klasor = 0;

    /// <summary>
    /// Turu olan dosyalar icin ISTEGE BAGLI cizilmis yedek. Kabuk simgeyi
    /// verirse zaten kullanilmaz; burada olmayan bir tur genel dosya
    /// simgesine duser.
    /// </summary>
    private static readonly Dictionary<DosyaTuru, Func<Bitmap>> Cizimler = new()
    {
        [DosyaTuru.Parca] = Simgeler.Parca,
        [DosyaTuru.Montaj] = Simgeler.Montaj,
        [DosyaTuru.TeknikResim] = Simgeler.TeknikResim,
        [DosyaTuru.Pdf] = Simgeler.Pdf,
    };

    private static readonly IReadOnlyList<DosyaTuru> Sirali = DosyaTurleri.Turler();

    /// <summary>Tanimadigimiz uzantilarin simge sirasi. Her zaman son.</summary>
    internal static int GenelDosya => Sirali.Count + 1;

    /// <summary>Turun simge sirasi. Kayitli degilse genel dosya simgesi.</summary>
    internal static int Sira(DosyaTuru tur)
    {
        for (int i = 0; i < Sirali.Count; i++)
        {
            if (Sirali[i] == tur)
            {
                return i + 1;   // 0 klasorun
            }
        }

        return GenelDosya;
    }

    /// <summary>
    /// Ortak simge listesini kurar.
    ///
    /// Once WINDOWS KABUGU denenir: SOLIDWORKS kurulu bir makinede
    /// .SLDPRT/.SLDASM/.SLDDRW simgeleri kabuga kayitlidir ve Gezgin'de
    /// gorunen GERCEK simge gelir. Kabuk vermezse koda cizilmis yedege
    /// dusulur - hicbir durumda simgesiz kalinmaz.
    /// </summary>
    internal static ImageList Liste()
    {
        var liste = new ImageList
        {
            ImageSize = new Size(Simgeler.Boy, Simgeler.Boy),
            ColorDepth = ColorDepth.Depth32Bit,
        };

        // [Klasor]
        liste.Images.Add(KabukSimgeleri.Klasor() ?? Simgeler.Klasor());

        // [1..N] cekirdekteki tur listesi, ayni sirayla
        foreach (DosyaTuru tur in Sirali)
        {
            string? uzanti = DosyaTurleri.Uzantisi(tur);
            Bitmap? kabuktan = uzanti is null ? null : KabukSimgeleri.Dosya(uzanti);
            liste.Images.Add(kabuktan ?? Yedek(tur));
        }

        // [GenelDosya] tanimadigimiz uzantilar - kabugun genel simgesi zaten
        // bizim yedegimizle ayni ise gerek yok, cizilmis kalir.
        liste.Images.Add(Simgeler.Dosya());

        return liste;
    }

    private static Bitmap Yedek(DosyaTuru tur)
        => Cizimler.TryGetValue(tur, out Func<Bitmap>? ciz) ? ciz() : Simgeler.Dosya();
}
