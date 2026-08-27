using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// Coklu secimin ozeti. Cumleyi de kendisi yazar - hem onizleme paneli hem
/// durum cubugu ayni cumleyi kullaniyor, ikinci kopya yok (CLAUDE.md 8).
/// </summary>
/// <param name="DosyaSayisi">Secili dosya sayisi.</param>
/// <param name="KlasorSayisi">Secili klasor sayisi.</param>
/// <param name="ToplamBoyut">Secili DOSYALARIN toplam boyutu.</param>
/// <param name="BoyutTam">
/// Toplam boyut secimin TAMAMINI kapsiyor mu. Klasor secildiyse false olur:
/// klasorun ici taranmadi, boyutu BILINMIYOR. CLAUDE.md 3 - bilinmeyen bir
/// seyi toplama katmak, kullaniciya yanlis bir sayi gostermektir.
/// </param>
public sealed record SecimOzeti(int DosyaSayisi, int KlasorSayisi, long ToplamBoyut, bool BoyutTam)
{
    /// <summary>Secili oge sayisi.</summary>
    public int Toplam => DosyaSayisi + KlasorSayisi;

    /// <summary>Agac dugumlerine bagli cekirdek nesnelerinden ozet cikarir.</summary>
    public static SecimOzeti Hesapla(IEnumerable<object?> etiketler)
    {
        ArgumentNullException.ThrowIfNull(etiketler);

        int dosya = 0;
        int klasor = 0;
        long boyut = 0;

        foreach (object? etiket in etiketler)
        {
            switch (etiket)
            {
                case DosyaOgesi d:
                    dosya++;
                    boyut += d.Boyut;
                    break;

                case KlasorOgesi:
                    klasor++;
                    break;
            }
        }

        return new SecimOzeti(dosya, klasor, boyut, BoyutTam: klasor == 0);
    }

    /// <summary>Ekranda gosterilecek cumle.</summary>
    public string Yaz()
    {
        var parcalar = new List<string>(3);

        if (DosyaSayisi > 0)
        {
            parcalar.Add($"{DosyaSayisi} dosya");
        }

        if (KlasorSayisi > 0)
        {
            parcalar.Add($"{KlasorSayisi} klasör");
        }

        if (parcalar.Count == 0)
        {
            return "Seçim yok";
        }

        string cumle = string.Join(" · ", parcalar) + " seçildi";

        if (DosyaSayisi > 0)
        {
            // Klasor de secildiyse sayi TAMAM DEGIL ve bu SOYLENIYOR. Sessizce
            // eksik bir toplam gostermek, kullanicinin ona gore karar vermesine
            // yol acar (CLAUDE.md 3).
            cumle += BoyutTam
                ? "  ·  " + Boyut.Yaz(ToplamBoyut)
                : "  ·  dosyalar " + Boyut.Yaz(ToplamBoyut) + " (klasörlerin içi taranmadı)";
        }

        return cumle;
    }
}
