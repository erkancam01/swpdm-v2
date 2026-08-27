using System;
using System.IO;
using System.Threading;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>Klasor boyutu GERCEK klasorlerle olculuyor.</summary>
public class KlasorBoyutuTestleri : IDisposable
{
    private readonly string _kok;

    public KlasorBoyutuTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-boyut-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_kok);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_kok, recursive: true);
        }
        catch (IOException)
        {
            // Temizlik sonucu degistirmez.
        }

        GC.SuppressFinalize(this);
    }

    private string Yol(params string[] p)
    {
        string yol = _kok;
        foreach (string parca in p)
        {
            yol = WindowsYolu.Birlestir(yol, parca);
        }

        return yol;
    }

    [Fact]
    public void Hesapla_ALT_KLASORLERI_DE_TOPLAR()
    {
        Directory.CreateDirectory(Yol("alt", "derin"));
        File.WriteAllBytes(Yol("a.bin"), new byte[100]);
        File.WriteAllBytes(Yol("alt", "b.bin"), new byte[250]);
        File.WriteAllBytes(Yol("alt", "derin", "c.bin"), new byte[650]);

        BoyutSonucu sonuc = KlasorBoyutu.Hesapla(_kok);

        Assert.Equal(1000, sonuc.Bayt);
        Assert.Equal(3, sonuc.DosyaSayisi);
        // Klasorun KENDISI sayilmaz: kullanici "buyuk klasorunde kac klasor
        // var" diye soruyor, "buyuk dahil" demiyor.
        Assert.Equal(2, sonuc.KlasorSayisi);   // alt + derin
        Assert.True(sonuc.Tam);
    }

    [Fact]
    public void Hesapla_BOS_KLASOR_SIFIR()
    {
        BoyutSonucu sonuc = KlasorBoyutu.Hesapla(_kok);

        Assert.Equal(0, sonuc.Bayt);
        Assert.Equal(0, sonuc.DosyaSayisi);
        Assert.Equal(0, sonuc.KlasorSayisi);
        Assert.True(sonuc.Tam);
    }

    [Fact]
    public void Hesapla_COP_KLASORUNU_SAYMAZ()
    {
        // "Bu klasor kac GB" diye soran kullanici sildiklerini kastetmiyor.
        Directory.CreateDirectory(Yol(Cop.KlasorAdi, "1"));
        File.WriteAllBytes(Yol(Cop.KlasorAdi, "1", "silinmis.bin"), new byte[9999]);
        File.WriteAllBytes(Yol("gercek.bin"), new byte[42]);

        BoyutSonucu sonuc = KlasorBoyutu.Hesapla(_kok);

        Assert.Equal(42, sonuc.Bayt);
        Assert.Equal(1, sonuc.DosyaSayisi);
    }

    [Fact]
    public void Hesapla_IPTAL_EDILINCE_SONUCUN_YARIM_OLDUGUNU_SOYLER()
    {
        File.WriteAllBytes(Yol("a.bin"), new byte[10]);

        using var kaynak = new CancellationTokenSource();
        kaynak.Cancel();

        BoyutSonucu sonuc = KlasorBoyutu.Hesapla(_kok, kaynak.Token);

        Assert.True(sonuc.Iptal);
        Assert.False(sonuc.Tam);
        Assert.Contains("YARIM", sonuc.Yaz(), StringComparison.Ordinal);
    }

    [Fact]
    public void Hesapla_OLMAYAN_KLASOR_OKUNAMAYAN_SAYILIR()
    {
        BoyutSonucu sonuc = KlasorBoyutu.Hesapla(Yol("hic-yok"));

        // Sessizce "0 bayt" demek YALAN olurdu: klasor bos degil, OKUNAMADI.
        Assert.Single(sonuc.OkunamayanKlasorler);
        Assert.False(sonuc.Tam);
        Assert.Contains("EKSİK", sonuc.Yaz(), StringComparison.Ordinal);
    }

    [Fact]
    public void Hesapla_ILERLEME_BILDIRIR()
    {
        Directory.CreateDirectory(Yol("a"));
        Directory.CreateDirectory(Yol("b"));

        int cagri = 0;
        KlasorBoyutu.Hesapla(_kok, default, (_, _) => cagri++);

        Assert.Equal(3, cagri);   // kok + a + b
    }
}
