using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>Bir dosyanin arsivlenmis TEK versiyonu.</summary>
/// <param name="No">Versiyon numarasi; 0'dan baslar.</param>
/// <param name="Zaman">Arsivlendigi an.</param>
/// <param name="Not">Kullanicinin notu; bos olabilir.</param>
/// <param name="ArsivYolu">Arsivdeki kopyanin tam yolu.</param>
/// <param name="Boyut">Kopyanin bayt boyutu.</param>
public sealed record SurumKaydi(
    int No,
    DateTime Zaman,
    string Not,
    string ArsivYolu,
    long Boyut);

/// <summary>
/// Bir dosyanin versiyon listesinin OKUNMUS hali.
///
/// NEDEN AYRI TIP (Cop.CopDurumu ile ayni sebep): "hic versiyon yok" ile
/// "kayit okunamadi" ayni sey DEGIL. Ikisini bos listeyle anlatmak,
/// kullaniciya versiyonlarinin kayboldugunu dusundurur (CLAUDE.md 3).
/// </summary>
/// <param name="Ogeler">Versiyonlar, EN YENI basta.</param>
/// <param name="Okunamadi">Kayit okunamadiysa sebebi; okunduysa null.</param>
/// <param name="BozukSatir">Cozulemeyen ya da arsiv dosyasi kayip kayit sayisi.</param>
/// <param name="EnBuyukNo">
/// Kayit dosyasindaki EN BUYUK numara - dosyasi kayip/bozuk satirlar DAHIL;
/// hic numara yoksa -1. Yeni numara BUNDAN turetilir: gosterilebilenlerin
/// en buyugunden turetmek, dosyasi bir an okunamayan en yeni kaydin
/// numarasini CALDIRIYORDU (ayni No'dan iki satir, sonra boyut uyusmazligi -
/// Erkan'da olculdu, 31.08.2026).
/// </param>
public sealed record SurumDurumu(
    IReadOnlyList<SurumKaydi> Ogeler,
    string? Okunamadi,
    int BozukSatir,
    int EnBuyukNo)
{
    /// <summary>Kayit okunabildi mi. false ise SAYI GOSTERILMEZ.</summary>
    public bool Guvenilir => Okunamadi is null;
}

/// <summary>
/// VERSIYON ARSIVI - "ayni ad, tek dosya + gizli arsiv" (Erkan'in karari,
/// 31.08.2026). Dosya hep ayni adla yerinde durur; eski icerikler kokun
/// icindeki bu klasorde TAM KOPYA olarak saklanir. Parcayi eski versiyona
/// dondurmek montaj dosyasina HIC dokunmaz - ad degismedigi icin referanslar
/// kendiliginden saglam kalir; onarim gerektirmeyen tek versiyon modeli bu.
///
/// Cop kutusuyla (Cop.cs) ayni kalip: ayni diskte gizli klasor, duz metin
/// kayit (bozulursa ELLE onarilabilir, dosyalar zaten yerinde), agacta
/// GOSTERILMEZ ve TARANMAZ (Cop.KlasorAdi'nin dislandigi dort noktada bu da
/// dislanir).
///
/// Yapisi:
///   kok\.SwPdmSurum\&lt;goreli klasor&gt;\&lt;dosyanin TAM adi&gt;\v3.SLDPRT   icerik
///   kok\.SwPdmSurum\&lt;goreli klasor&gt;\&lt;dosyanin TAM adi&gt;\kayit.txt   no·zaman·not
/// Klasor adi dosyanin TAM adi: "X.SLDPRT" ile "X.SLDDRW" cakisamaz.
///
/// IKI KURAL (CLAUDE.md 1a/3):
///   - MEVCUT DOSYA v0 SAYILIR: ilk "versiyon olustur" o anki icerigi v0
///     olarak arsivler; onceden hicbir hazirlik gerekmez.
///   - DONUS DE BIR VERSIYONDUR: eski bir versiyona donmeden once bugunku
///     icerik OTOMATIK arsivlenir. Boylece hicbir icerik hicbir islemle
///     kaybolmaz - donusten de geri donulur.
/// </summary>
public static partial class Surumler
{
    /// <summary>Arsiv klasorunun adi. Agacta GOSTERILMEZ, taranmaz.</summary>
    public const string KlasorAdi = ".SwPdmSurum";

