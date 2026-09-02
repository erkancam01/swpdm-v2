using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace SwPdm.Cekirdek;

/// <summary>Bir versiyonun BILESIMINDEKI tek cocuk.</summary>
/// <param name="GoreliYol">
/// Cocugun koke gore yolu - O GUNKU yeri. DONMUS bir bilgidir, dosya
/// sonradan tasinsa da DEGISMEZ: arsivdeki montajin ICINDE yazan yollar da
/// o gunku yollar ve sahne tam onlari kurmak zorunda (CLAUDE.md 5).
/// </param>
/// <param name="Karma">
/// Cocugun o gunku icerginin SHA-256'si; icerik deposundaki anahtar.
/// Eski (numara tabanli) kayitlarda null.
/// </param>
/// <param name="No">
/// ESKI BICIM: cocugun o gun gecerli versiyon numarasi. <paramref name="Karma"/>
/// varsa anlamsizdir (-1). 02.09.2026'dan onceki kayitlar icin duruyor.
/// </param>
public sealed record BilesimOgesi(string GoreliYol, string? Karma, int No);

/// <summary>
/// Bir versiyonun bilesim kaydinin OKUNMUS hali.
///
/// <see cref="SurumDurumu"/> ile ayni gerekce: "bilesim yok" ile "kayit
/// okunamadi" ayni sey degil. Ikisini bos listeyle anlatmak, kullaniciya
/// montajin bos oldugunu dusundurur (CLAUDE.md 3).
/// </summary>
/// <param name="Ogeler">Cocuklar; kayitsizsa bos.</param>
/// <param name="Var">Kayit dosyasi diskte VAR mi.</param>
/// <param name="Okunamadi">Okunamadiysa sebebi; okunduysa null.</param>
public sealed record BilesimDurumu(
    IReadOnlyList<BilesimOgesi> Ogeler,
    bool Var,
    string? Okunamadi)
{
    /// <summary>Bu versiyon "o gunku hali" ile acilabilir mi.</summary>
    public bool Kullanilabilir => Var && Okunamadi is null && Ogeler.Count > 0;
}

/// <summary>
/// VERSIYON BILESIMI - "o gunku hali" ile "bugunku parcalar" ayrimi.
///
/// ============ NEDEN (Erkan, 02.09.2026) ============
///
/// Sahne (<see cref="SurumSahnesi"/>) arsivdeki montaji acilir hale getirdi
/// ama BUGUNKU parcalarla aciyordu. Gecmise bakmanin anlami bu degil.
/// Montajin eski halini o gunku parcalariyla acabilmek icin o parcalarin o
/// gunku BAYTLARININ bir yerde durmasi sart - baska yolu yok.
///
/// ============ PARCANIN KENDI LISTESI KIRLETILMEZ ============
///
/// Ilk denemede her cocuk KENDI arsivinde versiyonlanmisti. Erkan:
/// "montajın versiyonunu oluştur dediğimde içindeki tüm parçaların
/// versiyonunu oluşturuyor" - hakli: parcanin VERSIYONLAR listesinde onun
/// olusturmadigi "(otomatik - ... ile)" satirlari cikiyordu ve
/// 01.09.2026'daki "versiyon = yalniz o dosya" karari GORUNURDE bozuluyordu.
///
/// Karari (02.09.2026): parcalarin o gunku icerigi GIZLI, ICERIK-ADRESLI bir
/// depoda durur:
///
///   kok\.SwPdmSurum\.icerik\&lt;ilk iki hane&gt;\&lt;sha256&gt;
///
/// Sonuc:
///   - Parcanin versiyon listesinde YALNIZCA kullanicinin olusturduklari var.
///   - Ayni icerik diskte BIR kere durur (anahtar icerigin kendisi).
///   - Depo yoldan BAGIMSIZ: parca tasinsa da, adi degisse de, hatta SILINSE
///     de o gunku hali bulunur. Yol tabanli aramanin bayatlama derdi yok.
///
/// ============ KAYITTAKI YOL NEDEN DONMUS ============
///
/// Sahne, cocugu kaydin gosterdigi GORELI YERE koyar. Arsivdeki montajin
/// icinde yazan yollar o gunku yollardir ve arsiv kopyasi SALT-OKUNUR,
/// yani hic onarilmaz. Dolayisiyla dogru yerlesim BUGUNKU yer degil O GUNKU
/// yerdir; kayittaki yol tasima ile GUNCELLENMEZ (bir tur once
/// guncelleniyordu - yanlisti, kaldirildi).
///
/// ============ KAYIT NEREDE ============
///
///   kok\.SwPdmSurum\&lt;goreli&gt;\&lt;ad&gt;\vN.cocuklar.txt
///
/// vN klasorunun ICINDE DEGIL, YANINDA - bilerek:
/// <see cref="Surumler.YanindaCocukVarMi"/> "vN klasorunde birden cok dosya
/// var mi" diye bakip eski (cocuklu) arsivleri taniyor; kaydi iceri koymak
/// her yeni versiyonu eski duzen sanmasina yol acardi (CLAUDE.md 1a).
///
/// Bicim: her satir "goreli yol &lt;sekme&gt; sha256". Duz metin, elle
/// okunabilir (kayit.txt ile ayni kalip). Ikinci alan SAYI ise eski
/// (versiyon numarali) kayittir ve oyle okunur (CLAUDE.md 1a).
/// </summary>
public static partial class Surumler
{
    private const string BilesimSonEki = ".cocuklar.txt";

