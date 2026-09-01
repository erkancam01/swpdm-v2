using System;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// REFERANS PANELINDE SAG TIK. Erkan, 30.08.2026: "içindekiler ve kullanıldığı
/// yerler bölümlerinde sağ tık yapamıyorum. ama yapabilmem lazım sonuçta
/// ordakilerde parça."
///
/// Ozelligin BUTUN karari burada (CLAUDE.md 1b): kaldirmak = bu dosyayi sil +
/// AnaForm'daki bir bloku sil. Menunun kendisi <see cref="AgacMenusu"/>'nden
/// URETILIYOR - ayni islem listesi, ayni sira, ikinci kopya yok (CLAUDE.md 8).
///
/// HEDEF SATIRDIR (Erkan'in karari): islem, tiklanan satirin dosyasina
/// uygulanir; agactaki secim ve okunan liste YERINDE KALIR. Tek istisna
/// <see cref="IslemHedefi.Sahip"/> diyen islem (ElleBagla) - o,
/// panelin gosterdigi dosyaya uygulanir.
///
/// UC SESSIZ HATA BURADA KAPATILIYOR (CLAUDE.md 3 - hepsi yanlis dosyaya
/// islem yaptirirdi):
///   1. ListView sag tikta SECMEZ. Secmeden acilan menu, kullanicinin
///      tikladigindan BASKA bir satira uygulanirdi -> once satir secilir.
///   2. Cozulememis satirin dosyasi YOKTUR. Orada secim BOS birakilir; her
///      islem kendi sebebini soyleyerek gri durur. "Sessizce agactakine
///      uygula" bu uygulamada saglam dosya sildirir.
///   3. Bayat indeks kok DISINDA bir yol tutabilir. AltindaMi ile bakilir;
///      disardaysa yine bos secim ve sebep yazilir.
///
/// WINE'DA OLCULEMEZ: acilan her ToolStripDropDown uygulamayi cokertiyor
/// (CLAUDE.md 11). Bu yuzden ayni kod KISAYOLLA da calisiyor ve kapi onu
/// olcuyor - menusuz kalan bir ozellik burada kor noktadir.
/// </summary>
internal sealed class ReferansMenusu
{
    private readonly ReferansListesi _liste;
    private readonly AgacMenusu _menu;

    private Func<SecimBaglami> _sahipSecimi = BosSecim;

    internal ReferansMenusu(ReferansListesi liste)
    {
        ArgumentNullException.ThrowIfNull(liste);
        _liste = liste;

        // SAG TIK ONCE SECER. MouseDown, ContextMenuStrip'in acildigi
        // MouseUp'tan once gelir; menu acildiginda satir secili olur ve
        // hedef GORUNUR (yukaridaki 1. madde).
        _liste.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            if (_liste.GetItemAt(e.X, e.Y) is ListViewItem satir)
            {
                satir.Selected = true;
                satir.Focused = true;
            }
        };

        _menu = new AgacMenusu(_liste);
        _menu.SecimKaynagi(() => Coz().Secim);
        _menu.UstBilgi(() => Coz().Baslik);

        // GRI KALMANIN GERCEK SEBEBI kisayol yolunda da yazilsin: Coz()
        // zaten uretiyordu ama yalnizca menu basligina gidiyordu ve Wine'da
        // menu hic acilamiyor (01.09.2026 denetimi).
        _menu.SatirSebebi(() => Coz().Sebep);

