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

    /// <summary>Hangi sutuna gore siralaniyor ve hangi yonde.</summary>
    private int _siraSutunu = 2;   // varsayilan: silinme zamani
    private bool _artan;

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

        // LISTE ARTIK FAREYE VE KLAVYEYE CEVAP VERIYOR (29.08.2026). Once
        // yalnizca SelectedIndexChanged bagliydi: cift tiklamak hicbir sey
        // yapmiyor, Delete hicbir sey yapmiyor, sutun basligina tiklamak
        // siralamiyordu - oysa bunlarin hepsi bir liste penceresinden
        // beklenen seyler.
        _liste.MouseDoubleClick += (_, _) => GeriYukle();
        _liste.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete && _liste.SelectedItems.Count > 0)
            {
                e.SuppressKeyPress = true;
                KaliciSil();
            }
            else if (e.KeyCode == Keys.Enter && _liste.SelectedItems.Count > 0)
            {
                e.SuppressKeyPress = true;
                GeriYukle();
            }
        };

        _liste.ColumnClick += (_, e) =>
        {
            // Ayni sutuna ikinci tik yonu cevirir.
            _artan = _siraSutunu == e.Column ? !_artan : true;
            _siraSutunu = e.Column;
            Doldur();
        };

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
        List<CopOgesi> ogeler = [.. durum.Ogeler];
        Sirala(ogeler);

        foreach (CopOgesi oge in ogeler)
        {
            var satir = new ListViewItem(oge.Ad) { Tag = oge };
            satir.SubItems.Add(WindowsYolu.Klasor(oge.EskiYol));
            // Tarih bicimi TEK yerde (Zaman.Yaz) - bir alt satirdaki Boyut.Yaz
            // ile ayni disiplin (CLAUDE.md 8).
            satir.SubItems.Add(Zaman.Yaz(oge.Zaman));

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

    /// <summary>
    /// Listeyi secili sutuna gore sirala. Ad ve klasor DOGAL siralamayla
    /// ("Parça10" > "Parça9") - agacta kullanilan karsilastiricinin aynisi
    /// (CLAUDE.md 8: ikinci bir siralama mantigi yazilmaz).
    /// </summary>
    /// <summary>
    /// Toplu isin ilerlemesini UST SATIRDA gosterir.
    ///
    /// NEDEN Refresh() VE NEDEN IPTAL YOK: bu pencere modal ve is arayuz is
    /// parcaciginda kosuyor; mesaj kuyrugu pompalanmadigi icin bir "Iptal"
    /// dugmesi TIKLANAMAZ - koysaydik calismayan bir dugme olurdu (CLAUDE.md 3).
    /// Refresh() yalnizca bu etiketi boyuyor, kuyruga dokunmuyor.
    ///
    /// ISI ARKA PLANA ALMAK dogru cozum ama bu pencerenin tamamini
    /// degistirmek demek; SIRADAKI.md'ye yazildi.
    /// </summary>
    private void Ilerle(string is_, int yapilan, int toplam)
    {
        _yer.Text = $"{is_}: {yapilan}/{toplam}…";
        _yer.Refresh();
    }

    private void Sirala(List<CopOgesi> ogeler)
    {
        Comparison<CopOgesi> olcut = _siraSutunu switch
        {
            0 => (a, b) => DogalKarsilastirici.Ortak.Compare(a.Ad, b.Ad),
            1 => (a, b) => DogalKarsilastirici.Ortak.Compare(
                WindowsYolu.Klasor(a.EskiYol), WindowsYolu.Klasor(b.EskiYol)),
            3 => (a, b) => a.Boyut.CompareTo(b.Boyut),
            _ => (a, b) => a.Zaman.CompareTo(b.Zaman),
        };

        ogeler.Sort((a, b) => _artan ? olcut(a, b) : olcut(b, a));
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

        // CAKISMA ARTIK SORULUYOR (29.08.2026): eskiden aynı adda bir şey
        // varsa karar kullanicinin degil KODUN'du - sessizce numaralaniyordu.
        // Uygulamada tam bu is icin bir cakisma kutusu duruyordu.
        Cakisma hepsiIcin = Cakisma.Sor;

        List<CopOgesi> secililer = Secililer();
        int sira = 0;

        foreach (CopOgesi oge in secililer)
        {
            Ilerle("Geri yükleniyor", ++sira, secililer.Count);
            Cakisma karar = hepsiIcin;
            string dogrudan = WindowsYolu.Birlestir(WindowsYolu.Klasor(oge.EskiYol), oge.Ad);

            if (DosyaIslemleri.Var(dogrudan) && karar == Cakisma.Sor)
            {
                CakismaKarari cevap = CakismaKutusu.Sor(
                    this, Cop.IcerdekiYolu(_cop, oge), dogrudan);

                if (cevap.Vazgecti)
                {
                    break;
                }

                if (cevap.Hepsine)
                {
                    hepsiIcin = cevap.Karar;
                }

                karar = cevap.Karar;
            }

            IslemRaporu rapor = Cop.GeriYukle(
                _cop, oge, karar == Cakisma.Sor ? Cakisma.IkisiniDeTut : karar);

            if (rapor.Sonuc == IslemSonucu.Atlandi)
            {
                continue;
            }

            if (!rapor.Oldu)
            {
                olmayan.Add(oge.Ad + " — " + rapor.Sebebi);
                continue;
            }

            olan.Add(oge.Ad);

            // Ayni adda bir sey varsa numaralanmis olabilir; bu SOYLENIR,
            // yoksa kullanici dosyayi eski adiyla arar ve bulamaz.
            // Karar TEK yerde: Cop.DegisenAd (CLAUDE.md 8).
            if (Cop.DegisenAd(rapor, oge) is string yeniAd)
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
        int sira = 0;

        foreach (CopOgesi oge in secililer)
        {
            Ilerle("Kalıcı siliniyor", ++sira, secililer.Count);
            IslemRaporu rapor = Cop.KaliciSil(_cop, oge);
            (rapor.Oldu ? olan : olmayan).Add(
                rapor.Oldu ? oge.Ad : oge.Ad + " — " + rapor.Sebebi);
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
        int sira = 0;

        foreach (CopOgesi oge in hepsi)
        {
            Ilerle("Boşaltılıyor", ++sira, hepsi.Count);
            IslemRaporu rapor = Cop.KaliciSil(_cop, oge);
            (rapor.Oldu ? olan : olmayan).Add(
                rapor.Oldu ? oge.Ad : oge.Ad + " — " + rapor.Sebebi);
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

        // Madde listeleri TEK yerden (MaddeKutusu, CLAUDE.md 8).
        if (notlar.Count > 0)
        {
            metin.AppendLine();
            metin.AppendLine(MaddeKutusu.Metin(notBasligi, notlar));
        }

        if (olmayan.Count > 0)
        {
            metin.AppendLine();
            metin.AppendLine(MaddeKutusu.Metin($"{olmayan.Count} öğe OLMADI:", olmayan));
        }

        MessageBox.Show(this, metin.ToString(), baslik,
            MessageBoxButtons.OK,
            olmayan.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }
}
