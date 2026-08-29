using System;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>Kullanicinin cakisma karari.</summary>
/// <param name="Karar">Ne yapilacak.</param>
/// <param name="Hepsine">Kalan butun cakismalara da uygulansin mi.</param>
/// <param name="Vazgecti">Islemin tamami iptal edildi mi.</param>
internal readonly record struct CakismaKarari(Cakisma Karar, bool Hepsine, bool Vazgecti);

/// <summary>
/// AD CAKISMASI KUTUSU. Hedefte ayni adda bir sey varsa ne yapilacagini sorar.
///
/// Once tek davranis vardi: islem yapilmaz, "zaten var" denirdi. Guvenliydi
/// ama 50 dosya yapistirirken cakisan 3'u icin karar hakki yoktu.
///
/// IKI DOSYANIN BOYUTU VE TARIHI GOSTERILIYOR - karar ancak boyle verilebilir;
/// "hangisi yeni" sorusunu kullanici yerine tahmin etmeyiz.
///
/// VARSAYILAN DUGME "Ikisini de tut": hicbir sey kaybettirmeyen secenek.
/// "Degistir" tehlikeli olan; varsayilan olmasi elin kaymasiyla dosya
/// kaybettirirdi (CLAUDE.md 1a).
/// </summary>
internal static class CakismaKutusu
{
    /// <summary>Kullaniciya sorar.</summary>
    internal static CakismaKarari Sor(IWin32Window sahip, string kaynak, string hedef)
    {
        // Diske TEK kapidan gidilir (CLAUDE.md 8): once burasi FileInfo ve
        // GetLastWriteTime'i kendisi cagiriyordu.
        bool klasorMu = DosyaIslemleri.Ozet(kaynak).KlasorMu;

        var pencere = new Form
        {
            Text = klasorMu ? "Bu klasör zaten var" : "Bu dosya zaten var",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
        };

        // Yazi tipleri de Component degil, Controls ile yok olmuyorlar.
        var govdeYazisi = new Font("Segoe UI", 9f);
        pencere.Font = govdeYazisi;
        var kalinYazi = new Font(govdeYazisi, FontStyle.Bold);
        pencere.Disposed += (_, _) =>
        {
            kalinYazi.Dispose();
            govdeYazisi.Dispose();
        };

        var baslik = new Label
        {
            Text = $"\"{WindowsYolu.DosyaAdi(kaynak)}\" hedefte zaten var.",
            AutoSize = true,
            Location = new Point(14, 14),
            Font = kalinYazi,
        };

        var karsilastirma = new Label
        {
            Text = "TAŞINAN / KOPYALANAN\n" + Anlat(kaynak)
                 + "\n\nHEDEFTE OLAN\n" + Anlat(hedef),
            AutoSize = false,
            Size = new Size(492, 92),
            Location = new Point(14, 40),
            ForeColor = Renkler.SuzgecYazi,
        };

        var hepsine = new CheckBox
        {
            Text = "Kalan bütün çakışmalara da uygula",
            AutoSize = true,
            Location = new Point(14, 140),
        };

        int y = 168;
        var karar = new CakismaKarari(Cakisma.Atla, Hepsine: false, Vazgecti: true);

        Button Dugme(string yazi, string aciklama, Cakisma secim, Color? renk = null)
        {
            var d = new Button
            {
                Text = yazi,
                Size = new Size(492, 34),
                Location = new Point(14, y),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
            };

            if (renk is Color c)
            {
                d.ForeColor = c;
            }

            var ipucu = new ToolTip();
            ipucu.SetToolTip(d, aciklama);

            // ToolTip bir Component; pencerenin Controls'una girmedigi icin
            // pencere kapaninca KENDILIGINDEN yok olmuyor.
            pencere.Disposed += (_, _) => ipucu.Dispose();

            d.Click += (_, _) =>
            {
                karar = new CakismaKarari(secim, hepsine.Checked, Vazgecti: false);
                pencere.DialogResult = DialogResult.OK;
                pencere.Close();
            };

            y += 40;
            return d;
        }

        var ikisiniDeTut = Dugme(
            "İkisini de tut",
            "Yeni gelen \"(2)\" ekiyle konur; hiçbir şey kaybolmaz.",
            Cakisma.IkisiniDeTut);

        pencere.Controls.Add(ikisiniDeTut);
        pencere.Controls.Add(Dugme("Atla", "Bu öğe olduğu yerde kalır.", Cakisma.Atla));

        if (!klasorMu)
        {
            pencere.Controls.Add(Dugme(
                "Değiştir  —  eskisi çöp kutusuna gider",
                "Hedefteki dosya çöp kutusuna taşınır, sonra yenisi konur. "
                + "Yanlışlıkla olursa çöp kutusundan geri alınır.",
                Cakisma.Degistir,
                Color.FromArgb(0xB0, 0x30, 0x30)));
        }

        var vazgec = new Button
        {
            Text = "Vazgeç",
            Size = new Size(100, 28),
            Location = new Point(406, y + 6),
            DialogResult = DialogResult.Cancel,
        };

        // PENCERE BOYUTU DUGMELERDEN TURETILIYOR, elle yazilmiyor.
        // OLCULDU (27.08.2026): elle yazilan yukseklik (306) "Degistir"
        // dugmesi eklendikten sonra YETMIYORDU ve "Vazgec" pencerenin
        // ALTINDA KALIYORDU - Wine'da goruntusu alindi. Hicbir hata yok,
        // dugme sadece yok. Turetilen olcu bir daha kayamaz (CLAUDE.md 1b).
        pencere.ClientSize = new Size(520, vazgec.Bottom + 12);

        pencere.Controls.Add(baslik);
        pencere.Controls.Add(karsilastirma);
        pencere.Controls.Add(hepsine);
        pencere.Controls.Add(vazgec);
        pencere.AcceptButton = ikisiniDeTut;   // en guvenli secenek varsayilan
        pencere.CancelButton = vazgec;

        using (pencere)
        {
            pencere.ShowDialog(sahip);
        }

        return karar;
    }

    /// <summary>
    /// Bir ogeyi boyutu ve tarihiyle anlatir. Okunamiyorsa SEBEP yerine
    /// "okunamadi" der - uydurma bir tarih gostermek karari bozar.
    /// </summary>
    private static string Anlat(string yol)
    {
        DosyaIslemleri.YolOzeti ozet = DosyaIslemleri.Ozet(yol);

        if (ozet.Degistirme is not DateTime zaman)
        {
            return "  (okunamadı)";
        }

        return ozet.KlasorMu
            ? "  Klasör  ·  değiştirme: " + Zaman.Yaz(zaman)
            : $"  {(ozet.Boyut is long b ? Boyut.Yaz(b) : "boyut okunamadı")}"
              + $"  ·  değiştirme: {Zaman.Yaz(zaman)}";
    }
}
