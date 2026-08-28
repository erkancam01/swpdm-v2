using System;
using System.Drawing;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Sag alt panel: secili dosyanin referanslari. Solda dosya adi, sagda rolu
/// ("içinde" ya da "kullanan").
///
/// Yalnizca GORUNUM; referans cozmez, bolumleri kendisi kurmaz. Hangi bolum
/// var, hangi sirada ve ne yaziyor kararinin TAMAMI
/// <see cref="ReferansSurucusu"/>'nda (CLAUDE.md 1b). Bu sinif iki sey
/// cizebilir: bir BASLIK satiri ve bir REFERANS satiri.
///
/// CLAUDE.md 3 geregi bos liste tek basina "referansi yok" anlamina GELMEZ -
/// o ayrimi dolduran kod yapar, bu sinif kendiliginden hicbir sey iddia etmez.
/// </summary>
internal sealed class ReferansListesi : ListView
{
    private readonly ColumnHeader _adSutunu;
    private readonly ColumnHeader _rolSutunu;
    private readonly bool _hazir;

    /// <summary>
    /// Bir satirin gorunmeyen tarafi.
    ///
    /// <paramref name="Hedef"/> cift tikta gidilecek yer (cozulememis satirda
    /// null). <paramref name="TamMetin"/> ipucunda gosterilen ve panoya
    /// kopyalanan metin: cozulmusse dosyanin GERCEK yolu, cozulememisse
    /// dosyanin ICINDE yazan yol, aciklama satirinda ise cumlenin tamami.
    /// </summary>
    private sealed record Satir(string? Hedef, string TamMetin);

    private Font? _kalin;

    internal ReferansListesi()
    {
        // Sutunlar, BOYUT DEGISTIREN her seyden (Dock, BorderStyle...) ONCE
        // olusturuluyor. Bu sinif BaslikSeridi ile AYNI hatayi tasiyordu:
        // Dock = Fill kurucunun icinden OnResize'i tetikleyebiliyor ve orasi
        // henuz null olan sutunlara dokunuyordu. BaslikSeridi'nin cokmesi bunu
        // gizlemisti - uygulama oraya varamadan oluyordu.
        _adSutunu = new ColumnHeader { Text = "Dosya", Width = 170 };
        _rolSutunu = new ColumnHeader
        {
            Text = "Rol",
            Width = 110,
            TextAlign = HorizontalAlignment.Right,
        };
        Columns.AddRange([_adSutunu, _rolSutunu]);

        View = View.Details;
        HeaderStyle = ColumnHeaderStyle.None;
        FullRowSelect = true;
        MultiSelect = false;
        ShowItemToolTips = true;
        BorderStyle = BorderStyle.None;
        BackColor = Renkler.OnizlemeArkaPlan;
        Dock = DockStyle.Fill;

        _hazir = true;
        SutunlariPaylastir();
    }

    /// <summary>
    /// Bir BOLUM BASLIGI satiri ekler: solda "▼ KULLANDIKLARI", sagda "9 dosya".
    ///
    /// NEDEN AYRI BIR SATIR, ListViewGroup DEGIL: gruplar Wine'da
    /// olculemez ve bu depoda olculemeyen bir gorunum kor nokta demek
    /// (CLAUDE.md 11). Duz satir ekran goruntusunden sayilabiliyor.
    ///
    /// SAYI NEDEN SAG SUTUNDA - OLCULDU (28.08.2026): ikisi tek metin
    /// oldugunda dar pencerede "▼ KULLANDIKLARI ..." diye KIRPILIYOR ve
    /// kirpilan sey tam da sayinin kendisi oluyordu. Iki sutuna bolununce
    /// sayi her genislikte gorunuyor.
    ///
    /// Hedef yolu YOK: basliga cift tiklayinca hicbir yere gidilmez.
    /// </summary>
    internal void Baslik(string metin, string sayi)
    {
        var satir = new ListViewItem(metin)
        {
            ImageIndex = -1,
            Tag = new Satir(null, metin),
            BackColor = Renkler.ReferansBolumZemin,
            ForeColor = Renkler.BolumBasligiYazi,
            Font = KalinYazi(),
        };

        satir.SubItems.Add(sayi);
        Items.Add(satir);
    }

