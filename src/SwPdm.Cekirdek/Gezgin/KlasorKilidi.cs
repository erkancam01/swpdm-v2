using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>
/// KILITLI KLASORLERIN O ANKI HALI - bir kez okunur, cok kez sorulur.
///
/// NEDEN ANLIK GORUNTU: kilidi her klasor dugumu icin diskten sormak, ag
/// surucusunde dugum basina bir gidis gelis demekti. Agac dolarken bir kez
/// okunuyor, sonra bellekten cevaplaniyor (CLAUDE.md 4: erisilemeyen yolda
/// bekleyen cagri bir ozelligi iki kez oldurdu).
/// </summary>
public sealed class KilitKumesi
{
    private readonly string? _kok;
    private readonly List<string> _tamYollar;

    internal KilitKumesi(string? kok, IReadOnlyList<string> goreliYollar)
    {
        _kok = kok;
        GoreliYollar = goreliYollar;
        _tamYollar = new List<string>(goreliYollar.Count);

        if (string.IsNullOrWhiteSpace(kok))
        {
            return;
        }

        foreach (string goreli in goreliYollar)
        {
            _tamYollar.Add(TamYol(kok, goreli));
        }
    }

    /// <summary>Hicbir sey kilitli degil.</summary>
    public static KilitKumesi Bos { get; } = new(null, []);

    /// <summary>Kilitli klasorlerin koke GORELI yollari.</summary>
    public IReadOnlyList<string> GoreliYollar { get; }

    /// <summary>Kac klasor kilitli.</summary>
    public int Sayi => GoreliYollar.Count;

