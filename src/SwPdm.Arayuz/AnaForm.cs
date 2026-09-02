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
    private readonly ReferansMenusu _referansMenusu;

    /// <summary>Referans panelinin su an gosterdigi dosya; serit degisince gerekiyor.</summary>
    private string? _referansYolu;
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
        _agac.Durum += (_, cumle) => _durum.Bilgi(cumle);
        _suzgecler.SecimDegisti += (_, tur) => _doldurucu.TurSuzgeci = tur;
        _suzgecler.Durum += (_, cumle) => _durum.Bilgi(cumle);

        // --- siralama: kalicilik ve duyuru kararlari secicinin kendi dosyasinda
        _suzgecler.SiralamaSecici.Kur(_ayarlar.Siralama);
        _doldurucu.Siralama = _ayarlar.Siralama;
        _suzgecler.SiralamaSecici.Degisti += (_, sira) => _doldurucu.Siralama = sira;
        _suzgecler.SiralamaSecici.KaliciligiBagla(_ayarlar, cumle => _durum.Bilgi(cumle));

        // --- otomatik tazeleme
        _izleyici = new DiskIzleyici(this);
        _izleyici.Degisti += (_, sessiz) => DisaridanDegisti(sessiz);
        _izleyici.Sorun += (_, cumle) =>
        {
            // Izleme koptu: indeks artik tam taramayi atlayamaz.
            _referansSurucusu.IzlemeGuvenilir = false;
            _durum.Bilgi(cumle);
        };

        // --- baslik seridi: raptiyenin karari kendi dosyasinda (CLAUDE.md 1b)
        _baslik.RaptiyeyiBagla(this, cumle => _durum.Bilgi(cumle));
        _baslik.AyarDugmesi.Click += (_, _) => _sekmeler.SelectedTab = _ayarlarSekmesi;

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
        _menu.AgactaGosteren(hedef => ReferansaGit(hedef));
        _menu.ReferansSurucusunu(_referansSurucusu);
        _menu.Durum += (_, cumle) => _durum.Bilgi(cumle);
        _menu.Tazele += (_, yol) => AgaciTazele(yol);
        _surukleBirak = new SurukleBirak(_agac);
        _surukleBirak.Tasindi += (_, e) =>
        {
            // Surukle-birak AgacMenusu'nden GECMEZ; 3B belge kilidi burada
            // ayrica birakilir. "?." sart: lambda kurulurken _onizleme HENUZ
            // atanmamis ve derleyici bunu yakaladi - "() => _doldurucu?.Kok"
            // ile ayni sebep (CLAUDE.md 6'nin kurucu tuzagi).
            // KILIT SURUKLE-BIRAKTA DA GECERLI: bu yol AgacMenusu'nden
            // GECMIYOR, yani merkezi denetim burayi gormuyor. Kaynak ya da
            // HEDEF kilitliyse bitmis is bozulurdu (CLAUDE.md 1a).
            KilitKumesi kilitler = _doldurucu.Kilitler;
            string? kilitliOlan = kilitler.Kilitli(e.HedefKlasor) ? e.HedefKlasor : null;
            foreach (string yol in e.Yollar)
            {
                kilitliOlan ??= kilitler.Kilitli(yol) ? yol : null;
            }

            if (kilitliOlan is not null)
            {
                _durum.Bilgi(
                    $"\"{WindowsYolu.DosyaAdi(kilitliOlan)}\" kilitli — sağ tık ile kilidi kaldırın.");
                return;
            }

            _onizleme?.BelgeyiBirak();
            Aktar.Yurut(
                new IslemBaglami(
                    this, SecimBaglamiKur(), AgaciTazele, _durum.Bilgi, _ilerleme,
                    _doldurucu.HepsiniKapat, _referansSurucusu,
                    hedef => ReferansaGit(hedef)),
                e.Yollar,
                e.HedefKlasor,
                e.Kopyala ? AktarmaKipi.Kopyala : AktarmaKipi.Tasi);
        };

        // --- klasor secme
        _kokSecici = new KokSecici(_acDugmesi) { Sahip = this };
        _kokSecici.Secildi += (_, yol) => KokuAc(yol);

        // --- yol cubugu: agacin ustunde, tiklanabilir
        _yol.Secildi += (_, klasor) => _doldurucu.YoluSec(klasor);

        // --- onizleme. 3B ayari LAMBDA ile okunur: Ayarlar'dan degistirilince
        // bir sonraki secimde aninda etkir.
        _onizleme = new Gorunum.Onizleme(
            _onizlemePaneli, this, () => _ayarlar.OnizlemeUcBoyutlu);
        _onizleme.Durum += (_, cumle) => _durum.Bilgi(cumle);

        // DOSYA KILIDI (CLAUDE.md 1a): 3B kipte eDrawings actigi dosyayi
        // tutar; her islem baslamadan belge birakilir. Islemlerin tamami
        // AgacMenusu.Calistir'dan gecer - surukle-birak ve cop penceresi
        // asagida ayrica baglanir.
        _menu.IslemOncesi(() => _onizleme.BelgeyiBirak());

        // --- referans paneli (AnaForm.Referans.cs): sag tik · serit ·
        // tek tik · cift tik · "satira git". "_onizleme" atamasindan
        // SONRA cagriliyor - IslemOncesi kancasi onu okuyor.
        _referansMenusu = ReferansPaneliniKur();

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

        // --- kisayollar: ayrica KURULMUYOR. Hepsi ProcessCmdKey'den geciyor
        // (AnaForm.Kisayollar.cs); baglanacak bir olay yok.

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

        // Yerlesim (boyut, boluculer, suzgec) HATIRLANIYOR; karari
        // Yerlesim dosyasi veriyor (CLAUDE.md 1b). Boluculer ancak denetimin
        // gercek boyutu olustuktan sonra ayarlanabilir - orasi da orada.
        Yerlesim.Uygula(this, _dikeyBolen, _altBolen, _suzgecler, _ayarlar);

        // Acilista hangi kok acilir - kararin tamami KokSecici'de
        // (CLAUDE.md 1b); Secildi olayi zaten KokuAc'a bagli.
        _kokSecici.AcilistaAc(_ayarlar, _acilistaAcilacakKok, cumle => _durum.Bilgi(cumle));
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

    /// <summary>Kapanirken yerlesim saklanir; bir dahaki acilista geri gelir.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Yerlesim.Sakla(this, _dikeyBolen, _altBolen, _suzgecler, _ayarlar);
        _ayarlar.Yaz();
        base.OnFormClosing(e);
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
        _arama.Kilitler = _doldurucu.Kilitler;

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
        //
        // PANO DA BOSALIR - AYNI GEREKCE, ama once unutulmustu: panoda A
        // kokunun mutlak yollari duruyor, agacta B koku aciliyordu ve
        // "Yapıştır (3 öğe)" hicbir sey belli etmeden A'dan B'ye tasiyordu.
        GeriAlDefteri.Temizle();
        Pano.Bosalt();
        CopDugmesiniTazele();
        GeriAlDugmesiniTazele();
        _ayarlarSayfasi?.Tazele();

        // KOK ACILINCA REFERANSLAR KENDILIGINDEN TARANIR (Erkan, 02.09.2026:
        // "uygulama açılır açılmaz otomatik tarama yapsın").
        //
        // NEDEN GUVENLI: tarama zaten ARKA PLANDA kosuyor (ReferansTaramaIslemi
        // bir Task aciyor), ilerleme cubugu ve IPTAL dugmesi var, Esc de
        // iptal ediyor - yani ag surucusunde uzun surerse uygulama
        // kullanilamaz hale GELMIYOR. Ikinci ve sonraki taramalar artimli:
        // boyutu ve tarihi degismeyen dosya bir daha acilmiyor.
        //
        // NEDEN BU SATIR: tarama menude, kisayolda ve burada AYNI koddan
        // geciyor (CLAUDE.md 1b) - ikinci bir "tarama baslat" kopyasi yok.
        // Geri al dugmesi de ayni kalibi kullaniyor.
        _menu.TusaBasildi(Keys.Control | Keys.Shift | Keys.R);
    }

    /// <summary>
    /// Islemlere verilecek secim. Agactan ogeleri TOPLAMAK baglama isi
    /// (agaci yalniz bu sinif bilir); "etkin klasor" KURALI ise tipin kendi
    /// dosyasinda (SecimBaglami.Kur, CLAUDE.md 1b).
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

        return SecimBaglami.Kur(
            ogeler, _doldurucu.Kok, _doldurucu.AramaKipinde, CopKlasoru(), _doldurucu.Kilitler);
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

        // ARAMADA KALINIR. "Yenile" arama kipini DUSURUYOR (KokuAc
        // _aramaSonucu'nu null yapiyor) ve kutuda metin dururken agac
        // sessizce gezinmeye donuyordu - kullanici sildigi dosyanin
        // arama sonucunda ne oldugunu goremiyordu. Arama kipindeysek
        // sonuc yeniden URETILIYOR (diskten), degilse eski yol.
        if (!_doldurucu.AramaKipinde || !_arama.YenidenAra())
        {
            _doldurucu.Yenile();
        }

        CopDugmesiniTazele();
        GeriAlDugmesiniTazele();

        if (secilecekYol is not null)
        {
            _doldurucu.YoluSec(secilecekYol);
        }

        SecimiGoster();
        _izleyici.Sustur(false);
    }


    /// <summary>Cop kutusu penceresini acar ve kapaninca agaci tazeler.</summary>
    private void CopKutusunuAc()
    {
        if (_doldurucu.Kok is not string kok)
        {
            _durum.Bilgi("Önce bir klasör açın.");
            return;
        }

        // Geri yukleme onizlenen dosyanin USTUNE yazabilir ("Değiştir");
        // 3B belge kilidi pencere acilmadan birakilir.
        _onizleme.BelgeyiBirak();
        CopKutusuPenceresi.Goster(
            this, Cop.Yolu(kok, _ayarlar.CopUstKlasoru), cumle => _durum.Bilgi(cumle));
        AgaciTazele(null);
    }

    /// <summary>Kurallar AracDugmeleri'nde; burasi yalnizca baglar.</summary>
    private void CopDugmesiniTazele()
        => AracDugmeleri.CopuTazele(_copDugmesi, _doldurucu.Kok, _ayarlar);

    private void GeriAlDugmesiniTazele() => AracDugmeleri.GeriAliTazele(_geriAlDugmesi);


    /// <summary>Ayarlar sekmesinin icerigi. Tasarim tarafindan cagriliyor.</summary>
    private Control AyarlarSayfasiKur()
    {
        // "_durum.Bilgi" DEGIL, LAMBDA - ve sebebi OLCULDU (29.08.2026,
        // calistirma kapisi yakaladi): bu metot kurucudan cagriliyor ve
        // o an "_durum" HENUZ ATANMAMIS. Metot grubu yazmak delegeyi
        // HEMEN kuruyor ve null bir "this" ile
        // "Delegate to an instance method cannot have null 'this'" atiyor -
        // uygulama HIC ACILMIYORDU. Lambda alani cagri aninda okuyor.
        // (CLAUDE.md 6'nin kurucu tuzaginin kardesi; yandaki
        // "() => _doldurucu?.Kok" da tam bu yuzden lambda.)
        var sayfa = new AyarlarSayfasi(_ayarlar, () => _doldurucu?.Kok, cumle => _durum.Bilgi(cumle));
        sayfa.Degisti += (_, _) =>
        {
            CopDugmesiniTazele();
            _izleyici.AcKapat(_ayarlar.OtomatikTazele, _doldurucu.Kok);
            _referansSurucusu.IzlemeGuvenilir = _izleyici.Guvenilir;

            // 2B/3B secimi degistiyse acik secim yeni kiple hemen cizilsin -
            // "ayari actim, hicbir sey olmadi" sanilmasin (CLAUDE.md 3).
            SecimiGoster();
        };
        _ayarlarSayfasi = sayfa;
        return sayfa;
    }

    /// <summary>
    /// Silinenlerin gidecegi klasor. Kullanici ayarlardan degistirmediyse
    /// kokun kendi ici - ayni diskte oldugu icin silme ANLIK.
    /// </summary>
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
            ReferanslariGoster(null);
            _durum.Secildi(ozet);
            return;
        }

        switch (AgacDoldurucu.Etiket(secililer.Count == 1 ? secililer[0] : null))
        {
            case DosyaOgesi dosya:
                _onizleme.Goster(dosya);
                ReferanslariGoster(dosya.Yol);
                _durum.Secildi(dosya);
                _yol.Goster(WindowsYolu.Klasor(dosya.Yol));
                break;

            case KlasorOgesi klasor:
                _onizleme.Goster(klasor);
                ReferanslariGoster(null);
                _durum.Secildi(klasor);
                _yol.Goster(klasor.Yol);
                break;

            default:
                _onizleme.Temizle();
                ReferanslariGoster(null);
                break;
        }
    }

}
