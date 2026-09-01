using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace SwPdm.Cekirdek;

/// <summary>Bir yama denemesinin sonucu.</summary>
/// <param name="Oldu">Yama yazildi VE okunarak dogrulandi mi.</param>
/// <param name="DegisenAkis">Kac akis degisti.</param>
/// <param name="DegisenDize">Kac yol yazisi degisti.</param>
/// <param name="Sebep">Olmadiysa SEBEP - bos donmek yasak (CLAUDE.md 3).</param>
/// <param name="KalanAkislar">
/// Eski adin HALA yazili oldugu akislar. Tam yamada BOS olmali; "yalniz
/// Header2" secildiginde dolu olmasi beklenir ve bu bir HATA DEGIL, bilgidir -
/// ama sessiz kalmak yalan olurdu (CLAUDE.md 3).
/// </param>
public sealed record YamaSonucu(
    bool Oldu,
    int DegisenAkis,
    int DegisenDize,
    string? Sebep,
    IReadOnlyList<string> KalanAkislar)
{
    /// <summary>Basarisiz sonuc.</summary>
    public static YamaSonucu Olmadi(string sebep) => new(false, 0, 0, sebep, []);
}

/// <summary>
/// SOLIDWORKS DOSYASININ ICINE YAZAR - yalnizca YAZILI YOLLARI degistirmek icin.
///
/// NEDEN GEREKLI: bir parcanin ADI degisince onu kullanan montaj/teknik resim
/// ESKI ADI arar ve bulamaz. Tasimada durum farkli - SOLIDWORKS once ebeveynin
/// yanina bakiyor (CLAUDE.md 5, olculdu) - ama ad degisiminde komsuluk kurali
/// da kurtarmiyor. Tek cozum ebeveynin icindeki yaziyi degistirmek.
///
/// ============ YERINDE YAMA: DOSYADA TEK BAYT KAYMAZ ============
///
/// Dosya YENIDEN KURULMUYOR. Sebebi olculdu (28.08.2026): akis zinciri
/// dosyanin ancak %86-94'unu kapsiyor. Basta ~700, akislar arasinda ~1700,
/// sonda ~3500-4300 bayt BILMEDIGIMIZ veri var. Onlari dogru yerine
/// koydugumuzu asla kanitlayamayiz; yeniden kurmak dosya bozardi (CLAUDE.md 1a).
///
/// Bunun yerine akisin VERI YUVASI ayni kaliyor:
///   - sikisikBoyut DEGISMIYOR  -> sonraki hicbir akis kaymiyor
///   - yeni deflate verisi yuvaya yaziliyor, artan yer SIFIRLANIYOR
///   - dosya BOYUTU bile ayni kaliyor
/// Boylece bilmedigimiz %13'e hic dokunulmuyor ve dosyadaki her ofset
/// gecerli kaliyor.
///
/// SIGMAZSA YAZILMAZ. Yeni sikisik veri eski yuvadan buyukse islem
/// REDDEDILIR - dosyayi buyutup zinciri kaydirmaktansa "yapamadim" demek
/// dogru (CLAUDE.md 3). Olculdu: Montaj1'in Header2 akisi 923 baytlik
/// yuvada, ad degisiminden sonra 880 bayt - rahat siginiyor.
///
/// ============ NE OLCULMEDI ============
///
/// Dosyanin ilk 4 bayti her kayitta degisiyor (ayni parcanin iki surumunde
/// farkli). SAGLAMA TOPLAMI OLABILIR. Sekiz standart varyant denendi
/// (CRC32/Adler32/bayt toplami, farkli baslangiclarla): sekiz dosyada da
/// 0/8 tuttu. Yani NE OLDUGU BILINMIYOR. Sagalama ise ve SOLIDWORKS onu
/// dogruluyorsa, yamalanan dosya REDDEDILIR.
///
/// Bu yuzden bu sinif TEK BASINA KULLANILMAZ: cagiran once bir KOPYA
/// uzerinde calisir, sonucu SOLIDWORKS'te acarak dogrular, ancak ondan
/// sonra asil dosyaya dokunur (CLAUDE.md 3: KOPYALA -> ONAR -> DOGRULA -> SIL).
/// </summary>
public static class SwYazici
{
    /// <summary>Referanslarin durdugu akis; "yalniz burasi" secenegi icin.</summary>
    public const string DogrudanAkis = "Header2";

