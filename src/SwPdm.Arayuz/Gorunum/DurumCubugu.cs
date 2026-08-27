using System;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// DURUM CUBUGUNUN TEK KAPISI. Alttaki yazilara dair bir sey degisecekse
/// BU DOSYA degisir (CLAUDE.md 1b).
///
/// Once boyle degildi ve olculdu (27.08.2026): uc ayri dosya iki etikete
/// dogrudan metin yaziyordu; "alttaki yazilar sunlar olsun" demek uc dosyaya
/// dokunmak demekti.
///
/// Sol taraf NEREDE OLDUGUNU soyler (yol, secilen dosya), sag taraf NE
/// OLDUGUNU (tarama ozeti, arama ilerlemesi, hata). CLAUDE.md 3: her terminal
/// hal bir cumle birakir; sessiz basari YASAK.
/// </summary>
internal sealed class DurumCubugu : StatusStrip
{
    private readonly ToolStripStatusLabel _sol = new()
    {
        Spring = true,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private readonly ToolStripStatusLabel _sag = new()
    {
        TextAlign = ContentAlignment.MiddleRight,
    };

    private readonly ToolStripProgressBar _cubuk = new()
    {
        Visible = false,
        AutoSize = false,
        Width = 160,
        Minimum = 0,
        Style = ProgressBarStyle.Blocks,
    };

    private readonly ToolStripStatusLabel _sayac = new()
    {
        Visible = false,
        TextAlign = ContentAlignment.MiddleRight,
    };

    private readonly ToolStripButton _iptal = new()
    {
        Visible = false,
        Text = "İptal",
        DisplayStyle = ToolStripItemDisplayStyle.Text,
        AutoSize = false,
        Width = 54,
    };

    internal DurumCubugu()
    {
        // CLAUDE.md 6: alanlar boyut degistiren her seyden ONCE atanir.
        // Burada alan baslaticilariyla atandilar; asagidaki atamalar
        // OnResize'i tetiklerse bile null bulmuyorlar.
        Dock = DockStyle.Bottom;
        SizingGrip = false;

        Items.Add(_sol);
        Items.Add(_sayac);
        Items.Add(_cubuk);
        Items.Add(_iptal);
        Items.Add(_sag);
    }

    /// <summary>Kullanici ilerleyen isi iptal etmek istedi.</summary>
    internal event EventHandler? IptalIstendi
    {
        add => _iptal.Click += value;
        remove => _iptal.Click -= value;
    }

    /// <summary>Henuz klasor secilmemis acilis hali.</summary>
    internal void Bekliyor()
    {
        _sol.Text = "Klasör seçilmedi.";
        _sag.Text = string.Empty;
    }

    /// <summary>Kok klasor acildi.</summary>
    internal void Kok(string yol) => _sol.Text = yol;

    /// <summary>Agacta bir dosya secildi.</summary>
    internal void Secildi(DosyaOgesi dosya)
        => _sol.Text = string.Join("  ·  ", dosya.Ad, Boyut.Yaz(dosya.Boyut), Zaman.Yaz(dosya.Degistirme));

    /// <summary>
    /// Agacta bir klasor secildi. Okunamayan klasorde SEBEP de yaziliyor -
    /// CLAUDE.md 3: hata sebebi EKRANDA gosterilir, yalnizca gunlukte degil.
    /// </summary>
    internal void Secildi(KlasorOgesi klasor)
        => _sol.Text = klasor.Hata is null ? klasor.Yol : klasor.Yol + "  ·  " + klasor.Hata;

    /// <summary>Agacta birden cok oge secildi.</summary>
    internal void Secildi(SecimOzeti ozet) => _sol.Text = ozet.Yaz();

    /// <summary>
    /// Uzun bir isin basladigini bildirir ve cubugu gosterir.
    ///
    /// CLAUDE.md 3: UYDURMA ILERLEME YOK. Buraya SAYILABILIR bir toplam
    /// veriliyor (kac oge tasinacak); bilinmeyen bir is icin yuzde
    /// uydurulmuyor.
    /// </summary>
    internal void IsBasladi(int toplam)
    {
        _cubuk.Maximum = Math.Max(1, toplam);
        _cubuk.Value = 0;
        _cubuk.Visible = true;
        _sayac.Text = $"0 / {toplam}";
        _sayac.Visible = true;
        _iptal.Enabled = true;
        _iptal.Visible = true;
    }

    /// <summary>Ilerlemeyi gunceller. <paramref name="ad"/> su an islenen oge.</summary>
    internal void Ilerleme(int yapilan, int toplam, string ad)
    {
        // ================== OLCULMUS TUZAK (CLAUDE.md 6) ==================
        // Ilerleme cubugu ILERI giderken ANIMASYONLU: verilen degere yavasca
        // kayiyor ve is bitmeden dolmus gorunmuyor. GERIYE giden deger ise
        // ANINDA uygulaniyor. O yuzden once hedef+1'e, hemen sonra hedefe
        // yaziliyor - boylece cubuk gercek durumu ANINDA gosteriyor.
        // ===================================================================
        int hedef = Math.Clamp(yapilan, _cubuk.Minimum, _cubuk.Maximum);
        if (hedef < _cubuk.Maximum)
        {
            _cubuk.Value = hedef + 1;
        }

        _cubuk.Value = hedef;
        _sayac.Text = $"{yapilan} / {toplam}";
        _sag.Text = ad;
    }

    /// <summary>Is bitti; cubuk ve sayac gizlenir.</summary>
    internal void IsBitti()
    {
        _cubuk.Visible = false;
        _sayac.Visible = false;
        _iptal.Visible = false;
    }

    /// <summary>Iptal istendi; dugme bir daha basilamasin.</summary>
    internal void IptalBekleniyor()
    {
        _iptal.Enabled = false;
        _sag.Text = "İptal ediliyor…";
    }

    /// <summary>
    /// Sag taraftaki bilgi cumlesi. Cumlenin kendisi onu URETEN ozelligin
    /// dosyasinda yazilir (agac kendi ozetini, arama kendi ilerlemesini);
    /// burasi yalnizca NEREYE yazilacagini bilir.
    /// </summary>
    internal void Bilgi(string cumle) => _sag.Text = cumle;
}
