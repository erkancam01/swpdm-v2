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
/// <see cref="ReferansSurucusu"/>'nda (CLAUDE.md 1b). Bu sinif TEK bir sey
/// cizer: bir REFERANS satiri. (Bolum basligi satiri 30.08.2026'da KALKTI -
/// isini <see cref="ReferansSeridi"/> yapiyor; baslik liste kaydirilinca
/// ekrandan cikiyordu, serit cikmiyor.)
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

    /// <summary>
    /// Secim degisti - fareyle TEK TIK ya da klavyeyle ok. Panele bakan
    /// (onizleme gibi) taraflar bunu dinler; cift tik "oraya git" olarak
    /// ayri yasamaya devam eder.
    /// </summary>
    internal event EventHandler? SecimDegisti
    {
        add => SelectedIndexChanged += value;
        remove => SelectedIndexChanged -= value;
    }

    /// <summary>Secili satirin hedef yolu (Enter icin); yoksa null.</summary>
    internal string? SeciliHedef => Secili?.Hedef;

    /// <summary>
    /// Secili satirin panoya kopyalanacak metni - tam yol ya da aciklama
    /// cumlesinin tamami. Secim yoksa null.
    /// </summary>
    internal string? SeciliMetin => Secili?.TamMetin;

    private Satir? Secili
        => SelectedItems.Count > 0 ? SelectedItems[0].Tag as Satir : null;

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
