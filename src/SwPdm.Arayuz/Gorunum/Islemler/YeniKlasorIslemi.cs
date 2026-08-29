using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// YENI KLASOR. Bu islemin butun karari burada: nereye acilir, adi
/// nasil sorulur, hata olursa ne yazar (CLAUDE.md 1b). Ad kutusunun kendisi
/// ortak arac (Islemler/AdKutusu.cs) - ad degistirme de onu kullaniyor.
/// </summary>
internal sealed class YeniKlasorIslemi : IAgacIslemi
{
    private const string VarsayilanAd = "Yeni klasör";

    /// <inheritdoc/>
    public string Ad => "Yeni klasör";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.N;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (secim.AramaKipinde)
        {
            // Arama sonucu duz bir listedir; "burasi" diye bir yer yok.
            nedenOlmaz = "Arama sonucunda klasör açılamaz — önce aramayı temizleyin.";
            return false;
        }

        if (secim.EtkinKlasor is null)
        {
            nedenOlmaz = "Önce bir klasör açın.";
            return false;
        }

        nedenOlmaz = string.Empty;
        return true;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        string? ust = baglam.Secim.EtkinKlasor;
        if (ust is null)
        {
            return;
        }

        // ADI SORULUYOR (Erkan, 29.08.2026): burasi eskiden hic sormadan
        // "Yeni klasör" adinda bir klasor aciyordu; kullanici adini vermek
        // icin ayrica F2'ye basmak zorundaydi. Kutu, cakismayan bir ad ile
        // DOLU geliyor - yani "Enter"a basmak eski davranisin aynisi.
        string? onerilen = DosyaIslemleri.BosAdBul(ust, VarsayilanAd);
        if (onerilen is null)
        {
            baglam.Bildir("Bu klasörde boş bir ad bulunamadı.");
            return;
        }

        if (AdKutusu.Sor(baglam.Sahip, "Yeni klasör", onerilen, ust, uzantiliMi: false)
            is not string ad)
        {
            baglam.Bildir("Yeni klasör açılmadı.");
            return;
        }

        IslemRaporu rapor = DosyaIslemleri.KlasorOlustur(ust, ad);

        if (!rapor.Oldu)
        {
            // CLAUDE.md 3: sebep EKRANDA, yalnizca gunlukte degil.
            MessageBox.Show(
                baglam.Sahip,
                rapor.Sebebi,
                "Klasör açılamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            baglam.Bildir("Klasör açılamadı — " + ad);
            return;
        }

        if (rapor.YeniYol is string yeniYol)
        {
            GeriAlDefteri.Kaydet(GeriAlmasi(yeniYol, ad));
        }

        baglam.Tazele(rapor.YeniYol);
        baglam.Bildir("Klasör açıldı: " + ad);
    }

    /// <summary>
    /// Geri alma: klasoru siler - ama YALNIZCA hala bossa. Icine bir sey
    /// konduysa silmek kullanicinin isini yok etmek olurdu (CLAUDE.md 1a).
    /// </summary>
    private static GeriAlinabilir GeriAlmasi(string yol, string ad)
        => new(
            $"\"{ad}\" klasörünün açılması",
            Ters: () => YenidenAcmasi(yol, ad),
            Uygula: baglam =>
            {
                // Diske giden her yol TEK kapidan (DosyaIslemleri) gecer -
                // burasi eskiden Directory.Delete'i kendisi cagiriyordu ve
                // "bossa sil" karari cekirdegin disinda yasiyordu (CLAUDE.md 8).
                var olmayan = new List<string>();
                IslemRaporu rapor = DosyaIslemleri.BosKlasoruSil(yol);
                if (!rapor.Oldu)
                {
                    olmayan.Add(ad + " — " + rapor.Sebebi);
                }

                return olmayan;
            });

    /// <summary>
    /// ILERI ALMA: silinen klasoru yeniden acar. Tersi yine silme, yani
    /// kullanici Ctrl+Z ve Ctrl+Y arasinda istedigi kadar gidip gelebilir.
    /// </summary>
    private static GeriAlinabilir YenidenAcmasi(string yol, string ad)
        => new(
            $"\"{ad}\" klasörünün açılması",
            Ters: () => GeriAlmasi(yol, ad),
            Uygula: baglam =>
            {
                var olmayan = new List<string>();
                IslemRaporu rapor = DosyaIslemleri.KlasorOlustur(
                    WindowsYolu.Klasor(yol), ad);

                if (!rapor.Oldu)
                {
                    olmayan.Add(ad + " — " + rapor.Sebebi);
                }

                return olmayan;
            });
}
