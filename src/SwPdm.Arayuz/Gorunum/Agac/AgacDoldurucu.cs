using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Agaci doldurur. Diski KENDISI okumaz - <see cref="KlasorTarayici"/>'ya sorar.
///
/// CLAUDE.md 7: bir arayuz sinifi hem ekran hem is akisi surucusu olmaz.
///
/// TEMBEL YUKLEME: bir klasor ancak ACILDIGINDA taranir. Sebep somut: dosyalar
/// ag surucusunde duruyor ve her seyi bastan taramak dakikalarca donma demek.
///
/// TARANANI HATIRLAR: her acilan klasorun icerigi onbellege aliniyor. Boylece
/// tur suzgeci degistiginde agac YENIDEN KURULMUYOR - yalnizca dosya dugumleri
/// tazeleniyor. Erkan'in bildirdigi hata buydu: suzgece basinca actigi butun
/// dallar kapaniyordu.
/// </summary>
internal sealed partial class AgacDoldurucu
{
    /// <summary>Henuz taranmamis bir dali isaretler; "+" kutusu bunun icin var.</summary>
    private static readonly object HenuzTaranmadi = new();

    /// <summary>Kilitli klasorun adinin yanina yazilan.</summary>
    private const string KilitliIsareti = "• kilitli";

    private readonly SecimliAgac _agac;
    private readonly Dictionary<TreeNode, KlasorIcerigi> _taranan = [];
    private DosyaTuru? _turSuzgeci;
    private Siralama _siralama = Siralama.Varsayilan;
    private string? _aramaMetni;
    private AramaSonucu? _aramaSonucu;
    private AgacDurumu? _gezinmeDurumu;
    private KilitKumesi _kilitler = KilitKumesi.Bos;

    internal AgacDoldurucu(SecimliAgac agac)
    {
        _agac = agac;
        _agac.BeforeExpand += DalAcilirken;
    }

    /// <summary>Ekranda gosterilecek durum cumlesi uretildiginde tetiklenir.</summary>
    internal event EventHandler<string>? Durum;

    /// <summary>Su an acik olan kok klasor. Yoksa null.</summary>
    internal string? Kok { get; private set; }

    /// <summary>
    /// Kilitli klasorlerin anlik hali. BIR KEZ okunuyor (kok acilirken /
    /// yenilenirken), sonra her dugum ondan soruyor - kilidi dugum basina
    /// diskten sormak ag surucusunde dugum basina bir gidis gelis olurdu
    /// (CLAUDE.md 4).
    /// </summary>
    internal KilitKumesi Kilitler => _kilitler;

    /// <summary>Agac su an arama sonucu mu gosteriyor.</summary>
    internal bool AramaKipinde => _aramaSonucu is not null;

    /// <summary>
    /// Agacin sirasi. Degisince agac YENIDEN KURULMUYOR: yalnizca taranmis
    /// dallar diskten tazeleniyor ve acik dallar korunuyor.
    /// </summary>
    internal Siralama Siralama
    {
        get => _siralama;
        set
        {
            if (_siralama == value)
            {
                return;
            }

            _siralama = value;

            if (Kok is not null)
            {
                Yenile();
            }
        }
    }

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

