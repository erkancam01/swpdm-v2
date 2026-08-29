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
/// </summary>
internal sealed partial class AnaForm
{
    /// <summary>
    /// ESC: ARAMADAN CIK - ve neden KeyDown DEGIL de burasi.
    ///
    /// OLCULDU (28.08.2026, kapi yakaladi): Esc once Form.KeyDown'a
    /// baglanmisti (KeyPreview acik). Odak agactayken calisiyor, ARAMA
    /// KUTUSUNDAYKEN hicbir sey yapmiyordu - kutu bir ToolStripTextBox ve
    /// ToolStrip, Escape'i kendi tus isleyisinde YUTUYOR; ne Form.KeyDown'a
    /// ne de kutunun kendi KeyDown'ina geliyor (ikisi de denendi, ikisi de
    /// olcumde HAYIR dedi).
    ///
    /// ProcessCmdKey bu zincirin ONUNDE calisiyor: tus once komut tusu
    /// olarak denetim-ebeveyn zincirinde soruluyor, ToolStrip'in dialog tusu
    /// isleyisi ondan SONRA geliyor.
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

        return base.ProcessCmdKey(ref m, tus);
    }

    private void KisayollariKur()
    {
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

            // Tur suzgeci kisayolu (Ctrl+Shift+F) - ayni kalip, karar
            // SuzgecSeridi'nde.
            if (_suzgecler.TusaBasildi(e.KeyData))
            {
                e.SuppressKeyPress = true;
                return;
            }

            // AGAC ODAKTAYKEN klavye: Enter = dosyayi ac, Backspace = ust
            // klasor. Karari Agac/AgacTuslari veriyor (CLAUDE.md 1b).
            if (_agac.Focused
                && AgacTuslari.Isle(
                    e.KeyData, _agac,
                    dosya => _durum.Bilgi(DosyaAcici.Ac(this, dosya)),
                    _durum.Bilgi))
            {
                e.SuppressKeyPress = true;
                return;
            }

            // REFERANS PANELI ODAKTAYKEN: once panelin kendi tuslari
            // (Enter = git, Ctrl+C = yolu kopyala). Karari panel dosyasi
            // veriyor, burada yalnizca tus iletiliyor (CLAUDE.md 1b).
            if (_referanslar.Focused
                && ReferansPaneliTuslari.Isle(
                    e.KeyData, _referanslar, ReferansaGit, _durum.Bilgi))
            {
                e.SuppressKeyPress = true;
                return;
            }

            // Kisayollar islem listesinden geliyor; menudeki yazi ile calisan
            // tus AYRISAMAZ (CLAUDE.md 1b).
            //
            // PANEL ODAKTAYKEN DE GECERLI: panelde secili satirin EBEVEYNI
            // agacta secili olan dosyadir - yani "Referansı elle bağla"
            // (Ctrl+Shift+L) tam da oradan istenir. Once agaca tiklamak
            // zorunda kalmak gereksiz bir adimdi.
            if ((_agac.Focused || _referanslar.Focused) && _menu.TusaBasildi(e.KeyData))
            {
                e.SuppressKeyPress = true;
            }
        };
    }
}
