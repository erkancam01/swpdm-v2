using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using SwPdm.Arayuz.Gorunum;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz;

/// <summary>
/// Uygulamanin tek penceresi.
///
/// BU SINIF SADECE BAGLAR. Hicbir ozelligin karari burada durmaz: onizleme
/// <see cref="Gorunum.Onizleme"/>'de, arama <see cref="AramaSurucusu"/>'nda,
/// agac <see cref="AgacDoldurucu"/>'da, dosya acma <see cref="DosyaAcici"/>'da,
/// klasor secme <see cref="KokSecici"/>'de, alttaki yazilar
/// <see cref="DurumCubugu"/>'nda.
///
/// CLAUDE.md 1b'nin olcutu: bir ozelligi kaldirmak = onun dosyasini silmek +
/// buradaki BIR baglanti satirini kesmek.
///
/// CLAUDE.md 7 - v1'in en pahali dersi: bir arayuz sinifi hem ekran hem is
/// akisi surucusu OLMAZ. v1'de tek bir arayuz sinifi 9.918 satira, urun
/// kodunun %38'ine cikti ve bolunemedi.
/// </summary>
internal sealed partial class AnaForm : Form
{
    private readonly AgacDoldurucu _doldurucu;
    private readonly AramaSurucusu _arama;
    private readonly KokSecici _kokSecici;
    private readonly AgacMenusu _menu;
    private readonly SurukleBirak _surukleBirak;
    private readonly IlerlemeYuzeyi _ilerleme;
    private readonly DiskIzleyici _izleyici;
    private readonly ReferansSurucusu _referansSurucusu = new();
    private readonly Ayarlar _ayarlar = Ayarlar.Oku();
    private AyarlarSayfasi? _ayarlarSayfasi;
    private readonly Gorunum.Onizleme _onizleme;
    private readonly string? _acilistaAcilacakKok;

