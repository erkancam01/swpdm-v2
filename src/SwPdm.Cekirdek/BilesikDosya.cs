using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>
/// OLE Bilesik Belge (Compound File Binary Format) okuyucusu - SALT OKUNUR.
///
/// SOLIDWORKS dosyalari (.SLDPRT/.SLDASM/.SLDDRW) bu bicimde; onizleme resmi
/// icerideki bir akista duruyor. SOLIDWORKS kurulu OLMADAN okuyabilmek icin
/// bicimi kendimiz cozuyoruz. Hedef net8.0: gercek bir bilesik dosyayla
/// Linux'ta TEST EDILEBILIYOR (CLAUDE.md 2 - once Kapi).
///
/// AKIS TABANLI, dosyayi bellege ALMAZ. Bir montaj dosyasi 100 MB'i gecebilir;
/// kucuk bir onizleme icin tamamini bellege almak kabul edilemez. Yalnizca
/// yerlesim tablolari (FAT, dizin, miniFAT ve mini kap) onden okunuyor -
/// hepsi kucuk - govde ise istendiginde diskten.
///
/// SALDIRGAN GIRDIYE DAYANIKLI olmak zorunda: bu dosyalar disaridan geliyor.
/// Her zincir sinirli, her sektor dogrulanıyor; bozuk dosya istisna atmaz,
/// null doner.
/// </summary>
public sealed class BilesikDosya : IDisposable
{
    private const uint SonZincir = 0xFFFFFFFE;
    private const uint EnBuyukNormalSektor = 0xFFFFFFFA;
    private const int DizinGirdiBoyu = 128;
    private const int EnFazlaZincirAdimi = 1_000_000;

    private static readonly byte[] Imza = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private readonly Stream _akis;
    private readonly bool _akisiBizActik;
    private readonly int _sektorBoyu;
    private readonly int _miniSektorBoyu;
    private readonly uint _miniEsik;
    private readonly uint[] _fat;
    private readonly uint[] _miniFat;
    private readonly byte[] _miniKap;
    private readonly Dictionary<string, (uint Baslangic, ulong Boyut)> _akislar;

    private BilesikDosya(Stream akis, bool akisiBizActik, int sektorBoyu, int miniSektorBoyu,
                         uint miniEsik, uint[] fat, uint[] miniFat, byte[] miniKap,
                         Dictionary<string, (uint, ulong)> akislar)
    {
        _akis = akis;
        _akisiBizActik = akisiBizActik;
        _sektorBoyu = sektorBoyu;
        _miniSektorBoyu = miniSektorBoyu;
        _miniEsik = miniEsik;
        _fat = fat;
        _miniFat = miniFat;
        _miniKap = miniKap;
        _akislar = akislar;
    }

    /// <summary>Icindeki akislarin adlari.</summary>
    public IReadOnlyCollection<string> AkisAdlari => _akislar.Keys;

    /// <summary>Baytlar bir bilesik belge imzasiyla basliyor mu.</summary>
    public static bool ImzaUyuyorMu(ReadOnlySpan<byte> bas)
        => bas.Length >= Imza.Length && bas[..Imza.Length].SequenceEqual(Imza);

    /// <summary>Dosyayi acar. Bilesik belge degilse ya da bozuksa null doner.</summary>
    public static BilesikDosya? Ac(string yol)
    {
        FileStream akis;
        try
        {
            akis = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                         or ArgumentException or NotSupportedException)
        {
            return null;
        }

        BilesikDosya? sonuc = Ac(akis, akisiBizActik: true);
        if (sonuc is null)
        {
            akis.Dispose();
        }

        return sonuc;
    }

    /// <summary>Verilen akistan okur. Akis SAHIPLENILMEZ.</summary>
    public static BilesikDosya? Ac(Stream akis) => Ac(akis, akisiBizActik: false);