    /// <summary>
    /// <paramref name="kaynak"/>'taki yazili yollarda gecen
    /// <paramref name="eskiAd"/> dosya adini <paramref name="yeniAd"/> yapar
    /// ve sonucu <paramref name="hedef"/>'e yazar. KAYNAGA DOKUNMAZ.
    /// </summary>
    /// <param name="yalnizDogrudan">
    /// true ise sadece "Header2"; false ise yol yazan BUTUN akislar.
    /// </param>
    public static YamaSonucu AdiDegistir(
        string kaynak, string hedef, string eskiAd, string yeniAd, bool yalnizDogrudan = false)
        => Degistir(kaynak, hedef, eskiAd, yeniAd, null, null, yalnizDogrudan);

    /// <summary>
    /// Dosya TASINDIGINDA yazili yolu yeni konuma cevirir.
    ///
    /// AD DEGISIMINDEN FARKI: orada dosya ebeveynin YANINDA kaliyordu ve
    /// yazili klasor bir ipucuydu. Burada dosya BASKA KLASORE gitti; artik
    /// yazili yolun gercekten dogru yeri gostermesi gerekiyor - komsuluk
    /// kurali kurtarmaz.
    ///
    /// Once EBEVEYNE GORELI yol denenir (kisa oldugu icin uzunluga daha sik
    /// sigar ve agac topluca tasinsa bile gecerli kalir), sigmazsa MUTLAK yol.
    /// Ikisi de sigmazsa yazilmaz ve sebebi soylenir.
    /// </summary>
    public static YamaSonucu YoluDegistir(
        string kaynak, string hedef, string eskiAd, string yeniTamYol, string ebeveynKlasoru,
        bool yalnizDogrudan = false)
        => Degistir(kaynak, hedef, eskiAd, null, yeniTamYol, ebeveynKlasoru, yalnizDogrudan);

    private static YamaSonucu Degistir(
        string kaynak, string hedef, string eskiAd, string? yeniAd,
        string? yeniTamYol, string? ebeveynKlasoru, bool yalnizDogrudan)
    {
        if (string.IsNullOrWhiteSpace(kaynak) || string.IsNullOrWhiteSpace(hedef))
        {
            return YamaSonucu.Olmadi("Kaynak ya da hedef yolu boş.");
        }

        string yeniAdi = yeniAd ?? WindowsYolu.DosyaAdi(yeniTamYol);
        if (string.IsNullOrWhiteSpace(eskiAd) || string.IsNullOrWhiteSpace(yeniAdi))
        {
            return YamaSonucu.Olmadi("Eski ya da yeni ad boş.");
        }

        if (yeniAd is not null
            && string.Equals(eskiAd, yeniAd, StringComparison.OrdinalIgnoreCase))
        {
            return YamaSonucu.Olmadi("Eski ve yeni ad aynı.");
        }

        return Isle(
            kaynak, hedef, yalnizDogrudan,
            acik => DizeleriDegistir(acik, eskiAd, yeniAd, yeniTamYol, ebeveynKlasoru),
            $"\"{eskiAd}\" bu dosyada yazılı değil; değiştirilecek bir şey yok.",
            hedefYol => yeniAd is not null
                ? Dogrula(hedefYol, eskiAd, yeniAd)
                : YoluDogrula(hedefYol, eskiAd, yeniTamYol!, ebeveynKlasoru));
    }

