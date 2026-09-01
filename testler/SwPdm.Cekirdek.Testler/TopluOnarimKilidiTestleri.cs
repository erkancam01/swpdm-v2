using System;
using System.IO;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// TOPLU ONARIM KILITLI KLASORE YAZMAZ (01.09.2026 denetiminde bulundu).
///
/// BULUNAN HATA: "Bulunanlari duzelt" (rapor penceresi) indeksin TAMAMINI
/// geziyordu ve yalnizca SOLIDWORKS'un "~$" kilidini soruyordu; bizim
/// klasor kilidimiz hic sorulmuyordu. Islemlerin kilit kapisi
/// (Kilitler.Engel) da devreye girmiyor cunku o SECIME bakiyor, oysa
/// duzeltme secimden bagimsiz.
///
/// Belirti sessizdi: kullanici bitmis projeyi kilitler, haftalar sonra
/// duzelt der; kilitli klasordeki montajlarin ICINE yazilir ve agacta hala
/// "kilitli" yazar. Kilit, tam da onlemek icin konuldugu seyi yapar
/// (CLAUDE.md 1a/3).
///
/// ILK TEST TABAN OLCUYOR - ve o olmadan ikincisi kendini kandirirdi:
/// kilitliyken dosyanin degismemesi, "onarilacak bir sey yoktu" yuzunden de
/// olabilirdi. Once kilitsizken DEGISTIGI gosteriliyor.
/// </summary>
public sealed class TopluOnarimKilidiTestleri : IDisposable
{
    private static string Ornek => Path.Combine(AppContext.BaseDirectory, "veri", "tertemiz");

    private readonly string _kok;
    private readonly string _bitmis;
    private readonly string _montaj;

    public TopluOnarimKilidiTestleri()
    {
        _kok = Path.Combine(Path.GetTempPath(), "swpdm-kilitonarim-" + Guid.NewGuid().ToString("N"));

        // Ornek agac KOKE degil ALT KLASORE kopyalaniyor - kok
        // kilitlenemiyor (KlasorKilidi kokun kendisini bilerek disliyor).
        _bitmis = Path.Combine(_kok, "Bitmis");
        Kopyala(Ornek, _bitmis);

        // BAYAT YOL KURULUYOR: parca UYGULAMA DISINDA baska bir klasore
        // tasiniyor - yani onarim hic calismadi. Dosya duruyor, biz onu
        // buluyoruz (cozucu ADA gore ariyor, CLAUDE.md 5) ama montajin
        // icinde yazan yol ESKI yeri gosteriyor. Ayni kalip
        // ReferansOnarimiTestleri'nde de kullaniliyor.
        //
        // TEK KOPYA OLMASI SART - ILK YAZISTA IKI KOPYA VARDI VE TEST
        // KENDINI YAKALADI: ayni adli iki "Parça1.SLDPRT" olunca cozum
        // BELIRSIZ oluyor, belirsiz olan da BAYAT sayilmiyor; hicbir sey
        // onarilmiyordu. Taban olcumu tam bunun icin var.
        string alt = Path.Combine(_bitmis, "3");
        Directory.CreateDirectory(alt);
        File.Move(
            Path.Combine(_bitmis, "Parça1.SLDPRT"), Path.Combine(alt, "Parça1.SLDPRT"));

        _montaj = Path.Combine(_bitmis, "Montaj1.SLDASM");
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, recursive: true); } catch (IOException) { }
    }

    // ---------------------------------------------------------------------

    [Fact]
    public void TABAN_kilit_YOKKEN_bayat_yol_ONARILIYOR()
    {
        byte[] once = File.ReadAllBytes(_montaj);

        OnarimOzeti ozet = YolBaglama.BayatlariOnar(Indeks(), kilitler: null);

        Assert.True(ozet.Onarilan > 0, "ornek agacta onarilacak bayat yol yok - test kendini kandirir");
        Assert.Equal(0, ozet.AtlananKilitli);
        Assert.False(
            once.AsSpan().SequenceEqual(File.ReadAllBytes(_montaj)),
            "kilit yokken montajin ICINE yazilmis olmaliydi");
    }

    [Fact]
    public void KILITLI_klasordeki_dosyaya_DOKUNULMUYOR_ve_SAYISI_YAZILIYOR()
    {
        byte[] once = File.ReadAllBytes(_montaj);
        KlasorKilidi.Degistir(_kok, [_bitmis], kilitle: true);

        OnarimOzeti ozet = YolBaglama.BayatlariOnar(Indeks(), KlasorKilidi.Oku(_kok));

        // 1. ASIL SART: dosya BIREBIR ayni kaldi.
        Assert.True(
            once.AsSpan().SequenceEqual(File.ReadAllBytes(_montaj)),
            "kilitli klasordeki montajin icine YAZILDI");

        // 2. SESSIZ ATLAMA YOK (CLAUDE.md 3): kac tanesinin atlandigi
        //    sayiliyor ki arayuz bunu yazabilsin.
        Assert.True(ozet.AtlananKilitli > 0);

        // 3. KILITLI OLANA HIC DOKUNULMADI: "dokunulan" listesi bos ve
        //    onarilan yok - taban olcumu ayni agacta bunun TERSINI
        //    gosteriyor, yani sifir burada kilidin sonucu.
        Assert.Empty(ozet.Dokunulan);
        Assert.Equal(0, ozet.Onarilan);
    }

    // ---------------------------------------------------------------------

    private ReferansIndeksi Indeks()
    {
        var indeks = new ReferansIndeksi(_kok);
        IndeksTarama.Tara(indeks);
        return indeks;
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