    /// <summary>Icerik deposunun arsiv icindeki klasor adi.</summary>
    private const string IcerikKlasoru = ".icerik";

    /// <summary>SHA-256'nin onaltilik uzunlugu.</summary>
    private const int KarmaUzunlugu = 64;

    /// <summary>
    /// <paramref name="yol"/>'un <paramref name="no"/> numarali versiyonu icin
    /// bilesim kaydi yazar: her cocugun O ANKI icerigini gizli icerik
    /// deposuna koyar (ayni icerik zaten oradaysa dokunmaz) ve karmasini
    /// kaydeder. Cocuklarin KENDI versiyon listelerine hicbir sey eklenmez.
    /// </summary>
    /// <param name="kok">Acik kok klasor.</param>
    /// <param name="yol">Versiyonu olusturulan dosya (montaj/teknik resim).</param>
    /// <param name="no">Az once olusturulan versiyonun numarasi.</param>
    /// <param name="cocuklar">Dosyanin butun torunlari - MUTLAK yollar.</param>
    /// <param name="yeniSaklanan">Depoya YENI giren icerik sayisi.</param>
    /// <param name="atlanan">Kaydedilemeyen cocuklar ve sebepleri.</param>
    public static IslemRaporu BilesimYaz(
        string kok,
        string yol,
        int no,
        IEnumerable<string> cocuklar,
        out int yeniSaklanan,
        out IReadOnlyList<string> atlanan)
    {
        ArgumentNullException.ThrowIfNull(cocuklar);

        yeniSaklanan = 0;
        var sorunlar = new List<string>();
        atlanan = sorunlar;

        string? kayitYolu = BilesimYolu(kok, yol, no);
        if (kayitYolu is null)
        {
            return new IslemRaporu(
                IslemSonucu.Bilinmeyen, null, "Kök dışındaki dosyanın bileşimi yazılamaz.");
        }

        var satirlar = new List<string>();

        foreach (string cocuk in cocuklar)
        {
            string? goreli = SurumSahnesi.GoreliDuz(kok, cocuk);
            if (string.IsNullOrEmpty(goreli))
            {
                sorunlar.Add(WindowsYolu.DosyaAdi(cocuk) + " — kökün dışında");
                continue;
            }

            if (!File.Exists(cocuk))
            {
                sorunlar.Add(WindowsYolu.DosyaAdi(cocuk) + " — dosya bulunamadı");
                continue;
            }

            string? karma = IcerigiSakla(kok, cocuk, out bool yeni, out string? sebep);
            if (karma is null)
            {
                sorunlar.Add(WindowsYolu.DosyaAdi(cocuk) + " — " + sebep);
                continue;
            }

            if (yeni)
            {
                yeniSaklanan++;
            }

            satirlar.Add(goreli.Replace('\t', ' ') + '\t' + karma);
        }

        // BOS KAYIT DA YAZILIR: "bu versiyonun cocugu yoktu" ile "bilesim
        // hic tutulmadi" ayri seyler ve ayrimi dosyanin VARLIGI tasiyor.
        try
        {
            File.WriteAllLines(kayitYolu, satirlar);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return new IslemRaporu(
                IslemSonucu.Bilinmeyen, null, "Bileşim kaydı yazılamadı: " + hata.Message);
        }

        return new IslemRaporu(IslemSonucu.Tamam, kayitYolu, null);
    }

