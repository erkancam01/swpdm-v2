using System;
using System.IO;
using System.Windows.Forms;

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
