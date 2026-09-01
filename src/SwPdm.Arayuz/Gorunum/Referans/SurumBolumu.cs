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
    internal static string Ac(System.Windows.Forms.IWin32Window sahip, string? arsivYolu)
    {
        if (arsivYolu is null)
        {
            return "Bu satırda açılacak bir versiyon yok.";
        }

        // Acma kalibi TEK KOPYA (CLAUDE.md 8); buraya yalnizca versiyona
        // ozel cumle ekleniyor.
        string cumle = DosyaAcici.YoluAc(sahip, arsivYolu);
        if (!cumle.EndsWith("açılıyor…", StringComparison.Ordinal))
        {
            return cumle;
        }

        cumle += "  (salt-okunur arşiv kopyası — düzenlemek için: Enter ile bu versiyona dön)";

        // Yalnizca COCUGU OLABILECEK turlerde soruluyor: parcanin zaten
        // cocugu yok, orada bu cumle bos yere korkuturdu. Tur bilgisi
        // DosyaTurleri'nden turetiliyor (CLAUDE.md 1b).
        DosyaTuru tur = DosyaTurleri.Tani(arsivYolu);
        if ((tur == DosyaTuru.Montaj || tur == DosyaTuru.TeknikResim)
            && !Surumler.YanindaCocukVarMi(arsivYolu))
        {
            cumle += "  · parçaları yanında değil — BUGÜNKÜ parçalarla açılır";
        }

        return cumle;
    }

    /// <summary>Cizilen siradaki versiyon kaydi; sira bir versiyon satiri degilse null.</summary>
    internal SurumKaydi? Kayit(int sira)
        => sira >= 0 && sira < _sonListe.Count ? _sonListe[sira] : null;

    private static void Aciklama(ReferansListesi liste, string cumle, string rol)
        => liste.Ekle(cumle, rol, -1, Renkler.UstBilgiYazi, hedefYol: null, tamMetin: cumle);
}
