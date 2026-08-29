using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// COP KUTUSU PENCERESI - silinenleri gosterir ve geri yukler.
///
/// CLAUDE.md 1b: cop kutusuna dair her arayuz karari burada. Kaldirmak =
/// bu dosyayi sil + AnaForm'daki bir satiri kes.
///
/// CLAUDE.md 3: cop klasorunun YOLU her zaman ekranda yaziyor. Klasor agacta
/// gizli, ama nerede oldugu gizli DEGIL.
/// </summary>
internal sealed class CopKutusuPenceresi : Form
{
    private readonly string _cop;
    private readonly Action<string> _bildir;
    private readonly ListView _liste = new();
    private readonly Label _yer = new();
    private readonly Button _geriYukle = new();
    private readonly Button _kaliciSil = new();
    private readonly Button _bosalt = new();
    private readonly Button _kapat = new();

    private CopKutusuPenceresi(string cop, Action<string> bildir)
    {
        // CLAUDE.md 6: alanlar boyut degistiren her seyden ONCE atanmis olmali.
        // Hepsi alan baslaticilariyla atandi; asagisi guvenli.
        _cop = cop;
        _bildir = bildir;

        Text = "Çöp kutusu";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ClientSize = new Size(760, 420);
        MinimumSize = new Size(560, 300);
        Font = new Font("Segoe UI", 9f);
        BackColor = Renkler.GovdeArkaPlan;

        _liste.View = View.Details;
        _liste.FullRowSelect = true;
        _liste.MultiSelect = true;          // ListView'de coklu secim HAZIR var
        _liste.HideSelection = false;
        _liste.Dock = DockStyle.Fill;
        _liste.Columns.Add("Ad", 190);
        _liste.Columns.Add("Eski konum", 300);
        _liste.Columns.Add("Silinme", 130);
        _liste.Columns.Add("Boyut", 90, HorizontalAlignment.Right);
        _liste.SelectedIndexChanged += (_, _) => DugmeleriTazele();

        _yer.Dock = DockStyle.Top;
        _yer.Height = 34;
        _yer.TextAlign = ContentAlignment.MiddleLeft;
        _yer.Padding = new Padding(8, 0, 8, 0);
        _yer.ForeColor = Renkler.UstBilgiYazi;

        var serit = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 8, 8, 8),
        };

        Dugme(_kapat, "Kapat", (_, _) => Close());
        Dugme(_bosalt, "Tümünü boşalt", (_, _) => Bosalt());
        Dugme(_kaliciSil, "Kalıcı sil", (_, _) => KaliciSil());
        Dugme(_geriYukle, "Geri yükle", (_, _) => GeriYukle());

        serit.Controls.Add(_kapat);
        serit.Controls.Add(_bosalt);
        serit.Controls.Add(_kaliciSil);
        serit.Controls.Add(_geriYukle);

        Controls.Add(_liste);
        Controls.Add(serit);
        Controls.Add(_yer);
        CancelButton = _kapat;

        Doldur();
    }

    /// <summary>Pencereyi acar.</summary>
    internal static void Goster(IWin32Window sahip, string cop, Action<string> bildir)
    {
        using var pencere = new CopKutusuPenceresi(cop, bildir);
        pencere.ShowDialog(sahip);
    }

    private static void Dugme(Button d, string yazi, EventHandler tiklandi)
    {
        d.Text = yazi;
        d.AutoSize = false;
        d.Size = new Size(110, 28);
        d.Margin = new Padding(6, 0, 0, 0);
        d.Click += tiklandi;
    }

    private void Doldur()
    {
        _liste.BeginUpdate();
        _liste.Items.Clear();

        CopDurumu durum = Cop.Oku(_cop);
        IReadOnlyList<CopOgesi> ogeler = durum.Ogeler;
        foreach (CopOgesi oge in ogeler)
        {
            var satir = new ListViewItem(oge.Ad) { Tag = oge };
            satir.SubItems.Add(WindowsYolu.Klasor(oge.EskiYol));
            satir.SubItems.Add(oge.Zaman.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture));

            // Klasorun boyutu BILINMIYOR ve "0 B" yazmak yalan olurdu.
            satir.SubItems.Add(oge.KlasorMu ? "klasör" : Boyut.Yaz(oge.Boyut));
            _liste.Items.Add(satir);
        }

        _liste.EndUpdate();

        // "OKUNAMADI" ILE "BOS" AYRI SEY. Okunamayan bir kutuya "boş" demek,
        // kullaniciya silinmis dosyalarinin kayboldugunu dusundurur
        // (CLAUDE.md 3). Bozuk satir varsa o da sayilir: listede gorunmeyen
        // ama diskte duran oge demektir.
        string ek = durum.BozukSatir > 0
            ? $"   ({durum.BozukSatir} kayıt satırı okunamadı)"
            : string.Empty;

        _yer.Text = !durum.Guvenilir
            ? durum.Okunamadi + "   Yeri: " + _cop
            : ogeler.Count == 0
                ? "Çöp kutusu boş.   Yeri: " + _cop + ek
                : $"{ogeler.Count} öğe.   Yeri: {_cop}{ek}";

        DugmeleriTazele();
    }

    private void DugmeleriTazele()
    {
        bool secimVar = _liste.SelectedItems.Count > 0;
        _geriYukle.Enabled = secimVar;
        _kaliciSil.Enabled = secimVar;
        _bosalt.Enabled = _liste.Items.Count > 0;
    }

    private List<CopOgesi> Secililer()
    {
        var sonuc = new List<CopOgesi>(_liste.SelectedItems.Count);
        foreach (ListViewItem satir in _liste.SelectedItems)
        {
            if (satir.Tag is CopOgesi oge)
            {
                sonuc.Add(oge);
            }
        }

        return sonuc;
    }

    private void GeriYukle()
    {
        var olan = new List<string>();
        var olmayan = new List<string>();
        var adiDegisen = new List<string>();

        foreach (CopOgesi oge in Secililer())
        {
            IslemRaporu rapor = Cop.GeriYukle(_cop, oge);
            if (!rapor.Oldu)
            {
                olmayan.Add(oge.Ad + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
                continue;
            }

            olan.Add(oge.Ad);

            // Ayni adda bir sey varsa numaralanmis olabilir; bu SOYLENIR,
            // yoksa kullanici dosyayi eski adiyla arar ve bulamaz.
            string yeniAd = WindowsYolu.DosyaAdi(rapor.YeniYol ?? string.Empty);
            if (!string.Equals(yeniAd, oge.Ad, StringComparison.Ordinal))
            {
                adiDegisen.Add($"{oge.Ad}  →  {yeniAd}");
            }
        }

        Doldur();
        // ADI DEGISENIN REFERANSI KIRILIR: onu kullanan montaj eski adi
        // arar ve artik baska bir dosya o adi tasiyor. Sessiz kalmak
        // kullaniciya YANLIS dosyayi actirir (CLAUDE.md 3).
        Rapor("Geri yükleme", olan, olmayan, adiDegisen,
            "Aynı adda dosya olduğu için adı değiştirilenler "
            + "(bunları kullanan belgeler parçayı bulamaz):");
    }

    private void KaliciSil()
    {
        List<CopOgesi> secililer = Secililer();
        if (secililer.Count == 0)
        {
            return;
        }

        if (!Onayla($"{secililer.Count} öğe KALICI olarak silinecek.\n\n"
            + "Bu işlem GERİ ALINAMAZ — dosyalar bir daha geri gelmez."))
        {
            return;
        }

        var olan = new List<string>();
        var olmayan = new List<string>();
        foreach (CopOgesi oge in secililer)
        {
            IslemRaporu rapor = Cop.KaliciSil(_cop, oge);
            (rapor.Oldu ? olan : olmayan).Add(
                rapor.Oldu ? oge.Ad : oge.Ad + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
        }

        Doldur();
        Rapor("Kalıcı silme", olan, olmayan, [], string.Empty);
    }

    private void Bosalt()
    {
        CopDurumu durum = Cop.Oku(_cop);
        IReadOnlyList<CopOgesi> hepsi = durum.Ogeler;

        // OKUNAMAYAN KUTU BOSALTILMAZ: elimizdeki liste eksik olabilir ve
        // "boşalttım" demek yalan olurdu (CLAUDE.md 3).
        if (!durum.Guvenilir)
        {
            MessageBox.Show(
                this, durum.Okunamadi, "Çöp kutusu boşaltılamadı",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (hepsi.Count == 0)
        {
            return;
        }

        if (!Onayla($"Çöp kutusundaki {hepsi.Count} öğenin TAMAMI kalıcı olarak "
            + "silinecek.\n\nBu işlem GERİ ALINAMAZ."))
        {
            return;
        }

        var olan = new List<string>();
        var olmayan = new List<string>();
        foreach (CopOgesi oge in hepsi)
        {
            IslemRaporu rapor = Cop.KaliciSil(_cop, oge);
            (rapor.Oldu ? olan : olmayan).Add(
                rapor.Oldu ? oge.Ad : oge.Ad + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
        }

        Doldur();
        Rapor("Çöp kutusunu boşaltma", olan, olmayan, [], string.Empty);
    }

    /// <summary>Varsayilan dugme VAZGEC - geri alinamaz islemde dogrusu bu.</summary>
    /// <summary>Onay - butun uygulamada tek yerden (CLAUDE.md 1b).</summary>
    private bool Onayla(string metin)
        => OnayKutusu.Sor(this, "Onay", metin, tehlikeli: true);

    /// <summary>
    /// CLAUDE.md 3: kismi basarisizlikta NE OLDU NE OLMADI tek tek yazilir.
    /// "Bazilari olmadi" demek kullaniciyi ikinci kez denemeye iter.
    /// </summary>
    private void Rapor(
        string baslik,
        List<string> olan,
        List<string> olmayan,
        List<string> notlar,
        string notBasligi)
    {
        _bildir(olmayan.Count == 0
            ? $"{baslik}: {olan.Count} öğe."
            : $"{baslik}: {olan.Count} oldu · {olmayan.Count} olmadı");

        if (olmayan.Count == 0 && notlar.Count == 0)
        {
            return;
        }

        var metin = new StringBuilder();
        metin.AppendLine($"{olan.Count} öğe tamam.");

        if (notlar.Count > 0)
        {
            metin.AppendLine();
            metin.AppendLine(notBasligi);
            foreach (string satir in notlar)
            {
                metin.AppendLine("  • " + satir);
            }
        }

        if (olmayan.Count > 0)
        {
            metin.AppendLine();
            metin.AppendLine($"{olmayan.Count} öğe OLMADI:");
            foreach (string satir in olmayan)
            {
                metin.AppendLine("  • " + satir);
            }
        }

        MessageBox.Show(this, metin.ToString(), baslik,
            MessageBoxButtons.OK,
            olmayan.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }
}