    /// <summary>
    /// Bu yol kilitli mi - klasorun KENDISI ya da ALTINDA olmasi yeter.
    ///
    /// ALTINI DA SAYMAK SART: agac kilitli klasoru acmiyor ama referans
    /// paneli oradaki bir dosyayi satir olarak gosterebiliyor ve orada F2'ye
    /// basilabiliyor. Yalniz klasorun kendisine bakan bir kontrol o kapidan
    /// sizardi.
    /// </summary>
    public bool Kilitli(string? yol)
    {
        if (string.IsNullOrWhiteSpace(yol))
        {
            return false;
        }

        foreach (string kilitli in _tamYollar)
        {
            if (WindowsYolu.AltindaMi(yol, kilitli))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Verilen yollardan KILITLI OLAN ILKI; hicbiri kilitli degilse null.
    ///
    /// NEDEN KUME HALINDE SORULUYOR: bir islem cogu zaman TEK dosyaya degil
    /// bir LISTEYE dokunuyor (tasima ciftleri, geri alma adiminin yollari).
    /// Cagiranin kendi dongusunu yazmasi ayni mantigin ikinci kopyasi olurdu
    /// (CLAUDE.md 8) - ve biri "altini da say" kuralini unutabilirdi.
    ///
    /// ILKINI DONDURUYOR, SAYIYI DEGIL: ekranda gosterilecek sey "hangi
    /// dosya yuzunden" (CLAUDE.md 3); sayi bir sey anlatmiyor.
    /// </summary>
    public string? IlkKilitli(IEnumerable<string>? yollar)
    {
        if (yollar is null)
        {
            return null;
        }

        foreach (string yol in yollar)
        {
            if (Kilitli(yol))
            {
                return yol;
            }
        }

        return null;
    }

    /// <summary>Bu klasorun KENDISI kilitli mi (altinda olmak saymaz).</summary>
    public bool KendisiKilitli(string? klasorYolu)
    {
        if (string.IsNullOrWhiteSpace(klasorYolu))
        {
            return false;
        }

        foreach (string kilitli in _tamYollar)
        {
            if (string.Equals(klasorYolu, kilitli, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Kok bilgisi kaybolmasin diye; kilit degistirirken gerekiyor.</summary>
    internal string? Kok => _kok;

    /// <summary>
    /// Goreli yolu koke ekler. Kaydedilen yol ".\\Is1" gibi bir NOKTAYLA
    /// basliyor (WindowsYolu.Goreli boyle yaziyor); duz birlestirmek "."
    /// adinda bir parca uretir ve eslesme kaybolurdu. Cozme tek kapidan
    /// geciyor (CLAUDE.md 8).
    /// </summary>
    internal static string TamYol(string kok, string goreli)
        => WindowsYolu.TabandanCoz(kok, goreli) ?? kok;
}

/// <summary>
/// KLASOR KILIDI - "bitmis isler acilmasin" (Erkan, 31.08.2026: "deneme
/// yaparken yanlislikla bitmis islerin icerigini degistirdim. Bitmis isleri
/// tikleyeyim, kilitleyeyim, ondan sonra rahat rahat calisayim").
///
/// NE YAPAR: kilitli klasor agacta GORUNUR ama ACILMAZ - "+" cikmaz. Icindeki
/// dosya agacta hic gorunmedigi icin secilemez, yani kazayla dokunulamaz.
/// Klasorun KENDISI de korunur (ad degistir/sil/tasi reddedilir), yoksa
/// koruma sizar: klasoru yanlislikla suruklemek de bitmis isi bozmaktir.
///
/// NE YAPMAZ - VE BU DURUSTCE SOYLENMELI (CLAUDE.md 3):
///   - GUVENLIK DEGIL, KAZA KORUMASI. Sifre yok; uygulamadan herkes acabilir.
///   - GEZGIN'I BAGLAMAZ. Dosya Windows'tan ya da SOLIDWORKS'un kendi "Ac"
///     penceresinden yine acilir. Kotu niyete karsi tek gercek kalkan
///     Windows klasor izni - onu uygulama YAZMAZ (yonetici hakki ister ve
///     yanlis yazilirsa klasore erisim tumden kaybedilir).
///   - DOSYA OZNITELIGINE DOKUNMAZ. Salt-okunur biti kullanicinin; biz
///     koymadigimiz bir seyi kaldirmayalim diye hic ellenmiyor.
///
/// KILIT NEYI DURDURMAZ (bilerek):
///   - REFERANS TARAMASI. Bitmis montaj hala parcalari kullaniyor; indeksten
///     duserse panel "bu parcayi kimse kullanmiyor" der ve SAGLAM DOSYA
///     SILDIRIR. Kilit gozden saklar, GERCEGI degil.
///   - REFERANS ONARIMI. Canli bir parcanin adi degisince kilitli montajin
///     icindeki yol da duzeltilir; duzeltilmezse bitmis is SESSIZCE kirilir
///     (CLAUDE.md 1a).
///
/// NEREDE DURUYOR: "&lt;kok&gt;\.SwPdmKilit\kilitler.txt", satir basina bir
/// GORELI klasor yolu. Goreli, cunku kok tasinsa ya da baska bir harften
/// baglansa kilitler gecerli kalmali. Diskte olmayan satir sessizce yok
/// sayilir ve liste yeniden yazilirken duser.
///
/// SILMEK (CLAUDE.md 1b): bu dosyayi sil + AgacIslemleri'nden bir satir +
/// AgacDoldurucu'daki tek "if". Kullanicinin dosyalarinda hicbir iz kalmaz;
/// ".SwPdmKilit" klasoru elle silinince her sey acilir.
/// </summary>
public static class KlasorKilidi
{
    /// <summary>Kilit listesinin durdugu klasor (kokun icinde).</summary>
    public const string KlasorAdi = ".SwPdmKilit";

    private const string KayitAdi = "kilitler.txt";

    /// <summary>
    /// Kilitli klasorleri diskten okur. Kok yoksa ya da liste okunamazsa
    /// BOS doner - okunamayan bir liste yuzunden butun agaci kilitlemek ya
    /// da acmak, ikisi de yanlis olurdu; bos liste "kilit yok" demek ve
    /// kullanici kilidini yeniden koyabilir.
    /// </summary>
    public static KilitKumesi Oku(string? kok)
    {
        if (string.IsNullOrWhiteSpace(kok))
        {
            return KilitKumesi.Bos;
        }

        string kayit = KayitYolu(kok);
        var goreliler = new List<string>();

        try
        {
            if (!File.Exists(kayit))
            {
                return new KilitKumesi(kok, goreliler);
            }

            foreach (string satir in File.ReadAllLines(kayit))
            {
                string temiz = satir.Trim();
                if (temiz.Length == 0 || goreliler.Contains(temiz, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                // DISKTE OLMAYAN SATIR ATLANIR: klasor Gezgin'den silinmis
                // ya da tasinmis olabilir. Kaydi burada silmiyoruz - okuma
                // yazmaz (CLAUDE.md 1a); liste bir sonraki degisiklikte
                // kendiliginden temizlenir.
                if (Directory.Exists(KilitKumesi.TamYol(kok, temiz)))
                {
                    goreliler.Add(temiz);
                }
            }
        }
        catch (Exception hata) when (IslemSonuclari.DiskHatasi(hata))
        {
            return KilitKumesi.Bos;
        }

        return new KilitKumesi(kok, goreliler);
    }

    /// <summary>
    /// Verilen klasorleri kilitler ya da acar. Kokun ALTINDA olmayan klasor
    /// ATLANIR (kilit listesi koke goreli; disaridaki bir klasor yazilamaz).
    /// </summary>
    /// <returns>Kac klasorun durumu degisti; sebep doluysa atlananlar var.</returns>
    public static IslemRaporu Degistir(
        string? kok, IReadOnlyList<string>? klasorler, bool kilitle)
    {
        if (string.IsNullOrWhiteSpace(kok))
        {
            return new IslemRaporu(IslemSonucu.Bilinmeyen, null, "Önce bir klasör açın.");
        }

        if (klasorler is null || klasorler.Count == 0)
        {
            return new IslemRaporu(IslemSonucu.Bulunamadi, null, "Klasör seçilmedi.");
        }

        KilitKumesi suanki = Oku(kok);
        var yeni = new List<string>(suanki.GoreliYollar);
        int degisen = 0;
        var atlanan = new List<string>();

        foreach (string klasor in klasorler)
        {
            if (!WindowsYolu.AltindaMi(klasor, kok)
                || string.Equals(klasor, kok, StringComparison.OrdinalIgnoreCase))
            {
                // KOKUN KENDISI KILITLENMEZ: kilitlenirse uygulama hicbir
                // sey gosteremez ve kullanici kilidi kaldiracak satiri da
                // bulamaz - kendi kendini kilitleyen bir kutu olurdu.
                atlanan.Add(WindowsYolu.DosyaAdi(klasor));
                continue;
            }

            string? goreli = WindowsYolu.Goreli(kok, klasor);
            if (goreli is null || goreli.Length == 0)
            {
                atlanan.Add(WindowsYolu.DosyaAdi(klasor));
                continue;
            }

            int sira = Sirasi(yeni, goreli);
            if (kilitle && sira < 0)
            {
                yeni.Add(goreli);
                degisen++;
            }
            else if (!kilitle && sira >= 0)
            {
                yeni.RemoveAt(sira);
                degisen++;
            }
        }

        if (degisen == 0)
        {
            return new IslemRaporu(
                IslemSonucu.Atlandi, null,
                atlanan.Count > 0
                    ? "Kilitlenemedi (kökün altında değil): " + string.Join(", ", atlanan)
                    : (kilitle ? "Zaten kilitliydi." : "Zaten açıktı."));
        }

        string? hata = Yaz(kok, yeni);
        if (hata is not null)
        {
            return new IslemRaporu(IslemSonucu.Bilinmeyen, null, "Kilit yazılamadı: " + hata);
        }

        return new IslemRaporu(
            IslemSonucu.Tamam, null,
            atlanan.Count > 0 ? "Atlananlar: " + string.Join(", ", atlanan) : null);
    }

    /// <summary>
    /// Yazan bir islem bu yola dokunabilir mi. Dokunamazsa EKRANDA
    /// gosterilecek bir sebep doner (CLAUDE.md 3).
    /// </summary>
    public static bool YazmayaKapaliMi(KilitKumesi? kilitler, string? yol, out string sebep)
    {
        if (kilitler is not null && kilitler.Kilitli(yol))
        {
            sebep = "Bu klasör kilitli — önce sağ tık ile kilidi kaldırın.";
            return true;
        }

        sebep = string.Empty;
        return false;
    }

    private static string KayitYolu(string kok)
        => WindowsYolu.Birlestir(WindowsYolu.Birlestir(kok, KlasorAdi), KayitAdi);

    private static int Sirasi(List<string> liste, string goreli)
    {
        for (int i = 0; i < liste.Count; i++)
        {
            if (string.Equals(liste[i], goreli, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Listeyi yazar. Once geciciye, sonra yerine - yarim yazma butun
    /// kilitleri kaybettirirdi (Surumler.KaydiYenidenYaz ile ayni gerekce).
    /// </summary>
    private static string? Yaz(string kok, List<string> goreliler)
    {
        string kayit = KayitYolu(kok);
        string gecici = kayit + ".yeni";

        try
        {
            Directory.CreateDirectory(WindowsYolu.Klasor(kayit));

            File.WriteAllText(
                gecici,
                goreliler.Count == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine, goreliler) + Environment.NewLine);

            if (File.Exists(kayit))
            {
                File.Replace(gecici, kayit, destinationBackupFileName: null);
            }
            else
            {
                File.Move(gecici, kayit);
            }

            return null;
        }
        catch (Exception hata) when (IslemSonuclari.DiskHatasi(hata))
        {
            try
            {
                File.Delete(gecici);
            }
            catch (Exception temizlik) when (IslemSonuclari.DiskHatasi(temizlik))
            {
                // Temizlik tutmazsa sonucu degistirmez.
            }

            return hata.Message;
        }
    }
}