    /// <summary>
    /// HICBIR METNI DEGISTIRMEDEN akislari yeniden sikistirir - DENEY ICIN.
    ///
    /// NEDEN VAR: dosyanin ilk 4 bayti her kayitta degisiyor ve SAGLAMA
    /// TOPLAMI OLABILIR (sekiz standart varyant denendi, 0/8 tuttu; ne oldugu
    /// BILINMIYOR). Eger yamalanan bir dosya SOLIDWORKS'te acilmazsa iki ayri
    /// sebep olabilir: (a) yeniden sikistirma/dolgu kabul edilmiyor,
    /// (b) degistirilen metin yanlis. Bu metot (a)'yi TEK BASINA olculebilir
    /// kiliyor - CLAUDE.md 2: "belirtiyi tek sebebe baglama, iki ucuz
    /// hipotezden birini secmek yerine ikisini birden kapat".
    /// </summary>
    /// <param name="yalnizIceren">
    /// Verilirse YALNIZCA bu dosya adini yazan akislar yeniden sikistirilir.
    /// KONTROL DENEYININ ADILLIGI ICIN SART: gercek yama da yalnizca o
    /// akislara dokunuyor. Butun akislari sikistirmak adil bir kiyas olmazdi -
    /// ve olculdu: "PreviewPNG" zaten sikistirilmis bir PNG, yeniden deflate
    /// edilince 4 bayt BUYUYOR ve yuvaya sigmiyor.
    /// </param>
    public static YamaSonucu YenidenSikistir(string kaynak, string hedef, string? yalnizIceren = null)
        => Isle(
            kaynak, hedef, yalnizDogrudan: false,
            acik => Iceriyor(acik, yalnizIceren) ? (acik, 0, null) : (null, 0, null),
            "Yeniden sıkıştırılacak akış bulunamadı.",
            _ => (null, []));

    /// <summary>Tamponda bu dosya adini yazan bir yol var mi.</summary>
    private static bool Iceriyor(byte[] acik, string? dosyaAdi)
    {
        if (string.IsNullOrEmpty(dosyaAdi))
        {
            return true;
        }

        bool var = false;
        MfcDize.Tara(acik, bulgu =>
        {
            if (string.Equals(
                WindowsYolu.DosyaAdi(bulgu.Deger), dosyaAdi, StringComparison.OrdinalIgnoreCase))
            {
                var = true;
            }
        });

        return var;
    }

    private static YamaSonucu Isle(
        string kaynak,
        string hedef,
        bool yalnizDogrudan,
        Func<byte[], (byte[]? Yeni, int Sayi, string? Sebep)> donusturucu,
        string bosSebep,
        Func<string, (string? Kusur, IReadOnlyList<string> Kalan)> dogrulayici)
    {
        try
        {
            // KOPYALA once: asil dosyaya hicbir asamada dokunulmuyor.
            File.Copy(kaynak, hedef, overwrite: true);
            File.SetAttributes(hedef, FileAttributes.Normal);

            List<SwAkis> akislar = AkislariAl(hedef);
            if (akislar.Count == 0)
            {
                return YamaSonucu.Olmadi("Dosya SOLIDWORKS paketi gibi görünmüyor.");
            }

            (int akis, int dize, string? sebep) = Yamala(hedef, akislar, yalnizDogrudan, donusturucu);
            if (sebep is not null)
            {
                return YamaSonucu.Olmadi(sebep);
            }

            if (akis == 0)
            {
                // AYRIM SART (CLAUDE.md 3): "aranan ad hic gecmiyor" ile
                // "geciyor ama DEGISIKLIK GEREKMEDI" ayni sey degil.
                // Ikincisi BASARIDIR - Erkan'da ciplak adla yazilmis bir
                // referans yuzunden butun onarim reddediliyordu (31.08.2026).
                // Donusturucu bunu DEGISMEDI isaretiyle (Sayi < 0) soyluyor.
                if (dize < 0)
                {
                    // Yan dosya gereksiz: asilda degistirilecek bir sey yok.
                    try
                    {
                        File.Delete(hedef);
                    }
                    catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
                    {
                        // Kalirsa ReferansOnarimi'nin temizligi alir.
                    }

                    return new YamaSonucu(
                        true, 0, 0,
                        "yazılı yol zaten geçerli; dosyaya dokunulmadı", []);
                }

                return YamaSonucu.Olmadi(bosSebep);
            }

            // DOGRULAMA OKUYARAK YAPILIR. Yazma isleminin "oldum" demesi yetmez -
            // v1'de ReplaceViewModel true dondu ve HICBIR SEY YAPMADI
            // (CLAUDE.md 2). Sonuc diskten yeniden okunup karsilastiriliyor.
            (string? kusur, IReadOnlyList<string> kalan) = dogrulayici(hedef);
            return kusur is null
                ? new YamaSonucu(true, akis, dize, null, kalan)
                : YamaSonucu.Olmadi("Yazıldı ama doğrulama tutmadı: " + kusur);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                         or NotSupportedException or ArgumentException)
        {
            return YamaSonucu.Olmadi(hata.Message);
        }
    }