    /// <summary>
    /// Bir referans satiri ekler.
    ///
    /// <paramref name="yazi"/> satirin YONUNU tasiyor (asagi/yukari). Liste
    /// kayinca bolum basligi gorunmez olur; o an yonu anlatan tek sey satirin
    /// kendi rengi ve rol kelimesidir.
    ///
    /// <paramref name="hedefYol"/> verilirse satira cift tiklanabilir ve
    /// agac oraya gider. Cozulememis bir referansta null gecilir - o satir
    /// tiklanabilir GORUNMEZ, cunku gidilecek bir yer yok.
    /// </summary>
    /// <param name="tamMetin">
    /// Ipucunda gosterilecek ve panoya kopyalanacak metin - GENELDE TAM YOL.
    ///
    /// NEDEN SART (Erkan'in ekraninda olculdu): satirda yalnizca dosya adi
    /// yaziyordu ve ipucu da AYNI adi tekrarliyordu. Iki farkli klasordeki
    /// ayni adli iki dosya boylece AYIRT EDILEMIYOR, hedefin nerede oldugu
    /// hic gorunmuyordu - oysa bu uygulamada "hangi dosya" sorusunun cevabi
    /// listenin kendisi kadar onemli (CLAUDE.md 5: ad esitligi tek basina
    /// yeterli degil).
    /// </param>
    internal void Ekle(
        string dosyaAdi, string rol, int simgeSirasi, Color yazi,
        string? hedefYol = null, string? tamMetin = null)
    {
        var satir = new ListViewItem(dosyaAdi)
        {
            ImageIndex = simgeSirasi,
            Tag = new Satir(hedefYol, tamMetin ?? dosyaAdi),
            ForeColor = yazi,

            // Panel dar oldugunda uzun ad ve uzun SEBEP cumlesi kirpiliyor
            // (olculdu: "Tarama yarım kaldı; list..."). Kirpilan sey bir
            // sebepse kullanici NEDEN eksik oldugunu goremez - CLAUDE.md 3.
            // Tam metin ipucunda duruyor.
            ToolTipText = tamMetin ?? dosyaAdi,
        };

        satir.SubItems.Add(rol);
        Items.Add(satir);
    }

    /// <summary>Cift tiklanan satirin hedef yolu; yoksa null.</summary>
    internal string? TiklananHedef(Point nokta)
        => (GetItemAt(nokta.X, nokta.Y)?.Tag as Satir)?.Hedef;

    /// <summary>Secili satirin hedef yolu (Enter icin); yoksa null.</summary>
    internal string? SeciliHedef => Secili?.Hedef;

    /// <summary>
    /// Secili satirin panoya kopyalanacak metni - tam yol ya da aciklama
    /// cumlesinin tamami. Secim yoksa null.
    /// </summary>
    internal string? SeciliMetin => Secili?.TamMetin;

    private Satir? Secili
        => SelectedItems.Count > 0 ? SelectedItems[0].Tag as Satir : null;

    /// <summary>Yazi tipi degisince kalin kopya bayatlar; atilir.</summary>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _kalin?.Dispose();
        _kalin = null;
    }

    /// <summary>Sutun genisliklerini panelin genisligine gore paylastirir.</summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (!_hazir)
        {
            return;
        }

        SutunlariPaylastir();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kalin?.Dispose();
            _kalin = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Basliklarin kalin yazisi.
    ///
    /// KURUCUDA URETILMIYOR: denetim bir kaba eklenene kadar <see cref="Font"/>
    /// kabin yazisini almiyor; kurucuda alinan kalin kopya YANLIS aileden
    /// olurdu ve hata SESSIZ kalirdi (yalnizca baslik otekilerden farkli
    /// gorunurdu). Ilk kullanimda uretiliyor, yazi degisince atiliyor.
    /// </summary>
    private Font KalinYazi() => _kalin ??= new Font(Font, FontStyle.Bold);

    private void SutunlariPaylastir()
    {
        int kullanilabilir = ClientSize.Width - 4;
        if (kullanilabilir <= 0)
        {
            return;
        }

        // ROL SUTUNU 0,42'den 0,34'e indi: adlar (YMB.00905.ENJ.00-ND-...)
        // ve sebep cumleleri sagdaki kelimelerden cok daha uzun.
        _rolSutunu.Width = (int)(kullanilabilir * 0.34);
        _adSutunu.Width = kullanilabilir - _rolSutunu.Width;
    }
}
