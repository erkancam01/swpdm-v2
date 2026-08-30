using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// REFERANS PANELININ SEKME SERIDI: İÇİNDEKİLER · KULLANILDIĞI YERLER · KIRIK.
///
/// Erkan, 30.08.2026 (67 referansli gercek bir montajda): "içindekiler -
/// kullanıldığı yerler ve kırık referanslar olsa … her alan ayrı pencerede,
/// bakmak istediği şeye tıklar açar. ama varsayılan olarak en başta
/// içindekiler gelsin."
///
/// NEDEN SERIT, BOLUM BASLIGI DEGIL - IKI OLCULMUS SEBEP:
///   1. 67 satirlik bir bolumun ALTINDAKI bolum hic gorunmuyordu.
///   2. Baslik satiri liste kaydirilinca EKRANDAN CIKIYOR; o an "hangi yone
///      bakiyorum" sorusunu cevaplayan hicbir sey kalmiyordu. Yonu
///      karistirmak bu uygulamada saglam dosya sildirir (CLAUDE.md 3).
/// Serit sabit durur, kaymaz.
///
/// DUGMELER <see cref="ReferansBolumleri.Tumu"/>'DEN URETILIYOR - burada elle
/// yazilmis bir bolum listesi YOK (CLAUDE.md 1b). Gorunum kurallari
/// <see cref="SuzgecSeridi"/> ile ayni; o dosya kopyalanmadi, ayni paletten
/// besleniyorlar.
/// </summary>
internal sealed class ReferansSeridi : FlowLayoutPanel
{
    private readonly List<Button> _dugmeler = [];
    private readonly Dictionary<ReferansBolumu, string> _sayilar = [];

    private Button? _secili;

    internal ReferansSeridi()
    {
        FlowDirection = FlowDirection.LeftToRight;

        // SARMALI VE KENDI BOYUNU BULAN - OLCULDU (30.08.2026): sabit tek
        // satirda "KULLANILDIĞI YERLER" kirpiliyor ve "KIRIK" hic
        // gorunmuyordu (572 piksellik pencerede olculdu). Gorunmeyen bir
        // sekme, olmayan bir sekmedir - kullanici kirik referanslarin
        // varligindan haberdar olmaz (CLAUDE.md 3). Genis pencerede ucu de
        // tek satira siger, darda alt satira gecer; ikisinde de hepsi
        // gorunur.
        WrapContents = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(4, 1, 4, 1);
        BackColor = Renkler.GovdeArkaPlan;

        foreach (ReferansBolumu bolum in ReferansBolumleri.Tumu)
        {
            Button d = Dugme(bolum);
            _dugmeler.Add(d);
            Controls.Add(d);
        }

        if (_dugmeler.Count > 0)
        {
            Sec(_dugmeler[0], duyur: false);   // varsayilan: ILK bolum
        }
    }

    /// <summary>Bolum degisti; panel yeniden doldurulmali.</summary>
    internal event EventHandler<ReferansBolumu>? SecimDegisti;

    /// <summary>
    /// Durum cubuguna yazilacak cumle. CUMLEYI BU DOSYA KURAR (CLAUDE.md 1b);
    /// kisayol ipucuyla gosterilemiyor - ToolTip Wine'da tiklamayi yiyor
    /// (CLAUDE.md 6) - o yuzden cumleyle duyuruluyor.
    /// </summary>
    internal event EventHandler<string>? Durum;

    /// <summary>Su an acik olan bolum.</summary>
    internal ReferansBolumu SeciliBolum
        => _secili?.Tag is ReferansBolumu b ? b : ReferansBolumu.Icindekiler;

    /// <summary>
    /// Dugme etiketlerine SAYIYI yazar: "İÇİNDEKİLER 67".
    ///
    /// NEDEN SART (CLAUDE.md 3): sekmeli duzende yalnizca bir bolum aciktir.
    /// Otekilerin sayisi gorunmezse kullanici, sekmeyi hic acmadan "kullanan
    /// yok" sanabilir - oysa cevap "taranmadi" olabilir. Sayilari SURUCU
    /// uretiyor; burasi yalnizca yaziyor.
    /// </summary>
    internal void Sayilari(Func<ReferansBolumu, string> sayi)
    {
        ArgumentNullException.ThrowIfNull(sayi);

        foreach (Button d in _dugmeler)
        {
            if (d.Tag is not ReferansBolumu bolum)
            {
                continue;
            }

            string deger = sayi(bolum);
            _sayilar[bolum] = deger;
            d.Text = deger.Length == 0
                ? ReferansBolumleri.Adi(bolum)
                : ReferansBolumleri.Adi(bolum) + "  " + deger;
        }
    }

    /// <summary>
    /// BOLUM KISAYOLU (Ctrl+Shift+E): siradaki bolume gecer, sonunda basa
    /// doner. Doner: tus kullanildi mi.
    ///
    /// NEDEN VAR: CLAUDE.md 11 - fareye bagli kalan bir ozellik burada KOR
    /// NOKTADIR (Wine'da acilir menu cokuyor, tiklama olcumleri koordinata
    /// bagli). Kisayol hem kullaniciya yarar hem ozelligi olculebilir kilar;
    /// tur suzgecinin Ctrl+Shift+F'i ile ayni kalip.
    ///
    /// Karar BURADA (CLAUDE.md 1b): AnaForm yalnizca tusu iletiyor.
    /// </summary>
    internal bool TusaBasildi(Keys tus)
    {
        if (tus != (Keys.Control | Keys.Shift | Keys.E) || _dugmeler.Count == 0)
        {
            return false;
        }

        int siradaki = _secili is null ? 0 : _dugmeler.IndexOf(_secili) + 1;
        Sec(_dugmeler[siradaki >= _dugmeler.Count ? 0 : siradaki], duyur: true);
        return true;
    }

    private Button Dugme(ReferansBolumu bolum)
    {
        var d = new Button
        {
            Text = ReferansBolumleri.Adi(bolum),
            Tag = bolum,
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

        // ToolTip BILEREK YOK: Wine'da tiklamayi yiyor ve dugme "bazen
        // calisan" bir sey oluyor (CLAUDE.md 6, olculdu). Soylenecek sey
        // durum cubuguna yaziliyor.
        d.Click += (_, _) => Sec(d, duyur: true);

        return d;
    }

    private void Sec(Button d, bool duyur)
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

        if (!duyur || d.Tag is not ReferansBolumu bolum)
        {
            return;
        }

        SecimDegisti?.Invoke(this, bolum);
        Durum?.Invoke(
            this,
            ReferansBolumleri.Adi(bolum)
            + (_sayilar.TryGetValue(bolum, out string? sayi) && sayi.Length > 0
                ? ": " + sayi
                : string.Empty)
            + "  ·  Ctrl+Shift+E ile ilerlet");
    }
}
