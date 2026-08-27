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

    internal DurumCubugu()
    {
        // CLAUDE.md 6: alanlar boyut degistiren her seyden ONCE atanir.
        // Burada alan baslaticilariyla atandilar; asagidaki atamalar
        // OnResize'i tetiklerse bile null bulmuyorlar.
        Dock = DockStyle.Bottom;
        SizingGrip = false;

        Items.Add(_sol);
        Items.Add(_sag);
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
    /// Sag taraftaki bilgi cumlesi. Cumlenin kendisi onu URETEN ozelligin
    /// dosyasinda yazilir (agac kendi ozetini, arama kendi ilerlemesini);
    /// burasi yalnizca NEREYE yazilacagini bilir.
    /// </summary>
    internal void Bilgi(string cumle) => _sag.Text = cumle;
}