    internal AnaForm(string? acilistaAcilacakKok = null)
    {
        TasarimiKur();
        _acilistaAcilacakKok = acilistaAcilacakKok;

        // --- agac
        _doldurucu = new AgacDoldurucu(_agac);
        _doldurucu.Durum += (_, cumle) => _durum.Bilgi(cumle);
        _agac.SecimDegisti += (_, _) => SecimiGoster();
        _suzgecler.SecimDegisti += (_, tur) => _doldurucu.TurSuzgeci = tur;

        // --- siralama: secim KALICI
        _suzgecler.SiralamaSecici.Kur(_ayarlar.Siralama);
        _doldurucu.Siralama = _ayarlar.Siralama;
        _suzgecler.SiralamaSecici.Degisti += (_, sira) =>
        {
            _doldurucu.Siralama = sira;
            _ayarlar.Siralama = sira;
            _ayarlar.Yaz();
        };

        // --- otomatik tazeleme
        _izleyici = new DiskIzleyici(this);
        _izleyici.Degisti += (_, sessiz) => DisaridanDegisti(sessiz);
        _izleyici.Sorun += (_, cumle) =>
        {
            // Izleme koptu: indeks artik tam taramayi atlayamaz.
            _referansSurucusu.IzlemeGuvenilir = false;
            _durum.Bilgi(cumle);
        };

        // --- dosya acma (cift tiklama)
        _agac.NodeMouseDoubleClick += (_, e) =>
        {
            if (AgacDoldurucu.Etiket(e.Node) is DosyaOgesi dosya)
            {
                _durum.Bilgi(DosyaAcici.Ac(this, dosya));
            }
        };

        // --- sag tik menusu ve dosya islemleri (yeni klasor / adlandir / sil / tasi)
        _ilerleme = new IlerlemeYuzeyi(this, _durum, mesgul =>
        {
            _agac.Enabled = !mesgul;

            // ================== OLCULMUS TUZAK ==================
            // Devre disi birakilan bir denetim ODAGI KAYBEDIYOR. Aktarma
            // bitip agac yeniden acilinca odak baska yerde kaliyordu ve
            // kisayollar (Ctrl+Z, Delete, F2...) CALISMIYORDU - cunku
            // asagidaki kisayol kancasi "_agac.Focused" sartina bagli.
            // Belirti sinsi: kopyalama calisiyor, hemen ardindan Ctrl+Z
            // hicbir sey yapmiyor. Odak geri veriliyor.
            // ====================================================
            if (!mesgul && !_agac.IsDisposed)
            {
                _agac.Focus();
            }
        });

        _menu = new AgacMenusu(_agac);
        _menu.SecimKaynagi(SecimBaglamiKur);
        _menu.IlerlemeYuzeyi(_ilerleme);
        _menu.AgaciKapatan(_doldurucu.HepsiniKapat);
        _menu.ReferansSurucusunu(_referansSurucusu);
        _menu.Durum += (_, cumle) => _durum.Bilgi(cumle);
        _menu.Tazele += (_, yol) => AgaciTazele(yol);
        _surukleBirak = new SurukleBirak(_agac);
        _surukleBirak.Tasindi += (_, e) => Aktar.Yurut(
            new IslemBaglami(
                this, SecimBaglamiKur(), AgaciTazele, _durum.Bilgi, _ilerleme,
                _doldurucu.HepsiniKapat, _referansSurucusu),
            e.Yollar,
            e.HedefKlasor,
            AktarmaKipi.Tasi);

        // --- referans listesinde cift tik: o dosyaya GIT
        // PDM'de asil ise yarayan sey bu: "bu parcayi Montaj3 kullaniyor"
        // yazisini gormek yetmez, oraya GIDEBILMEK gerekir.
        _referanslar.MouseDoubleClick += (_, e) => ReferansaGit(_referanslar.TiklananHedef(e.Location));

        // --- klasor secme
        _kokSecici = new KokSecici(_acDugmesi) { Sahip = this };
        _kokSecici.Secildi += (_, yol) => KokuAc(yol);

        // --- yol cubugu: agacin ustunde, tiklanabilir
        _yol.Secildi += (_, klasor) => _doldurucu.YoluSec(klasor);

        // --- onizleme
        _onizleme = new Gorunum.Onizleme(_onizlemePaneli, this);

        // --- arama
        _arama = new AramaSurucusu(_araKutusu, this);
        _arama.Durum += (_, cumle) => _durum.Bilgi(cumle);
        _arama.Mesgul += (_, mesgul) => _agac.Enabled = !mesgul;
        _arama.Bitti += (_, sonuc) => _doldurucu.AramaSonucunuGoster(sonuc.Metin, sonuc.Sonuc);
        _arama.Bosaltildi += (_, _) =>
        {
            if (_doldurucu.AramaKipinde)
            {
                // Aramadan cikarken kullanici actigi dallari ACIK bulmali.
                _doldurucu.GezinmeyeDon();
            }
        };

        // --- kisayollar
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                e.SuppressKeyPress = true;
                _kokSecici.Sor();
                return;
            }

            // Siralama kisayolu: karari SiralamaSecici veriyor, burada
            // yalnizca tus iletiliyor (CLAUDE.md 1b).
            if (_suzgecler.SiralamaSecici.TusaBasildi(e.KeyData))
            {
                e.SuppressKeyPress = true;
                return;
            }

