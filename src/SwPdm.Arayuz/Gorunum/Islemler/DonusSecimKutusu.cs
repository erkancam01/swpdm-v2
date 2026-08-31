using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// "BU VERSIYONA DON" KUTUSU - iki yonu birden gosterir.
///
/// ASAGI (bu versiyonun KULLANDIGI dosyalar) - Erkan'in ilk versiyon
/// isteginin 3. maddesi: montajin versiyonu artik o gunku PARCALARI da
/// tasiyor. Yalniz montaji geri yazmak "eski versiyona dondum" sanisi
/// yaratirdi, oysa parcalar bugunku halinde kalirdi (CLAUDE.md 3). Karar
/// kullanicinin: hangi parcanin geri yazilacagini GORUP secer.
/// Varsayilan akilli: yalnizca BUGUNKUNDEN FARKLI olanlar isaretli gelir.
/// Engelli satir (SOLIDWORKS'te acik, bugun yok) isaretlenemez ve SEBEBI
/// yaninda yazar.
///
/// YUKARI (bu dosyayi KULLANAN montajlar) - Erkan, 31.08.2026: "versiyon
/// secince o parcanin kullanildigi tum montajlar degissin, yoksa karisiklik
/// olur." CEVAP: zaten oyle oluyor. Donus, arsiv kopyasini CANLI DOSYANIN
/// KENDI YOLUNA yaziyor (Surumler.Don -> File.Replace); tek dosya var, kopya
/// yok, montajlar dosyaya yol uzerinden bakiyor. Montaj dosyalarina hic
/// dokunulmuyor - dokunulmasina gerek de yok.
///
/// EKSIK OLAN SEY GOSTERMEKTI. Kutu bunu yalnizca ASAGI soruyordu; parcada o
/// liste bos oldugu icin ise yaramaz bir satir cikiyor, etkilenen montajlar
/// hic yazmiyordu. Kullanici "karisiklik olur" derken tam bunu gordu.
/// Simdi etkilenenler ADLARIYLA yaziyor. BOS ve GUVENILIR DEGILSE hicbir
/// sayi yazilmaz, sebebi yazilir (CLAUDE.md 3: taranmamis kokte bos liste
/// "kimse kullanmiyor" DEMEK DEGILDIR).
///
/// OnayKutusu KULLANILMADI: o duz metin gosteriyor, burada isaretlenebilir
/// satirlar gerekiyor. Ozellik kendi dosyasinda (CLAUDE.md 1b): kaldirmak =
/// bu dosyayi sil + SurumeDonusu'ndeki bir cagriyi kes.
/// </summary>
internal static class DonusSecimKutusu
{
    /// <summary>Bir satirin yuksekligi (her iki liste de ayni).</summary>
    private const int SatirYuksekligi = 18;

    /// <summary>Listeler bundan uzunsa kaydirma cubugu cikar.</summary>
    private const int EnFazlaSatir = 6;

