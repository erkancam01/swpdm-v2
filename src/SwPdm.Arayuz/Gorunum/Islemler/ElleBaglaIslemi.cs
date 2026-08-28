using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// REFERANSI ELLE BAGLA (Erkan, 28.08.2026: "dosyaların referansını kendim
/// seçerek düzeltebiliyor muyum").
///
/// NEDEN GEREKLI - OTOMATIGIN YETMEDIGI YER: bizim cozucumuz yazili yolu
/// ADA gore ariyor (CLAUDE.md 5: dosyalarin icindeki yollar yazarin
/// makinesine ait, tam yol eslesmesi calismaz). Dosya baska bir programla
/// YENIDEN ADLANDIRILDIYSA ortada eslesecek ad kalmaz; "BULUNAMADI" satiri
/// boyle dogar ve toplu onarim da onu duzeltemez - baglayacagi hedefi
/// bilmiyor. Hangi dosya oldugunu YALNIZCA kullanici bilir.
///
/// NE YAPMAZ: dosya secmeyi ARAMAYA cevirmez, tahmin yurutmez, "en yakin
/// ad" onermez. Yanlis bir tahmini onaylatmak, kullaniciya yanlis dosyayi
/// bagletirdi (CLAUDE.md 3: belirsizse tek cevap uydurulmaz).
///
/// SIRA: tara -> baglanabilecekleri goster -> hedefi SEC -> ONAYLA -> yama.
/// Yama tek yerde (<see cref="YolBaglama.Bagla"/>): KOPYALA -> YAMA ->
/// DOGRULA -> DEGISTIR; tutmazsa asil dosya HIC degismez.
/// </summary>
internal sealed class ElleBaglaIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Referansı elle bağla…";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.L;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        ArgumentNullException.ThrowIfNull(secim);

        if (secim.TekOge is not DosyaOgesi dosya)
        {
            nedenOlmaz = "Tek bir dosya seçin.";
            return false;
        }

        if (!SwReferans.TasiyabilirMi(dosya.Yol))
        {
            nedenOlmaz = "Bu tür referans taşımıyor.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        ArgumentNullException.ThrowIfNull(baglam);

        // ONCE TARA: liste bayat bir indeksten gelirse kullaniciya artik
        // dogru olmayan bir "BULUNAMADI" gosterilir ve o da olmayan bir
        // sorunu "duzeltir" - dosyaya bosuna yazmis oluruz (CLAUDE.md 3).
        ReferansTazeleme.Once(baglam, () => Devam(baglam));
    }

    private static void Devam(IslemBaglami baglam)
    {
        if (SecimBaglami.Yolu(baglam.Secim.TekOge) is not string ebeveyn)
        {
            baglam.Bildir("Tek bir dosya seçin.");
            return;
        }

        ReferansIndeksi? indeks = baglam.Referanslar.Indeks;
        if (indeks is null)
        {
            baglam.Bildir("Önce bir klasör açın.");
            return;
        }

        IndeksKaydi? kayit = indeks.Kayit(ebeveyn);
        if (kayit is null)
        {
            baglam.Bildir("Bu dosya taranmadı; referansları bilinmiyor.");
            return;
        }

        if (!kayit.Okundu)
        {
            baglam.Bildir("Bu dosyanın referansları okunamadı: " + (kayit.Sebep ?? "sebep yok"));
            return;
        }

        IReadOnlyList<(string YazilanYol, Cozum Cozum)> adaylar =
            YolBaglama.BaglanabilirYollar(indeks, ebeveyn);

        // BOS LISTE BURADA IYI HABER, ama yine de SEBEBIYLE soyleniyor:
        // "hicbir sey olmadi" demek kullaniciyi ikinci kez denemeye iter.
        if (adaylar.Count == 0)
        {
            baglam.Bildir(kayit.YazilanYollar.Count == 0
                ? WindowsYolu.DosyaAdi(ebeveyn) + " başka dosya kullanmıyor."
                : WindowsYolu.DosyaAdi(ebeveyn) + " — bağlanacak referans yok; hepsi çözülüyor.");
            return;
        }

        string? secilenYazilan = BaglanacakYol(baglam.Sahip, ebeveyn, adaylar);
        if (secilenYazilan is null)
        {
            return;   // vazgecildi; kutu zaten kapandi, durum cubuguna yazacak sey yok
        }

        string yazilanAd = WindowsYolu.DosyaAdi(secilenYazilan);
        string? hedef = HedefiSor(baglam.Sahip, baglam.Secim.Kok, yazilanAd);
        if (hedef is null)
        {
            return;
        }

        if (!Onayla(baglam.Sahip, ebeveyn, secilenYazilan, hedef))
        {
            return;
        }

        Yaz(baglam, ebeveyn, yazilanAd, hedef);
    }

    /// <summary>
    /// Hangi yazili yol baglanacak. Tek aday varsa KUTU ACILMAZ - secilecek
    /// bir sey yokken secim sormak, kullaniciya bos bir adim attirir.
    /// </summary>
    private static string? BaglanacakYol(
        IWin32Window sahip, string ebeveyn,
        IReadOnlyList<(string YazilanYol, Cozum Cozum)> adaylar)
    {
        if (adaylar.Count == 1)
        {
            return adaylar[0].YazilanYol;
        }

        var liste = new ListView
        {
            View = View.Details,
            HeaderStyle = ColumnHeaderStyle.None,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            BorderStyle = BorderStyle.FixedSingle,
        };

        liste.Columns.Add(new ColumnHeader { Text = "Yazılı yol", Width = 400 });
        liste.Columns.Add(new ColumnHeader
        {
            Text = "Durum", Width = 120, TextAlign = HorizontalAlignment.Right,
        });

        foreach ((string yazilan, Cozum cozum) in adaylar)
        {
            var satir = new ListViewItem(yazilan)
            {
                Tag = yazilan,
                ToolTipText = yazilan,
                ForeColor = ReferansIndeksi.BayatMi(ebeveyn, yazilan, cozum)
                    ? Renkler.YolBayatYazi
                    : Renkler.ReferansAsagiYazi,
            };

            satir.SubItems.Add(Durum(ebeveyn, yazilan, cozum));
            liste.Items.Add(satir);
        }

        liste.Items[0].Selected = true;

        var sec = new Button { Text = "Seç", DialogResult = DialogResult.OK, Width = 90 };
        var vazgec = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel, Width = 90 };

        using var pencere = new Form
        {
            Text = "Hangi referans bağlanacak?",
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(560, 320),
            Font = new Font("Segoe UI", 9f),
        };

        liste.SetBounds(12, 12, 536, 258);
        sec.SetBounds(346, 280, 90, 28);
        vazgec.SetBounds(446, 280, 90, 28);
        liste.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        sec.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        vazgec.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        pencere.Controls.Add(liste);
        pencere.Controls.Add(sec);
        pencere.Controls.Add(vazgec);
        pencere.AcceptButton = sec;
        pencere.CancelButton = vazgec;

        // Cift tik = "bunu sec": listede en dogal hareket bu ve kullanici
        // dugmeyi aramak zorunda kalmiyor.
        liste.MouseDoubleClick += (_, e) =>
        {
            if (liste.GetItemAt(e.X, e.Y) is not null)
            {
                pencere.DialogResult = DialogResult.OK;
            }
        };

        if (pencere.ShowDialog(sahip) != DialogResult.OK || liste.SelectedItems.Count == 0)
        {
            return null;
        }

        return liste.SelectedItems[0].Tag as string;
    }

    /// <summary>Satirin durumu - referans panelindeki kelimelerin AYNISI.</summary>
    private static string Durum(string ebeveyn, string yazilan, Cozum cozum)
    {
        if (ReferansIndeksi.BayatMi(ebeveyn, yazilan, cozum))
        {
            return "yol BAYAT";
        }

        return cozum.Durum == CozumDurumu.Belirsiz
            ? $"{cozum.Adaylar.Count} aday"
            : "BULUNAMADI";
    }

    /// <summary>
    /// Gercek dosyayi KENDI AGACIMIZDAN sectirir; vazgecerse null.
    ///
    /// WINDOWS DOSYA KUTUSU KULLANILMIYOR (Erkan, 28.08.2026: "windows dosya
    /// yoneticisiyle bir isimiz olsun istemiyorum"). Ayni agac, ayni simgeler,
    /// ayni siralama - kullanici zaten bildigi yerde seciyor. Yan kazanc: kabuk
    /// kutusunun CALISMA KLASORUNU kaydirma tuzagi (CLAUDE.md 4) bu yolda hic
    /// dogmuyor.
    ///
    /// SINIR VE SEBEBI: secim ACIK KOKUN icinden yapilir; agacimiz orayi
    /// gosteriyor. Dosya baska bir surucudeyse once o klasor acilmali - kutuda
    /// yaziyor, sessizce "bulunamadi" denmiyor (CLAUDE.md 3).
    /// </summary>
    private static string? HedefiSor(IWin32Window sahip, string? kok, string yazilanAd)
    {
        if (string.IsNullOrWhiteSpace(kok))
        {
            return null;
        }

        // CLAUDE.md 6: alanlar, BOYUT DEGISTIREN her seyden (Dock, ClientSize)
        // once atanir - boyut degisimi OnResize'i o anda tetikliyor.
        var simgeler = TurSimgeleri.Liste();
        var agac = new SecimliAgac
        {
            ImageList = simgeler,
            HideSelection = false,
            BorderStyle = BorderStyle.FixedSingle,
            ShowNodeToolTips = true,
        };

        var baslik = new Label
        {
            AutoSize = false,
            Text = $"\"{yazilanAd}\" hangi dosya? — açık kök içinden seçin.",
            ForeColor = Renkler.UstBilgiYazi,
        };

        var sec = new Button { Text = "Seç", DialogResult = DialogResult.OK, Width = 90, Enabled = false };
        var vazgec = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel, Width = 90 };

        using var pencere = new Form
        {
            Text = "Dosyayı seç",
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(520, 460),
            Font = new Font("Segoe UI", 9f),
        };

        baslik.SetBounds(12, 10, 496, 18);
        agac.SetBounds(12, 32, 496, 380);
        sec.SetBounds(306, 422, 90, 28);
        vazgec.SetBounds(406, 422, 90, 28);
        baslik.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        agac.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        sec.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        vazgec.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        pencere.Controls.Add(baslik);
        pencere.Controls.Add(agac);
        pencere.Controls.Add(sec);
        pencere.Controls.Add(vazgec);
        pencere.AcceptButton = sec;
        pencere.CancelButton = vazgec;

        // KLASOR SECILEMEZ: bir referans dosyaya baglanir. Dugme sebebiyle
        // gri kaliyor, tiklanip sessizce hicbir sey yapmiyor degil.
        agac.AfterSelect += (_, _) => sec.Enabled = Secilen(agac) is not null;
        agac.NodeMouseDoubleClick += (_, e) =>
        {
            if (SecimliAgac.Yolu(e.Node) is not null
                && AgacDoldurucu.Etiket(e.Node) is DosyaOgesi)
            {
                pencere.DialogResult = DialogResult.OK;
            }
        };

        var doldurucu = new AgacDoldurucu(agac);
        doldurucu.KokuAc(kok);

        try
        {
            return pencere.ShowDialog(sahip) == DialogResult.OK ? Secilen(agac) : null;
        }
        finally
        {
            simgeler.Dispose();   // ImageList pencereyle birlikte atilmiyor
        }
    }

    /// <summary>Agacta secili olan DOSYANIN yolu; klasor ya da bos ise null.</summary>
    private static string? Secilen(SecimliAgac agac)
        => AgacDoldurucu.Etiket(agac.SelectedNode) is DosyaOgesi dosya ? dosya.Yol : null;

    /// <summary>
    /// ONAY - dosyanin ICINE yaziyoruz ve bu GERI ALINAMAZ.
    ///
    /// Kutu iki yolu da TAM yaziyor: neyin yerine ne gececek. Kisaltilmis bir
    /// ad ("Parça1.SLDPRT -> Parça1.SLDPRT") iki farkli klasordeki iki dosyayi
    /// AYNI gosterirdi ve onay bir sey ifade etmezdi.
    /// </summary>
    private static bool Onayla(IWin32Window sahip, string ebeveyn, string yazilan, string hedef)
        => OnayKutusu.Sor(
            sahip,
            "Referansı bağla",
            WindowsYolu.DosyaAdi(ebeveyn) + " dosyasının içindeki yol değiştirilecek:"
            + "\n\nŞu an yazan:\n" + yazilan
            + "\n\nBundan sonra:\n" + hedef
            + "\n\nBu işlem geri ALINAMAZ (Ctrl+Z bunu geri almaz)."
            + "\nDosya SOLIDWORKS'te açıksa önce kapatın.",
            tehlikeli: true);

    private static void Yaz(IslemBaglami baglam, string ebeveyn, string yazilanAd, string hedef)
    {
        string? hata = YolBaglama.Bagla(ebeveyn, yazilanAd, hedef);

        if (hata is not null)
        {
            // EKRANDA gosteriliyor, yalnizca durum cubugunda degil: kullanici
            // "duzelttim" saniyor olabilir (CLAUDE.md 3).
            MessageBox.Show(
                baglam.Sahip,
                "Referans bağlanamadı — dosya DEĞİŞMEDİ.\n\n" + hata,
                "Referansı elle bağla",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            baglam.Bildir("Bağlanamadı: " + hata);
            return;
        }

        // Indeks bu dosya icin BAYATLADI: az once icindeki yol degisti.
        // Tazelenmezse panel hala eski yolu gosterir ve kullanici onarimin
        // olmadigini sanir.
        baglam.Referanslar.Tazele([ebeveyn]);
        baglam.Tazele(ebeveyn);
        baglam.Bildir(
            WindowsYolu.DosyaAdi(ebeveyn) + " → " + WindowsYolu.DosyaAdi(hedef)
            + " bağlandı. SOLIDWORKS'te açıp doğrulayın.");
    }
}
