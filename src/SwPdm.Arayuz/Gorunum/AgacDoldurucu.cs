using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Agaci doldurur. Diski KENDISI okumaz - <see cref="KlasorTarayici"/>'ya sorar.
///
/// CLAUDE.md 7: bir arayuz sinifi hem ekran hem is akisi surucusu olmaz.
/// Bu sinif yalnizca "cekirdegin verdigi listeyi dugumlere cevirmek"ten
/// sorumlu; tarama, arama, hata uretme onun isi degil.
///
/// TEMBEL YUKLEME: bir klasor ancak ACILDIGINDA taranir. Sebep olculmemis
/// bir hiz iddiasi degil, somut bir risk: dosyalar ag surucusunde duruyor
/// (\\10.34.1.250\ortak) ve her seyi bastan taramak dakikalarca donma demek.
/// </summary>
internal sealed class AgacDoldurucu
{
    /// <summary>Henuz taranmamis bir dali isaretler; "+" kutusu bunun icin var.</summary>
    private static readonly object HenuzTaranmadi = new();

    private readonly TreeView _agac;
    private DosyaTuru? _turSuzgeci;

    internal AgacDoldurucu(TreeView agac)
    {
        _agac = agac;
        _agac.BeforeExpand += DalAcilirken;
    }

    /// <summary>Ekranda gosterilecek durum cumlesi uretildiginde tetiklenir.</summary>
    internal event EventHandler<string>? Durum;

    /// <summary>Su an acik olan kok klasor. Yoksa null.</summary>
    internal string? Kok { get; private set; }

    /// <summary>null = butun turler.</summary>
    internal DosyaTuru? TurSuzgeci
    {
        get => _turSuzgeci;
        set
        {
            if (_turSuzgeci == value)
            {
                return;
            }

            _turSuzgeci = value;
            if (Kok is not null)
            {
                KokuAc(Kok);
            }
        }
    }

    /// <summary>Bir kok klasoru acar ve ilk seviyeyi gosterir.</summary>
    internal void KokuAc(string yol)
    {
        Kok = yol;
        KlasorIcerigi icerik = KlasorTarayici.Tara(yol);

        _agac.BeginUpdate();
        _agac.Nodes.Clear();

        var kokDugum = new TreeNode(WindowsYolu.DosyaAdi(yol))
        {
            ImageIndex = SimgeSirasi.Klasor,
            SelectedImageIndex = SimgeSirasi.Klasor,
            Tag = new KlasorOgesi(yol, WindowsYolu.DosyaAdi(yol), null, null, icerik.Hata),
            ToolTipText = yol,
        };
        _agac.Nodes.Add(kokDugum);
        DaliDoldur(kokDugum, icerik);
        kokDugum.Expand();
        _agac.EndUpdate();

        if (icerik.Hata is not null)
        {
            // CLAUDE.md 3: sebep EKRANDA. Bos agac "burada bir sey yok" demek DEGILDIR.
            Durum?.Invoke(this, "Klasör okunamadı — " + icerik.Hata);
        }
        else
        {
            Durum?.Invoke(this, Ozet(icerik));
        }
    }

    /// <summary>Acik kokü bastan tarar.</summary>
    internal void Yenile()
    {
        if (Kok is not null)
        {
            KokuAc(Kok);
        }
    }

    /// <summary>Agaci bosaltir.</summary>
    internal void Temizle()
    {
        Kok = null;
        _agac.Nodes.Clear();
    }

    /// <summary>
    /// Arama sonucunu agaca yazar: eslesmeler bulunduklari klasore gore gruplanir.
    /// Kesilme ve sinir asimi GIZLENMEZ.
    /// </summary>
    internal void AramaSonucunuGoster(string metin, AramaSonucu sonuc)
    {
        _agac.BeginUpdate();
        _agac.Nodes.Clear();

        string kokAdi = Kok is null ? "Arama" : WindowsYolu.DosyaAdi(Kok);
        var kokDugum = new TreeNode($"{kokAdi}  —  \"{metin}\": {sonuc.Bulunanlar.Count} eşleşme")
        {
            ImageIndex = SimgeSirasi.Klasor,
            SelectedImageIndex = SimgeSirasi.Klasor,
        };
        _agac.Nodes.Add(kokDugum);

        var gruplar = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        foreach (DosyaOgesi dosya in sonuc.Bulunanlar)
        {
            if (!TureUyuyorMu(dosya.Tur))
            {
                continue;
            }

            string klasor = WindowsYolu.Klasor(dosya.Yol);
            if (!gruplar.TryGetValue(klasor, out TreeNode? grup))
            {
                grup = new TreeNode(GoreceliYol(klasor))
                {
                    ImageIndex = SimgeSirasi.Klasor,
                    SelectedImageIndex = SimgeSirasi.Klasor,
                    Tag = new KlasorOgesi(klasor, WindowsYolu.DosyaAdi(klasor), null, null, null),
                    ToolTipText = klasor,
                };
                gruplar[klasor] = grup;
                kokDugum.Nodes.Add(grup);
            }

            grup.Nodes.Add(DosyaDugumu(dosya));
        }

        kokDugum.ExpandAll();
        _agac.EndUpdate();

        Durum?.Invoke(this, AramaOzeti(sonuc));
    }