    private static List<SwAkis> AkislariAl(string yol)
    {
        using SwPaket? paket = SwPaket.Ac(yol);
        return paket is null ? [] : [.. paket.Akislar];
    }

    private static (int Akis, int Dize, string? Sebep) Yamala(
        string hedef,
        List<SwAkis> akislar,
        bool yalnizDogrudan,
        Func<byte[], (byte[]? Yeni, int Sayi, string? Sebep)> donusturucu)
    {
        int degisenAkis = 0;
        int degisenDize = 0;

        using var dosya = new FileStream(hedef, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        foreach (SwAkis a in akislar)
        {
            if (yalnizDogrudan && !string.Equals(a.Ad, DogrudanAkis, StringComparison.Ordinal))
            {
                continue;
            }

            byte[]? acik = Ac(dosya, a);
            if (acik is null)
            {
                continue;   // acilamayan akisa DOKUNULMAZ
            }

            (byte[]? yenisi, int sayi, string? sebep) = donusturucu(acik);
            if (sebep is not null)
            {
                return (0, 0, sebep);
            }

            if (sayi < 0)
            {
                // DEGISIKLIK GEREKMEDI: ad geciyor ama yazilacak deger
                // eskisinin ayni. Akisa dokunulmuyor, isaret yukari gidiyor.
                degisenDize = -1;
                continue;
            }

            if (yenisi is null)
            {
                continue;   // bu akista gecmiyor
            }

            byte[] sikisik = Sikistir(yenisi);
            if (sikisik.Length > a.SikisikBoyut)
            {
                // Buyutup zinciri kaydirmak yerine YAPMIYORUZ ve SOYLUYORUZ.
                return (0, 0,
                    $"\"{a.Ad}\" akışı yuvaya sığmıyor ({sikisik.Length} > {a.SikisikBoyut} bayt). "
                    + "Dosya büyütülmedi; hiçbir şey değiştirilmedi.");
            }

            Yaz(dosya, a, sikisik, yenisi.Length);
            degisenAkis++;
            degisenDize += sayi;
        }

        return (degisenAkis, degisenDize, null);
    }

    /// <summary>Akisin verisini okuyup acar; bozuksa null.</summary>
    private static byte[]? Ac(FileStream dosya, SwAkis a)
    {
        try
        {
            var sikisik = new byte[a.SikisikBoyut];
            dosya.Position = a.VeriBaslangici;
            dosya.ReadExactly(sikisik, 0, sikisik.Length);

            using var girdi = new MemoryStream(sikisik, writable: false);
            using var deflate = new DeflateStream(girdi, CompressionMode.Decompress);
            using var cikti = new MemoryStream(a.AcilmisBoyut);
            deflate.CopyTo(cikti);
            return cikti.ToArray();
        }
        catch (Exception hata) when (hata is InvalidDataException or IOException or EndOfStreamException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tampondaki yol dizelerinde dosya adini degistirir.
    /// Degisen yoksa null doner (o akisa dokunulmaz).
    /// </summary>
    private static (byte[]? Yeni, int Sayi, string? Sebep) DizeleriDegistir(
        byte[] acik, string eskiAd, string? yeniAd, string? yeniTamYol, string? ebeveynKlasoru)
    {
        int sayi = 0;
        var bulgular = new List<MfcBulgu>();
        MfcDize.Tara(acik, bulgu =>
        {
            if (string.Equals(WindowsYolu.DosyaAdi(bulgu.Deger), eskiAd, StringComparison.OrdinalIgnoreCase))
            {
                bulgular.Add(bulgu);
            }
        });

        if (bulgular.Count == 0)
        {
            return (null, 0, null);
        }

        using var cikti = new MemoryStream(acik.Length + (bulgular.Count * 64));
        int imlec = 0;

        foreach (MfcBulgu b in bulgular)
        {
            // UZUNLUK KORUNUYOR - olculdu: dize uzayinca SOLIDWORKS dosyayi
            // acmiyor (Erkan, 28.08.2026). Fark klasor kismindan karsilaniyor.
            string? yeniDeger = yeniAd is not null
                ? YazilacakYol.AdDegisimi(b.Deger, yeniAd)
                : YazilacakYol.Tasima(b.Deger, yeniTamYol!, ebeveynKlasoru);

            if (yeniDeger is null)
            {
                return (null, 0,
                    $"\"{yeniAd ?? yeniTamYol}\" için yazılı yolun uzunluğu korunamıyor "
                    + $"(eski yol {b.Deger.Length} karakter). Dosyaya dokunulmadı.");
            }

            // DEGISIKLIK GEREKMEDI: yeni deger eskisiyle ayni (ciplak ad -
            // bkz. YazilacakYol.Tasima). Dizeye dokunulmaz; sayilmaz da.
            if (string.Equals(yeniDeger, b.Deger, StringComparison.Ordinal))
            {
                continue;
            }

            byte[]? dize = MfcDize.Yaz(yeniDeger);
            if (dize is null)
            {
                // 254 karakteri asiyor: MFC kacis bicimi OLCULMEDI, tahminle
                // yazmak dosyayi bozar (CLAUDE.md 2).
                return (null, 0,
                    $"Yeni yol {yeniDeger.Length} karakter; 254'ten uzun yolun biçimi ölçülmedi.");
            }

            cikti.Write(acik, imlec, b.Baslangic - imlec);
            cikti.Write(dize, 0, dize.Length);
            imlec = b.Baslangic + b.ToplamBayt;
            sayi++;
        }

        if (sayi == 0)
        {
            // Bulgu vardi ama hicbiri degismedi (ciplak ad). Akis YENIDEN
            // YAZILMAZ - gereksiz yere sikistirip yuvaya sigdirmaya
            // calismanin anlami yok.
            return (null, -1, null);
        }

        cikti.Write(acik, imlec, acik.Length - imlec);
        return (cikti.ToArray(), sayi, null);
    }

    /// <summary>
    /// TASIMA dogrulamasi. Ad DEGISMEDIGI icin ada bakmak ise yaramaz;
    /// yazilan yol COZULUP gercek hedefe esit mi diye bakiliyor.
    /// Bir tanesi bile eski yeri gosteriyorsa dosya degistirilmez.
    /// </summary>
    private static (string? Kusur, IReadOnlyList<string> Kalan) YoluDogrula(
        string hedef, string eskiAd, string yeniTamYol, string? ebeveynKlasoru)
    {
        using SwPaket? paket = SwPaket.Ac(hedef);
        if (paket is null)
        {
            return ("yazıldıktan sonra dosya paket olarak okunamıyor", []);
        }

        string? beklenen = WindowsYolu.Cozumle(null, yeniTamYol);

        // ARANAN AD, YAZILAN ADIN KENDISI - eski ad DEGIL.
        //
        // OLCULDU (28.08.2026): burasi once yalnizca eskiAd'i ariyordu ve
        // tasimada bu dogruydu (dosya adi degismiyor, yalniz klasor degisiyor).
        // ELLE BAGLAMA'da ad da degisebiliyor; yamadan sonra dosyada eski ad
        // hic gecmedigi icin dogrulama HICBIR SEY bulamiyor ve tutmus bir yama
        // "tutmadi" diye reddediliyordu. Belirti yaniltici: yazma dogru,
        // olcum yanlis.
        string yeniAdi = WindowsYolu.DosyaAdi(yeniTamYol);
        bool bulundu = false;
        var kalanlar = new List<string>();

        foreach (SwAkis a in paket.Akislar)
        {
            byte[]? acik = paket.AkisiOku(a.Ad);
            if (acik is null)
            {
                continue;
            }

            bool sapan = false;
            MfcDize.Tara(acik, bulgu =>
            {
                string ad = WindowsYolu.DosyaAdi(bulgu.Deger);
                bool eski = string.Equals(ad, eskiAd, StringComparison.OrdinalIgnoreCase);
                bool yeni = string.Equals(ad, yeniAdi, StringComparison.OrdinalIgnoreCase);

                if (!eski && !yeni)
                {
                    return;
                }

                string? cozulen = WindowsYolu.Cozumle(ebeveynKlasoru, bulgu.Deger);
                if (string.Equals(cozulen, beklenen, StringComparison.OrdinalIgnoreCase))
                {
                    bulundu = true;
                    return;
                }

                // Hedefi GOSTERMIYOR. Iki ayri durum, ikisi ayni sey degil:
                //
                //   ESKI ad  -> BIZIM degistirmemiz gereken bir dizeydi ve
                //               geride kalmis. Kusur: SOLIDWORKS o adi
                //               aramaya devam eder (CLAUDE.md 5'te olculdu -
                //               ayni yol montajda DORT akista yazili).
                //
                //   YENI ad  -> belgenin BASKA bir referansi ayni adi
                //               tasiyor olabilir (iki klasorde ayni adli iki
                //               dosya). O bizim isimiz degil; kusur saymak
                //               MESRU bir baglamayi reddettirirdi - olculdu
                //               (28.08.2026, Wine): Montaj1'in Parça2
                //               referansi elle baglanirken, montajin kendi
                //               Parça1 referansi yuzunden reddedildi.
                if (eski)
                {
                    sapan = true;
                }
            });

            if (sapan)
            {
                kalanlar.Add(a.Ad);
            }
        }

        return bulundu && kalanlar.Count == 0
            ? (null, kalanlar)
            : ($"yazılan yol \"{yeniTamYol}\" hedefini göstermiyor", kalanlar);
    }

    private static byte[] Sikistir(byte[] veri)
    {
        using var cikti = new MemoryStream();
        using (var deflate = new DeflateStream(cikti, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(veri, 0, veri.Length);
        }

        return cikti.ToArray();
    }

    /// <summary>
    /// Yeni veriyi AYNI YUVAYA yazar ve artani sifirlar; baslkitaki "acilmis
    /// boyut" alanini gunceller. Yuva boyutu (sikisikBoyut) DEGISMEZ, yani
    /// dosyada hicbir sey kaymaz.
    /// </summary>
    private static void Yaz(FileStream dosya, SwAkis a, byte[] sikisik, int acilmisBoyut)
    {
        dosya.Position = a.VeriBaslangici;
        dosya.Write(sikisik, 0, sikisik.Length);

        int dolgu = a.SikisikBoyut - sikisik.Length;
        if (dolgu > 0)
        {
            dosya.Write(new byte[dolgu], 0, dolgu);
        }

        // Baslik: [sikisikBoyut][acilmisBoyut][adUzunlugu][ad]. Ad, nibble
        // takasli ASCII oldugu icin bayt sayisi = karakter sayisi.
        long baslik = a.VeriBaslangici - 12 - a.Ad.Length;
        var alan = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(alan, (uint)acilmisBoyut);
        dosya.Position = baslik + 4;
        dosya.Write(alan, 0, alan.Length);
    }

    /// <summary>
    /// Yazilani DISKTEN YENIDEN OKUYARAK dogrular.
    ///
    /// KUSUR: yeni ad hicbir akista yoksa - yani yazdigimizi sandigimiz sey
    /// diskte YOK. Bu bir basarisizliktir.
    ///
    /// KALAN: eski adin hala yazili oldugu akislar. Bu bir kusur DEGIL,
    /// olculmus bir gercek: OLCULDU (28.08.2026) - ayni yol "Header2" ile
    /// "Contents/Config-0-ModelHeader" akislarinda BIREBIR AYNI iceriklerle
    /// duruyor (ikisi de 2558 bayt acilmis). Yalnizca birini degistirmek
    /// otekini geride birakir; cagirana bu SOYLENIR.
    /// </summary>
    private static (string? Kusur, IReadOnlyList<string> Kalan) Dogrula(
        string hedef, string eskiAd, string yeniAd)
    {
        using SwPaket? paket = SwPaket.Ac(hedef);
        if (paket is null)
        {
            return ("yazıldıktan sonra dosya paket olarak okunamıyor", []);
        }

        bool yeniVar = false;
        var kalanlar = new List<string>();

        foreach (SwAkis a in paket.Akislar)
        {
            byte[]? acik = paket.AkisiOku(a.Ad);
            if (acik is null)
            {
                continue;
            }

            bool kaldi = false;
            MfcDize.Tara(acik, bulgu =>
            {
                string ad = WindowsYolu.DosyaAdi(bulgu.Deger);
                if (string.Equals(ad, eskiAd, StringComparison.OrdinalIgnoreCase))
                {
                    kaldi = true;
                }
                else if (string.Equals(ad, yeniAd, StringComparison.OrdinalIgnoreCase))
                {
                    yeniVar = true;
                }
            });

            if (kaldi)
            {
                kalanlar.Add(a.Ad);
            }
        }

        return yeniVar ? (null, kalanlar) : ($"\"{yeniAd}\" hiçbir akışta bulunamadı", kalanlar);
    }
}
