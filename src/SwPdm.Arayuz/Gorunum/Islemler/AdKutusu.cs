using System;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// AD SORAN KUTU - "yeniden adlandır" ve "yeni klasör" ikisi de burayı çağırır.
///
/// ORTAK ARAC, OZELLIK DEGIL (CLAUDE.md 1b'nin 3. kurali): silinecek bir
/// ozellik degil, iki islemin kullandigi tek kopya bir arac. Ad dogrulamasi,
/// uzunluk siniri, cakisma uyarisi ve UZANTI KILIDI burada TEK yerde yasiyor;
/// yeni bir cagiran gelirse ayni davranisi kendiliginden alir.
///
/// UZANTI KILIDI - NEDEN VAR (Erkan, 29.08.2026): "dosyaların adını
/// değiştirirken uzantılarını da değiştirmeme olanak sağlıyor, bu çok
/// tehlikeli". Onceden uzanti serbestce degisiyordu; tek engel onarim
/// kutusunun icinde, baska cumlelerin arasinda duran bir UYARI SATIRIYDI.
/// Simdi uzanti AYRI ve KILITLI geliyor: degistirmek icin kullanicinin ayrica
/// bir kutu isaretlemesi gerekiyor. Yani kaza ile degismesi imkansiz, bilerek
/// degistirmek hala mumkun.
/// </summary>
internal static class AdKutusu
{
    /// <summary>
    /// Yeni adi sorar. Vazgecilirse null.
    /// </summary>
    /// <param name="sahip">Sahip pencere.</param>
    /// <param name="baslik">Kutu basligi.</param>
    /// <param name="eskiAd">Kutuda dolu gelecek ad.</param>
    /// <param name="klasor">
    /// Adin olusacagi klasor. Cakisma ve TAM YOL uzunlugu buna gore olculur;
    /// bos verilirse yalnizca adin kendisi denetlenir.
    /// </param>
    /// <param name="uzantiliMi">
    /// Oge bir DOSYA mi. true ise uzanti ayri kutuda ve kilitli gelir.
    /// Klasorde uzanti kavrami yoktur.
    /// </param>
    internal static string? Sor(
        IWin32Window sahip, string baslik, string eskiAd, string klasor, bool uzantiliMi)
    {
        string uzanti = uzantiliMi ? WindowsYolu.Uzanti(eskiAd) : string.Empty;
        string govde = uzanti.Length > 0 ? eskiAd[..^uzanti.Length] : eskiAd;
        bool uzantiVar = uzanti.Length > 0;

        // CLAUDE.md 6: alanlar BOYUT DEGISTIREN her seyden once atanir.
        var adKutusu = new TextBox { Text = govde };
        var uzantiKutusu = new TextBox { Text = uzanti, ReadOnly = true, Enabled = uzantiVar };
        var uzantiOnayi = new CheckBox
        {
            Text = "Uzantıyı da değiştir",
            AutoSize = true,
            Visible = uzantiVar,
        };
        var uyari = new Label { ForeColor = Renkler.UyariYazi, AutoSize = false };
        var tamam = new Button { Text = "Tamam", DialogResult = DialogResult.OK };
        var vazgec = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel };

        using var pencere = new Form
        {
            Text = baslik,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(400, uzantiVar ? 150 : 126),
            Font = new Font("Segoe UI", 9f),
        };

        int uzantiGenisligi = uzantiVar ? 90 : 0;
        adKutusu.SetBounds(12, 12, 376 - uzantiGenisligi, 24);
        uzantiKutusu.SetBounds(12 + 380 - uzantiGenisligi, 12, uzantiGenisligi - 4, 24);
        uzantiOnayi.SetBounds(12, 42, 376, 20);
        uyari.SetBounds(12, uzantiVar ? 66 : 42, 376, 40);
        tamam.SetBounds(216, uzantiVar ? 112 : 88, 80, 26);
        vazgec.SetBounds(304, uzantiVar ? 112 : 88, 80, 26);

        pencere.Controls.Add(adKutusu);
        pencere.Controls.Add(uzantiKutusu);
        pencere.Controls.Add(uzantiOnayi);
        pencere.Controls.Add(uyari);
        pencere.Controls.Add(tamam);
        pencere.Controls.Add(vazgec);
        pencere.AcceptButton = tamam;
        pencere.CancelButton = vazgec;

        // Adin TAMAMI secili gelir; uzanti zaten ayri kutuda, yani Gezgin'in
        // "uzantiyi secme" davranisi burada kendiliginden saglaniyor.
        adKutusu.SelectAll();

        string Birlesik() => adKutusu.Text + uzantiKutusu.Text;

        void Denetle()
        {
            string yeni = Birlesik();
            bool olur = klasor.Length > 0
                ? WindowsYolu.YolGecerliMi(klasor, yeni, out string sebep)
                : WindowsYolu.AdGecerliMi(yeni, out sebep);

            // CAKISMA ONDEN SOYLENIR. Eskiden ad kutusu "Tamam" diyor, cekirdek
            // "zaten var" hatasi donduruyor ve kullanici ayri bir hata kutusu
            // goruyordu; yeni klasorde ise hic sorulmadan "(2)" ekleniyordu.
            if (olur && klasor.Length > 0
                && !string.Equals(yeni, eskiAd, StringComparison.OrdinalIgnoreCase)
                && DosyaIslemleri.Var(WindowsYolu.Birlestir(klasor, yeni)))
            {
                olur = false;
                sebep = $"\"{yeni}\" bu klasörde zaten var.";
            }

            // Uzanti degisiyorsa uyari ANINDA gorunur - kutuyu kapatip baska
            // bir kutuda soylemek gec kaliyor.
            if (olur && uzantiVar
                && !string.Equals(uzantiKutusu.Text, uzanti, StringComparison.OrdinalIgnoreCase))
            {
                sebep = $"DİKKAT: uzantı değişiyor ({uzanti} → {uzantiKutusu.Text}). "
                    + "Dosya kullanılamaz hale gelebilir.";
            }

            uyari.Text = sebep;
            tamam.Enabled = olur;
        }

        adKutusu.TextChanged += (_, _) => Denetle();
        uzantiKutusu.TextChanged += (_, _) => Denetle();
        uzantiOnayi.CheckedChanged += (_, _) =>
        {
            uzantiKutusu.ReadOnly = !uzantiOnayi.Checked;
            if (!uzantiOnayi.Checked)
            {
                uzantiKutusu.Text = uzanti;   // isaret kalkinca uzanti geri gelir
            }

            Denetle();
        };

        Denetle();

        return pencere.ShowDialog(sahip) == DialogResult.OK ? Birlesik() : null;
    }
}