    /// <summary>Adi verilen akisin baytlari; yoksa null.</summary>
    public byte[]? AkisiOku(string ad)
    {
        if (!_akislar.TryGetValue(ad, out (uint Baslangic, ulong Boyut) akis))
        {
            return null;
        }

        return akis.Boyut < _miniEsik
            ? MiniSektorleriTopla(akis.Baslangic, akis.Boyut)
            : SektorleriTopla(akis.Baslangic, akis.Boyut);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_akisiBizActik)
        {
            _akis.Dispose();
        }
    }

    private static BilesikDosya? Ac(Stream akis, bool akisiBizActik)
    {
        try
        {
            if (!akis.CanRead || !akis.CanSeek || akis.Length < 512)
            {
                return null;
            }

            var basl = new byte[512];
            akis.Position = 0;
            akis.ReadExactly(basl);
            if (!ImzaUyuyorMu(basl))
            {
                return null;
            }

            return Coz(akis, akisiBizActik, basl);
        }
        catch (Exception hata) when (hata is IOException or EndOfStreamException
                                         or IndexOutOfRangeException
                                         or ArgumentOutOfRangeException
                                         or OverflowException
                                         or NotSupportedException
                                         or ObjectDisposedException)
        {
            return null;   // bozuk ya da okunamayan dosya: istisna sizdirmiyoruz
        }
    }

    private static BilesikDosya Coz(Stream akis, bool akisiBizActik, byte[] basl)
    {
        int sektorBoyu = 1 << Oku16(basl, 0x1E);
        int miniSektorBoyu = 1 << Oku16(basl, 0x20);
        if (sektorBoyu is not (512 or 4096) || miniSektorBoyu != 64)
        {
            throw new ArgumentOutOfRangeException(nameof(basl), "Desteklenmeyen sektör boyu.");
        }

        uint fatSektorSayisi = Oku32(basl, 0x2C);
        uint ilkDizin = Oku32(basl, 0x30);
        uint miniEsik = Oku32(basl, 0x38);
        uint ilkMiniFat = Oku32(basl, 0x3C);
        uint miniFatSayisi = Oku32(basl, 0x40);
        uint ilkDifat = Oku32(basl, 0x44);
        uint difatSayisi = Oku32(basl, 0x48);

        int girdiSayisi = sektorBoyu / 4;

        // --- DIFAT: FAT sektorlerinin numaralari
        var fatSektorleri = new List<uint>();
        for (int i = 0; i < 109 && fatSektorleri.Count < fatSektorSayisi; i++)
        {
            uint s = Oku32(basl, 0x4C + (i * 4));
            if (s <= EnBuyukNormalSektor)
            {
                fatSektorleri.Add(s);
            }
        }

        uint difat = ilkDifat;
        int difatAdimi = 0;
        while (difat <= EnBuyukNormalSektor && difatAdimi++ <= difatSayisi
               && fatSektorleri.Count < fatSektorSayisi)
        {
            byte[] d = SektoruOku(akis, difat, sektorBoyu);
            for (int i = 0; i < girdiSayisi - 1 && fatSektorleri.Count < fatSektorSayisi; i++)
            {
                uint s = Oku32(d, i * 4);
                if (s <= EnBuyukNormalSektor)
                {
                    fatSektorleri.Add(s);
                }
            }

            difat = Oku32(d, (girdiSayisi - 1) * 4);
        }

        // --- FAT
        var fat = new uint[fatSektorleri.Count * girdiSayisi];
        for (int s = 0; s < fatSektorleri.Count; s++)
        {
            byte[] sektor = SektoruOku(akis, fatSektorleri[s], sektorBoyu);
            for (int i = 0; i < girdiSayisi; i++)
            {
                fat[(s * girdiSayisi) + i] = Oku32(sektor, i * 4);
            }
        }

        // --- mini FAT
        var miniFat = new List<uint>();
        foreach (uint s in Zincir(fat, ilkMiniFat, (int)Math.Min(miniFatSayisi + 1, EnFazlaZincirAdimi)))
        {
            byte[] sektor = SektoruOku(akis, s, sektorBoyu);
            for (int i = 0; i < girdiSayisi; i++)
            {
                miniFat.Add(Oku32(sektor, i * 4));
            }
        }

        // --- dizin
        var akislar = new Dictionary<string, (uint, ulong)>(StringComparer.OrdinalIgnoreCase);
        uint miniKapBaslangic = SonZincir;
        ulong miniKapBoyut = 0;

        foreach (uint s in Zincir(fat, ilkDizin, EnFazlaZincirAdimi))
        {
            byte[] sektor = SektoruOku(akis, s, sektorBoyu);
            for (int g = 0; g + DizinGirdiBoyu <= sektorBoyu; g += DizinGirdiBoyu)
            {
                byte tur = sektor[g + 0x42];
                if (tur is not (2 or 5))
                {
                    continue;   // 2 = akis, 5 = kok
                }

                int adUzunlugu = Oku16(sektor, g + 0x40);
                if (adUzunlugu is < 2 or > 64)
                {
                    continue;
                }

                string ad = System.Text.Encoding.Unicode.GetString(sektor, g, adUzunlugu - 2);
                uint baslangic = Oku32(sektor, g + 0x74);
                ulong boyut = Oku64(sektor, g + 0x78);

                if (tur == 5)
                {
                    miniKapBaslangic = baslangic;
                    miniKapBoyut = boyut;
                }
                else
                {
                    akislar[ad] = (baslangic, boyut);
                }
            }
        }

        var gecici = new BilesikDosya(akis, akisiBizActik, sektorBoyu, miniSektorBoyu,
                                      miniEsik, fat, [.. miniFat], [], akislar);

        byte[] miniKap = miniKapBaslangic <= EnBuyukNormalSektor
            ? gecici.SektorleriTopla(miniKapBaslangic, miniKapBoyut)
            : [];

        return new BilesikDosya(akis, akisiBizActik, sektorBoyu, miniSektorBoyu,
                                miniEsik, fat, [.. miniFat], miniKap, akislar);
    }

    private byte[] SektorleriTopla(uint baslangic, ulong boyut)
    {
        var hedef = new byte[(int)Math.Min(boyut, int.MaxValue)];
        int yazilan = 0;

        foreach (uint s in Zincir(_fat, baslangic, EnFazlaZincirAdimi))
        {
            if (yazilan >= hedef.Length)
            {
                break;
            }

            long konum = ((long)s + 1) * _sektorBoyu;
            if (konum + 1 > _akis.Length)
            {
                break;   // dosya kirpilmis
            }

            int adet = (int)Math.Min(Math.Min(_sektorBoyu, hedef.Length - yazilan), _akis.Length - konum);
            _akis.Position = konum;
            _akis.ReadExactly(hedef, yazilan, adet);
            yazilan += adet;
        }

        return yazilan == hedef.Length ? hedef : hedef[..yazilan];
    }

    private byte[] MiniSektorleriTopla(uint baslangic, ulong boyut)
    {
        var hedef = new byte[(int)Math.Min(boyut, int.MaxValue)];
        int yazilan = 0;
        uint s = baslangic;
        int adim = 0;

        while (s <= EnBuyukNormalSektor && yazilan < hedef.Length && adim++ < EnFazlaZincirAdimi)
        {
            int taban = (int)s * _miniSektorBoyu;
            int adet = Math.Min(_miniSektorBoyu, hedef.Length - yazilan);
            if (taban + adet > _miniKap.Length)
            {
                break;
            }

            Array.Copy(_miniKap, taban, hedef, yazilan, adet);
            yazilan += adet;
            s = s < _miniFat.Length ? _miniFat[s] : SonZincir;
        }

        return yazilan == hedef.Length ? hedef : hedef[..yazilan];
    }

    private static IEnumerable<uint> Zincir(uint[] fat, uint baslangic, int enFazla)
    {
        uint s = baslangic;
        int adim = 0;
        while (s <= EnBuyukNormalSektor && adim++ < enFazla)
        {
            yield return s;
            s = s < fat.Length ? fat[s] : SonZincir;
        }
    }

    private static byte[] SektoruOku(Stream akis, uint sektor, int sektorBoyu)
    {
        var tampon = new byte[sektorBoyu];
        long konum = ((long)sektor + 1) * sektorBoyu;
        if (konum + sektorBoyu > akis.Length)
        {
            return tampon;   // kirpilmis dosya: sifir dolu sektor
        }

        akis.Position = konum;
        akis.ReadExactly(tampon);
        return tampon;
    }

    private static ushort Oku16(byte[] v, int k) => BinaryPrimitives.ReadUInt16LittleEndian(v.AsSpan(k, 2));

    private static uint Oku32(byte[] v, int k) => BinaryPrimitives.ReadUInt32LittleEndian(v.AsSpan(k, 4));

    private static ulong Oku64(byte[] v, int k) => BinaryPrimitives.ReadUInt64LittleEndian(v.AsSpan(k, 8));
}