    /// <summary>
    /// Sorar. Doner: geri yazilacak COCUK yollari; vazgecilirse null.
    /// Bos liste GECERLIDIR: "yalniz asil dosyayi dondur" demektir.
    /// </summary>
    /// <param name="kullananlar">
    /// Bu dosyayi kullananlar (<see cref="ReferansIndeksi.Kullananlar"/>).
    /// null verilebilir (kok acik degil); guvenilirlik satiri kutuda yazar.
    /// </param>
    internal static IReadOnlyList<string>? Sor(
        IWin32Window sahip, string dosyaAdi, int no, IReadOnlyList<DonusOgesi> ogeler,
        KullanimSonucu? kullananlar)
    {
        ArgumentNullException.ThrowIfNull(ogeler);

        // CLAUDE.md 6: alanlar BOYUT DEGISTIREN her seyden once atanir.
        var bilgi = new Label { AutoSize = false };
        var etkiBasligi = new Label { AutoSize = false };
        var etkiListesi = new ListBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
            SelectionMode = SelectionMode.None,
            BackColor = Renkler.EtkiZemin,
        };
        var cocukBasligi = new Label { AutoSize = false };
        var liste = new CheckedListBox
        {
            CheckOnClick = true,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle,
        };
        var evet = new Button { Text = "Evet", DialogResult = DialogResult.OK, Width = 90 };
        var vazgec = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel, Width = 90 };

        bilgi.Text =
            $"\"{dosyaAdi}\" v{no} içeriğine dönecek.\n"
            + "Bugünkü hâl önce otomatik arşivlenir — hiçbir içerik kaybolmaz.";

        // ---- YUKARI: kimler etkilenecek.
        etkiBasligi.Text =
            "Bu dosyanın KENDİSİ değişiyor — onu kullanan her yer dönülen içeriği görür\n"
            + "(SOLIDWORKS'te açınca yeniden oluşturmak gerekebilir):";
        foreach (string satir in EtkiSatirlari(kullananlar))
        {
            etkiListesi.Items.Add(satir);
        }

        // ---- ASAGI: bu versiyonun kullandiklari.
        var satirlar = new List<DonusOgesi>();
        foreach (DonusOgesi oge in ogeler)
        {
            satirlar.Add(oge);

            string ad = WindowsYolu.DosyaAdi(oge.CanliYol);
            string etiket = oge.Engel is not null
                ? $"{ad}  —  {oge.Engel}"
                : oge.Farkli ? $"{ad}  —  bugünkü hâli FARKLI" : $"{ad}  —  değişmemiş";

            liste.Items.Add(etiket, oge.Engel is null && oge.Farkli);
        }

        // COCUK YOKSA O BLOK HIC CIZILMEZ. Once devre disi bir "Bu versiyonda
        // baska dosya yok." satiri cikiyordu; parcada her zaman oyleydi, yani
        // kutunun yarisi ise yaramaz bir satirdi (CLAUDE.md 6: kutu az ve
        // dogru olsun).
        bool cocukVar = satirlar.Count > 0;
        cocukBasligi.Text = "Bu versiyonun kullandığı dosyalardan hangileri de geri yazılsın?";

        int etkiYuksekligi = Yukseklik(etkiListesi.Items.Count);
        int cocukYuksekligi = cocukVar ? Yukseklik(satirlar.Count) : 0;

        int y = 12;
        using var pencere = new Form
        {
            Text = "Versiyona dön",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            Font = new Font("Segoe UI", 9f),
        };

        bilgi.SetBounds(14, y, 492, 36);
        y += 42;

        etkiBasligi.SetBounds(14, y, 492, 32);
        y += 34;
        etkiListesi.SetBounds(14, y, 492, etkiYuksekligi);
        y += etkiYuksekligi + 12;

        if (cocukVar)
        {
            cocukBasligi.SetBounds(14, y, 492, 18);
            y += 20;
            liste.SetBounds(14, y, 492, cocukYuksekligi);
            y += cocukYuksekligi + 12;
        }

        pencere.ClientSize = new Size(520, y + 40);
        evet.SetBounds(316, y + 4, 90, 28);
        vazgec.SetBounds(412, y + 4, 90, 28);

        pencere.Controls.Add(bilgi);
        pencere.Controls.Add(etkiBasligi);
        pencere.Controls.Add(etkiListesi);
        if (cocukVar)
        {
            pencere.Controls.Add(cocukBasligi);
            pencere.Controls.Add(liste);
        }

        pencere.Controls.Add(evet);
        pencere.Controls.Add(vazgec);
        pencere.AcceptButton = evet;
        pencere.CancelButton = vazgec;

        if (pencere.ShowDialog(sahip) != DialogResult.OK)
        {
            return null;
        }

        var secilen = new List<string>();
        foreach (int sira in liste.CheckedIndices)
        {
            // Engelli satir isaretlenmis olsa bile GECMEZ: cekirdek zaten
            // atlar ama burada da elemek, sebebi iki kez yazdirmiyor.
            if (sira < satirlar.Count && satirlar[sira].Engel is null)
            {
                secilen.Add(satirlar[sira].CanliYol);
            }
        }

        return secilen;
    }

    /// <summary>
    /// Etkilenenler listesinin satirlari - UC HAL, ucu de durust (CLAUDE.md 3).
    ///
    /// KURAL PANELDEKININ AYNISI (Erkan'in karari, 30.08.2026): LISTE DOLUYSA
    /// ADLAR KAZANIR, guvenilirlik satiri sonra gelir. Onceki halim tersini
    /// yapiyordu - guvenilir degilse adlari tumden gizliyordu - ve bu tam da
    /// sikayet edilen karisikligi uretirdi: arkadaki panel "KULLANILDIGI
    /// YERLER 2 dosya" derken kutu "BILINMIYOR" derdi.
    ///
    /// Bos VE guvenilir degilse ad YAZILMAZ: taranmamis kokte "hicbir sey
    /// etkilenmiyor" demek, kullaniciyi yanlis guvenle onaylatir.
    /// </summary>
    private static IReadOnlyList<string> EtkiSatirlari(KullanimSonucu? kullananlar)
    {
        if (kullananlar is null)
        {
            return ["Kimin kullandığı BİLİNMİYOR — önce bir klasör açın."];
        }

        var satirlar = new List<string>(kullananlar.Kullananlar.Count + 1);
        foreach (string yol in kullananlar.Kullananlar)
        {
            satirlar.Add(WindowsYolu.DosyaAdi(yol));
        }

        if (satirlar.Count > 0)
        {
            // Dolu listede eksiklik BIR SATIR: adlari bastirmiyor ama
            // "hepsi bu" sanisini da engelliyor.
            if (!kullananlar.Guvenilir)
            {
                satirlar.Add("(liste EKSİK olabilir — " + (kullananlar.Sebep ?? "tarama tam değil") + ")");
            }

            return satirlar;
        }

        return kullananlar.Guvenilir
            ? ["Bu dosyayı kullanan başka dosya yok."]
            :
            [
                "Kimin kullandığı BİLİNMİYOR — " + (kullananlar.Sebep ?? "tarama yapılmadı"),
                "Ctrl+Shift+R ile tarayın.",
            ];
    }

    private static int Yukseklik(int satir)
        => (Math.Clamp(satir, 1, EnFazlaSatir) * SatirYuksekligi) + 6;
}