    /// <summary>
    /// Bir arsiv kopyasinin bilesim kaydi. Kayit yoksa
    /// <see cref="BilesimDurumu.Var"/> false doner - bu bir HATA DEGIL,
    /// "bu versiyon bilesim tutulmadan olusturuldu" demek.
    /// </summary>
    public static BilesimDurumu BilesimOku(string? arsivYolu)
    {
        string? kayitYolu = BilesimYolu(arsivYolu);
        if (kayitYolu is null || !File.Exists(kayitYolu))
        {
            return new BilesimDurumu([], false, null);
        }

        string[] satirlar;
        try
        {
            satirlar = File.ReadAllLines(kayitYolu);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return new BilesimDurumu([], true, "Bileşim kaydı okunamadı: " + hata.Message);
        }

        var ogeler = new List<BilesimOgesi>();
        foreach (string satir in satirlar)
        {
            if (satir.Length == 0)
            {
                continue;
            }

            int ayrac = satir.LastIndexOf('\t');
            if (ayrac <= 0)
            {
                continue;
            }

            string yol = satir[..ayrac];
            string deger = satir[(ayrac + 1)..];

            if (KarmaMi(deger))
            {
                ogeler.Add(new BilesimOgesi(yol, deger, -1));
            }
            else if (int.TryParse(
                         deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out int eskiNo))
            {
                // ESKI BICIM (02.09.2026 sabahi): versiyon numarasi.
                ogeler.Add(new BilesimOgesi(yol, null, eskiNo));
            }
        }

        return new BilesimDurumu(ogeler, true, null);
    }

    /// <summary>
    /// Bilesimdeki bir cocugun O GUNKU kopyasi; bulunamazsa null.
    ///
    /// Karma varsa icerik deposundan gelir - YOLDAN BAGIMSIZ, yani parca
    /// tasinmis, adi degismis ya da silinmis olsa bile bulunur. Eski
    /// (numara tabanli) kayitlarda parcanin kendi arsivine bakilir ve
    /// numara <see cref="Listele"/>'den aranir (arsiv adini/duzenini bilen
    /// zincir orada duruyor, ikinci kopyasi yazilmiyor - CLAUDE.md 8).
    /// </summary>
    public static string? BilesimArsivi(string kok, BilesimOgesi? oge)
    {
        if (oge is null || string.IsNullOrWhiteSpace(kok))
        {
            return null;
        }

        if (oge.Karma is string karma)
        {
            string blob = IcerikYolu(kok, karma);
            return File.Exists(blob) ? blob : null;
        }

        string gercek = WindowsYolu.Birlestir(kok, oge.GoreliYol);
        SurumDurumu durum = Listele(kok, gercek);
        if (!durum.Guvenilir)
        {
            return null;
        }

        foreach (SurumKaydi kayit in durum.Ogeler)
        {
            if (kayit.No == oge.No)
            {
                return kayit.ArsivYolu;
            }
        }

        return null;
    }

