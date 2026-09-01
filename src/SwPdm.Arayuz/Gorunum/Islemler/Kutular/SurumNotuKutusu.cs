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
    /// <param name="baslangic">Kutuda hazir duracak metin - VAR OLAN bir
    /// notu duzeltirken (F2) doldurulur; yeni versiyonda bos gecilir.</param>
    /// <param name="listeyiGoster">
    /// Verilirse kutuya "Listeyi göster…" dugmesi konur ve tiklanınca bu is
    /// kosar; kutu KAPANMAZ.
    ///
    /// NEDEN VAR (Erkan, 01.09.2026: "241 dosya diyor, bi hata var"):
    /// arsivlenecek dosya sayisi buyuk bir montajda uc haneli oluyor ve
    /// kullanici o sayiyi DOGRULAYAMIYOR. Sayiya bakip "arsivle" demek
    /// zorunda kalmak, CLAUDE.md 3'un tam tersi. Listeyi gormek, sayinin
    /// dogru olup olmadigini KULLANICININ kendi verisinde olcmesinin tek
    /// yolu (CLAUDE.md 2).
    /// </param>
    internal static string? Sor(
        IWin32Window sahip, string baslik, string aciklama, string baslangic = "",
        Action? listeyiGoster = null)
    {
        // CLAUDE.md 6: alanlar BOYUT DEGISTIREN her seyden once atanir.
        var bilgi = new Label { Text = aciklama, AutoSize = false };
        var notKutusu = new TextBox { Text = baslangic ?? string.Empty };
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

        if (listeyiGoster is not null)
        {
            var liste = new Button { Text = "Listeyi göster…" };
            liste.SetBounds(12, 88, 120, 26);

            // DialogResult YOK: kutu acik kalir, kullanici listeye bakip
            // notunu yazmaya devam eder.
            liste.Click += (_, _) => listeyiGoster();
            pencere.Controls.Add(liste);
        }

        pencere.Controls.Add(bilgi);
        pencere.Controls.Add(notKutusu);
        pencere.Controls.Add(tamam);
        pencere.Controls.Add(vazgec);
        pencere.AcceptButton = tamam;
        pencere.CancelButton = vazgec;

        // ODAK PENCERE GORUNDUKTEN SONRA (CLAUDE.md 11): Focus() gorunmeyen
        // pencerede sessizce hicbir sey yapmiyor. Var olan not SECILI gelir -
        // duzeltmeye gelen kullanici dogrudan yazabilsin.
        pencere.Shown += (_, _) =>
        {
            notKutusu.Focus();
            notKutusu.SelectAll();
        };

        return pencere.ShowDialog(sahip) == DialogResult.OK
            ? notKutusu.Text.Trim()
            : null;
    }
}
