using System;
using System.Drawing;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// VERSIYON NOTU SORAN KUTU. AdKutusu KULLANILMADI - o ad dogrular ve uzanti
/// kilitler; not ise serbest metindir, bos da gecilebilir. Yanlis araci
/// esnetmek yerine kucuk bir dogru arac (CLAUDE.md 1b).
///
/// Enter = Tamam (bos notla da) - kapinin olcumu ve gunluk kullanim ayni
/// yoldan gecsin diye AcceptButton kurulu.
/// </summary>
internal static class SurumNotuKutusu
{
    /// <summary>Notu sorar. Vazgecilirse null; bos not GECERLIDIR ("").</summary>
    internal static string? Sor(IWin32Window sahip, string baslik, string aciklama)
    {
        // CLAUDE.md 6: alanlar BOYUT DEGISTIREN her seyden once atanir.
        var bilgi = new Label { Text = aciklama, AutoSize = false };
        var notKutusu = new TextBox();
        var tamam = new Button { Text = "Tamam", DialogResult = DialogResult.OK };
        var vazgec = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel };

        using var pencere = new Form
        {
            Text = baslik,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(400, 126),
            Font = new Font("Segoe UI", 9f),
        };

        bilgi.SetBounds(12, 12, 376, 34);
        notKutusu.SetBounds(12, 50, 376, 24);
        tamam.SetBounds(216, 88, 80, 26);
        vazgec.SetBounds(304, 88, 80, 26);

        pencere.Controls.Add(bilgi);
        pencere.Controls.Add(notKutusu);
        pencere.Controls.Add(tamam);
        pencere.Controls.Add(vazgec);
        pencere.AcceptButton = tamam;
        pencere.CancelButton = vazgec;

        return pencere.ShowDialog(sahip) == DialogResult.OK
            ? notKutusu.Text.Trim()
            : null;
    }
}