    private const string KayitAdi = "kayit.txt";

    /// <summary>
    /// Bir dosyanin versiyonlari - EN YENI basta. Kayitsiz dosyada bos ve
    /// guvenilir doner: "hic versiyonlanmamis" dogru bir cevaptir.
    /// </summary>
    public static SurumDurumu Listele(string kok, string yol)
    {
        string? yuva = Yuvasi(kok, yol);
        if (yuva is null)
        {
            return new SurumDurumu([], "Dosya açık kökün altında değil.", 0, -1);
        }

        string kayitYolu = WindowsYolu.Birlestir(yuva, KayitAdi);
        if (!File.Exists(kayitYolu))
        {
            return new SurumDurumu([], null, 0, -1);
        }

        string[] satirlar;
        try
        {
            satirlar = File.ReadAllLines(kayitYolu);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return new SurumDurumu([], "Versiyon kaydı okunamadı: " + hata.Message, 0, -1);
        }

        // AYNI No'DAN IKI SATIR OLABILIR (gecmis bir numara carpismasi):
        // EN SON YAZILAN satir esas alinir, oncekiler bozuk SAYILIR -
        // ikisini birden gostermek ayni dosyaya iki farkli boyut/tarih
        // yakistirir ve donusu kilitler (Erkan'da olculdu, 31.08.2026).
        var noyaGore = new Dictionary<int, SurumKaydi>();
        int bozuk = 0;
        int enBuyukNo = -1;

        foreach (string satir in satirlar)
        {
            if (satir.Length == 0)
            {
                continue;
            }

            SurumKaydi? kayit = SatiriCoz(yuva, satir, out int satirNo);
            if (satirNo > enBuyukNo)
            {
                enBuyukNo = satirNo;
            }

            // Arsiv dosyasi kayipsa kayit GOSTERILMEZ ama SAYILIR - sessizce
            // yutmak, kullaniciya "o versiyon hic olmadi" dedirtir (CLAUDE.md 3).
            if (kayit is null || !File.Exists(kayit.ArsivYolu))
            {
                bozuk++;
                continue;
            }

            if (noyaGore.ContainsKey(kayit.No))
            {
                bozuk++;   // onceki satir gecersiz sayildi ama GIZLENMEDI
            }

            noyaGore[kayit.No] = kayit;
        }

        var ogeler = new List<SurumKaydi>(noyaGore.Values);
        ogeler.Sort((a, b) => b.No.CompareTo(a.No));
        return new SurumDurumu(ogeler, null, bozuk, enBuyukNo);
    }

