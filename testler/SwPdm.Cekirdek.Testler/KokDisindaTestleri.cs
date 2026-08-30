using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// KOK DISINDA AYRIMI (Erkan'in sectigi is, 30.08.2026): yazili yol diskte
/// VAR ama acik kokun DISINDA -> bu "kayip dosya" degil; BULUNAMADI'dan
/// ayrilir, panelde gizlenmez ve kendi raporuna gider.
///
/// Ayirt etmenin uc siniri da burada olculuyor:
///   - diskte VAR + kok DISINDA        -> KokDisinda
///   - diskte YOK                      -> Bulunamadi (gizlenmeye devam)
///   - diskte var ama kokun ALTINDA    -> Bulunamadi (o taramanin isi)
/// </summary>
public sealed class KokDisindaTestleri : IDisposable
{
    private readonly string _kok;

    /// <summary>Kokun DISINDA bir klasor - ayrimin olculdugu yer.</summary>
    private readonly string _dis;

    public KokDisindaTestleri()
    {
        string ek = Guid.NewGuid().ToString("N")[..8];
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-kokdisi-" + ek);
        _dis = Path.Combine(Path.GetTempPath(), "swpdm-dis-" + ek);
        Kopyala(Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz"), _kok);
        Directory.CreateDirectory(_dis);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_kok, recursive: true);
            Directory.Delete(_dis, recursive: true);
        }
        catch (IOException)
        {
            // Temizlik sonucu degistirmez.
        }
    }

    private static void Kopyala(string kaynak, string hedef)
    {
        Directory.CreateDirectory(hedef);
        foreach (string d in Directory.GetFiles(kaynak))
        {
            File.Copy(d, Path.Combine(hedef, Path.GetFileName(d)));
        }

        foreach (string k in Directory.GetDirectories(kaynak))
        {
            Kopyala(k, Path.Combine(hedef, Path.GetFileName(k)));
        }
    }

    private ReferansIndeksi Taranmis()
    {
        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);
        return indeks;
    }

    private string Yol(params string[] p) => Path.Combine([_kok, .. p]);

    /// <summary>Montaj1'in kaydina, kokun disindaki gercek bir dosyayi yazar.</summary>
    private string DisReferansEkle(ReferansIndeksi indeks)
    {
        string disDosya = Path.Combine(_dis, "Kutuphane.SLDPRT");
        File.WriteAllText(disDosya, "icerik onemsiz; varligi olculuyor");

        IndeksKaydi kayit = indeks.Kayit(Yol("Montaj1.SLDASM"))!;
        indeks.Koy(kayit with { YazilanYollar = [.. kayit.YazilanYollar, disDosya] });
        return disDosya;
    }

    private Cozum Cozumu(ReferansIndeksi indeks, string yazilan)
        => indeks.Kullandiklari(Yol("Montaj1.SLDASM")).Single(x => x.YazilanYol == yazilan).Cozum;

    [Fact]
    public void KOK_DISINDAKI_GERCEK_DOSYA_BULUNAMADI_SAYILMAZ()
    {
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks);

        Cozum cozum = Cozumu(indeks, disDosya);

        Assert.Equal(CozumDurumu.KokDisinda, cozum.Durum);
        Assert.Equal(disDosya, cozum.Yol);   // onizleme ve ipucu bu yolu kullaniyor
    }

    [Fact]
    public void KOK_DISINDAKI_SATIR_PANELDE_GIZLENMEZ()
    {
        ReferansIndeksi indeks = Taranmis();
        DisReferansEkle(indeks);

        PanelSatirlari p = indeks.KullandiklariGorunur(Yol("Montaj1.SLDASM"));

        Assert.Contains(p.Gosterilecekler, x => x.Cozum.Durum == CozumDurumu.KokDisinda);
        Assert.Equal(0, p.Gizlenen);
    }

    [Fact]
    public void DISKTE_DE_OLMAYAN_YOL_BULUNAMADI_KALIR()
    {
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks);

        // Dis dosya silindi; bir sonraki indeks degisikliginde (her islem
        // oncesi tarama bir degisikliktir) cevap BULUNAMADI'ya doner.
        File.Delete(disDosya);
        indeks.Koy(indeks.Kayit(Yol("Montaj1.SLDASM"))!);   // dokunus: onbellek duser

        Assert.Equal(CozumDurumu.Bulunamadi, Cozumu(indeks, disDosya).Durum);
    }

    [Fact]
    public void KOKUN_ALTINDAKI_TARANMAMIS_DOSYA_KOK_DISINDA_SAYILMAZ()
    {
        // Dosya diskte ve kokun ALTINDA ama indekste yok (bayat indeks).
        // "Kok disinda" demek yalan olurdu; orasi taramanin isi.
        ReferansIndeksi indeks = Taranmis();

        string icDosya = Yol("Taranmamis.SLDPRT");
        File.WriteAllText(icDosya, "diskte var, indekste yok");

        IndeksKaydi kayit = indeks.Kayit(Yol("Montaj1.SLDASM"))!;
        indeks.Koy(kayit with { YazilanYollar = [.. kayit.YazilanYollar, icDosya] });

        Assert.Equal(CozumDurumu.Bulunamadi, Cozumu(indeks, icDosya).Durum);
    }

    [Fact]
    public void Rapor_KOK_DISINDAKINI_KENDI_RAPORU_LISTELER_KIRIK_LISTELEMEZ()
    {
        ReferansIndeksi indeks = Taranmis();
        string disDosya = DisReferansEkle(indeks);

        RaporSonucu disaridakiler = Uret("Kök dışındakiler", indeks);
        RaporSonucu kirik = Uret("Kırık referanslar", indeks);

        Assert.Single(disaridakiler.Satirlar);
        Assert.Contains(disDosya, disaridakiler.Satirlar[0].Aciklama, StringComparison.Ordinal);
        Assert.Empty(kirik.Satirlar);   // saglam kutuphane bagi KIRIK degildir
    }

    private static RaporSonucu Uret(string ad, ReferansIndeksi indeks)
        => RaporListesi.Tumu.First(r => r.Ad == ad).Uret(indeks);
}