    /// <summary>
    /// Dosyanin icerigini depoya koyar ve karmasini doner; olmadiysa null
    /// ve <paramref name="sebep"/> dolu.
    ///
    /// AYNI ICERIK IKINCI KEZ YAZILMAZ: anahtar icerigin kendisi oldugu icin
    /// "zaten var mi" sorusu tek File.Exists. Degismeyen bir parca, kac
    /// montaj versiyonu olusursa olusun diskte BIR kere durur.
    /// </summary>
    private static string? IcerigiSakla(
        string kok, string dosya, out bool yeni, out string? sebep)
    {
        yeni = false;
        sebep = null;

        string karma;
        try
        {
            using FileStream akis = File.OpenRead(dosya);
            karma = Convert.ToHexString(SHA256.HashData(akis)).ToLowerInvariant();
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            sebep = "içeriği okunamadı: " + hata.Message;
            return null;
        }

        string hedef = IcerikYolu(kok, karma);
        if (File.Exists(hedef))
        {
            return karma;
        }

        try
        {
            Directory.CreateDirectory(WindowsYolu.Klasor(hedef));

            // GECICI ADA YAZ, SONRA YERINE KOY: yarim kalan bir kopya, adi
            // karmasi olan ama icerigi baska bir dosya olurdu - ve o hata
            // SESSIZ olurdu (CLAUDE.md 1a).
            string gecici = hedef + ".yarim";
            File.Copy(dosya, gecici, overwrite: true);

            if (new FileInfo(gecici).Length != new FileInfo(dosya).Length)
            {
                TemizlemeyeCalis(gecici);
                sebep = "kopya doğrulanamadı";
                return null;
            }

            File.Move(gecici, hedef, overwrite: true);
            new FileInfo(hedef).IsReadOnly = true;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            sebep = "içerik deposuna yazılamadı: " + hata.Message;
            return null;
        }

        yeni = true;
        return karma;
    }

    /// <summary>Icerik deposundaki bir karmanin tam yolu.</summary>
    private static string IcerikYolu(string kok, string karma)
    {
        string taban = WindowsYolu.Birlestir(
            WindowsYolu.Birlestir(kok, KlasorAdi), IcerikKlasoru);

        // IKI HANELIK DAGITIM: tek klasorde on binlerce dosya, Windows'ta
        // listelemeyi de silmeyi de yavaslatiyor (CLAUDE.md 4).
        return WindowsYolu.Birlestir(WindowsYolu.Birlestir(taban, karma[..2]), karma);
    }

    private static bool KarmaMi(string deger)
    {
        if (deger.Length != KarmaUzunlugu)
        {
            return false;
        }

        foreach (char k in deger)
        {
            if (!char.IsAsciiHexDigitLower(k))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Bilesim kaydinin yolu; dosya kokun altinda degilse null.</summary>
    private static string? BilesimYolu(string kok, string yol, int no)
    {
        string? yuva = Yuvasi(kok, yol);
        return yuva is null
            ? null
            : WindowsYolu.Birlestir(yuva, ArsivKlasoru(no) + BilesimSonEki);
    }

    /// <summary>
    /// Bilesim kaydinin yolu, ARSIV KOPYASINDAN turetilir:
    /// ...\&lt;ad&gt;\vN\&lt;ad&gt; -> ...\&lt;ad&gt;\vN.cocuklar.txt
    /// Kok GEREKMIYOR - kayit zaten arsivin yaninda (CLAUDE.md 2).
    /// </summary>
    private static string? BilesimYolu(string? arsivYolu)
    {
        if (string.IsNullOrWhiteSpace(arsivYolu))
        {
            return null;
        }

        string vKlasoru = WindowsYolu.Klasor(arsivYolu);
        string govde = WindowsYolu.DosyaAdi(vKlasoru);

        // ESKI DUZ DUZEN ("v3.SLDPRT" dogrudan yuvada): bilesim kaydi yok.
        if (govde.Length < 2 || govde[0] is not ('v' or 'V')
            || !int.TryParse(
                govde[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return null;
        }

        return WindowsYolu.Birlestir(WindowsYolu.Klasor(vKlasoru), govde + BilesimSonEki);
    }

    /// <summary>
    /// Bir versiyonun ("...\\vN") bilesim kaydini siler. Kayit yoksa sessizce
    /// gecilir - silinen versiyonun bilesimi hic olmayabilir.
    /// </summary>
    private static void BilesimKaydiniSil(string vKlasoru)
    {
        try
        {
            string kayit = WindowsYolu.Birlestir(
                WindowsYolu.Klasor(vKlasoru),
                WindowsYolu.DosyaAdi(vKlasoru) + BilesimSonEki);

            if (File.Exists(kayit))
            {
                File.SetAttributes(kayit, FileAttributes.Normal);
                File.Delete(kayit);
            }
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Silinemediyse versiyonun kendisi ZATEN gitti; kayit oksuz
            // kalir ve BilesimOku onu okusa bile sahne kurulamaz - asil
            // silme basarili sayilir (CLAUDE.md 1c).
        }
    }
}