    /// <summary>Dugume bagli cekirdek nesnesi; yoksa null.</summary>
    internal static object? Etiket(TreeNode? dugum)
        => ReferenceEquals(dugum?.Tag, HenuzTaranmadi) ? null : dugum?.Tag;

    private void DalAcilirken(object? gonderen, TreeViewCancelEventArgs e)
    {
        TreeNode? dugum = e.Node;
        if (dugum is null || dugum.Nodes.Count != 1)
        {
            return;
        }

        if (!ReferenceEquals(dugum.Nodes[0].Tag, HenuzTaranmadi))
        {
            return;
        }

        if (dugum.Tag is not KlasorOgesi klasor)
        {
            return;
        }

        _agac.BeginUpdate();
        dugum.Nodes.Clear();
        KlasorIcerigi icerik = KlasorTarayici.Tara(klasor.Yol);
        DaliDoldur(dugum, icerik);
        _agac.EndUpdate();

        if (icerik.Hata is not null)
        {
            Durum?.Invoke(this, klasor.Ad + " okunamadı — " + icerik.Hata);
        }
    }

    private void DaliDoldur(TreeNode dal, KlasorIcerigi icerik)
    {
        foreach (KlasorOgesi klasor in icerik.Klasorler)
        {
            dal.Nodes.Add(KlasorDugumu(klasor));
        }

        foreach (DosyaOgesi dosya in icerik.Dosyalar)
        {
            if (TureUyuyorMu(dosya.Tur))
            {
                dal.Nodes.Add(DosyaDugumu(dosya));
            }
        }
    }

    private static TreeNode KlasorDugumu(KlasorOgesi klasor)
    {
        // Sayi BILINMIYORSA "0" yazilmaz - CLAUDE.md 3. "(0)" gormek
        // "ici bos" demektir ve okunamayan bir klasor icin bu YALAN olur.
        string etiket = klasor.DosyaSayisi switch
        {
            null => $"{klasor.Ad}  (okunamadı)",
            0 => klasor.Ad,
            int sayi => $"{klasor.Ad} ({sayi})",
        };

        var dugum = new TreeNode(etiket)
        {
            ImageIndex = SimgeSirasi.Klasor,
            SelectedImageIndex = SimgeSirasi.Klasor,
            Tag = klasor,
            ToolTipText = klasor.Hata is null ? klasor.Yol : klasor.Yol + "\n" + klasor.Hata,
        };

        bool icindeBirSeyVar = (klasor.AltKlasorVarMi ?? false) || (klasor.DosyaSayisi ?? 0) > 0;
        if (icindeBirSeyVar)
        {
            // "+" kutusu ancak bir cocuk varsa cikiyor; gercek icerik acilinca taranacak.
            dugum.Nodes.Add(new TreeNode(string.Empty) { Tag = HenuzTaranmadi });
        }

        return dugum;
    }

    private static TreeNode DosyaDugumu(DosyaOgesi dosya)
    {
        int simge = SimgeSirasi.Turden(dosya.Tur);
        return new TreeNode(dosya.Ad)
        {
            ImageIndex = simge,
            SelectedImageIndex = simge,
            Tag = dosya,
            ToolTipText = dosya.Yol,
        };
    }

    private bool TureUyuyorMu(DosyaTuru tur) => _turSuzgeci is null || _turSuzgeci == tur;

    private string GoreceliYol(string klasor)
    {
        if (Kok is null || klasor.Length <= Kok.Length)
        {
            return klasor;
        }

        return klasor[Kok.Length..].TrimStart(WindowsYolu.Ayirici, WindowsYolu.EgikAyirici);
    }

    private static string Ozet(KlasorIcerigi icerik)
        => $"{icerik.Klasorler.Count} klasör · {icerik.Dosyalar.Count} dosya";

    private static string AramaOzeti(AramaSonucu sonuc)
    {
        string ozet = $"{sonuc.Bulunanlar.Count} eşleşme · {sonuc.TarananKlasor} klasör tarandı";

        // Sessiz kirpma "hepsini kapsadim" gibi okunur (CLAUDE.md 9).
        if (sonuc.Iptal)
        {
            ozet += " · ARAMA YARIDA KESİLDİ";
        }
        else if (sonuc.SinirAsildi)
        {
            ozet += " · SINIRA ULAŞILDI, daha fazlası olabilir";
        }

        if (sonuc.OkunamayanKlasorler.Count > 0)
        {
            ozet += $" · {sonuc.OkunamayanKlasorler.Count} klasör okunamadı";
        }

        return ozet;
    }
}
