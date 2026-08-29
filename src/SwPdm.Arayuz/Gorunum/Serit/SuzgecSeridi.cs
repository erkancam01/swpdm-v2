using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Tur suzgeci seridi: "Tumu" + cekirdekteki her tur.
///
/// Dugmeler <see cref="DosyaTurleri.Tumu"/>'den URETILIYOR - burada elle
/// yazilmis bir tur listesi YOK (CLAUDE.md 1b). Yeni bir tur eklendiginde
/// dugmesi kendiliginden cikar, bir tur kaldirildiginda kendiliginden gider;
/// bu dosya degismez.
/// </summary>
internal sealed class SuzgecSeridi : FlowLayoutPanel
{
    private readonly List<Button> _dugmeler = [];
    private Button? _secili;

    internal SuzgecSeridi()
    {
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = false;
        AutoSize = false;
        Height = 28;
        Padding = new Padding(4, 2, 4, 2);
        BackColor = Renkler.GovdeArkaPlan;

        Ekle("Tümü", null);
        foreach (DosyaTuru tur in DosyaTurleri.Turler())
        {
            Ekle(DosyaTurleri.Adi(tur), tur);
        }

        if (_dugmeler.Count > 0)
        {
            Sec(_dugmeler[0]);
        }

        // Siralama secicisi de bu seritte: ikisi de "agac neyi nasil
        // gosteriyor" sorusunun cevabi, yan yana dursunlar.
        SiralamaSecici = new SiralamaSecici { Margin = new Padding(16, 1, 0, 0) };
        Controls.Add(SiralamaSecici);
    }

    /// <summary>Seritteki siralama secicisi.</summary>
    internal SiralamaSecici SiralamaSecici { get; }

    /// <summary>Secim degistiginde tetiklenir. null = butun turler.</summary>
    internal event EventHandler<DosyaTuru?>? SecimDegisti;

    /// <summary>
    /// Durum cubuguna yazilacak cumle. CUMLEYI BU DOSYA KURAR (CLAUDE.md 1b):
    /// once AnaForm kuruyordu, yani suzgec ozelliginin bir karari baska
    /// dosyada yasiyordu. Kisayol ipucuyla gosterilemiyor - ToolTip Wine'da
    /// tiklamayi yiyor (CLAUDE.md 6) - o yuzden cumleyle duyuruluyor.
    /// </summary>
    internal event EventHandler<string>? Durum;

    /// <summary>Su an secili tur. null = butun turler.</summary>
    internal DosyaTuru? SeciliTur => _secili?.Tag as DosyaTuru?;

    /// <summary>
    /// Kayitli suzgeci geri koyar (tur ADIYLA). Bulunamazsa "Tümü" kalir -
    /// bir tur kaldirilirsa eski ayar sessizce yanlis bir suzgec secmesin.
    ///
    /// SiralamaSecici.Kur ile ayni kalip: "kalici secim"i geri koymanin tek
    /// yolu serit dosyasinin kendisidir (CLAUDE.md 1b).
    /// </summary>
    internal void Kur(string? turAdi)
    {
        if (string.IsNullOrWhiteSpace(turAdi))
        {
            return;
        }

        foreach (Button d in _dugmeler)
        {
            if (string.Equals(d.Text, turAdi, StringComparison.Ordinal))
            {
                Sec(d);
                return;
            }
        }
    }

    /// <summary>
    /// SUZGEC KISAYOLU (Ctrl+Shift+F): siradaki suzgece gecer, sonunda
    /// "Tümü"ye doner. Doner: tus kullanildi mi.
    ///
    /// NEDEN VAR - IKI SEBEP:
    /// 1. Suzgec YALNIZCA fareyle kullanilabiliyordu (dugmeler TabStop=false
    ///    ve hicbir tus bagli degildi); siralamanin klavye karsiligi vardi,
    ///    suzgecin yoktu.
    /// 2. CLAUDE.md 11: Wine'da acilir menu coktugu icin "menusuz kalan
    ///    ozellik kor noktadir" - kisayol, ozelligi olculebilir de kiliyor.
    ///
    /// Karar BURADA (CLAUDE.md 1b): AnaForm yalnizca tusu iletiyor.
    /// </summary>
    internal bool TusaBasildi(Keys tus)
    {
        if (tus != (Keys.Control | Keys.Shift | Keys.F) || _dugmeler.Count == 0)
        {
            return false;
        }

        int siradaki = _secili is null ? 0 : _dugmeler.IndexOf(_secili) + 1;
        Sec(_dugmeler[siradaki >= _dugmeler.Count ? 0 : siradaki]);
        return true;
    }

    private void Ekle(string etiket, DosyaTuru? tur)
    {
        Button d = Dugme(etiket);
        d.Tag = tur;
        _dugmeler.Add(d);
        Controls.Add(d);
    }

    private Button Dugme(string etiket)
    {
        var d = new Button
        {
            Text = etiket,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            BackColor = Renkler.GovdeArkaPlan,
            ForeColor = Renkler.SuzgecYazi,
            Margin = new Padding(1, 0, 1, 0),
            Padding = new Padding(6, 1, 6, 1),
            TabStop = false,
        };
        d.FlatAppearance.BorderSize = 0;

        // ================== BURAYA ToolTip KONMAZ - OLCULDU ==================
        // 29.08.2026: kisayolu gostermek icin dugmelere ToolTip eklendi ve
        // calistirma kapisi ANINDA yakaladi: 8. olcum "once 14, sonra 14 -
        // suzulmedi" dedi, 9. olcumde de taban 9 yerine 14 cikti. Yani
        // ipucu penceresi TIKLAMAYI YIYOR ve tik bir sonraki etkilesime
        // kadar bekliyor. Kullanicinin gozunde bu "dugmeye basiyorum,
        // bazen calisiyor" demek - CLAUDE.md 8'deki silinmis Click
        // baglantisinin sinsi kardesi.
        //
        // Kisayol yine EKRANDA: secim degisince durum cubuguna yaziliyor
        // (asagidaki Sec). Tiklamayi bozmayan bir yer.
        // ======================================================================

        // ================== BU SATIR BIR KEZ SILINDI ==================
        // Bu baglanti olmadan dugmeler CIZILIYOR, odagi aliyor, uzerine
        // gelince renk degistiriyor - ama TIKLAMA HICBIR SEY YAPMIYOR.
        // Erkan'in "Montaj/Parca/Teknik resim/PDF tepki vermiyor" dedigi sey
        // tam olarak buydu ve sebebi bir WinForms tuzagi degil, bu eksik
        // satirdi. Nasil silindigi CLAUDE.md 8'de yaziyor.
        // ==============================================================
        d.Click += (_, _) => Sec(d);

        return d;
    }

    private void Sec(Button d)
    {
        if (ReferenceEquals(_secili, d))
        {
            return;
        }

        if (_secili is not null)
        {
            _secili.BackColor = Renkler.GovdeArkaPlan;
            _secili.FlatAppearance.BorderSize = 0;
        }

        _secili = d;
        d.BackColor = Renkler.SuzgecSeciliArkaPlan;
        d.FlatAppearance.BorderSize = 1;
        d.FlatAppearance.BorderColor = Renkler.SuzgecSeciliKenar;

        SecimDegisti?.Invoke(this, d.Tag as DosyaTuru?);
        Durum?.Invoke(
            this,
            (d.Tag is DosyaTuru tur ? "Süzgeç: " + DosyaTurleri.Adi(tur) : "Süzgeç kalktı")
            + "  ·  Ctrl+Shift+F ile ilerlet");
    }
}
