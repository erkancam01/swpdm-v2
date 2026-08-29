using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// 29.08.2026'da kapatilan kucuk bosluklar. Hepsi GERCEK dosyalarla kosuyor.
///
/// NEDEN AYRI DOSYA: bunlar tek bir sinifin degil, bir TURUN testleri -
/// "ekranda yalan soyleyen ya da veri kaybettiren kucuk hata". Bir gun bu
/// davranislardan biri bilerek degistirilirse, hangi testin neden kirildigi
/// buradan okunur.
/// </summary>
public class BosluklarTestleri : IDisposable
{
    private readonly string _kok;

    public BosluklarTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-bosluk-" + Guid.NewGuid().ToString("N")[..8]);
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
            // Temizlik tutmazsa testin sonucunu degistirmez.
        }

        GC.SuppressFinalize(this);
    }

    // ---------- ad uzunlugu ----------

    [Fact]
    public void COK_UZUN_AD_REDDEDILIR()
    {
        Assert.False(WindowsYolu.AdGecerliMi(new string('a', 256), out string sebep));
        Assert.Contains("uzun", sebep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TAM_255_KARAKTER_KABUL_EDILIR()
        => Assert.True(WindowsYolu.AdGecerliMi(new string('a', 255), out _));

    [Fact]
    public void AD_GECERLI_OLSA_DA_TAM_YOL_UZUNSA_REDDEDILIR()
    {
        // Ad tek basina gecerli (200 karakter), ama derin bir klasorde tam
        // yol 259'u asiyor. Eskiden bu hic olculmuyordu ve hata ancak diske
        // yazarken, .NET'in ham mesajiyla cikiyordu.
        string derin = @"C:\" + new string('k', 100) + @"\" + new string('m', 100);
        string ad = new('a', 200);

        Assert.True(WindowsYolu.AdGecerliMi(ad, out _));
        Assert.False(WindowsYolu.YolGecerliMi(derin, ad, out string sebep));
        Assert.Contains("259", sebep, StringComparison.Ordinal);
    }

    [Fact]
    public void BOSLUKLA_BASLAYAN_AD_REDDEDILIR()
        => Assert.False(WindowsYolu.AdGecerliMi(" Parça1.SLDPRT", out _));

    [Fact]
    public void AYRILMIS_AD_SONDAKI_BOSLUKLA_DA_YAKALANIR()
    {
        // "CON .SLDPRT": govde "CON " oldugu icin kirpmayan bir denetim
        // bunu KACIRIYORDU; Windows sondaki boslugu atip aygit adi sayar.
        Assert.False(WindowsYolu.AdGecerliMi("CON .SLDPRT", out _));
        Assert.False(WindowsYolu.AdGecerliMi("CON.SLDPRT", out _));
        Assert.True(WindowsYolu.AdGecerliMi("CONTROL.SLDPRT", out _));
    }

    // ---------- bos ad tukenmesi ----------

    [Fact]
    public void BOS_AD_BULUNAMAZSA_NULL_DONER()
    {
        string klasor = Path.Combine(_kok, "dolu");
        Directory.CreateDirectory(klasor);

        // Istenen ad ve (2)..(1000) hepsi dolu.
        File.WriteAllText(Path.Combine(klasor, "a.txt"), "x");
        for (int sira = 2; sira <= 1000; sira++)
        {
            File.WriteAllText(Path.Combine(klasor, $"a ({sira}).txt"), "x");
        }

        Assert.Null(DosyaIslemleri.BosAdBul(klasor, "a.txt"));
    }

    [Fact]
    public void BOS_AD_VARSA_NUMARALANIR()
    {
        File.WriteAllText(Path.Combine(_kok, "a.txt"), "x");
        Assert.Equal("a (2).txt", DosyaIslemleri.BosAdBul(_kok, "a.txt"));
    }

    // ---------- var olan klasore dokunulmaz ----------

    [Fact]
    public void KOPYALAMA_PATLARSA_VAR_OLAN_HEDEF_KLASOR_SILINMEZ()
    {
        // Bu, olasiligi dusuk ama ETKISI YUKSEK olan haldi: yarim kalani
        // silen kod hedefin onceden var olup olmadigini bilmiyordu ve var
        // olan bir klasoru ICERIGIYLE siliyordu.
        string kaynak = Path.Combine(_kok, "kaynak");
        Directory.CreateDirectory(kaynak);
        File.WriteAllText(Path.Combine(kaynak, "ic.txt"), "kaynak");

        string hedefKlasor = Path.Combine(_kok, "hedef");
        Directory.CreateDirectory(hedefKlasor);

        string carpisan = Path.Combine(hedefKlasor, "kaynak");
        Directory.CreateDirectory(carpisan);
        File.WriteAllText(Path.Combine(carpisan, "degerli.txt"), "kaybolmamali");

        // "Degistir" klasorde YASAK, yani islem yapilmadan reddedilmeli.
        IslemRaporu rapor = DosyaIslemleri.Kopyala(kaynak, hedefKlasor, Cakisma.Degistir);

        Assert.False(rapor.Oldu);
        Assert.True(File.Exists(Path.Combine(carpisan, "degerli.txt")));
    }

    // ---------- cope tasindiktan sonra kayit yazilamazsa ----------

    [Fact]
    public void COP_KAYDI_YAZILAMAZSA_DOSYA_YERINE_GERI_KONUR()
    {
        // Kayit dosyasinin yerine KLASOR koyuluyor: AppendAllText o yolda
        // patlar. Boylece "tasima oldu ama kayit yazilamadi" hali gercekten
        // uretiliyor - taklit edilmiyor.
        string cop = Cop.Yolu(_kok);
        Directory.CreateDirectory(Path.Combine(cop, "kayit.txt"));

        string dosya = Path.Combine(_kok, "Parça1.SLDPRT");
        File.WriteAllText(dosya, "veri");

        IslemRaporu rapor = Cop.Sil(cop, dosya);

        Assert.False(rapor.Oldu);
        Assert.True(File.Exists(dosya));                  // dosya YERINDE
        Assert.Equal("veri", File.ReadAllText(dosya));    // ve bozulmamis
        Assert.Contains("kaydı", rapor.Sebep ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void COP_KAYDI_OKUNAMAZSA_BOS_DEMEZ()
    {
        // Kayit yerinde bir KLASOR var: ReadAllLines patlar. Eskiden burasi
        // sessizce bos liste donuyordu ve ekranda "Çöp kutusu boş." yaziyordu.
        string cop = Cop.Yolu(_kok);
        Directory.CreateDirectory(Path.Combine(cop, "kayit.txt"));

        CopDurumu durum = Cop.Oku(cop);

        Assert.False(durum.Guvenilir);
        Assert.NotNull(durum.Okunamadi);
        Assert.Empty(durum.Ogeler);
    }

    [Fact]
    public void HIC_SILINMEMISSE_COP_GERCEKTEN_BOSTUR()
    {
        CopDurumu durum = Cop.Oku(Cop.Yolu(_kok));

        Assert.True(durum.Guvenilir);   // "bos" ile "okunamadi" ayni sey degil
        Assert.Empty(durum.Ogeler);
    }

    [Fact]
    public void BOZUK_KAYIT_SATIRI_SAYILIR()
    {
        string cop = Cop.Yolu(_kok);
        Directory.CreateDirectory(cop);
        File.WriteAllText(Path.Combine(cop, "kayit.txt"), "bu satir bozuk\n");

        CopDurumu durum = Cop.Oku(cop);

        Assert.True(durum.Guvenilir);
        Assert.Empty(durum.Ogeler);
        Assert.Equal(1, durum.BozukSatir);   // sessizce atlanmiyor
    }

    // ---------- ayni klasore tasima ----------

    [Fact]
    public void AYNI_KLASORE_TASIMA_ZATENVAR_DONER()
    {
        string dosya = Path.Combine(_kok, "a.txt");
        File.WriteAllText(dosya, "x");

        IslemRaporu rapor = DosyaIslemleri.Tasi(dosya, _kok);

        Assert.False(rapor.Oldu);
        Assert.Equal(IslemSonucu.ZatenVar, rapor.Sonuc);
        Assert.True(File.Exists(dosya));
    }
}
