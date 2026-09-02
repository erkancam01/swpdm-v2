using System;
using System.Windows.Forms;
using SwPdm.Arayuz.Gorunum;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz;

/// <summary>
/// ANA PENCERENIN KLAVYESI - tuslarin hangi kanaldan gectigi ve kime
/// gittigi. Ayri dosyada cunku burasi bir SIRALAMA karari: hangi tus once
/// kime sorulacak. AnaForm'un geri kalani "kim kime baglanir" ile ilgili.
///
/// TUSUN NE YAPACAGINA BURASI KARAR VERMEZ: her tus, sahibinin dosyasina
/// iletilir (AgacTuslari · ReferansPaneliTuslari · SiralamaSecici ·
/// AgacMenusu · AramaSurucusu). Buradaki tek bilgi ODAK: hangi denetim
/// odaktayken kimin tuslari gecerli (CLAUDE.md 1b).
///
/// TEK KANAL: ProcessCmdKey - OLCULDU (01.09.2026).
///
/// Kisayollar once Form.KeyDown'a (KeyPreview acik) bagliydi. Cogu tus
/// calisiyordu ama Ctrl+Shift+E (referans bolumu) ve Ctrl+Shift+U (yeni
/// versiyon) HIC calismiyordu: kod yollari dogruydu, tus KeyDown'a hic
/// gelmiyordu. Ayni sinifta zaten olculmus bir tuzagin kardesi bu
/// (CLAUDE.md 6: "ToolStrip Escape'i YUTUYOR - Form.KeyDown gormuyor");
/// belirti de aynisi: "bazen calisan" tus.
///
/// ProcessCmdKey bu zincirin ONUNDE calisiyor - komut tuslari, dialog ve
/// ToolStrip tus isleyisinden ONCE sorulur. Bu yuzden ARTIK BUTUN
/// kisayollar buradan geciyor; KeyDown kanali kaldirildi. Odak sartlari
/// (_agac.Focused / _referanslar.Focused) aynen duruyor: arama kutusuna
/// yazarken Delete'in dosya silmemesini onlar sagliyor.
/// </summary>
internal sealed partial class AnaForm
{
    /// <summary>
    /// BUTUN KISAYOLLARIN TEK KAPISI.
    ///
    /// ESC: ARAMADAN CIK - ve neden KeyDown DEGIL de burasi.
    ///
    /// OLCULDU (28.08.2026, kapi yakaladi): Esc once Form.KeyDown'a
    /// baglanmisti (KeyPreview acik). Odak agactayken calisiyor, ARAMA
    /// KUTUSUNDAYKEN hicbir sey yapmiyordu - kutu bir ToolStripTextBox ve
    /// ToolStrip, Escape'i kendi tus isleyisinde YUTUYOR; ne Form.KeyDown'a
    /// ne de kutunun kendi KeyDown'ina geliyor (ikisi de denendi, ikisi de
    /// olcumde HAYIR dedi).
    ///
    /// Esc'in NE YAPACAGI yine AramaSurucusu'nda (CLAUDE.md 1b); burasi
    /// yalnizca tusun ulasabildigi yer.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message m, Keys tus)
    {
        // ESC ONCE ISI IPTAL EDER, sonra aramayi kapatir.
        //
        // SIRA BOYLE, cunku bir is surerken kullanicinin acil istegi odur;
        // ayrica is surerken agac Enabled=false ve iptal dugmesi sekme
        // sirasinda degil - yani iptalin baska KLAVYE yolu yok.
        if (tus == Keys.Escape && _durum.IptalEdilebilir)
        {
            _durum.IptaliIste();
            return true;
        }

        if (tus == Keys.Escape && _arama.Kapat())
        {
            AgacaOdaklan();
            return true;
        }

        return KisayolIsle(tus) || base.ProcessCmdKey(ref m, tus);
    }

