using System;
using System.Collections.Generic;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// VERSIYONLAR sekmesinin icerigi - "bu dosyanin hangi versiyonlari var,
/// hangisine donulur" sorusunun arayuzdeki TEK sahibi (CLAUDE.md 1b).
/// Veriyi <see cref="Surumler"/> verir; burasi satirlari cizer ve son
/// cizilen listeyi tutar ki "Enter = bu versiyona don" sira numarasindan
/// kaydi bulabilsin.
///
/// Satir duzeni: solda "v3 — not", sagda tarih. Ipucunda (ve Ctrl+C ile
/// panoda) arsiv kopyasinin TAM YOLU - kullanici isterse arsive Gezgin'den
/// bakabilir; yolu gizlemek "arsiv nerede" sorusunu cevapsiz birakirdi
/// (CLAUDE.md 3).
/// </summary>
internal sealed class SurumBolumu
{
    private readonly List<SurumKaydi> _sonListe = [];

    /// <summary>Sekmede yazacak sayi: "yok" · "N" · "okunamadı".</summary>
    internal static string SayiMetni(string kok, string yol)
    {
        SurumDurumu durum = Surumler.Listele(kok, yol);
        if (!durum.Guvenilir)
        {
            return "okunamadı";
        }

        if (durum.Ogeler.Count == 0)
        {
            // Sekme etiketi de yalan soylemez: kayit VARKEN "yok" yazmak
            // kullaniciyi hic bakmadan gecirir (CLAUDE.md 3).
            return durum.BozukSatir > 0 ? "okunamadı" : "yok";
        }

        return durum.Ogeler.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Bolumu doldurur; cizilen kayitlari sira icin saklar.</summary>
    internal void Doldur(ReferansListesi liste, string kok, string yol)
    {
        _sonListe.Clear();

        SurumDurumu durum = Surumler.Listele(kok, yol);
        if (!durum.Guvenilir)
        {
            Aciklama(liste, durum.Okunamadi ?? "Versiyon kaydı okunamadı.", "hata");
            return;
        }

        if (durum.Ogeler.Count == 0)
        {
            // BOS LISTE "YOK" DEMEK DEGILDIR (CLAUDE.md 3). Kayit VAR ama
            // arsiv kopyasi cozulemiyorsa "Versiyon yok" demek duz yalandi -
            // Erkan'da tam bu oldu (31.08.2026): ad degisince arsivdeki asil
            // dosya bulunamadi ve panel "versiyon yok" dedi, oysa arsiv
            // diskte duruyordu.
            Aciklama(
                liste,
                durum.BozukSatir > 0
                    ? $"{durum.BozukSatir} versiyon kaydı var ama arşiv kopyası okunamadı — "
                      + "SİLMEYİN, arşiv .SwPdmSurum altında duruyor."
                    : "Versiyon yok — Ctrl+Shift+U ile başlat.",
                durum.BozukSatir > 0 ? "!" : "—");
            return;
        }

        foreach (SurumKaydi kayit in durum.Ogeler)
        {
            _sonListe.Add(kayit);

            string ad = kayit.Not.Length == 0
                ? $"v{kayit.No}"
                : $"v{kayit.No} — {kayit.Not}";

            // HEDEF = ARSIV KOPYASI (Erkan, 31.08.2026: "versiyonların
            // önizlemesini görmek ve çift tıklayınca açabilmek"). Tek tik
            // boylece komsu onizleme borusuna girer: panelde o versiyonun
            // resmi, baslikta "◂ v3.SLDPRT". Cift tikin "ac" anlami
            // AnaForm'daki dallanmada; sag tik icin ReferansMenusu arsiv
            // yolunu tanir ve dosya islemlerini uygulamaz.
            liste.Ekle(
                ad,
                Zaman.Yaz(kayit.Zaman),
                simgeSirasi: -1,
                Renkler.ReferansAsagiYazi,
                hedefYol: kayit.ArsivYolu,
                tamMetin: kayit.ArsivYolu);
        }

        // KAYIP/BOZUK KAYIT SESSIZCE YUTULMAZ (CLAUDE.md 3): satiri
        // gosteremiyoruz ama VARLIGINI soyluyoruz - yoksa kullanici o
        // versiyonun hic olmadigini sanir.
        if (durum.BozukSatir > 0)
        {
            Aciklama(
                liste,
                $"{durum.BozukSatir} kayıt bozuk ya da arşiv kopyası kayıp",
                "!");
        }
    }

    /// <summary>
    /// Cift tik: versiyonu ACAR - arsivdeki dosyayi DOGRUDAN.
    ///
    /// HER TUR ACILIR; ama VERSIYON ARTIK YALNIZ O DOSYA (Erkan'in karari,
    /// 01.09.2026) ve bu, ACARKEN soylenmesi gereken bir sey:
    ///
    /// Arsiv klasorunde parcalar yoksa SOLIDWORKS onlari ebeveynin yaninda
    /// bulamaz (CLAUDE.md 5) ve montaji BUGUNKU parcalarla acar. Kullanici
    /// gecmise baktigini sanar; hicbir sey patlamaz, hicbir sebep gorunmez -
    /// tam da CLAUDE.md 3'un yasakladigi hal. O yuzden durum cubuguna bir
    /// cumle daha yaziliyor.
    ///
    /// SART DISKTEN OKUNUYOR, bilerek: Erkan'in elindeki ESKI arsivlerde
    /// cocuklar YANINDA duruyor ve orada bu cumle CIKMAMALI. Sabit bir tarih
    /// ya da bayrak yerine gercege bakiliyor - bayat uyari, fazla uyaridan
    /// tehlikelidir (CLAUDE.md 6).
    ///
    /// Kopyalar diskte SALT-OKUNUR durur; SOLIDWORKS [Read-Only] acar ve
    /// gecmisin ustune kaza ile kaydedilemez (CLAUDE.md 1a).
    /// </summary>
    /// <returns>Durum cubuguna yazilacak cumle.</returns>
    internal static string Ac(
        System.Windows.Forms.IWin32Window sahip,
        string? arsivYolu,
        string? kok = null,
        ReferansIndeksi? indeks = null)
    {
        if (arsivYolu is null)
        {
            return "Bu satırda açılacak bir versiyon yok.";
        }

        // ============ ONCE SAHNE, SONRA AC ============
        //
        // Arsiv kopyasi IZOLE bir klasorde duruyor ve montajin icindeki
        // cocuk yollari KOMSULUGA bagli (ciplak ad = "yanima bak"). Oradan
        // acinca SOLIDWORKS hicbir parcayi bulamiyor ve montaj BOS geliyor
        // (Erkan, 02.09.2026 - gercek dosyayla olculdu).
        //
        // Cozum yolu YAMALAMAK degil DUZENI KURMAK: gercek PDM'ler de bir
        // versiyonu arsivden acmaz, once kendi normal yerine yazar
        // (SurumSahnesi'nde ayrintili). Sahne kurulamazsa eski davranisa
        // dusulur ve SEBEBI yazilir - sessizce bos dosya actirmaktansa
        // (CLAUDE.md 3).
        DosyaTuru tur = DosyaTurleri.Tani(arsivYolu);
        bool cocukluTur = tur == DosyaTuru.Montaj || tur == DosyaTuru.TeknikResim;
        SahneSonucu? sahne = null;
        bool oGunku = false;

        if (cocukluTur && !Surumler.YanindaCocukVarMi(arsivYolu))
        {
            string? orijinal = SurumSahnesi.OrijinalYol(kok, arsivYolu);

            // ============ O GUNKU HAL MI, BUGUNKU PARCALAR MI ============
            //
            // Bilesim kaydi varsa bu versiyonun O GUN hangi parcalari hangi
            // versiyonda kullandigi BILINIYOR (Surumler.BilesimYaz). Gercek
            // PDM'ler de tam bu ikisini ayirir: "referenced version" ve
            // "latest". Karari KULLANICI verir - ikisi de mesru:
            //   o gunku hal  = versiyonun gercekten neydi
            //   bugunku      = bugunku parcalar o gunku montajda nasil durur
            //
            // Kayit yoksa soru da yok: secenek zaten tek (CLAUDE.md 6 -
            // kutuda yalnizca kararin gerektirdigi kadari durur).
            BilesimDurumu bilesim = Surumler.BilesimOku(arsivYolu);

            if (bilesim.Kullanilabilir)
            {
                switch (Sor(sahip, bilesim.Ogeler.Count))
                {
                    case SahneSecimi.Iptal:
                        return "Versiyon açma iptal edildi.";

                    case SahneSecimi.OGunku:
                        oGunku = true;
                        sahne = SurumSahnesi.KurBilesimle(kok, arsivYolu, orijinal, bilesim);
                        break;

                    default:
                        sahne = SurumSahnesi.Kur(kok, arsivYolu, orijinal, indeks);
                        break;
                }
            }
            else
            {
                sahne = SurumSahnesi.Kur(kok, arsivYolu, orijinal, indeks);
            }
        }

        string acilacak = sahne?.AcilacakYol ?? arsivYolu;

        // Acma kalibi TEK KOPYA (CLAUDE.md 8); buraya yalnizca versiyona
        // ozel cumle ekleniyor.
        string cumle = DosyaAcici.YoluAc(sahip, acilacak);
        if (!cumle.EndsWith("açılıyor…", StringComparison.Ordinal))
        {
            return cumle;
        }

        cumle += "  (salt-okunur arşiv kopyası — düzenlemek için: Enter ile bu versiyona dön)";

        if (sahne is null)
        {
            return cumle;
        }

        if (sahne.AcilacakYol is null)
        {
            // Sahne kurulamadi: dosya yine acildi ama BOS gelecek. Bunu
            // soylemek sart.
            return cumle + "  · parçaları yanına dizilemedi (" + sahne.Sebep
                + ") — montaj BOŞ açılabilir";
        }

        cumle += oGunku
            ? $"  · {sahne.Dizilen} parça yanına dizildi — O GÜNKÜ hâlleriyle"
            : $"  · {sahne.Dizilen} parça yanına dizildi — BUGÜNKÜ parçalarla";

        // O GUNKU denip bugunku parcayi dizmek sessiz bir yalan olurdu:
        // arsiv kopyasi elle silinmisse sayisi SOYLENIR (CLAUDE.md 3).
        if (oGunku && sahne.Bugunku > 0)
        {
            cumle += $" · {sahne.Bugunku} tanesi bugünkü hâliyle (o günkü kopyası yok)";
        }

        if (sahne.Atlanan.Count > 0)
        {
            cumle += $" · {sahne.Atlanan.Count} dizilemedi";
        }

        return cumle;
    }

    /// <summary>Versiyon acilirken hangi parcalarla acilacagi.</summary>
    private enum SahneSecimi
    {
        /// <summary>Bilesim kaydindaki versiyonlar - versiyonun gercek hali.</summary>
        OGunku,

        /// <summary>Diskteki bugunku parcalar.</summary>
        Bugunku,

        /// <summary>Acilmasin.</summary>
        Iptal,
    }

    /// <summary>
    /// "O gunku hal mi, bugunku parcalar mi" sorusu. YALNIZCA bilesim kaydi
    /// olan versiyonlarda cikar; kayitsizda secenek tek oldugu icin soru da
    /// sorulmaz.
    /// </summary>
    private static SahneSecimi Sor(System.Windows.Forms.IWin32Window sahip, int adet)
    {
        System.Windows.Forms.DialogResult cevap = System.Windows.Forms.MessageBox.Show(
            sahip,
            $"Bu versiyonun bileşimi kayıtlı: {adet} parça, o günkü hâlleriyle.\n\n"
            + "Evet\t— O GÜNKÜ parçalarla aç (versiyonun gerçek hâli)\n"
            + "Hayır\t— BUGÜNKÜ parçalarla aç\n"
            + "İptal\t— açma\n\n"
            + "Her iki durumda da kopyalar salt-okunur; bugünkü dosyalarınıza dokunulmaz.",
            "Versiyon nasıl açılsın?",
            System.Windows.Forms.MessageBoxButtons.YesNoCancel,
            System.Windows.Forms.MessageBoxIcon.Question,
            System.Windows.Forms.MessageBoxDefaultButton.Button1);

        return cevap switch
        {
            System.Windows.Forms.DialogResult.Yes => SahneSecimi.OGunku,
            System.Windows.Forms.DialogResult.No => SahneSecimi.Bugunku,
            _ => SahneSecimi.Iptal,
        };
    }

    /// <summary>Cizilen siradaki versiyon kaydi; sira bir versiyon satiri degilse null.</summary>
    internal SurumKaydi? Kayit(int sira)
        => sira >= 0 && sira < _sonListe.Count ? _sonListe[sira] : null;

    private static void Aciklama(ReferansListesi liste, string cumle, string rol)
        => liste.Ekle(cumle, rol, -1, Renkler.UstBilgiYazi, hedefYol: null, tamMetin: cumle);
}