    /// <summary>
    /// O ANKI icerigi yeni versiyon olarak arsivler. Ilk cagri v0'i yaratir
    /// (mevcut dosya v0 sayilir - Erkan'in kurali); sonrakiler vN+1.
    /// KOPYALA -> BOYUT DOGRULA -> KAYDA YAZ; kayit yazilamazsa kopya geri
    /// silinir ki listede olmayan bir kopya kalmasin.
    /// </summary>
    /// <param name="no">Olusan versiyonun numarasi; islem olmadiysa -1.</param>
    public static IslemRaporu Olustur(string kok, string yol, string not, out int no)
    {
        no = -1;

        if (!File.Exists(yol))
        {
            return new IslemRaporu(IslemSonucu.Bulunamadi, null, "Bulunamadı: " + yol);
        }

        string? yuva = Yuvasi(kok, yol);
        if (yuva is null)
        {
            return new IslemRaporu(
                IslemSonucu.Bilinmeyen, null, "Kök dışındaki dosya versiyonlanamaz.");
        }

        SurumDurumu durum = Listele(kok, yol);
        if (!durum.Guvenilir)
        {
            // Kayit okunamiyorsa numara da bilinemez; ustune yazmak eski
            // versiyonlari cignerdi (CLAUDE.md 1a).
            return new IslemRaporu(IslemSonucu.Bilinmeyen, null, durum.Okunamadi);
        }

        int yeniNo = durum.EnBuyukNo + 1;

        // ============ VERSIYON KENDI KENDINE YETER ============
        //
        // Arsiv artik bir KLASOR: "v3\" icinde asil dosya GERCEK ADIYLA ve
        // o gunku COCUKLARI yaninda duruyor. Erkan'da olculdu (31.08.2026):
        // tek basina duran bir montaj arsivden ACILMIYOR - SOLIDWORKS once
        // ebeveynin yanina bakiyor (CLAUDE.md 5) ve parcalari bulamiyor.
        // Parcada sorun gorulmemesinin sebebi de buydu: parcanin cocugu yok,
        // yani zaten kendi kendine yetiyordu. Artik montaj/teknik resim de
        // parcayla AYNI yoldan aciliyor; ayrik dal yok.
        string klasor = WindowsYolu.Birlestir(yuva, ArsivKlasoru(yeniNo));
        string arsiv = WindowsYolu.Birlestir(klasor, WindowsYolu.DosyaAdi(yol));

        CocukKumesi cocuklar = Cocuklari(yol);

        long boyut;
        try
        {
            Directory.CreateDirectory(klasor);

            boyut = Kopyala(yol, arsiv);
            foreach (string cocuk in cocuklar.Yollar)
            {
                // Cocuklar DUZ, gercek adlariyla yan yana: komsuluk kurali
                // ancak boyle isler. Ayni ad iki klasorden geliyorsa ilki
                // kalir - SOLIDWORKS'un actigi da o olurdu.
                string hedef = WindowsYolu.Birlestir(klasor, WindowsYolu.DosyaAdi(cocuk));
                if (!File.Exists(hedef))
                {
                    Kopyala(cocuk, hedef);
                }
            }
        }
        catch (Exception hata)
        {
            // HEPSI YA DA HICBIRI: yarim bir versiyon, "don" dendiginde
            // dosyanin yerine gecer (CLAUDE.md 1a). Klasorun tamami silinir.
            KlasoruTemizlemeyeCalis(klasor);
            return IslemSonuclari.HatayiCevir(hata);
        }

        try
        {
            File.AppendAllText(
                WindowsYolu.Birlestir(yuva, KayitAdi),
                SatirYap(yeniNo, DateTime.Now, not, boyut, WindowsYolu.DosyaAdi(yol)));
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Kayitta olmayan kopya, Listele'de gorunmez = SESSIZ KAYIP olur.
            KlasoruTemizlemeyeCalis(klasor);
            return new IslemRaporu(
                IslemSonucu.Bilinmeyen, null,
                "Versiyon kaydı yazılamadı — arşiv geri alındı: " + hata.Message);
        }

        // ARSIV KOPYASI SALT-OKUNUR (CLAUDE.md 1a): kullanici versiyonu
        // cift tikla ACABILIYOR; SOLIDWORKS salt-okunur dosyayi [Read-Only]
        // acar ve kaza ile gecmisin ustune kaydedilemez. Kayittan SONRA
        // konuyor ki basarisizlik temizligi (File.Delete) engellenmesin.
        try
        {
            foreach (string kopya in Directory.GetFiles(klasor))
            {
                File.SetAttributes(kopya, FileAttributes.ReadOnly);
            }
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Oznitelik konamadiysa versiyon YINE GECERLI; koruma eksik
            // kaldi ama arsiv duruyor - islemi geri almak asiri olurdu.
        }

        no = yeniNo;

        // COZULEMEYEN COCUK SESSIZ GECILMEZ (CLAUDE.md 3): eksik cocukla
        // arsivlenen versiyon eksik acilir, kullanici bunu BILMELI.
        return new IslemRaporu(
            IslemSonucu.Tamam, arsiv,
            cocuklar.Cozulemeyen > 0
                ? $"{cocuklar.Cozulemeyen} referans bulunamadı — versiyon eksik olabilir"
                : null);
    }

