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
public sealed record SurumDurumu(
    IReadOnlyList<SurumKaydi> Ogeler,
    string? Okunamadi,
    int BozukSatir)
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
public static class Surumler
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
            return new SurumDurumu([], "Dosya açık kökün altında değil.", 0);
        }

        string kayitYolu = WindowsYolu.Birlestir(yuva, KayitAdi);
        if (!File.Exists(kayitYolu))
        {
            return new SurumDurumu([], null, 0);
        }

        string[] satirlar;
        try
        {
            satirlar = File.ReadAllLines(kayitYolu);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return new SurumDurumu([], "Versiyon kaydı okunamadı: " + hata.Message, 0);
        }

        var ogeler = new List<SurumKaydi>();
        int bozuk = 0;

        foreach (string satir in satirlar)
        {
            if (satir.Length == 0)
            {
                continue;
            }

            SurumKaydi? kayit = SatiriCoz(yuva, satir);

            // Arsiv dosyasi kayipsa kayit GOSTERILMEZ ama SAYILIR - sessizce
            // yutmak, kullaniciya "o versiyon hic olmadi" dedirtir (CLAUDE.md 3).
            if (kayit is null || !File.Exists(kayit.ArsivYolu))
            {
                bozuk++;
                continue;
            }

            ogeler.Add(kayit);
        }

        ogeler.Sort((a, b) => b.No.CompareTo(a.No));
        return new SurumDurumu(ogeler, null, bozuk);
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

        int yeniNo = durum.Ogeler.Count == 0 ? 0 : durum.Ogeler[0].No + 1;
        string arsiv = WindowsYolu.Birlestir(yuva, ArsivAdi(yeniNo, yol));

        long boyut;
        try
        {
            Directory.CreateDirectory(yuva);
            File.Copy(yol, arsiv, overwrite: false);

            boyut = new FileInfo(arsiv).Length;
            long asil = new FileInfo(yol).Length;
            if (boyut != asil)
            {
                // Yarim kopya versiyon DEGILDIR; birakilirsa gunun birinde
                // "don" ile dosyanin yerine gecer (CLAUDE.md 1a).
                File.Delete(arsiv);
                return new IslemRaporu(
                    IslemSonucu.Bilinmeyen, null,
                    $"Kopya doğrulanamadı ({boyut} ≠ {asil} bayt) — versiyon oluşturulmadı.");
            }
        }
        catch (Exception hata)
        {
            TemizlemeyeCalis(arsiv);
            return IslemSonuclari.HatayiCevir(hata);
        }

        try
        {
            File.AppendAllText(
                WindowsYolu.Birlestir(yuva, KayitAdi),
                SatirYap(yeniNo, DateTime.Now, not, boyut));
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Kayitta olmayan kopya, Listele'de gorunmez = SESSIZ KAYIP olur.
            TemizlemeyeCalis(arsiv);
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
            File.SetAttributes(arsiv, FileAttributes.ReadOnly);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Oznitelik konamadiysa versiyon YINE GECERLI; koruma eksik
            // kaldi ama arsiv duruyor - islemi geri almak asiri olurdu.
        }

        no = yeniNo;
        return IslemRaporu.Basarili(arsiv);
    }

    /// <summary>
    /// Dosyayi verilen versiyonun icerigine dondurur.
    ///
    /// SIRA (CLAUDE.md 1a - hicbir icerik kaybolmaz):
    ///   1. SOLIDWORKS'te acik gorunen dosyaya DOKUNULMAZ (~$ kilidi).
    ///   2. Bugunku icerik once OTOMATIK arsivlenir ("donmeden once" notuyla).
    ///   3. Eski icerik ayni klasorde geciciye kopyalanir, boyutu dogrulanir,
    ///      File.Replace ile oturur - yarim yazma dosyayi bozamaz.
    /// </summary>
    public static IslemRaporu Don(string kok, string yol, int no)
    {
        if (Kilit.AcikMi(yol))
        {
            return new IslemRaporu(
                IslemSonucu.Kilitli, null,
                "Dosya SOLIDWORKS'te açık görünüyor (~$ kilidi) — kapatıp yeniden deneyin.");
        }

        SurumDurumu durum = Listele(kok, yol);
        if (!durum.Guvenilir)
        {
            return new IslemRaporu(IslemSonucu.Bilinmeyen, null, durum.Okunamadi);
        }

        SurumKaydi? hedef = null;
        foreach (SurumKaydi kayit in durum.Ogeler)
        {
            if (kayit.No == no)
            {
                hedef = kayit;
                break;
            }
        }

        if (hedef is null)
        {
            return new IslemRaporu(
                IslemSonucu.Bulunamadi, null, $"v{no} arşivde yok.");
        }

        IslemRaporu guvence = Olustur(kok, yol, $"v{no}'a dönmeden önce", out int _);
        if (!guvence.Oldu)
        {
            // Bugunku hal saklanamadiysa donus YAPILMAZ - aksi, mevcut
            // icerigi geri donussuz silmek olurdu (CLAUDE.md 1a).
            return new IslemRaporu(
                guvence.Sonuc, null,
                "Dönülmedi — bugünkü hâl arşivlenemedi: " + guvence.Sebebi);
        }

        string gecici = yol + ".swpdm-don";
        try
        {
            File.Copy(hedef.ArsivYolu, gecici, overwrite: true);

            // File.Copy OZNITELIGI DE kopyaliyor: arsiv salt-okunur, gecici
            // de salt-okunur dogar. Temizlenmezse Replace sonrasi CANLI
            // dosya salt-okunur kalir ve SOLIDWORKS kaydedemez olur.
            File.SetAttributes(gecici, FileAttributes.Normal);

            long kopya = new FileInfo(gecici).Length;
            if (kopya != hedef.Boyut)
            {
                File.Delete(gecici);
                return new IslemRaporu(
                    IslemSonucu.Bilinmeyen, null,
                    $"Kopya doğrulanamadı ({kopya} ≠ {hedef.Boyut} bayt) — dosyaya dokunulmadı.");
            }

            File.Replace(gecici, yol, destinationBackupFileName: null);
        }
        catch (Exception hata)
        {
            TemizlemeyeCalis(gecici);
            return IslemSonuclari.HatayiCevir(hata);
        }

        return IslemRaporu.Basarili(yol);
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

    private static string ArsivAdi(int no, string yol)
        => "v" + no.ToString(CultureInfo.InvariantCulture) + WindowsYolu.Uzanti(yol);

    /// <summary>
    /// Kayit satiri: no·zaman·boyut·not, sekmeyle. Not icindeki sekme ve
    /// satir sonu bosluga cevrilir - biçim tek satir, elle onarilabilir.
    /// </summary>
    private static string SatirYap(int no, DateTime zaman, string not, long boyut)
        => no.ToString(CultureInfo.InvariantCulture) + '\t'
           + zaman.ToString("O", CultureInfo.InvariantCulture) + '\t'
           + boyut.ToString(CultureInfo.InvariantCulture) + '\t'
           + (not ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ')
           + Environment.NewLine;

    private static SurumKaydi? SatiriCoz(string yuva, string satir)
    {
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

        // Uzanti kayitta durmuyor; arsiv dosyasi yuvada "v<no>.*" diye tektir.
        string? arsiv = ArsivBul(yuva, no);
        return arsiv is null ? null : new SurumKaydi(no, zaman, p[3], arsiv, boyut);
    }

    private static string? ArsivBul(string yuva, int no)
    {
        try
        {
            string govde = "v" + no.ToString(CultureInfo.InvariantCulture);
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
