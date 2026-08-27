using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// AYARLAR SEKMESI. Kalici ayarlarin gorunen yuzu (CLAUDE.md 1b).
///
/// Su an tek konu var: cop kutusunun yeri. Yeni bir ayar geldiginde buraya
/// bir satir eklenir; ayarin kendisi Cekirdek/Ayarlar.cs'te durur.
/// </summary>
internal sealed class AyarlarSayfasi : Panel
{
    private readonly Ayarlar _ayarlar;
    private readonly Func<string?> _kok;
    private readonly Label _copYolu = new();
    private readonly Label _ayarDosyasi = new();
    private readonly CheckBox _otomatikTazele = new();

    internal AyarlarSayfasi(Ayarlar ayarlar, Func<string?> kok)
    {
        // CLAUDE.md 6: alanlar boyut degistiren her seyden ONCE atandi.
        _ayarlar = ayarlar;
        _kok = kok;

        Dock = DockStyle.Fill;
        BackColor = Renkler.GovdeArkaPlan;
        Padding = new Padding(14);
        AutoScroll = true;

        var baslik = new Label
        {
            Text = "Çöp kutusu",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 14),
        };

        var aciklama = new Label
        {
            Text = "Sildiğiniz dosyalar buraya taşınır ve buradan geri yüklenir.\n"
                 + "Varsayılan: açtığınız kökün içinde. Aynı diskte olduğu için silme\n"
                 + "ANINDA olur — 1 GB'lık montaj bile kopyalanmaz.",
            AutoSize = true,
            ForeColor = Renkler.UstBilgiYazi,
            Location = new Point(14, 40),
        };

        _copYolu.AutoSize = false;
        _copYolu.Size = new Size(460, 22);
        _copYolu.Location = new Point(14, 104);
        _copYolu.BorderStyle = BorderStyle.FixedSingle;
        _copYolu.BackColor = Renkler.AgacArkaPlan;
        _copYolu.TextAlign = ContentAlignment.MiddleLeft;
        _copYolu.Padding = new Padding(4, 0, 0, 0);
        _copYolu.AutoEllipsis = true;

        var degistir = new Button
        {
            Text = "Değiştir…",
            Size = new Size(100, 26),
            Location = new Point(14, 134),
        };
        degistir.Click += (_, _) => Degistir();

        var varsayilan = new Button
        {
            Text = "Varsayılana dön",
            Size = new Size(130, 26),
            Location = new Point(122, 134),
        };
        varsayilan.Click += (_, _) => Varsayilana();

        var tazeleBaslik = new Label
        {
            Text = "Otomatik tazeleme",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 186),
        };

        _otomatikTazele.Text = "Diskte bir şey değişince ağacı kendiliğinden tazele";
        _otomatikTazele.AutoSize = true;
        _otomatikTazele.Location = new Point(14, 212);
        _otomatikTazele.Checked = _ayarlar.OtomatikTazele;
        _otomatikTazele.CheckedChanged += (_, _) =>
        {
            _ayarlar.OtomatikTazele = _otomatikTazele.Checked;
            Kaydet();
        };

        var tazeleAciklama = new Label
        {
            Text = "Ortak sürücüde başkası bir klasör eklediğinde ya da sildiğinde\n"
                 + "görürsünüz. Ağ sürücüsünde bazı değişiklikler kaçabilir; her\n"
                 + "durumda F5 ile elle yenileyebilirsiniz.",
            AutoSize = true,
            ForeColor = Renkler.UstBilgiYazi,
            Location = new Point(32, 234),
        };

        _ayarDosyasi.Text = "Ayarlar: " + Ayarlar.Yolu;
        _ayarDosyasi.AutoSize = true;
        _ayarDosyasi.ForeColor = Renkler.UstBilgiYazi;
        _ayarDosyasi.Location = new Point(14, 302);

        Controls.Add(baslik);
        Controls.Add(aciklama);
        Controls.Add(_copYolu);
        Controls.Add(degistir);
        Controls.Add(varsayilan);
        Controls.Add(tazeleBaslik);
        Controls.Add(_otomatikTazele);
        Controls.Add(tazeleAciklama);
        Controls.Add(_ayarDosyasi);

        Tazele();
    }

    /// <summary>Ayar degisti; cagiran cop dugmesini tazelemeli.</summary>
    internal event EventHandler? Degisti;

    /// <summary>Ekrandaki yolu yeniden yazar.</summary>
    internal void Tazele()
    {
        string? kok = _kok();

        _copYolu.Text = kok is null
            ? (_ayarlar.CopUstKlasoru is string secili
                ? Cop.Yolu(string.Empty, secili)
                : "(klasör açılınca belli olur — kökün içinde)")
            : Cop.Yolu(kok, _ayarlar.CopUstKlasoru);
    }

    private void Degistir()
    {
        // CLAUDE.md 4 - OLCULMUS TUZAK: kabuk iletisim kutusu surecin CALISMA
        // KLASORUNU kaydiriyor ve o klasor bir daha silinemiyor.
        string oncekiCalismaKlasoru = Directory.GetCurrentDirectory();

        try
        {
            using var kutu = new FolderBrowserDialog
            {
                Description = "Çöp kutusunun konacağı klasörü seçin",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
            };

            if (kutu.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (!Uyar(kutu.SelectedPath))
            {
                return;
            }

            _ayarlar.CopUstKlasoru = kutu.SelectedPath;
            Kaydet();
        }
        finally
        {
            GeriKoy(oncekiCalismaKlasoru);
        }
    }

    /// <summary>
    /// Secilen yer BASKA BIR DISKTEYSE soyler.
    ///
    /// Sebep somut: cop kutusu ayni diskteyken silme bir TASIMA'dir ve
    /// anliktir. Baska diske gecince KOPYALAMA olur; 1 GB'lik bir montaj
    /// icin bu dakikalar demek - ozellikle ag surucusunde. Kullanici bunu
    /// SECIM ANINDA bilmeli (CLAUDE.md 3).
    /// </summary>
    private bool Uyar(string secilen)
    {
        if (_kok() is not string kok || Cop.AyniSurucudeMi(kok, secilen))
        {
            return true;
        }

        return MessageBox.Show(
            this,
            "Seçtiğiniz klasör, açık olan kökten BAŞKA BİR DİSKTE.\n\n"
            + $"Kök:  {kok}\n"
            + $"Çöp:  {secilen}\n\n"
            + "Aynı diskteyken silme anlık bir taşımadır. Başka diske geçince\n"
            + "KOPYALAMAYA döner: büyük bir montajı silmek dakikalar sürebilir,\n"
            + "ağ sürücüsünde daha da uzun.\n\n"
            + "Yine de bu klasör kullanılsın mı?",
            "Başka disk",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK;
    }

    private void Varsayilana()
    {
        _ayarlar.CopUstKlasoru = null;
        Kaydet();
    }

    private void Kaydet()
    {
        if (!_ayarlar.Yaz())
        {
            // Sessiz basarisizlik YASAK: ayar kaydedilemediyse kullanici
            // bunu bilmeli, yoksa bir dahaki aciliste kaybolmus sanir.
            MessageBox.Show(
                this,
                "Ayar bu oturumda geçerli ama diske YAZILAMADI:\n" + Ayarlar.Yolu,
                "Ayar kaydedilemedi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        Tazele();
        Degisti?.Invoke(this, EventArgs.Empty);
    }

    private static void GeriKoy(string klasor)
    {
        try
        {
            Directory.SetCurrentDirectory(klasor);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                         or DirectoryNotFoundException)
        {
            // Eski klasor artik yoksa yapacak bir sey yok.
        }
    }
}
