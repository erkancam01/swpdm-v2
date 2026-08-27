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
            }
        };

        _onizleme.Temizle();
        _durum.Bekliyor();
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
