using System;
using System.Collections.Generic;
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
        _menu.Durum += (_, cumle) => _durum.Bilgi(cumle);
        _menu.Tazele += (_, yol) => AgaciTazele(yol);
        _surukleBirak = new SurukleBirak(_agac);
        _surukleBirak.Tasindi += (_, e) => Aktar.Yurut(
            new IslemBaglami(this, SecimBaglamiKur(), AgaciTazele, _durum.Bilgi, _ilerleme),
            e.Yollar,
            e.HedefKlasor,
            AktarmaKipi.Tasi);

        // --- klasor secme
        _kokSecici = new KokSecici(_acDugmesi) { Sahip = this };
        _kokSecici.Secildi += (_, yol) => KokuAc(yol);

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
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _arama.Dispose();
        _onizleme.Dispose();
        base.OnFormClosed(e);
    }

    private void KokuAc(string yol)
    {
        _arama.MetniTemizle();

        _doldurucu.KokuAc(yol);

        // Kokun tek sahibi AgacDoldurucu; arama onun bildigi kokte arar.
        _arama.Kok = _doldurucu.Kok;

        _onizleme.Temizle();
        _durum.Kok(yol);
        _kokSecici.GecmiseEkle(yol);

        // Kok degisti: eski yollara bakan geri alma adimlari artik BASKA bir
        // agacin yollari olur ve yanlis yere dokunurdu (CLAUDE.md 1a).
        GeriAlDefteri.Temizle();
        CopDugmesiniTazele();
        GeriAlDugmesiniTazele();
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

        return new SecimBaglami(ogeler, etkin ?? _doldurucu.Kok, _doldurucu.AramaKipinde, _doldurucu.Kok);
    }

    /// <summary>
    /// Bir dosya islemi bitti: agaci diskten tazeler, acik dallari korur.
    /// <paramref name="secilecekYol"/> verilirse orasi secili gelir.
    /// </summary>
    private void AgaciTazele(string? secilecekYol)
    {
        _doldurucu.Yenile();
        CopDugmesiniTazele();
        GeriAlDugmesiniTazele();

        if (secilecekYol is not null)
        {
            _doldurucu.YoluSec(secilecekYol);
        }

        SecimiGoster();
    }

    /// <summary>Cop kutusu penceresini acar ve kapaninca agaci tazeler.</summary>
    private void CopKutusunuAc()
    {
        if (_doldurucu.Kok is not string kok)
        {
            _durum.Bilgi("Önce bir klasör açın.");
            return;
        }

        CopKutusuPenceresi.Goster(this, kok, cumle => _durum.Bilgi(cumle));
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

        int adet = Cop.Listele(kok).Count;
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
            _durum.Secildi(ozet);
            return;
        }

        switch (AgacDoldurucu.Etiket(secililer.Count == 1 ? secililer[0] : null))
        {
            case DosyaOgesi dosya:
                _onizleme.Goster(dosya);
                _durum.Secildi(dosya);
                break;

            case KlasorOgesi klasor:
                _onizleme.Goster(klasor);
                _durum.Secildi(klasor);
                break;

            default:
                _onizleme.Temizle();
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
