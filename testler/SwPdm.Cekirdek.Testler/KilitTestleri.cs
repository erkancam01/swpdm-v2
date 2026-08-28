using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// SOLIDWORKS "~$" kilit dosyalarinin cozumu.
///
/// NEDEN CEKIRDEKTE OLCULUYOR: bu karar arayuzde dursaydi birim testi
/// yazilamazdi (CLAUDE.md 7). Burada Windows yollariyla, diske hic
/// dokunmadan olculuyor.
/// </summary>
public sealed class KilitTestleri
{
    private const string Kok = @"C:\proje";

    private static DosyaOgesi Oge(string klasor, string ad)
        => new(
            WindowsYolu.Birlestir(klasor, ad), ad, DosyaTurleri.Tani(ad), 10,
            new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Unspecified));

    private static KilitDurumu Coz(params DosyaOgesi[] dosyalar) => Kilit.Coz(dosyalar);

    [Fact]
    public void KilitMi_yalnizca_onekli_ve_DOLU_adlari_kilit_sayar()
    {
        Assert.True(Kilit.KilitMi("~$Parca1.SLDPRT"));
        Assert.False(Kilit.KilitMi("Parca1.SLDPRT"));

        // "~$" tek basina bir ad olabilir; sahibi BOS olurdu ve her sey
        // birbirine karisirdi.
        Assert.False(Kilit.KilitMi("~$"));
        Assert.False(Kilit.KilitMi(null));
    }

    [Fact]
    public void SahibiVARSA_kilit_GIZLENIR_ve_sahibi_ACIK_isaretlenir()
    {
        DosyaOgesi sahip = Oge(Kok, "Parca1.SLDPRT");
        KilitDurumu durum = Coz(sahip, Oge(Kok, "~$Parca1.SLDPRT"));

        Assert.Single(durum.Gosterilecek);
        Assert.Equal(sahip.Yol, durum.Gosterilecek[0].Yol);
        Assert.Contains(sahip.Yol, durum.AcikYollar);
        Assert.Equal(1, durum.GizlenenSayisi);
    }

    [Fact]
    public void SahibiYOKSA_kilit_GORUNUR_kalir()
    {
        // CLAUDE.md 3: aciklayamadigimiz bir seyi gizlemek, kullaniciya
        // klasoru bos gosterip "dizin bos degil" hatasini SEBEPSIZ birakir.
        KilitDurumu durum = Coz(Oge(Kok, "~$Kayip.SLDPRT"), Oge(Kok, "Baska.SLDPRT"));

        Assert.Equal(2, durum.Gosterilecek.Count);
        Assert.Contains(durum.Gosterilecek, d => d.Ad == "~$Kayip.SLDPRT");
        Assert.Empty(durum.AcikYollar);
        Assert.Equal(0, durum.GizlenenSayisi);
    }

    [Fact]
    public void Eslesme_KLASOR_BAZINDA_olur_baska_klasordeki_sahip_saymaz()
    {
        // Arama sonucu birden cok klasoru kapsiyor. Yalnizca ADA bakan bir
        // eslesme, A'daki kilidi B'deki dosyayla eslestirip YANLIS dosyayi
        // gizlerdi.
        DosyaOgesi sahip = Oge(@"C:\proje\A", "Parca1.SLDPRT");
        DosyaOgesi kilit = Oge(@"C:\proje\B", "~$Parca1.SLDPRT");

        KilitDurumu durum = Coz(sahip, kilit);

        Assert.Equal(2, durum.Gosterilecek.Count);
        Assert.Empty(durum.AcikYollar);
    }

    [Fact]
    public void Eslesme_buyuk_kucuk_harf_DUYARSIZ()
    {
        // Windows'ta dosya adlari duyarsiz; Ordinal karsilastirma ayni
        // dosyayi kacirir ve kilit gizlenmeden kalirdi.
        DosyaOgesi sahip = Oge(Kok, "PARCA1.sldprt");
        KilitDurumu durum = Coz(sahip, Oge(Kok, "~$parca1.SLDPRT"));

        Assert.Single(durum.Gosterilecek);
        Assert.Contains(sahip.Yol, durum.AcikYollar);
    }

    [Fact]
    public void Kilit_yoksa_liste_ve_SIRA_aynen_kalir()
    {
        DosyaOgesi[] girdi =
        [
            Oge(Kok, "b.SLDASM"),
            Oge(Kok, "a.SLDPRT"),
            Oge(Kok, "c.SLDDRW"),
        ];

        KilitDurumu durum = Kilit.Coz(girdi);

        Assert.Equal(
            girdi.Select(d => d.Yol).ToArray(),
            durum.Gosterilecek.Select(d => d.Yol).ToArray());
        Assert.Empty(durum.AcikYollar);
    }

    [Fact]
    public void Bos_ve_null_giris_cokmez()
    {
        Assert.Empty(Kilit.Coz(null).Gosterilecek);
        Assert.Empty(Kilit.Coz(Array.Empty<DosyaOgesi>()).Gosterilecek);
    }

    [Fact]
    public void Birden_cok_kilit_ayri_ayri_sayilir()
    {
        KilitDurumu durum = Coz(
            Oge(Kok, "Parca1.SLDPRT"),
            Oge(Kok, "~$Parca1.SLDPRT"),
            Oge(Kok, "Montaj1.SLDASM"),
            Oge(Kok, "~$Montaj1.SLDASM"),
            Oge(Kok, "~$Sahipsiz.SLDPRT"));

        Assert.Equal(2, durum.GizlenenSayisi);
        Assert.Equal(2, durum.AcikYollar.Count);
        Assert.Equal(3, durum.Gosterilecek.Count);
        Assert.Contains(durum.Gosterilecek, d => d.Ad == "~$Sahipsiz.SLDPRT");
    }

    [Fact]
    public void SahibininAdi_oneki_atar()
    {
        Assert.Equal("Parca1.SLDPRT", Kilit.SahibininAdi("~$Parca1.SLDPRT"));
        Assert.Null(Kilit.SahibininAdi("Parca1.SLDPRT"));
    }
}
