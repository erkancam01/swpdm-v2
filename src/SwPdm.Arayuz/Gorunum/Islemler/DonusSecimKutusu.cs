using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// "BU VERSIYONA DON" KUTUSU - hangi dosyalar geri yazilacak.
///
/// NEDEN LISTE (Erkan'in ilk versiyon isteginin 3. maddesi): montajin
/// versiyonu artik o gunku PARCALARI da tasiyor. Yalniz montaji geri yazmak
/// "eski versiyona dondum" sanisi yaratirdi, oysa parcalar bugunku halinde
/// kalirdi (CLAUDE.md 3). Karar kullanicinin: hangi parcanin geri yazilacagini
/// GORUP secer.
///
/// VARSAYILAN AKILLI: yalnizca BUGUNKUNDEN FARKLI olanlar isaretli gelir.
/// Ayni olani geri yazmak bos is ve bos risktir; degismeyene dokunmuyoruz.
/// Engelli satir (SOLIDWORKS'te acik, bugun yok) isaretlenemez ve SEBEBI
/// yaninda yazar - sessizce atlamak yerine gosteriyoruz.
///
/// OnayKutusu KULLANILMADI: o duz metin gosteriyor, burada isaretlenebilir
/// satirlar gerekiyor. Ozellik kendi dosyasinda (CLAUDE.md 1b): kaldirmak =
/// bu dosyayi sil + SurumeDonusu'ndeki bir cagriyi kes.
/// </summary>
internal static class DonusSecimKutusu
{
    /// <summary>
    /// Sorar. Doner: geri yazilacak COCUK yollari; vazgecilirse null.
    /// Bos liste GECERLIDIR: "yalniz asil dosyayi dondur" demektir.
    /// </summary>
    internal static IReadOnlyList<string>? Sor(
        IWin32Window sahip, string dosyaAdi, int no, IReadOnlyList<DonusOgesi> ogeler)
    {
        ArgumentNullException.ThrowIfNull(ogeler);

        // CLAUDE.md 6: alanlar BOYUT DEGISTIREN her seyden once atanir.
        var bilgi = new Label { AutoSize = false };
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
            + "Bugünkü hâl önce otomatik arşivlenir — hiçbir içerik kaybolmaz.\n\n"
            + "Bu versiyonun kullandığı dosyalardan hangileri de geri yazılsın?";

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

        if (satirlar.Count == 0)
        {
            liste.Items.Add("Bu versiyonda başka dosya yok.", false);
            liste.Enabled = false;
        }

        int yukseklik = Math.Min(satirlar.Count + 1, 8) * 18 + 8;

        using var pencere = new Form
        {
            Text = "Versiyona dön",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            Font = new Font("Segoe UI", 9f),
            ClientSize = new Size(520, 118 + yukseklik),
        };

        bilgi.SetBounds(14, 12, 492, 76);
        liste.SetBounds(14, 92, 492, yukseklik);
        evet.SetBounds(316, pencere.ClientSize.Height - 38, 90, 28);
        vazgec.SetBounds(412, pencere.ClientSize.Height - 38, 90, 28);

        pencere.Controls.Add(bilgi);
        pencere.Controls.Add(liste);
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
}
