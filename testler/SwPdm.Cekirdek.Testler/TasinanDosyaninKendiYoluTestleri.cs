using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// TASINAN DOSYANIN KENDI YAZILI YOLU BAYATLIYOR (Erkan, 01.09.2026:
/// "teknik resmi baska klasore attim, icindekiler ve kullanildigi yerler yok
/// yazyor ama tiklayinca dosya duzgun aciliyor").
///
/// OLCULEN: teknik resim baska klasore tasininca ICINDE YAZAN yol artik
/// modelin yerini gostermiyor. Dosya yine BULUNUYOR (cozucu ada gore ariyor)
/// ama BayatMi true oluyor ve satir ICINDEKILER'den KIRIK bolumune geciyor.
///
/// SEKME BUNU "yok" DIYE YAZIYORDU - iki kez yanlis (CLAUDE.md 3):
/// kullanici "bu teknik resim hicbir modeli kullanmiyor" diye okur, ve
/// onarilmasi gereken bayat bir yol oldugunu hic ogrenmez.
///
/// BURADA OLCULEN SEBEP, EKRANDAKI KELIME DEGIL: sekme metni arayuz
/// katmaninda ve kapi onu okuyamiyor (OCR yok). Kelime degisirse bu test
/// kirilmaz - ama kelimenin dayandigi GERCEK degisirse kirilir, ki asil
/// koruma odur.
///
/// TASINAN DOSYANIN KENDISI HALA ONARILMIYOR: ReferansOnarimi.TasimaPlani
/// yalnizca DISARIDA KALAN EBEVEYNLERI onariyor. Onarimi otomatiklestirmek
/// geri alma (Ctrl+Z) destegi ister - dosya eski klasorune donunce yeni
/// yazilan yol bu sefer ORADA bayat olurdu (CLAUDE.md 1a). Kullanicinin
/// bugunku yolu: "Bayat yollar" raporu -> "Bulunanlari duzelt".
/// SIRADAKI.md'de yazili.
/// </summary>
public sealed class TasinanDosyaninKendiYoluTestleri : IDisposable
{
    private static string Ornek => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private readonly string _kok;

    public TasinanDosyaninKendiYoluTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-tasima-" + Guid.NewGuid().ToString("N"));
        Kopyala(Ornek, _kok);
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void TABAN_yaninda_dururken_yol_BAYAT_DEGIL()
    {
        // TABAN SART: ikinci test tek basina "hep bayat diyen" bir kuralla da
        // gecerdi.
        ReferansIndeksi indeks = Indeks();
        string drw = Path.Combine(_kok, "Parça1.SLDDRW");

        Assert.False(Bayat(indeks, drw), "modelin yaninda dururken yol bayat sayildi");
    }

    [Fact]
    public void BASKA_KLASORE_tasininca_kendi_yolu_BAYATLIYOR()
    {
        string drw = Path.Combine(_kok, "Parça1.SLDDRW");
        string alt = Path.Combine(_kok, "TEKNİK RESİMLER");
        Directory.CreateDirectory(alt);
        string yeni = Path.Combine(alt, "Parça1.SLDDRW");
        File.Move(drw, yeni);

        ReferansIndeksi indeks = Indeks();

        // 1. DOSYA HALA BULUNUYOR - "kayip" degil. Erkan'in "tiklayinca
        //    duzgun aciliyor" dedigi sey bu.
        Cozum cozum = Tek(indeks, yeni);
        Assert.Equal(CozumDurumu.Bulundu, cozum.Durum);
        Assert.Equal(Path.Combine(_kok, "Parça1.SLDPRT"), cozum.Yol);

        // 2. AMA YAZILI YOL BAYAT: SOLIDWORKS'un okudugu yol modelin yerini
        //    gostermiyor. Satir bu yuzden KIRIK bolumune geciyor.
        Assert.True(Bayat(indeks, yeni), "tasinan teknik resmin yolu bayat sayilmadi");

        // 3. VE REFERANS SAYISI SIFIR DEGIL: sekmenin "yok" dememesinin
        //    dayanagi bu - dosya referans VERIYOR.
        Assert.NotEmpty(indeks.Kayit(yeni)!.YazilanYollar);
    }

    // ------------------------------------------------------------------

    private ReferansIndeksi Indeks()
    {
        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);
        return indeks;
    }

    private static Cozum Tek(ReferansIndeksi indeks, string yol)
    {
        IndeksKaydi kayit = indeks.Kayit(yol)!;
        string yazilan = Assert.Single(kayit.YazilanYollar);
        return indeks.Coz(kayit, yazilan);
    }

    private static bool Bayat(ReferansIndeksi indeks, string yol)
    {
        IndeksKaydi kayit = indeks.Kayit(yol)!;
        string yazilan = Assert.Single(kayit.YazilanYollar);
        return ReferansIndeksi.BayatMi(yol, yazilan, indeks.Coz(kayit, yazilan));
    }

    private static void Kopyala(string kaynak, string hedef)
    {
        Directory.CreateDirectory(hedef);
        foreach (string dosya in Directory.GetFiles(kaynak))
        {
            File.Copy(dosya, Path.Combine(hedef, Path.GetFileName(dosya)));
        }

        foreach (string klasor in Directory.GetDirectories(kaynak))
        {
            Kopyala(klasor, Path.Combine(hedef, Path.GetFileName(klasor)));
        }
    }
}