            // Kisayollar islem listesinden geliyor; menudeki yazi ile calisan
            // tus AYRISAMAZ (CLAUDE.md 1b).
            if (_agac.Focused && _menu.TusaBasildi(e.KeyData))
            {
                e.SuppressKeyPress = true;
            }
        };

        // Arac cubugundaki dugme SILMEZ - cop kutusunu ACAR. Silme Delete
        // tusunda ve sag tik menusunde (Erkan: "silme zaten sag tikta var").
        _copDugmesi.Click += (_, _) => CopKutusunuAc();

        // Cop kutusunun YANINDA geri al. Ayni kodu cagiriyor - ikinci kopya
        // yok (CLAUDE.md 8): kisayol, menu ogesi ve bu dugme hep ayni islem.
        _geriAlDugmesi.Click += (_, _) => _menu.TusaBasildi(Keys.Control | Keys.Z);

        _onizleme.Temizle();
        _durum.Bekliyor();
        CopDugmesiniTazele();
        GeriAlDugmesiniTazele();

    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Bolenler ancak denetimin gercek boyutu olustuktan sonra ayarlanabilir.
        BoleniAyarla(_dikeyBolen, 320);
        BoleniAyarla(_altBolen, 282);

        if (!string.IsNullOrWhiteSpace(_acilistaAcilacakKok))
        {
            KokuAc(_acilistaAcilacakKok);
            return;
        }

        // Gecmisi ac dugmesinin listesine koy - once, ki kok acilamasa bile
        // kullanici listeden secebilsin.
        foreach (string eski in _ayarlar.SonKokler)
        {
            _kokSecici.GecmiseEkle(eski);
        }

        SonKokuAc();
    }

    /// <summary>
    /// Pencere GORUNDUKTEN sonra odak agaca verilir.
    ///
    /// OLCULMUS TUZAK: Control.Focus() pencere gorunur DEGILKEN hicbir sey
    /// yapmiyor ve sessizce false donuyor. Odagi acilista <c>KokuAc</c>
    /// icinde vermek bu yuzden ISE YARAMADI - orasi OnLoad'dan cagriliyor
    /// ve pencere henuz gorunmemis oluyor. Belirti sinsi: uygulama aciliyor,
    /// her sey normal gorunuyor, ama HICBIR KISAYOL calismiyor; once agaca
    /// tiklamak gerekiyor. Hata yok, sebep yok.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AgacaOdaklan();
    }

    /// <summary>
    /// Odagi agaca verir. Kisayollar "_agac.Focused" sartina bagli
    /// (menudeki yaziyla calisan tus ayrisamasin diye, CLAUDE.md 1b), yani
    /// odak agacta degilse Ctrl+Shift+R gibi kisayollar hic calismaz.
    /// </summary>
    private void AgacaOdaklan()
    {
        if (IsHandleCreated && Visible && _agac.CanFocus)
        {
            _agac.Focus();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _izleyici.Dispose();
        _arama.Dispose();
        _onizleme.Dispose();
        base.OnFormClosed(e);
    }

    private void KokuAc(string yol)
    {
        _arama.MetniTemizle();

        _doldurucu.KokuAc(yol);
        _yol.KokuKur(_doldurucu.Kok);

        // Kokun tek sahibi AgacDoldurucu; arama onun bildigi kokte arar.
        _arama.Kok = _doldurucu.Kok;

        _onizleme.Temizle();
        _durum.KokAcildi();
        _izleyici.AcKapat(_ayarlar.OtomatikTazele, _doldurucu.Kok);
        _referansSurucusu.IzlemeGuvenilir = _izleyici.Guvenilir;
        _referansSurucusu.KokuKur(_doldurucu.Kok);

        AgacaOdaklan();
        _kokSecici.GecmiseEkle(yol);

        // Kok HATIRLANIR: bir dahaki acilista dosya yolunu yeniden gostermeye
        // gerek kalmasin (Erkan'in istegi).
        _ayarlar.KokEkle(yol);
        _ayarlar.Yaz();

        // Kok degisti: eski yollara bakan geri alma adimlari artik BASKA bir
        // agacin yollari olur ve yanlis yere dokunurdu (CLAUDE.md 1a).
        GeriAlDefteri.Temizle();
        CopDugmesiniTazele();
        GeriAlDugmesiniTazele();
        _ayarlarSayfasi?.Tazele();
    }

    /// <summary>
    /// Islemlere verilecek secim. "Etkin klasor" = secili klasor, yoksa secili
    /// dosyanin klasoru, o da yoksa kok.
    /// </summary>
    private SecimBaglami SecimBaglamiKur()
    {
        var ogeler = new List<object>();
        foreach (TreeNode dugum in _agac.Secililer)
        {
            if (AgacDoldurucu.Etiket(dugum) is object etiket)
            {
                ogeler.Add(etiket);
            }
        }

        string? etkin = null;
        foreach (object oge in ogeler)
        {
            etkin = oge switch
            {
                KlasorOgesi klasor => klasor.Yol,
                DosyaOgesi dosya => WindowsYolu.Klasor(dosya.Yol),
                _ => etkin,
            };

            if (oge is KlasorOgesi)
            {
                break;   // klasor secimi dosyanin klasorune tercih edilir
            }
        }

        return new SecimBaglami(
            ogeler, etkin ?? _doldurucu.Kok, _doldurucu.AramaKipinde,
            _doldurucu.Kok, CopKlasoru());
    }

    /// <summary>
    /// Diskte BASKASI bir sey degistirdi. Agac tazelenir ama secim ve acik
    /// dallar korunur - kullanicinin yeri kaybolmaz.
    /// </summary>
    private void DisaridanDegisti(bool sessiz)
    {
        // AGAC VE INDEKS AYNI OLAYDAN BESLENIR. Once yalnizca agac
        // tazeleniyordu; indeks bu degisikliklerden habersizdi ve guncel
        // kalmasinin tek yolu butun kokun yeniden taranmasiydi.
        _referansSurucusu.Kirlet(_izleyici.Kirlenenler());

        _doldurucu.Yenile();
        CopDugmesiniTazele();

        if (!sessiz)
        {
            _durum.Bilgi("Diskte değişiklik görüldü — ağaç tazelendi.");
        }
    }

    /// <summary>
    /// Bir dosya islemi bitti: agaci diskten tazeler, acik dallari korur.
    /// <paramref name="secilecekYol"/> verilirse orasi secili gelir.
    /// </summary>
    private void AgaciTazele(string? secilecekYol)
    {
        // Kendi islemimiz: izleyici susturuluyor, yoksa iki tazeleme
        // carpisir ve "yeni klasoru sec" davranisi kaybolur.
        _izleyici.Sustur(true);
        _doldurucu.Yenile();
        CopDugmesiniTazele();
        GeriAlDugmesiniTazele();

        if (secilecekYol is not null)
        {
            _doldurucu.YoluSec(secilecekYol);
        }

        SecimiGoster();
        _izleyici.Sustur(false);
    }

    /// <summary>
    /// Referans listesinden bir dosyaya gider.
    ///
    /// GIDILEMEZSE SEBEBI YAZILIR (CLAUDE.md 3). Sessizce hicbir sey
    /// yapmamak, kullaniciya cift tiklamanin bozuk oldugunu dusundurur;
    /// oysa sebep genelde belli: dosya taranan kokun disinda ya da
    /// referans cozulememis.
    /// </summary>
    private void ReferansaGit(string? hedef)
    {
        if (hedef is null)
        {
            _durum.Bilgi("Bu satırda gidilecek bir dosya yok — referans çözülemedi.");
            return;
        }

        if (!_doldurucu.YoluAcVeSec(hedef))
        {
            _durum.Bilgi("Dosya ağaçta bulunamadı (açık kökün dışında olabilir): " + hedef);
            return;
        }

        SecimiGoster();
        _agac.Focus();
    }

    /// <summary>Cop kutusu penceresini acar ve kapaninca agaci tazeler.</summary>
    private void CopKutusunuAc()
    {
        if (_doldurucu.Kok is not string kok)
        {
            _durum.Bilgi("Önce bir klasör açın.");
            return;
        }

        CopKutusuPenceresi.Goster(
            this, Cop.Yolu(kok, _ayarlar.CopUstKlasoru), cumle => _durum.Bilgi(cumle));
        AgaciTazele(null);
    }

    /// <summary>Cop dugmesinin yazisini ve durumunu tazeler.</summary>
    private void CopDugmesiniTazele()
    {
        if (_doldurucu.Kok is not string kok)
        {
            _copDugmesi.Enabled = false;
            _copDugmesi.Text = "Çöp";
            _copDugmesi.ToolTipText = "Çöp kutusu — önce bir klasör açın";
            return;
        }

        int adet = Cop.Listele(Cop.Yolu(kok, _ayarlar.CopUstKlasoru)).Count;
        _copDugmesi.Enabled = true;
        _copDugmesi.Text = adet == 0 ? "Çöp kutusu" : $"Çöp kutusu ({adet})";
        _copDugmesi.ToolTipText = "Silinenleri gör ve geri yükle";
    }

    /// <summary>
    /// Geri al dugmesini tazeler. Ipucu NEYIN geri alinacagini yazar -
    /// kullanici neye bastigini bilmeli (CLAUDE.md 3).
    /// </summary>
    private void GeriAlDugmesiniTazele()
    {
        _geriAlDugmesi.Enabled = GeriAlDefteri.Var;
        _geriAlDugmesi.ToolTipText = GeriAlDefteri.Sonraki is string ad
            ? $"Geri al: {ad}  (Ctrl+Z)"
            : "Geri al — geri alınacak bir işlem yok";
    }

    /// <summary>
    /// En son acilan koku kendiliginden acar.
    ///
    /// Klasor artik yoksa (ag surucusu kapali, disk cikarilmis) SESSIZCE
    /// gecilmez: sebep yazilir ve o kok gecmisten dusurulur, yoksa her
    /// aciliste ayni hatayi verirdi (CLAUDE.md 3).
    /// </summary>
    private void SonKokuAc()
    {
        if (_ayarlar.SonKok is not string kok)
        {
            return;
        }

        if (!Directory.Exists(kok))
        {
            _ayarlar.KokCikar(kok);
            _ayarlar.Yaz();
            _durum.Bilgi("Son açılan klasör bulunamadı: " + kok);
            return;
        }

        KokuAc(kok);
    }

    /// <summary>
    /// Silinenlerin gidecegi klasor. Kullanici ayarlardan degistirmediyse
    /// kokun kendi ici - ayni diskte oldugu icin silme ANLIK.
    /// </summary>
    /// <summary>Ayarlar sekmesinin icerigi. Tasarim tarafindan cagriliyor.</summary>
    private Control AyarlarSayfasiKur()
    {
        var sayfa = new AyarlarSayfasi(_ayarlar, () => _doldurucu?.Kok);
        sayfa.Degisti += (_, _) =>
        {
            CopDugmesiniTazele();
            _izleyici.AcKapat(_ayarlar.OtomatikTazele, _doldurucu.Kok);
        _referansSurucusu.IzlemeGuvenilir = _izleyici.Guvenilir;
        };
        _ayarlarSayfasi = sayfa;
        return sayfa;
    }

    private string? CopKlasoru()
        => _doldurucu.Kok is string kok ? Cop.Yolu(kok, _ayarlar.CopUstKlasoru) : null;

    private void SecimiGoster()
    {
        IReadOnlyList<TreeNode> secililer = _agac.Secililer;

        // Birden cok oge seciliyken tek bir dosyanin onizlemesi gosterilemez;
        // ne secildigi yazilir.
        if (secililer.Count > 1)
        {
            var etiketler = new List<object?>(secililer.Count);
            foreach (TreeNode dugum in secililer)
            {
                etiketler.Add(AgacDoldurucu.Etiket(dugum));
            }

            SecimOzeti ozet = SecimOzeti.Hesapla(etiketler);
            _onizleme.Goster(ozet);
            _referansSurucusu.Doldur(_referanslar, null);
            _durum.Secildi(ozet);
            return;
        }

        switch (AgacDoldurucu.Etiket(secililer.Count == 1 ? secililer[0] : null))
        {
            case DosyaOgesi dosya:
                _onizleme.Goster(dosya, _referansSurucusu.Ozet(dosya.Yol));
                _referansSurucusu.Doldur(_referanslar, dosya.Yol);
                _durum.Secildi(dosya);
                _yol.Goster(WindowsYolu.Klasor(dosya.Yol));
                break;

            case KlasorOgesi klasor:
                _onizleme.Goster(klasor);
                _referansSurucusu.Doldur(_referanslar, null);
                _durum.Secildi(klasor);
                _yol.Goster(klasor.Yol);
                break;

            default:
                _onizleme.Temizle();
                _referansSurucusu.Doldur(_referanslar, null);
                break;
        }
    }

    /// <summary>
    /// SplitterDistance araligin disinda kalirsa istisna atar. Sinira kirpiyoruz:
    /// pencere kucukken acilmak, acilmamaktan iyidir.
    /// </summary>
    private static void BoleniAyarla(SplitContainer bolen, int hedef)
    {
        int uzunluk = bolen.Orientation == Orientation.Horizontal ? bolen.Height : bolen.Width;
        int enBuyuk = uzunluk - bolen.SplitterWidth - bolen.Panel2MinSize;
        int enKucuk = bolen.Panel1MinSize;

        if (enBuyuk < enKucuk)
        {
            return;
        }

        bolen.SplitterDistance = Math.Clamp(hedef, enKucuk, enBuyuk);
    }
}
