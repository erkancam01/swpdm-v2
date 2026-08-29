using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// ONAY KUTUSU - "Evet" ve "Vazgeç" (Erkan, 28.08.2026).
///
/// NEDEN KENDI KUTUMUZ: MessageBox'in dugme yazilari Windows'tan geliyor;
/// YesNo "Evet/Hayır", OKCancel "Tamam/İptal" veriyor. Istenen "Evet/Vazgeç"
/// ikisinde de yok.
///
/// NEDEN TEK YER (CLAUDE.md 1b): butun onaylar buradan geciyor. Yoksa her
/// islem kendi kutusunu kurar ve dugme yazilari, varsayilan dugme, genislik
/// islemden isleme ayrisir - v1'de boyut bicimlendirmesi tam boyle uc ayri
/// sonuc gosteriyordu (CLAUDE.md 8).
///
/// KURAL: kutu yalnizca ONAY ve HATA icin cikar. Bilgi (kac dosya, kac MB)
/// durum cubuguna yazilir.
/// </summary>
internal static class OnayKutusu
{
    /// <summary>Kutunun en fazla genisligi; uzun yollar tasmasin.</summary>
    private const int EnFazlaGenislik = 520;

    /// <summary>
    /// "Bunlardan N tanesi SOLIDWORKS'te acik gorunuyor" satiri; acik yoksa
    /// bos metin.
    ///
    /// NEDEN VAR (29.08.2026): Kilit.AcikMi'nin kendi belgesi "dosyaya yazan
    /// her yol once bunu sormali" diyordu, ama sil/tasi/kopyala HIC
    /// sormuyordu. Acik bir dosyayi tasimayi Windows engelliyor ve kullanici
    /// sebebi ANCAK islem yarim kaldiktan sonra goruyordu. Uyari ONDEN
    /// veriliyor - ama ENGELLEMIYOR: kilit, SOLIDWORKS temiz kapanmadiysa
    /// kalinti da olabilir (CLAUDE.md 5).
    ///
    /// BURADA, cunku ayni satiri iki onay kutusu da kullaniyor (CLAUDE.md 8).
    /// </summary>
    internal static string AcikUyarisi(IReadOnlyList<string> yollar, string fiil)
    {
        ArgumentNullException.ThrowIfNull(yollar);

        var acikOlanlar = new List<string>();
        foreach (string yol in yollar)
        {
            if (Kilit.AcikMi(yol))
            {
                acikOlanlar.Add(WindowsYolu.DosyaAdi(yol));
            }
        }

        if (acikOlanlar.Count == 0)
        {
            return string.Empty;
        }

        // Madde listesi ve kirpma TEK yerden (MaddeKutusu, CLAUDE.md 8).
        return MaddeKutusu.Metin(
                $"DİKKAT: {acikOlanlar.Count} öğe SOLIDWORKS'te AÇIK görünüyor "
                + "(yanında \"~$\" kilit dosyası var):", acikOlanlar)
            + $"\nAçıkken {fiil} Windows tarafından engellenebilir.";
    }

    /// <summary>
    /// Onay sorar. Doner: kullanici "Evet" dedi mi.
    ///
    /// <paramref name="tehlikeli"/> true ise varsayilan dugme VAZGEC olur -
    /// geri alinamaz islemlerde elin kaymasi dosya kaybettirir (CLAUDE.md 1a).
    /// </summary>
    internal static bool Sor(IWin32Window sahip, string baslik, string metin, bool tehlikeli = false)
    {
        // CLAUDE.md 6: alanlar boyut degistiren her seyden ONCE atanir.
        var yazi = new Label { AutoSize = true, MaximumSize = new Size(EnFazlaGenislik, 0) };
        var evet = new Button { Text = "Evet", DialogResult = DialogResult.OK, Width = 90 };
        var vazgec = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel, Width = 90 };

        yazi.Text = metin;

        using var pencere = new Form
        {
            Text = baslik,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            Font = new Font("Segoe UI", 9f),
        };

        pencere.Controls.Add(yazi);
        yazi.SetBounds(14, 14, 0, 0);

        // Pencere YAZIYA gore olculuyor: sabit bir yukseklik uzun listede
        // metni kirpardi ve kullanici neyi onayladigini goremezdi.
        int genislik = Math.Max(yazi.PreferredWidth + 28, 300);
        int yukseklik = yazi.PreferredHeight + 78;
        pencere.ClientSize = new Size(genislik, yukseklik);

        evet.SetBounds(genislik - 196, yukseklik - 40, 90, 28);
        vazgec.SetBounds(genislik - 100, yukseklik - 40, 90, 28);
        pencere.Controls.Add(evet);
        pencere.Controls.Add(vazgec);

        pencere.AcceptButton = evet;
        pencere.CancelButton = vazgec;
        if (tehlikeli)
        {
            pencere.ActiveControl = vazgec;
        }

        return pencere.ShowDialog(sahip) == DialogResult.OK;
    }
}
