using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// TASIMA - "Kes" ve "Yapistir" ayni ozelliktir, o yuzden AYNI DOSYADA
/// (CLAUDE.md 1b: ozellik dikey, tek yerde). Surukle-birak de buradaki
/// <see cref="Tasi.Yurut"/>'u cagirir; tasima karari tek yerde durur.
/// </summary>
internal static class Tasi
{
    /// <summary>Kesilmis ogelerin yollari. Uygulama icinde; Windows panosuna DOKUNMAZ.</summary>
    internal static readonly List<string> Pano = [];

    /// <summary>
    /// Ogeleri hedef klasore tasir ve NE OLDUGUNU dondurur.
    /// Kismi basarisizlikta duran ogeler tek tek sayilir (CLAUDE.md 3).
    /// </summary>
    internal static void Yurut(IslemBaglami baglam, IReadOnlyList<string> yollar, string hedefKlasor)
    {
        if (yollar.Count == 0)
        {
            return;
        }

        if (!Onayla(baglam.Sahip, yollar, hedefKlasor))
        {
            baglam.Bildir("Taşıma iptal edildi.");
            return;
        }

        var tasinan = new List<string>();
        var kalan = new List<string>();

        foreach (string yol in yollar)
        {
            IslemRaporu rapor = DosyaIslemleri.Tasi(yol, hedefKlasor);
            if (rapor.Oldu)
            {
                tasinan.Add(WindowsYolu.DosyaAdi(yol));
            }
            else
            {
                kalan.Add(WindowsYolu.DosyaAdi(yol) + " — " + (rapor.Sebep ?? "bilinmeyen sebep"));
            }
        }

        Pano.Clear();
        baglam.Tazele(null);

        if (kalan.Count > 0)
        {
            var metin = new StringBuilder();
            metin.AppendLine($"{tasinan.Count} öğe taşındı.");
            metin.AppendLine();
            metin.AppendLine($"{kalan.Count} öğe TAŞINMADI (yerinde duruyor):");
            foreach (string satir in kalan)
            {
                metin.AppendLine("  • " + satir);
            }

            MessageBox.Show(
                baglam.Sahip, metin.ToString(), "Bazı öğeler taşınamadı",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        baglam.Bildir(kalan.Count == 0
            ? $"{tasinan.Count} öğe taşındı."
            : $"{tasinan.Count} taşındı · {kalan.Count} taşınamadı");
    }

    private static bool Onayla(IWin32Window sahip, IReadOnlyList<string> yollar, string hedef)
    {
        var metin = new StringBuilder();
        metin.AppendLine(yollar.Count == 1
            ? $"\"{WindowsYolu.DosyaAdi(yollar[0])}\" taşınacak:"
            : $"{yollar.Count} öğe taşınacak:");
        metin.AppendLine();
        metin.AppendLine(hedef);
        metin.AppendLine();

        // CLAUDE.md 5'te OLCULDU - oldugu gibi soyleniyor, fazlasi da eksigi de degil.
        metin.AppendLine("Ölçüldü: bir klasör taşındığında içindeki montaj–parça");
        metin.AppendLine("bağları YAŞIYOR. Kırılan, DIŞARIDAN bu dosyalara verilen");
        metin.AppendLine("referanslardır; onları şu an ONARAMIYORUZ.");
        metin.AppendLine();
        metin.AppendLine("Teknik resim → model bağı için bu ölçüm HENÜZ YAPILMADI.");

        return MessageBox.Show(
            sahip, metin.ToString(), "Taşı",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK;
    }
}

/// <summary>Secili ogeleri tasinmak uzere isaretler.</summary>
internal sealed class KesIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Kes";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.X;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (secim.Ogeler.Count == 0)
        {
            nedenOlmaz = "Önce taşınacak öğeleri seçin.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        Tasi.Pano.Clear();
        foreach (object oge in baglam.Secim.Ogeler)
        {
            string? yol = SecimBaglami.Yolu(oge);
            if (yol is not null)
            {
                Tasi.Pano.Add(yol);
            }
        }

        baglam.Bildir($"{Tasi.Pano.Count} öğe kesildi — yapıştırılacak klasörü seçip Ctrl+V.");
    }
}

/// <summary>Kesilmis ogeleri etkin klasore tasir.</summary>
internal sealed class YapistirIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Yapıştır";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.V;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (Tasi.Pano.Count == 0)
        {
            nedenOlmaz = "Kesilmiş bir şey yok.";
            return false;
        }

        if (secim.AramaKipinde)
        {
            nedenOlmaz = "Arama sonucuna yapıştırılamaz — önce aramayı temizleyin.";
            return false;
        }

        if (secim.EtkinKlasor is null)
        {
            nedenOlmaz = "Hedef klasörü seçin.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        if (baglam.Secim.EtkinKlasor is string hedef)
        {
            Tasi.Yurut(baglam, [.. Tasi.Pano], hedef);
        }
    }
}