    /// <summary>
    /// Iki dosya BIREBIR ayni mi - once boyut (ucuz), esitse bayt bayt.
    /// Okunamayan dosyada "farkli" denir: yanlis "ayni" cevabi, donusten
    /// once bugunku hali arsivlemeyi atlatir ve icerik kaybettirirdi.
    /// </summary>
    private static bool AyniIcerik(string a, string b)
    {
        try
        {
            var ba = new FileInfo(a);
            var bb = new FileInfo(b);
            if (!ba.Exists || !bb.Exists || ba.Length != bb.Length)
            {
                return false;
            }

            using FileStream sa = File.OpenRead(a);
            using FileStream sb = File.OpenRead(b);
            var ta = new byte[64 * 1024];
            var tb = new byte[64 * 1024];

            while (true)
            {
                int na = sa.ReadAtLeast(ta, ta.Length, throwOnEndOfStream: false);
                int nb = sb.ReadAtLeast(tb, tb.Length, throwOnEndOfStream: false);
                if (na != nb || !ta.AsSpan(0, na).SequenceEqual(tb.AsSpan(0, nb)))
                {
                    return false;
                }

                if (na == 0)
                {
                    return true;
                }
            }
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Bir dosyanin arsiv yuvasi; dosya kokun altinda degilse null.
    ///
    /// Goreli klasor DUZ ONEK KIRPMAYLA bulunuyor - WindowsYolu.Goreli
    /// KULLANILMAZ: o, dosyalarin ICINE yazilacak yollar icin tasarlandi ve
    /// ".." / "." susleri uretiyor (olculdu: kok icindeki dosyada bile
    /// "..\kok" dondu ve yuva kokun DISINA tasti).
    /// </summary>
    private static string? Yuvasi(string kok, string yol)
    {
        if (string.IsNullOrWhiteSpace(kok) || string.IsNullOrWhiteSpace(yol)
            || !WindowsYolu.AltindaMi(yol, kok))
        {
            return null;
        }

        string klasor = WindowsYolu.Klasor(yol);
        string goreli = klasor.Length > kok.Length
            ? klasor[kok.Length..].Trim(WindowsYolu.Ayirici, WindowsYolu.EgikAyirici)
            : string.Empty;

        string taban = WindowsYolu.Birlestir(kok, KlasorAdi);
        if (goreli.Length > 0)
        {
            taban = WindowsYolu.Birlestir(taban, goreli);
        }

        return WindowsYolu.Birlestir(taban, WindowsYolu.DosyaAdi(yol));
    }

    /// <summary>Bir versiyonun arsiv KLASORU: "v3".</summary>
    private static string ArsivKlasoru(int no)
        => "v" + no.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Kopyalar ve boyutunu DOGRULAR; tutmazsa atar (yarim kopya versiyon
    /// degildir - CLAUDE.md 1a). Doner: kopyanin boyutu.
    /// </summary>
    private static long Kopyala(string kaynak, string hedef)
    {
        File.Copy(kaynak, hedef, overwrite: false);

        long kopya = new FileInfo(hedef).Length;
        long asil = new FileInfo(kaynak).Length;
        if (kopya != asil)
        {
            throw new IOException(
                $"Kopya doğrulanamadı ({kopya} ≠ {asil} bayt): "
                + WindowsYolu.DosyaAdi(kaynak));
        }

        return kopya;
    }

    /// <summary>Yarim kalan bir versiyon klasorunu topluca kaldirir.</summary>
    private static void KlasoruTemizlemeyeCalis(string klasor)
    {
        try
        {
            if (!Directory.Exists(klasor))
            {
                return;
            }

            foreach (string dosya in Directory.GetFiles(klasor))
            {
                // Salt-okunur dosyayi Windows sildirmez (CLAUDE.md 4).
                File.SetAttributes(dosya, FileAttributes.Normal);
            }

            Directory.Delete(klasor, recursive: true);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Temizlik en iyi caba; asil hata zaten raporlaniyor.
        }
    }

    /// <summary>
    /// Kayit satiri: no·zaman·boyut·not, sekmeyle. Not icindeki sekme ve
    /// satir sonu bosluga cevrilir - biçim tek satir, elle onarilabilir.
    /// </summary>
    private static string SatirYap(int no, DateTime zaman, string not, long boyut, string asilAd)
        => no.ToString(CultureInfo.InvariantCulture) + '\t'
           + zaman.ToString("O", CultureInfo.InvariantCulture) + '\t'
           + boyut.ToString(CultureInfo.InvariantCulture) + '\t'
           + (not ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ')

           // BESINCI ALAN: ARSIVLENEN ASIL DOSYANIN ADI. Erkan'da olculdu
           // (31.08.2026): arsiv artik klasor ve icindeki kopya ARSIVLENDIGI
           // GUNKU adini koruyor; dosyanin adi degisince yuva yeni ada
           // tasiniyor ama asil dosya yuvanin adiyla ARANDIGI icin
           // bulunamiyor ve butun versiyonlar "yok" gorunuyordu. Ad artik
           // kayitta duruyor: yuva ne olursa olsun asil dosya bulunur.
           // Not 4. alanda KALDI - eski dort alanli satirlar okunmaya
           // devam ediyor (CLAUDE.md 3).
           + '\t' + (asilAd ?? string.Empty).Replace('\t', ' ')
           + Environment.NewLine;

    /// <param name="satirNo">Satirdan okunabilen numara; okunamadiysa -1.
    /// Dosyasi kayip satirin numarasi da SAYILIR - numara uretimi ona
    /// bakar, yoksa ayni numara ikinci kez dagitilir.</param>
    private static SurumKaydi? SatiriCoz(string yuva, string satir, out int satirNo)
    {
        satirNo = -1;

        string[] p = satir.Split('\t');
        if (p.Length < 4
            || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int no)
            || !DateTime.TryParse(
                p[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out DateTime zaman)
            || !long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out long boyut))
        {
            return null;
        }

        satirNo = no;

        // 5. alan varsa ASIL DOSYANIN ADI; eski dort alanli satirlarda yok.
        string? kayittakiAd = p.Length >= 5 && p[4].Length > 0 ? p[4] : null;

        string? arsiv = ArsivBul(yuva, no, kayittakiAd);
        return arsiv is null ? null : new SurumKaydi(no, zaman, p[3], arsiv, boyut);
    }

    /// <summary>
    /// Versiyonun ASIL dosyasini bulur. IKI DUZEN de okunur:
    ///   YENI: "v3\&lt;gercek ad&gt;" - cocuklariyla birlikte, kendi kendine yeter
    ///   ESKI: "v3.SLDPRT"            - tek dosya (31.08.2026 oncesi arsivler)
    /// Eskiyi okumaya devam etmek SART: kullanicinin elindeki versiyonlar o
    /// duzende ve onlari gormemek "versiyonlarim kayboldu" demek olurdu.
    /// </summary>
    /// <summary>
    /// Versiyonun ASIL dosyasini bulur. ADIM ADIM, en guveniliriden en zayifa:
    ///
    ///   1. KAYITTAKI AD ("v3\&lt;ad&gt;") - ad ve klasor ne olursa olsun tutar.
    ///   2. YUVANIN ADI - bu turdan onceki kayitlar (5. alan yok) ve dosya
    ///      henuz adlandirilmamis.
    ///   3. Klasorde TEK dosya - cocugu olmayan dosyalar.
    ///   4. Klasorde yuvanin UZANTISIYLA tek dosya - ERKAN'IN HALINI
    ///      KURTARAN MADDE: "X.SLDPRT" yuvasinin v0'inda bir .SLDPRT + bir
    ///      .SLDASM (in-context montaj) varsa asil olan .SLDPRT'dir.
    ///   5. ESKI DUZ DUZEN "v3.SLDPRT" - 31.08.2026 oncesi arsivler.
    ///
    /// Hicbiri tutmazsa null; kayit "bozuk" sayilir ve panel bunu SEBEBIYLE
    /// gosterir - bos liste asla "versiyon yok" demez (CLAUDE.md 3).
    /// </summary>
    private static string? ArsivBul(string yuva, int no, string? kayittakiAd = null)
    {
        try
        {
            string govde = "v" + no.ToString(CultureInfo.InvariantCulture);
            string klasor = WindowsYolu.Birlestir(yuva, govde);

            if (Directory.Exists(klasor))
            {
                if (kayittakiAd is not null)
                {
                    string kayitli = WindowsYolu.Birlestir(klasor, kayittakiAd);
                    if (File.Exists(kayitli))
                    {
                        return kayitli;
                    }
                }

                string yuvaAdi = WindowsYolu.DosyaAdi(yuva);
                string asil = WindowsYolu.Birlestir(klasor, yuvaAdi);
                if (File.Exists(asil))
                {
                    return asil;
                }

                string[] icerik = Directory.GetFiles(klasor);
                if (icerik.Length == 1)
                {
                    return icerik[0];
                }

                // Uzantiya gore tek aday: cocuklar genelde baska turdendir.
                string uzanti = WindowsYolu.Uzanti(yuvaAdi);
                if (uzanti.Length > 0)
                {
                    string? tekAday = null;
                    int sayi = 0;
                    foreach (string aday in icerik)
                    {
                        if (string.Equals(
                                WindowsYolu.Uzanti(aday), uzanti, StringComparison.OrdinalIgnoreCase))
                        {
                            tekAday = aday;
                            sayi++;
                        }
                    }

                    if (sayi == 1)
                    {
                        return tekAday;
                    }
                }
            }

            foreach (string aday in Directory.GetFiles(yuva))
            {
                string ad = WindowsYolu.DosyaAdi(aday);

                // "v1." on eki "v10.SLDPRT" ile eslesmez; uzantisiz dosya
                // icin duz "v1" esitligi de kabul.
                if (ad.StartsWith(govde + ".", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ad, govde, StringComparison.OrdinalIgnoreCase))
                {
                    return aday;
                }
            }
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Yuva okunamiyorsa kayit da "bozuk" sayilacak; sebep Listele'de.
        }

        return null;
    }

    /// <summary>Yuvadaki kayit dosyasinin tam yolu.</summary>
    private static string KayitYolu(string yuva) => WindowsYolu.Birlestir(yuva, KayitAdi);

    /// <summary>Satirin basindaki numara; okunamadiysa -1 (satir korunur).</summary>
    private static int SatirNosu(string satir)
    {
        string[] p = satir.Split('\t');
        return p.Length >= 1
               && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int no)
            ? no
            : -1;
    }

    private static void TemizlemeyeCalis(string yol)
    {
        try
        {
            if (File.Exists(yol))
            {
                // Windows salt-okunur dosyayi sildirmez; once oznitelik.
                File.SetAttributes(yol, FileAttributes.Normal);
                File.Delete(yol);
            }
        }
        catch (IOException)
        {
            // Temizlik en iyi caba; asil hata zaten raporlaniyor.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