        // Kisayollari KAYDETMIYOR, yalniz yaziyor; panelin kendi sahiplendigi
        // tuslarin (Enter, Ctrl+C) etiketi hic yazilmiyor.
        _menu.KisayollariYalnizYaz(ReferansPaneliTuslari.Sahiplenir);
    }

    /// <summary>
    /// Menuyu calisir hale getirir. Agacinkiyle AYNI baglantilar; tek fark
    /// <paramref name="sahipSecimi"/> - "satirin sahibi kim" sorusunun cevabi.
    /// </summary>
    internal void Bagla(
        IIlerlemeYuzeyi ilerleme,
        Action agaciKapat,
        ReferansSurucusu referanslar,
        Func<SecimBaglami> sahipSecimi,
        Action islemOncesi,
        EventHandler<string?> tazele,
        EventHandler<string> durum,
        Func<string?, bool> agactaGoster)
    {
        ArgumentNullException.ThrowIfNull(sahipSecimi);

        _sahipSecimi = sahipSecimi;
        _menu.IlerlemeYuzeyi(ilerleme);
        _menu.AgaciKapatan(agaciKapat);
        _menu.AgactaGosteren(agactaGoster);
        _menu.ReferansSurucusunu(referanslar);
        _menu.SahipSecimi(sahipSecimi);
        _menu.IslemOncesi(islemOncesi);
        _menu.Tazele += tazele;
        _menu.Durum += durum;
    }

    /// <summary>
    /// Panel odaktayken bir tusa basildi. Yalnizca SATIRA uygulanan islemler
    /// denenir; sahibine uygulananlar (Ctrl+Shift+L) ve genel isler agacin
    /// menusunden gecmeye devam eder - boylece bugunku davranis bozulmuyor.
    /// </summary>
    internal bool TusaBasildi(Keys tuslar)
        => _menu.TusaBasildi(tuslar, islem => islem.Hedef != IslemHedefi.Sahip);

    private static SecimBaglami BosSecim()
        => new([], null, AramaKipinde: false, Kok: null, CopKlasoru: null);

    /// <summary>
    /// Secili satiri, islemlerin anladigi secime cevirir - ve menunun
    /// ustunde yazacak hedef cumlesini uretir.
    ///
    /// Kok, arama kipi ve cop klasoru SAHIBIN baglamindan aliniyor: bunlar
    /// pencere geneli degerler ve iki yerde ayri ayri hesaplanirsa AYRISIR
    /// (CLAUDE.md 8).
    /// </summary>
    private (SecimBaglami Secim, string Baslik, string? Sebep) Coz()
    {
        SecimBaglami sahip = _sahipSecimi();

        if (_liste.SeciliHedef is not string yol)
        {
            return (Bos(sahip), "Bu satır bir dosyaya çözülemedi",
                "Bu satır bir dosyaya çözülemedi — üzerinde dosya işlemi yapılamaz.");
        }

        if (!WindowsYolu.AltindaMi(yol, sahip.Kok))
        {
            return (Bos(sahip), "Bu dosya açık kökün dışında",
                "Bu dosya açık kökün dışında — üzerinde işlem yapılamaz.");
        }

        // ARSIV KOPYASINA ISLEM UYGULANMAZ (CLAUDE.md 1a): VERSIYONLAR
        // satirlari hedef olarak arsiv kopyasini tasiyor (tek tik onizleme,
        // cift tik ac icin). F2/Sil/Kes oraya giderse kayit.txt ile dosya
        // eslesmesi kirilir ve versiyon "kayip" gorunur.
        if (ArsivdeMi(yol))
        {
            return (Bos(sahip), "Arşiv kopyası — dosya işlemleri uygulanmaz; Enter: bu versiyona dön",
                "Arşiv kopyası — dosya işlemleri uygulanmaz. Enter: bu versiyona dön.");
        }

        if (KlasorTarayici.DosyayiOku(yol) is not DosyaOgesi dosya)
        {
            // Indeks bayat olabilir: yazan dosya artik yerinde degil.
            return (Bos(sahip), "Bu dosya okunamadı: " + WindowsYolu.DosyaAdi(yol),
                "Bu dosya okunamadı: " + WindowsYolu.DosyaAdi(yol));
        }

        // SATIR COZULDU: sebep YOK - buradan sonra gri kalan bir islem
        // varsa sebebi KENDI kuralidir ("Tek bir dosya secin" gibi) ve
        // ezilmemeli.
        return (
            SecimBaglami.Kur(
                [dosya], sahip.Kok, sahip.AramaKipinde, sahip.CopKlasoru, sahip.Kilitler),
            dosya.Ad,
            null);
    }

    /// <summary>Yol, versiyon arsivinin icinde mi (adi Surumler.KlasorAdi'ndan).</summary>
    private static bool ArsivdeMi(string yol)
        => yol.Contains(
               WindowsYolu.Ayirici + Surumler.KlasorAdi + WindowsYolu.Ayirici,
               StringComparison.OrdinalIgnoreCase)
           || yol.Contains(
               WindowsYolu.EgikAyirici + Surumler.KlasorAdi + WindowsYolu.EgikAyirici,
               StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// COZULEMEYEN SATIRIN secimi: BOS - ve "buraya" da YOK.
    ///
    /// IKI HATA BURADA KAPANDI (01.09.2026 denetimi):
    /// 1. EtkinKlasor'a KOK konuyordu; Yapistir bu satirda GRI kalmiyor,
    ///    Ctrl+V dosyalari KOKUN KENDISINE tasiyordu. Menu ustunde "Bu
    ///    satir bir dosyaya cozulemedi" yazarken islem calisiyordu.
    /// 2. Kilit kumesi TASINMIYORDU (6. konum atlanmisti) -> Kilitler.Engel
    ///    "kilit yok" gorup her islem icin false donuyordu. Bugun
    ///    somurulemiyordu cunku tek hedef koktu ve kok kilitlenemiyor; 1.
    ///    madde duzelmeden once fark edilmesi sans isiydi.
    /// </summary>
    private static SecimBaglami Bos(SecimBaglami sahip)
        => new([], null, sahip.AramaKipinde, sahip.Kok, sahip.CopKlasoru, sahip.Kilitler);
}
