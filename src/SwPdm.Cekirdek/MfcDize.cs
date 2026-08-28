using System;
using System.Text;

namespace SwPdm.Cekirdek;

/// <summary>Tamponda bulunan bir MFC dizesi.</summary>
/// <param name="Baslangic">Dizenin ilk bayti (0xFF isaretinin yeri).</param>
/// <param name="VeriYeri">UTF-16LE karakterlerin baslangici.</param>
/// <param name="KarakterSayisi">Karakter sayisi (bayt degil).</param>
/// <param name="Deger">Cozulmus metin.</param>
public readonly record struct MfcBulgu(
    int Baslangic, int VeriYeri, int KarakterSayisi, string Deger)
{
    /// <summary>Dizenin toplam bayt uzunlugu (isaret + uzunluk + veri).</summary>
    public int ToplamBayt => 4 + (KarakterSayisi * 2);
}

/// <summary>
/// MFC UNICODE CString - SOLIDWORKS dosyalarindaki dizelerin bicimi.
///
///   FF FE FF  &lt;uzunluk: 1 bayt&gt;  &lt;uzunluk adet UTF-16LE karakter&gt;
///
/// Yedi gercek dosyadaki 52 yolun 52'sinde tuttu (CLAUDE.md 5). SOLIDWORKS
/// bir MFC uygulamasi; okudugumuz sey onlarin kendi serilestirme bicimi.
///
/// NEDEN AYRI DOSYA: bu bicimi hem OKUYAN (<see cref="SwReferans"/>) hem
/// YAZAN (<see cref="SwYazici"/>) var. Iki kopya olsaydi biri gunun birinde
/// otekinden ayrisir ve hata SESSIZ olurdu - yanlis yol yazilir, hicbir sey
/// patlamaz (CLAUDE.md 8).
///
/// OLCULMEDI - UZUN DIZE: uzunluk oneki 1 bayt, yani en fazla 254 karakter.
/// MFC daha uzunlari kacisla yaziyor; o bicim GORULMEDI. Kacis baytini
/// goren kod o dizeyi ATLAR ve sonucu EKSIK isaretler; yazici ise boyle bir
/// dize URETMEYI REDDEDER (CLAUDE.md 3: bilmedigimiz bicimde yazmak dosya
/// bozar).
/// </summary>
public static class MfcDize
{
    /// <summary>Dizenin basindaki isaret.</summary>
    public static readonly byte[] Isaret = [0xFF, 0xFE, 0xFF];

    /// <summary>Uzunluk oneki bu ise MFC kacis kullanmistir; bicimi olculmedi.</summary>
    public const byte KacisOneki = 0xFF;

    /// <summary>Bir dizenin tasiyabilecegi en fazla karakter.</summary>
    public const int EnFazlaKarakter = 254;

    /// <summary>
    /// Tampondaki dizeleri SIRAYLA verir. Doner: kacis onekli (okunamayan)
    /// bir dize goruldu mu.
    /// </summary>
    public static bool Tara(byte[]? tampon, Action<MfcBulgu> her)
    {
        ArgumentNullException.ThrowIfNull(her);
        if (tampon is null)
        {
            return false;
        }

        bool kacis = false;

        for (int i = 0; i + 4 < tampon.Length; i++)
        {
            if (tampon[i] != Isaret[0] || tampon[i + 1] != Isaret[1] || tampon[i + 2] != Isaret[2])
            {
                continue;
            }

            byte uzunluk = tampon[i + 3];
            if (uzunluk == KacisOneki)
            {
                kacis = true;
                continue;
            }

            if (uzunluk == 0)
            {
                continue;
            }

            int bas = i + 4;
            int bayt = uzunluk * 2;
            if (bas + bayt > tampon.Length)
            {
                continue;
            }

            string deger = Encoding.Unicode.GetString(tampon, bas, bayt);
            if (YazdirilabilirMi(deger))
            {
                her(new MfcBulgu(i, bas, uzunluk, deger));
            }

            i = bas + bayt - 1;
        }

        return kacis;
    }

    /// <summary>
    /// Bir dizeyi MFC bicimine cevirir. 254 karakteri asarsa null -
    /// kacis bicimi OLCULMEDI, tahminle yazmak dosya bozar.
    /// </summary>
    public static byte[]? Yaz(string? deger)
    {
        if (deger is null || deger.Length == 0 || deger.Length > EnFazlaKarakter)
        {
            return null;
        }

        byte[] metin = Encoding.Unicode.GetBytes(deger);
        var sonuc = new byte[4 + metin.Length];
        sonuc[0] = Isaret[0];
        sonuc[1] = Isaret[1];
        sonuc[2] = Isaret[2];
        sonuc[3] = (byte)deger.Length;
        metin.CopyTo(sonuc, 4);
        return sonuc;
    }

    private static bool YazdirilabilirMi(string dize)
    {
        foreach (char c in dize)
        {
            if (c < ' ' && c != '\t')
            {
                return false;
            }
        }

        return true;
    }
}