            if (_aramaSonucu is not null)
            {
                AramaSonucunuGoster(_aramaMetni ?? string.Empty, _aramaSonucu);
            }
            else if (Kok is not null)
            {
                // Agac YENIDEN KURULMUYOR: acik dallar oldugu gibi kaliyor,
                // yalnizca dosya dugumleri onbellekten tazeleniyor.
                SuzgeciYenidenUygula();
            }
        }
    }

    /// <summary>Bir kok klasoru acar ve ilk seviyeyi gosterir.</summary>
    internal void KokuAc(string yol, AgacDurumu? geriYuklenecek = null)
    {
        Kok = yol;
        _aramaMetni = null;
        _aramaSonucu = null;
        _taranan.Clear();
        _kilitler = KlasorKilidi.Oku(yol);

        // Dugumler yok edilecek: kumede kalan olu dugumler "3 oge secili"
        // yazip ekranda hicbir sey secili gostermezdi (CLAUDE.md 3).
        _agac.SecimiTemizle();

        KlasorIcerigi icerik = KlasorTarayici.Tara(yol, _siralama);

        _agac.BeginUpdate();
        _agac.Nodes.Clear();

        var kokDugum = new TreeNode(WindowsYolu.DosyaAdi(yol))
        {
            ImageIndex = TurSimgeleri.Klasor,
            SelectedImageIndex = TurSimgeleri.Klasor,
            Tag = new KlasorOgesi(yol, WindowsYolu.DosyaAdi(yol), null, null, icerik.Hata),
            ToolTipText = yol,
        };
        _agac.Nodes.Add(kokDugum);
        DaliDoldur(kokDugum, icerik);
        kokDugum.Expand();
        _agac.EndUpdate();

        if (geriYuklenecek is not null)
        {
            AgacDurumlari.GeriYukle(_agac, geriYuklenecek);
        }

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

    /// <summary>Acik kokü bastan tarar; acik dallari ve secimi KORUR.</summary>
    internal void Yenile()
    {
        if (Kok is null)
        {
            return;
        }

        AgacDurumu durum = AgacDurumlari.Al(_agac);
        KokuAc(Kok, durum);
    }

    /// <summary>Agaci bosaltir.</summary>
    internal void Temizle()
    {
        Kok = null;
        _aramaMetni = null;
        _aramaSonucu = null;
        _taranan.Clear();
        _agac.SecimiTemizle();
        _agac.Nodes.Clear();
    }

    /// <summary>
    /// Verilen yoldaki dugumu secer ve gorunur yapar. Dugum agacta yoksa
    /// (ust dali acilmamissa) baska bir sey SECMEZ - yanlis oge secmek
    /// sonraki islemi yanlis hedefe yollar.
    ///
    /// SESSIZ DEGIL (29.08.2026): burasi eskiden duz "return" ediyordu ve
    /// bunun gorunur bir sonucu vardi - ARAMA KIPINDE yol cubugundaki her
    /// tiklama hicbir sey yapmiyordu (arama sonucunda agacta yalnizca
    /// eslesme gruplari var). Imlec el sekline giriyor, tiklaniyor, hicbir
    /// sey olmuyor, sebep de yok (CLAUDE.md 3). Artik sebep sayiliyor.
    /// </summary>
    internal void YoluSec(string yol)
    {
        TreeNode? dugum = AgacDurumlari.DuguuBul(_agac, yol);
        if (dugum is null)
        {
            Durum?.Invoke(this, AramaKipinde
                ? "Arama sonucundayken yol çubuğu kullanılamaz — önce aramayı temizleyin (Esc)."
                : "Bu klasör ağaçta açık değil: " + yol);
            return;
        }

        _agac.YalnizSec(dugum);
        dugum.EnsureVisible();
    }

    /// <summary>
    /// Verilen yola GIDER: ust dallari acar, sonra dugumu secer.
    ///
    /// <see cref="YoluSec"/>'ten farki: o yalnizca ZATEN ACIK bir dalda
    /// calisir. Referans listesinden bir dosyaya gecerken hedef cogu zaman
    /// kapali bir dalda duruyor; ust zincir acilmadan bulunamaz.
    ///
    /// Doner deger: gercekten gidildi mi. false donerse cagiran SEBEBI
    /// soylemeli - sessizce hicbir sey yapmamak, kullaniciya tiklamanin
    /// bozuk oldugunu dusundurur (CLAUDE.md 3).
    /// </summary>
    internal bool YoluAcVeSec(string yol)
    {
        if (Kok is not string kok || string.IsNullOrWhiteSpace(yol))
        {
            return false;
        }

        // "Altinda mi" TEK kopyadan (CLAUDE.md 8). Onceki elle StartsWith
        // AYIRICISIZDI ve "C:\Kok2"yi "C:\Kok"un ici sayiyordu - yani komsu
        // bir klasorun dosyasina gitmeye kalkip yanlis dallari acabilirdi.
        if (!WindowsYolu.AltindaMi(yol, kok))
        {
            return false;   // taranan kokun disinda
        }

        // Ust klasorler KOKTEN ASAGIYA acilir: her Expand, tembel yuklemeyi
        // tetikleyip bir alt seviyeyi olusturuyor. Ters sirada acmak
        // calismaz cunku alt dugum daha var olmamis olur.
        var zincir = new List<string>();
        string klasor = WindowsYolu.Klasor(yol);
        while (klasor.Length > kok.Length && WindowsYolu.AltindaMi(klasor, kok))
        {
            zincir.Add(klasor);
            klasor = WindowsYolu.Klasor(klasor);
        }

        zincir.Reverse();
        foreach (string ata in zincir)
        {
            AgacDurumlari.DuguuBul(_agac, ata)?.Expand();
        }

        TreeNode? dugum = AgacDurumlari.DuguuBul(_agac, yol);
        if (dugum is null)
        {
            return false;
        }

        _agac.YalnizSec(dugum);
        dugum.EnsureVisible();
        return true;
    }

    /// <summary>
    /// Butun dallari kapatir ve koke doner. KOK ACIK KALIR - her seyi
    /// kapatmak "klasor bosaldi" hissi verirdi.
    /// </summary>
    internal void HepsiniKapat()
    {
        if (_agac.Nodes.Count == 0)
        {
            return;
        }

        _agac.BeginUpdate();
        _agac.CollapseAll();
        TreeNode kok = _agac.Nodes[0];
        kok.Expand();
        _agac.EndUpdate();

        // Secim koke iner: kullanici basa dondu, secili oge derinlerde
        // kalirsa sonraki islem gormedigi bir yere gider (CLAUDE.md 1a).
        _agac.YalnizSec(kok);
        kok.EnsureVisible();
    }

    /// <summary>Dugume bagli cekirdek nesnesi; yoksa null.</summary>
    internal static object? Etiket(TreeNode? dugum)
        => ReferenceEquals(dugum?.Tag, HenuzTaranmadi) ? null : dugum?.Tag;

    // ------------------------------------------------------------- suzgec

    /// <summary>
    /// Tur suzgeci degisince yalnizca DOSYA dugumlerini tazeler. Klasor
    /// dugumlerine ve aciklik durumuna DOKUNMAZ - Erkan'in bildirdigi hata
    /// (suzgece basinca acik dallarin kapanmasi) tam olarak buydu.
    /// Disk yeniden okunmaz; onbellekten calisir.
    /// </summary>
    private void SuzgeciYenidenUygula()
    {
        string? seciliyken = Etiket(_agac.SelectedNode) switch
        {
            DosyaOgesi dosya => dosya.Yol,
            KlasorOgesi klasor => klasor.Yol,
            _ => null,
        };

        // Dosya dugumleri yok edilip yeniden kurulacak; kumedeki eski dugum
        // nesneleri artik agacta degil. Secim bosaltilir, odakli dugum
        // asagida geri konur.
        _agac.SecimiTemizle();

        _agac.BeginUpdate();
        foreach ((TreeNode dal, KlasorIcerigi icerik) in _taranan)
        {
            // ================== OLCULMUS TUZAK (CLAUDE.md 6) ==================
            // Bir dugumun BUTUN cocuklari silinince TreeView onu DARALTIYOR ve
            // cocuk geri eklendiginde acik hali GERI GELMIYOR.
            //
            // Erkan'in bildirdigi hata tam buydu: yalnizca dosya iceren bir
            // klasorde suzgec butun cocuklari kaldiriyor, dugum o an sifir
            // cocuklu kaliyor ve kapaniyor. Ilk duzeltmem yetmedi cunku
            // "agaci yeniden kurmuyorum" demek tek basina yetmiyor.
            // ===================================================================
            bool acikti = dal.IsExpanded;

            for (int i = dal.Nodes.Count - 1; i >= 0; i--)
            {
                if (dal.Nodes[i].Tag is DosyaOgesi)
                {
                    dal.Nodes.RemoveAt(i);
                }
            }

            DosyaSatiri.Ekle(dal, icerik.Dosyalar, TureUyuyorMu);

            if (acikti && dal.Nodes.Count > 0)
            {
                dal.Expand();
            }
        }

        _agac.EndUpdate();

        // Secim geri koyma BeginUpdate'in DISINDA. Icerideyken secim
        // degistirmek olay zincirini cizim kapaliyken calistiriyor ve orasi
        // yerli denetimin guvenilir cevap vermedigi bir hal (bkz. SecimliAgac
        // .Secililer). Dosya dugumleri yok edilip yeniden kuruldugu icin secim
        // YOLDAN bulunur; suzgec o dosyayi gizlediyse geri konacak bir sey
        // yoktur ve secim BOS kalir - eski bir dugume yapisik kalmaz.
        if (seciliyken is not null)
        {
            TreeNode? geri = AgacDurumlari.DuguuBul(_agac, seciliyken);
            if (geri is not null)
            {
                _agac.YalnizSec(geri);
            }
        }

        if (_agac.Nodes.Count > 0 && _taranan.TryGetValue(_agac.Nodes[0], out KlasorIcerigi? kokIcerik))
        {
            Durum?.Invoke(this, Ozet(kokIcerik));
        }
    }

    // ------------------------------------------------------------- doldurma

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
        KlasorIcerigi icerik = KlasorTarayici.Tara(klasor.Yol, _siralama);
        DaliDoldur(dugum, icerik);
        _agac.EndUpdate();

        if (icerik.Hata is not null)
        {
            Durum?.Invoke(this, klasor.Ad + " okunamadı — " + icerik.Hata);
        }
    }

    private void DaliDoldur(TreeNode dal, KlasorIcerigi icerik)
    {
        _taranan[dal] = icerik;   // suzgec degisince diske geri donmemek icin

        foreach (KlasorOgesi klasor in icerik.Klasorler)
        {
            dal.Nodes.Add(KlasorDugumu(klasor));
        }

        DosyaSatiri.Ekle(dal, icerik.Dosyalar, TureUyuyorMu);
    }

    private TreeNode KlasorDugumu(KlasorOgesi klasor)
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
            ImageIndex = TurSimgeleri.Klasor,
            SelectedImageIndex = TurSimgeleri.Klasor,
            Tag = klasor,
            ToolTipText = klasor.Hata is null ? klasor.Yol : klasor.Yol + "\n" + klasor.Hata,
        };

        // KILITLI KLASOR ACILMAZ (Erkan, 31.08.2026: "bitmis isleri
        // kilitleyeyim, ondan sonra rahat rahat calisayim"). Yer tutucu
        // cocuk EKLENMEZ -> "+" kutusu hic cikmaz -> dal acilamaz -> icindeki
        // dosya agacta hic gorunmez, yani kazayla secilip degistirilemez.
        // Tek "if"; ozelligi kaldirmak bu blogu silmek (CLAUDE.md 1b).
        //
        // KLASOR GIZLENMIYOR, dosya sayisi da yazmaya devam ediyor:
        // gizlemek "klasor kayboldu" dedirtirdi (CLAUDE.md 3).
        if (_kilitler.KendisiKilitli(klasor.Yol))
        {
            dugum.Text = $"{dugum.Text}   {KilitliIsareti}";
            dugum.BackColor = Renkler.KilitliKlasorZemin;
            dugum.ForeColor = Renkler.KilitliKlasorYazi;
            dugum.ToolTipText =
                klasor.Yol + "\n" + "KİLİTLİ — açılmaz. Sağ tık (Ctrl+Shift+Q) ile kilidi kaldırın."
                + "\nKaza koruması: Gezgin'den yine açılabilir.";
            return dugum;
        }

        bool icindeBirSeyVar = (klasor.AltKlasorVarMi ?? false) || (klasor.DosyaSayisi ?? 0) > 0;
        if (icindeBirSeyVar)
        {
            dugum.Nodes.Add(new TreeNode(string.Empty) { Tag = HenuzTaranmadi });
        }

        return dugum;
    }

    private bool TureUyuyorMu(DosyaTuru tur) => _turSuzgeci is null || _turSuzgeci == tur;

    /// <summary>
    /// Arama sonucunda klasoru koke gore gosterir. Kokun kendisi icin tam yol
    /// yazmak ekrani tasiriyordu; kokun ADI yeter.
    /// </summary>
    private string GoreceliYol(string klasor)
    {
        if (Kok is null)
        {
            return klasor;
        }

        if (klasor.Length <= Kok.Length)
        {
            return WindowsYolu.DosyaAdi(Kok);
        }

        return klasor[Kok.Length..].TrimStart(WindowsYolu.Ayirici, WindowsYolu.EgikAyirici);
    }

    /// <summary>
    /// Durum cumlesi. Suzgec dosya GIZLIYORSA bunu soyler.
    ///
    /// CLAUDE.md 3: gizlenen dosyayi hic soylememek, kullaniciya klasorun
    /// oldugundan bos gorunmesine yol acar.
    /// </summary>
    private string Ozet(KlasorIcerigi icerik)
    {
        KilitDurumu kilit = Kilit.Coz(icerik.Dosyalar);

        int gorunen = 0;
        foreach (DosyaOgesi dosya in kilit.Gosterilecek)
        {
            if (TureUyuyorMu(dosya.Tur))
            {
                gorunen++;
            }
        }

        // Suzgecin gizledigi ile KILIDIN gizledigi ayri ayri yaziliyor:
        // ikisi ayni sey degil ve "neden 8 yerine 7 gorunuyor" sorusunun
        // cevabi ekranda olmali (CLAUDE.md 3).
        int suzgecinDisinda = kilit.Gosterilecek.Count - gorunen;
        string dosyaKismi = suzgecinDisinda == 0
            ? $"{gorunen} dosya"
            : $"{gorunen} / {kilit.Gosterilecek.Count} dosya (süzgeç açık)";

        string kilitKismi = kilit.GizlenenSayisi == 0
            ? string.Empty
            : $" · {kilit.GizlenenSayisi} kilit dosyası gizlendi (açık belgeler işaretli)";

        return $"{icerik.Klasorler.Count} klasör · {dosyaKismi}{kilitKismi}";
    }
}
