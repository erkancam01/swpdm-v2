using System;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// SIRALAMA SECICI - suzgec seridinin sagindaki dugme.
///
/// Agac bir liste degil, o yuzden tiklanacak sutun basligi yok; sira buradan
/// seciliyor. Secim KALICI (ayarlarda saklaniyor) - her aciliste yeniden
/// secmek zorunda kalmak, ozelligi kullanilmaz yapardi.
///
/// Dugmenin uzerinde SU ANKI sira yaziyor ("Ad ↑"): menuyu acmadan hangi
/// sirada oldugunu gormek gerekir, yoksa "neden bu sirada" sorusu dogar.
///
/// KISAYOL DA VAR (Ctrl+Shift+S) ve iki sebebi var. Birincisi kullanici icin:
/// klavyeden cikmadan sira degistirmek. Ikincisi OLCUM: menu bir
/// ContextMenuStrip ve Wine'da ToolStrip acmak uygulamayi COKERTIYOR
/// (CLAUDE.md 11). Menu ile olculemeyen siralama, kisayolla olculuyor -
/// ikisi de AYNI kodu cagiriyor, yani olculen sey gercek yol.
/// </summary>
internal sealed class SiralamaSecici : Button
{
    private readonly ContextMenuStrip _menu = new();

    private Siralama _secili = Siralama.Varsayilan;

    internal SiralamaSecici()
    {
        // CLAUDE.md 6: alanlar boyut degistiren her seyden ONCE atandi.
        AutoSize = false;
        Size = new Size(96, 22);
        FlatStyle = FlatStyle.Flat;
        BackColor = Renkler.GovdeArkaPlan;
        ForeColor = Renkler.SuzgecYazi;
        TextAlign = ContentAlignment.MiddleLeft;
        Padding = new Padding(6, 0, 0, 0);
        TabStop = false;

        // ToolTip KONMADI - suzgec dugmelerinde olculdu: ipucu penceresi
        // Wine'da tiklamayi yiyor (SuzgecSeridi'ndeki nota bakin). Kisayol
        // secim degisince DURUM CUBUGUNA yaziliyor.
        FlatAppearance.BorderSize = 1;
        FlatAppearance.BorderColor = Renkler.AyracCizgi;

        foreach (SiralamaOlcutu olcut in Enum.GetValues<SiralamaOlcutu>())
        {
            SiralamaOlcutu o = olcut;
            var oge = new ToolStripMenuItem(new Siralama(o, false).Adi);
            oge.Click += (_, _) => Sec(new Siralama(
                o,
                // Ayni olcute yeniden basmak YONU CEVIRIR - Gezgin de boyle.
                _secili.Olcut == o && !_secili.Azalan));
            _menu.Items.Add(oge);
        }

        Click += (_, _) => _menu.Show(this, new Point(0, Height));
        YaziyiKur();
    }

    /// <summary>Sirayi bir sonraki hale geciren kisayol.</summary>
    internal static Keys Kisayol => Keys.Control | Keys.Shift | Keys.S;

    /// <summary>Sira degisti.</summary>
    internal event EventHandler<Siralama>? Degisti;

    /// <summary>
    /// KALICILIK VE DUYURU - siralamanin butun karari bu dosyada
    /// (CLAUDE.md 1b): once secim AnaForm'da ayara yaziliyor ve cumle
    /// orada kuruluyordu. Kisayol ipucuyla gosterilemiyor (ToolTip Wine'da
    /// tiklamayi yiyor, CLAUDE.md 6); cumleyle duyuruluyor.
    /// </summary>
    internal void KaliciligiBagla(Ayarlar ayarlar, Action<string> bildir)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);
        ArgumentNullException.ThrowIfNull(bildir);

        Degisti += (_, sira) =>
        {
            ayarlar.Siralama = sira;
            ayarlar.Yaz();
            bildir($"Sıralama: {sira.Adi}  ·  Ctrl+Shift+S ile ilerlet");
        };
    }

    /// <summary>Su anki sira.</summary>
    internal Siralama Secili => _secili;

    /// <summary>
    /// Kisayola basildiysa sirayi ilerletir. Doner deger: tus BU dugmenindi.
    ///
    /// Tek tusla SEKIZ halin hepsine ulasilir (dort olcut x iki yon): once
    /// yon cevrilir, sonra bir sonraki olcute gecilir. Ikinci bir kisayol
    /// eklemek yerine boyle: bir tus, tam kapsam.
    /// </summary>
    internal bool TusaBasildi(Keys tuslar)
    {
        if (tuslar != Kisayol)
        {
            return false;
        }

        Sec(Sonraki(_secili));
        return true;
    }

    /// <summary>Sekiz halin sirasi: Ad↑ Ad↓ Tür↑ Tür↓ Boyut↑ Boyut↓ Tarih↑ Tarih↓.</summary>
    private static Siralama Sonraki(Siralama su_an)
    {
        if (!su_an.Azalan)
        {
            return su_an with { Azalan = true };
        }

        SiralamaOlcutu[] olcutler = Enum.GetValues<SiralamaOlcutu>();
        int sira = Array.IndexOf(olcutler, su_an.Olcut);
        return new Siralama(olcutler[(sira + 1) % olcutler.Length], Azalan: false);
    }

    /// <summary>Kayitli sirayi kurar; olay TETIKLEMEZ (acilista kullanilir).</summary>
    internal void Kur(Siralama siralama)
    {
        _secili = siralama;
        YaziyiKur();
    }

    private void Sec(Siralama yeni)
    {
        _secili = yeni;
        YaziyiKur();
        Degisti?.Invoke(this, yeni);
    }

    private void YaziyiKur() => Text = $"{_secili.Adi} {(_secili.Azalan ? "↓" : "↑")}";
}
