using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// KLASOR BOYUTU. Bilerek ISTEK UZERINE: bir klasorun boyutu ancak icindeki
/// her sey gezilerek bulunur ve ag surucusunde bu dakikalar surebilir. Her
/// klasor secilince kendiliginden hesaplamak uygulamayi kullanilmaz yapardi -
/// o yuzden agacta "(70)" gibi DOSYA SAYISI var ama boyut yok; boyut burada,
/// istendiginde.
/// </summary>
internal sealed class BoyutHesaplaIslemi : IAgacIslemi
{
    private static bool _kosuyor;

    /// <inheritdoc/>
    public string Ad => "Boyutu hesapla";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.B;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (_kosuyor)
        {
            nedenOlmaz = "Bir hesaplama zaten sürüyor.";
            return false;
        }

        foreach (object oge in secim.Ogeler)
        {
            if (oge is KlasorOgesi)
            {
                nedenOlmaz = string.Empty;
                return true;
            }
        }

        nedenOlmaz = "Önce bir klasör seçin — dosyaların boyutu zaten görünüyor.";
        return false;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        var klasorler = new List<KlasorOgesi>();
        foreach (object oge in baglam.Secim.Ogeler)
        {
            if (oge is KlasorOgesi klasor)
            {
                klasorler.Add(klasor);
            }
        }

        if (klasorler.Count == 0)
        {
            return;
        }

        _kosuyor = true;
        var iptal = new CancellationTokenSource();

        // Toplam BILINMIYOR: kac klasor gezilecegini gezmeden bilemeyiz.
        // CLAUDE.md 3 geregi uydurma yuzde YOK - cubuk klasor sayisiyla
        // ilerliyor, sayilan sey GERCEK.
        baglam.Ilerleme.Basladi(klasorler.Count, iptal);

        Task.Run(() => Hesapla(baglam, klasorler, iptal.Token))
            .ContinueWith(
                _ =>
                {
                    _kosuyor = false;
                    iptal.Dispose();
                },
                TaskScheduler.Default);
    }

    private static void Hesapla(
        IslemBaglami baglam, List<KlasorOgesi> klasorler, CancellationToken belirtec)
    {
        var satirlar = new List<string>(klasorler.Count);
        long toplam = 0;
        bool tam = true;

        for (int i = 0; i < klasorler.Count; i++)
        {
            KlasorOgesi klasor = klasorler[i];
            int sira = i;

            BoyutSonucu sonuc = KlasorBoyutu.Hesapla(
                klasor.Yol,
                belirtec,
                (gezilen, bayt) => baglam.Ilerleme.Adim(
                    sira, klasorler.Count,
                    $"{klasor.Ad} — {gezilen} klasör · {Boyut.Yaz(bayt)}"));

            satirlar.Add($"{klasor.Ad}\n    {sonuc.Yaz()}");
            toplam += sonuc.Bayt;
            tam &= sonuc.Tam;

            if (belirtec.IsCancellationRequested)
            {
                tam = false;
                break;
            }
        }

        baglam.Ilerleme.Adim(klasorler.Count, klasorler.Count, string.Empty);
        baglam.Ilerleme.Bitti(() => Goster(baglam, satirlar, toplam, tam, klasorler.Count));
    }

    private static void Goster(
        IslemBaglami baglam, List<string> satirlar, long toplam, bool tam, int adet)
    {
        var metin = new StringBuilder();
        foreach (string satir in satirlar)
        {
            metin.AppendLine(satir);
            metin.AppendLine();
        }

        if (adet > 1)
        {
            metin.AppendLine($"TOPLAM: {Boyut.Yaz(toplam)}");
        }

        if (!tam)
        {
            // CLAUDE.md 3: eksik bir sayiyi tam gibi gostermek, kullanicinin
            // ona gore karar vermesine yol acar.
            metin.AppendLine();
            metin.AppendLine("Bu sayı TAM DEĞİL — yukarıda sebebi yazıyor.");
        }

        // KUTU YALNIZCA SONUC EKSIKSE (28.08.2026). Tam sonuc zaten durum
        // cubugunda yaziyor; ayrica kutu cikarmak bilgiyi iki kez gostermek
        // olurdu. Eksik sonuc ise SESSIZ GECILEMEZ - yarim bir sayiyi tam
        // sanmak yanlis karar verdirir (CLAUDE.md 3).
        if (!tam)
        {
            MessageBox.Show(
                baglam.Sahip, metin.ToString(), "Klasör boyutu — EKSİK",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        baglam.Bildir(tam
            ? $"Boyut: {Boyut.Yaz(toplam)}"
            : $"Boyut: {Boyut.Yaz(toplam)} (tam değil)");
    }
}