    /// <summary>
    /// Tusu sahibine iletir. Doner: tus kullanildi mi (kullanildiysa
    /// baska kimseye gitmez).
    /// </summary>
    private bool KisayolIsle(Keys tus)
    {
        if (tus == (Keys.Control | Keys.O))
        {
            _kokSecici.Sor();
            return true;
        }

        // Siralama kisayolu: karari SiralamaSecici veriyor, burada
        // yalnizca tus iletiliyor (CLAUDE.md 1b).
        if (_suzgecler.SiralamaSecici.TusaBasildi(tus))
        {
            return true;
        }

        // Tur suzgeci kisayolu (Ctrl+Shift+F) - ayni kalip, karar
        // SuzgecSeridi'nde.
        if (_suzgecler.TusaBasildi(tus))
        {
            return true;
        }

        // Referans bolumu kisayolu (Ctrl+Shift+E) - ayni kalip, karar
        // ReferansSeridi'nde.
        if (_referansSeridi.TusaBasildi(tus))
        {
            return true;
        }

        // AGAC ODAKTAYKEN klavye: Enter = dosyayi ac, Backspace = ust
        // klasor. Karari Agac/AgacTuslari veriyor (CLAUDE.md 1b).
        if (_agac.Focused
            && AgacTuslari.Isle(
                tus, _agac,
                dosya => _durum.Bilgi(DosyaAcici.Ac(this, dosya)),
                _durum.Bilgi))
        {
            return true;
        }

        // VERSIYONLAR sekmesinde Enter = "bu versiyona don". Olagan
        // Enter'dan (git) ONCE bakilir cunku versiyon satirinin gidecek
        // yeri yok; akisin karari SurumeDonusu'nde (CLAUDE.md 1b).
        if (_referanslar.Focused
            && tus == Keys.Enter
            && _referansSeridi.SeciliBolum == ReferansBolumu.Surumler
            && SurumeDonusu.Calistir(
                this,
                _referansSurucusu.SurumKaydi(_referanslar.SeciliSira),
                SecimBaglamiKur(),
                _referansSurucusu.Indeks,
                () => _onizleme.BelgeyiBirak(),
                AgaciTazele,
                _durum.Bilgi))
        {
            return true;
        }

        // VERSIYONLAR sekmesinde F2 = notu duzelt, Delete = versiyonu sil.
        // Panelin genel tuslarindan ONCE: orada ayni tuslar SATIRIN
        // DOSYASINA gider ve arsiv kopyasinda zaten gri durur - sira
        // kayarsa versiyon satirinda F2 hicbir sey yapmaz.
        if (_referanslar.Focused
            && _referansSeridi.SeciliBolum == ReferansBolumu.Surumler
            && SurumBakimi.Calistir(
                this,
                tus,
                _referansSurucusu.SurumKaydi(_referanslar.SeciliSira),
                SecimBaglamiKur(),
                AgaciTazele,
                _durum.Bilgi))
        {
            return true;
        }

        // REFERANS PANELI ODAKTAYKEN: once panelin kendi tuslari
        // (Enter = git, Ctrl+C = yolu kopyala). Karari panel dosyasi
        // veriyor, burada yalnizca tus iletiliyor (CLAUDE.md 1b).
        if (_referanslar.Focused
            && ReferansPaneliTuslari.Isle(
                tus, _referanslar, hedef => ReferansaGit(hedef), _durum.Bilgi))
        {
            return true;
        }

        // PANEL ODAKTAYKEN: satira uygulanan isler (F2, Delete, Ctrl+X,
        // Ctrl+V...) SATIRIN dosyasina gider - sag tik menusuyle AYNI kod
        // (CLAUDE.md 11: Wine'da menu acilamiyor, olculebilen tek yol
        // kisayol). Sirasi burada: panelin kendi tuslari yukarida gecti,
        // sahibe uygulananlar ve genel isler asagida deneniyor.
        if (_referanslar.Focused && _referansMenusu.TusaBasildi(tus))
        {
            return true;
        }

        // Kisayollar islem listesinden geliyor; menudeki yazi ile calisan
        // tus AYRISAMAZ (CLAUDE.md 1b).
        //
        // PANEL ODAKTAYKEN DE GECERLI: panelde secili satirin EBEVEYNI
        // agacta secili olan dosyadir - yani "Referansı elle bağla"
        // (Ctrl+Shift+L) tam da oradan istenir. Once agaca tiklamak
        // zorunda kalmak gereksiz bir adimdi.
        return (_agac.Focused || _referanslar.Focused) && _menu.TusaBasildi(tus);
    }
}
