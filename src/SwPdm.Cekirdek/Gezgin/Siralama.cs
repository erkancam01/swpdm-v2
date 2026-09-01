using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>Neye gore siralanacak.</summary>
public enum SiralamaOlcutu
{
    /// <summary>Ada gore - dogal sira (1, 2, 33, 222).</summary>
    Ad,

    /// <summary>Tur, sonra ad.</summary>
    Tur,

    /// <summary>Boyut. Klasorlerin boyutu BILINMIYOR; onlar ada gore kalir.</summary>
    Boyut,

    /// <summary>Degistirme tarihi.</summary>
    Tarih,
}

/// <summary>
/// SIRALAMANIN TEK KAPISI. Neye gore ve hangi yonde siralanacagi burada
/// (CLAUDE.md 1b).
///
/// KLASORLER HER ZAMAN ONCE: olcut ne olursa olsun. Gezgin de boyle yapar ve
/// sebebi somut - klasorler gezinme, dosyalar icerik; karistirmak agaci
/// okunmaz yapar.
/// </summary>
/// <param name="Olcut">Neye gore.</param>
/// <param name="Azalan">Buyukten kucuge / yeniden eskiye.</param>
public readonly record struct Siralama(SiralamaOlcutu Olcut, bool Azalan)
{
    /// <summary>Varsayilan: ada gore artan - Gezgin'in acilis sirasi.</summary>
    public static readonly Siralama Varsayilan = new(SiralamaOlcutu.Ad, Azalan: false);

    /// <summary>Ekranda gorunen ad.</summary>
    public string Adi => Olcut switch
    {
        SiralamaOlcutu.Tur => "Tür",
        SiralamaOlcutu.Boyut => "Boyut",
        SiralamaOlcutu.Tarih => "Tarih",
        _ => "Ad",
    };

    /// <summary>Metinden cozer; tanimadigi degerde VARSAYILANA doner.</summary>
    public static Siralama Coz(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin))
        {
            return Varsayilan;
        }

        string[] parcalar = metin.Split(',');
        if (!Enum.TryParse(parcalar[0], ignoreCase: true, out SiralamaOlcutu olcut))
        {
            return Varsayilan;
        }

        return new Siralama(olcut, parcalar.Length > 1 && parcalar[1] == "azalan");
    }

    /// <summary>Ayar dosyasina yazilacak bicim.</summary>
    public string Yaz() => $"{Olcut},{(Azalan ? "azalan" : "artan")}";

    /// <summary>Dosyalari bu sirayla dizer.</summary>
    public void Uygula(List<DosyaOgesi> dosyalar)
    {
        ArgumentNullException.ThrowIfNull(dosyalar);

        // struct icindeki lambda "this"e dokunamiyor; olcut ve yon yerele alindi.
        SiralamaOlcutu olcut = Olcut;
        bool azalan = Azalan;

        dosyalar.Sort((a, b) =>
        {
            int sonuc = olcut switch
            {
                SiralamaOlcutu.Boyut => a.Boyut.CompareTo(b.Boyut),
                SiralamaOlcutu.Tarih => a.Degistirme.CompareTo(b.Degistirme),
                SiralamaOlcutu.Tur => string.CompareOrdinal(
                    DosyaTurleri.Adi(a.Tur), DosyaTurleri.Adi(b.Tur)),
                _ => 0,
            };

            if (sonuc != 0)
            {
                return azalan ? -sonuc : sonuc;
            }

            // Esitlik ADLA cozuluyor: yoksa ayni boyuttaki dosyalarin sirasi
            // her taramada degisir ve agac gozunun onunde oynar.
            int ada = DogalKarsilastirici.Ortak.Compare(a.Ad, b.Ad);

            // OLCULDU (27.08.2026): burasi once kosulsuz "ada" donuyordu ve
            // "Ad" olcutunde sonuc HER ZAMAN 0 oldugu icin AD AZALAN HIC
            // CALISMIYORDU - dugmede "Ad ↓" yaziyor, agac artan duruyordu.
            // Belirti sessiz: hata yok, yalnizca yanlis sira.
            // Diger olcutlerde esitlik bozucu ARTAN kalir (kararli sira).
            return olcut == SiralamaOlcutu.Ad && azalan ? -ada : ada;
        });
    }

    /// <summary>
    /// Klasorleri dizer. Klasorun boyutu ve icerigi TARANMADAN bilinmiyor;
    /// o yuzden boyut/tur olcutlerinde klasorler ADA gore kalir - uydurma bir
    /// siraya sokmak yalan olurdu (CLAUDE.md 3).
    /// </summary>
    public void Uygula(List<KlasorOgesi> klasorler)
    {
        ArgumentNullException.ThrowIfNull(klasorler);

        bool tersine = Azalan && Olcut == SiralamaOlcutu.Ad;

        klasorler.Sort((a, b) =>
        {
            int sonuc = DogalKarsilastirici.Ortak.Compare(a.Ad, b.Ad);
            return tersine ? -sonuc : sonuc;
        });
    }
}
