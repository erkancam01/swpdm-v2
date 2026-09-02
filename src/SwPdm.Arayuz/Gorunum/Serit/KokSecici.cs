using System;
using System.IO;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// KOK KLASOR SECMENIN TEK KAPISI: dugme, iletisim kutusu ve "son acilanlar"
/// listesi burada (CLAUDE.md 1b).
///
/// Disariya tek bir olay verir: <see cref="Secildi"/>. Nereden gelirse
/// gelsin - kutudan ya da gecmis listesinden - cagiran ayni yolu izler.
/// </summary>
internal sealed class KokSecici
{
    private readonly ToolStripSplitButton _dugme;

    internal KokSecici(ToolStripSplitButton dugme)
    {
        _dugme = dugme;
        _dugme.ButtonClick += (_, _) => Sor();
    }

    /// <summary>Bir kok secildi (kutudan ya da gecmisten).</summary>
    internal event EventHandler<string>? Secildi;

    /// <summary>Kutunun acilacagi yol; genelde su an acik olan kok.</summary>
    internal string? BaslangicYolu { get; set; }

    /// <summary>Kutuyu sahipsiz acar; Ctrl+O ve dugme buraya gelir.</summary>
    internal IWin32Window? Sahip { get; set; }

    /// <summary>Klasor secme kutusunu acar.</summary>
    internal void Sor()
    {
        // Kutu KabukKutusu'ndan geciyor: o, surecin calisma klasorunu geri
        // koyuyor (CLAUDE.md 4'te olculen tuzak, tek kopya).
        using var kutu = new FolderBrowserDialog
        {
            Description = "Çalışılacak kök klasörü seçin",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (BaslangicYolu is not null)
        {
            kutu.SelectedPath = BaslangicYolu;
        }

        if (KabukKutusu.Goster(kutu, Sahip) == DialogResult.OK)
        {
            Secildi?.Invoke(this, kutu.SelectedPath);
        }
    }

    /// <summary>
    /// ACILISTA HANGI KOK ACILIR - kararin tamami burada (CLAUDE.md 1b):
    /// once AnaForm.OnLoad'da yasiyordu. Kurallar:
    /// 1. Gecmis HER halde yuklenir (komut satiri dali once bunu atliyordu
    ///    ve "son acilanlar" menusu bos kaliyordu).
    /// 2. Komut satirindan kok verildiyse o acilir.
    /// 3. Yoksa son kok acilir; klasor artik yoksa GECMISTEN DUSURULUR ve
    ///    sebebi soylenir - bir daha ayni cikmaza girilmez.
    /// </summary>
    internal void AcilistaAc(Ayarlar ayarlar, string? komutSatiriKoku, Action<string> bildir)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);
        ArgumentNullException.ThrowIfNull(bildir);

        // TERSTEN: GecmiseEkle her girdiyi menunun BASINA koyuyor ("en son
        // acilan en ustte"). En yeniden en eskiye giden listeyi duz gezmek
        // menuyu ters cevirirdi; en eskiden baslayip her birini basa koymak
        // dogru sirayi verir.
        for (int i = ayarlar.SonKokler.Count - 1; i >= 0; i--)
        {
            GecmiseEkle(ayarlar.SonKokler[i]);
        }

        if (!string.IsNullOrWhiteSpace(komutSatiriKoku))
        {
            Secildi?.Invoke(this, komutSatiriKoku);
            return;
        }

        if (ayarlar.SonKok is not string kok)
        {
            return;
        }

        if (!Directory.Exists(kok))
        {
            ayarlar.KokCikar(kok);
            ayarlar.Yaz();
            bildir("Son açılan klasör bulunamadı: " + kok);
            return;
        }

        Secildi?.Invoke(this, kok);
    }

    /// <summary>
    /// Acilan koku gecmis listesine koyar. Yalnizca BU OTURUM icin; diske
    /// yazilmiyor - kalici ayar, Ayarlar adiminin isi (Erkan: "hayir").
    /// </summary>
    internal void GecmiseEkle(string yol)
    {
        BaslangicYolu = yol;

        foreach (ToolStripItem oge in _dugme.DropDownItems)
        {
            if (string.Equals(oge.Text, yol, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var girdi = new ToolStripMenuItem(yol);
        girdi.Click += (_, _) => Secildi?.Invoke(this, yol);
        _dugme.DropDownItems.Insert(0, girdi);
    }

}
