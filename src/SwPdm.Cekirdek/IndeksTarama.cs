using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SwPdm.Cekirdek;

/// <summary>Bir taramanin sonucu - hepsi SAYILMIS, hicbiri tahmin degil.</summary>
/// <param name="Toplam">Agacta bulunan SOLIDWORKS dosyasi.</param>
/// <param name="Okunan">Bu taramada gercekten acilan dosya.</param>
/// <param name="Atlanan">Degismedigi icin ACILMAYAN dosya (artimlilik).</param>
/// <param name="Okunamayan">Acildi ama referanslari cikarilamayan dosya.</param>
/// <param name="Dusen">Diskten silindigi icin indeksten cikarilan kayit.</param>
/// <param name="OkunamayanKlasorler">Izin verilmeyen ya da okunamayan klasorler.</param>
/// <param name="Iptal">Kullanici yarida kesti mi.</param>
/// <param name="Sure">Ne kadar surdu.</param>
public sealed record TaramaSonucu(
    int Toplam,
    int Okunan,
    int Atlanan,
    int Okunamayan,
    int Dusen,
    IReadOnlyList<string> OkunamayanKlasorler,
    bool Iptal,
    TimeSpan Sure)
{
    /// <summary>Sonuc TAM mi. Iptal ya da okunamayan varsa DEGIL.</summary>
    public bool Tam => !Iptal && Okunamayan == 0 && OkunamayanKlasorler.Count == 0;

    /// <summary>
    /// Ekranda gosterilecek cumle. HIZ BURADA YAZILIYOR: ag surucusundeki
    /// gercek maliyet tahmin edilmez, olculup soylenir.
    /// </summary>
    public string Yaz()
    {
        string temel = $"{Toplam} dosya · {Okunan} okundu · {Atlanan} değişmemiş · "
                     + $"{Sure.TotalSeconds:0.0} sn";

        if (Iptal)
        {
            return temel + "  (YARIM — tarama durduruldu)";
        }

        var eksikler = new List<string>();
        if (Okunamayan > 0)
        {
            eksikler.Add($"{Okunamayan} dosya okunamadı");
        }

        if (OkunamayanKlasorler.Count > 0)
        {
            eksikler.Add($"{OkunamayanKlasorler.Count} klasör okunamadı");
        }

        return eksikler.Count == 0 ? temel : temel + "  (EKSİK — " + string.Join(", ", eksikler) + ")";
    }
}

/// <summary>
/// INDEKSI DISKTEN DOLDURUR.
///
/// ARTIMLI - ve bu, ozelligin kullanilabilir olmasinin tek sebebi. Kayitta
/// dosyanin boyutu ve tarihi duruyor; ikisi de aynysa dosya HIC ACILMAZ.
/// Ilk tarama bir kez pahalidir, sonrasi yalnizca degisenler kadar surer.
///
/// IKI GECISLI: once agac gezilip dosyalar SAYILIYOR, sonra okunuyor.
/// Sebep CLAUDE.md 3: sayilabilir ilerleme yokken yuzde uydurulmaz. Ilk
/// gecis yalnizca klasor listeleme, ikincisi asil is.
///
/// OKUNAMAYAN KLASOR GIZLENMEZ: sayilir ve sonucun EKSIK oldugu soylenir.
/// Eksik bir indeks "bu parcayi kimse kullanmiyor" dedirtirse dosya
/// sildirir (CLAUDE.md 3) - o yuzden eksiklik sonuca kadar tasiniyor.
/// </summary>
public static class IndeksTarama
{
    /// <summary>Atlanan dosyalarda kac dosyada bir ilerleme bildirilecek.</summary>
    private const int IlerlemeAdimi = 100;

    /// <summary>Kokteki SOLIDWORKS dosyalarini indekse isler.</summary>
    /// <param name="indeks">Guncellenecek indeks.</param>
    /// <param name="belirtec">Iptal.</param>
    /// <param name="ilerleme">(yapilan, toplam, su anki dosya adi).</param>
    public static TaramaSonucu Tara(
        ReferansIndeksi indeks,
        CancellationToken belirtec = default,
        Action<int, int, string>? ilerleme = null)
    {
        ArgumentNullException.ThrowIfNull(indeks);

        var kronometre = Stopwatch.StartNew();
        var okunamayanKlasorler = new List<string>();
        List<Aday> dosyalar = Dosyalari_Topla(indeks.Kok, belirtec, okunamayanKlasorler);

        int okunan = 0, atlanan = 0, okunamayan = 0;
        var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // IPTAL AGAC GEZILIRKEN DE OLABILIR - ve bu bir test tarafindan
        // yakalandi: yalnizca okuma dongusune bakan ilk hal, gezme sirasinda
        // iptal edilen taramayi "iptal edilmedi" sayiyordu. Sonuc TEHLIKELIYDI:
        // sifir dosyayla TAM bir indeks, yani "bu parcayi kimse kullanmiyor"
        // diyen bir indeks (CLAUDE.md 3).
        bool iptal = belirtec.IsCancellationRequested;

        for (int i = 0; i < dosyalar.Count; i++)
        {
            if (belirtec.IsCancellationRequested)
            {
                iptal = true;
                break;
            }

            Aday aday = dosyalar[i];
            string yol = aday.Yol;
            gorulen.Add(yol);

            if (!aday.BilgiOkundu)
            {
                Bildir(ilerleme, i, dosyalar.Count, yol);
                okunamayan++;
                indeks.Koy(Okunamadi(yol));
                continue;
            }

            // ARTIMLILIK: boyut ve tarih aynysa dosya ACILMAZ.
            IndeksKaydi? eski = indeks.Kayit(yol);
            if (eski is not null
                && eski.Boyut == aday.Boyut
                && eski.Degistirme == aday.Degistirme)
            {
                atlanan++;
                if (!eski.Okundu)
                {
                    okunamayan++;
                }

                // ILERLEME SEYREK: atlanan dosya icin her seferinde arayuze
                // gecmek, taramanin kendisinden pahali olabiliyordu - her
                // cagri bir BeginInvoke ve bir durum cubugu cizimi. Sayim
                // yine dogru: ilerleme adimlari atlanir, sonuc atlanmaz.
                if (i % IlerlemeAdimi == 0)
                {
                    Bildir(ilerleme, i, dosyalar.Count, yol);
                }

                continue;
            }

            Bildir(ilerleme, i, dosyalar.Count, yol);
            IndeksKaydi kayit = Kayit(yol, aday.Boyut, aday.Degistirme);
            okunan++;
            if (!kayit.Okundu)
            {
                okunamayan++;
            }

            indeks.Koy(kayit);
        }

        // Diskten silinmis dosyalarin kayitlari DUSER. Yoksa indeks "bu
        // parcayi su montaj kullaniyor" derken olmayan bir montaji sayardi.
        int dusen = 0;
        if (!iptal)
        {
            foreach (IndeksKaydi k in new List<IndeksKaydi>(indeks.Kayitlar))
            {
                if (!gorulen.Contains(k.Yol) && indeks.Sil(k.Yol))
                {
                    dusen++;
                }
            }
        }

        // "Kok disinda" sorusunun disk yoklamasi TARAMANIN icinde kosuyor -
        // arayuz is parcaciginda kosunca uygulama donuyordu (ReferansIndeksi.
        // DiskiYokla belgesi). Sure kronometreye giriyor: olu ag yollarinin
        // bedeli tahmin edilmez, tarama cumlesinde olculmus gorunur.
        if (!iptal)
        {
            indeks.DiskiYokla(belirtec);
        }

        ilerleme?.Invoke(dosyalar.Count, dosyalar.Count, string.Empty);
        kronometre.Stop();

        var sonuc = new TaramaSonucu(
            dosyalar.Count, okunan, atlanan, okunamayan, dusen,
            okunamayanKlasorler, iptal, kronometre.Elapsed);

        indeks.TaramayiBitir(sonuc.Tam, DateTime.Now);
        return sonuc;
    }

    /// <summary>
    /// Agactaki SOLIDWORKS dosyalarini toplar. Yigin ile geziliyor; ozyineleme
    /// derin agaclarda yigini tasirabilir.
    /// </summary>
    /// <summary>
    /// TEK BIR DOSYAYI okuyup indekse koyar - butun agaci taramadan.
    ///
    /// NEDEN VAR: referans onarimindan sonra indeks ad degisiminden HABERSIZ
    /// kalir ve referans paneli YALAN soyler (silinmis bir adi "kullaniyor"
    /// diye gosterir). Onarim biter bitmez dokunulan dosyalar buradan
    /// tazeleniyor; butun kok yeniden taranmiyor.
    /// </summary>
    public static void Tazele(ReferansIndeksi? indeks, string? yol)
    {
        if (indeks is null || string.IsNullOrWhiteSpace(yol))
        {
            return;
        }

        if (!File.Exists(yol))
        {
            indeks.Sil(yol);
            return;
        }

        try
        {
            var bilgi = new FileInfo(yol);
            indeks.Koy(Kayit(yol, bilgi.Length, bilgi.LastWriteTime));
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            indeks.Koy(Okunamadi(yol));
        }
    }

    /// <summary>Ilerleme bildirimi - tek kopya.</summary>
    private static void Bildir(
        Action<int, int, string>? ilerleme, int yapilan, int toplam, string yol)
        => ilerleme?.Invoke(yapilan, toplam, WindowsYolu.DosyaAdi(yol));

    /// <summary>Bir dosyanin indeks kaydini uretir. TEK KOPYA (CLAUDE.md 8).</summary>
    private static IndeksKaydi Kayit(string yol, long boyut, DateTime degistirme)
    {
        SwReferanslar r = SwReferans.Oku(yol);
        return new IndeksKaydi(yol, boyut, degistirme, r.Dogrudan, r.KendiYolu, r.Okundu, r.Sebep);
    }

    /// <summary>Dosya bilgisi bile okunamayan kayit.</summary>
    private static IndeksKaydi Okunamadi(string yol)
        => new(yol, 0, default, [], null, Okundu: false, "Dosya bilgisi okunamadı.");

    /// <summary>
    /// Agacta bulunan bir dosya ve DIZIN LISTESIYLE BIRLIKTE gelen bilgisi.
    /// <paramref name="BilgiOkundu"/> false ise boyut/tarih alinamadi.
    /// </summary>
    private readonly record struct Aday(
        string Yol, long Boyut, DateTime Degistirme, bool BilgiOkundu);

    /// <summary>
    /// Agactaki SOLIDWORKS dosyalarini BOYUT VE TARIHIYLE toplar. Yigin ile
    /// geziliyor; ozyineleme derin agaclarda yigini tasirabilir.
    ///
    /// NEDEN "EnumerateFileSystemInfos" - VE OLCUMUN SOYLEDIGI (28.08.2026):
    /// onceki hal "Directory.GetFiles" ile yalnizca ADLARI aliyor, sonra her
    /// dosya icin AYRI bir "new FileInfo(yol).Length" cagiriyordu.
    ///
    /// BEKLENTI: dizin listesi boyutu ve tarihi zaten getirdigi icin dosya
    /// basina ayri bir metadata cagrisi kalkacakti.
    /// OLCULDU (Linux, 2000 dosya, strace ile SAYILDI): KALKMADI - eski hal
    /// 4000, yeni hal 4080 metadata cagrisi. Sure de ayni (9-11 ms / 8-9 ms).
    /// Sebep: Linux'ta dizin girisi boyut ve tarih TASIMIYOR, .NET her giris
    /// icin yine stat cagiriyor.
    ///
    /// Windows'ta durum farkli: FindFirstFile/FindNextFile boyutu ve tarihi
    /// WIN32_FIND_DATA icinde DONDURUYOR, "new FileInfo(...).Length" ise
    /// ayrica GetFileAttributesEx cagiriyor. Yani kazanc orada beklenir -
    /// AMA OLCULMEDI ve iddia EDILMIYOR (CLAUDE.md 2). Burada kalmasinin
    /// sebebi olculmus bir kayip da olmamasi; asil kazanc alt taraftaki
    /// "gereksiz taramayi hic kosma" kararinda (ReferansTazeleme).
    /// </summary>
    private static List<Aday> Dosyalari_Topla(
        string kok, CancellationToken belirtec, List<string> okunamayanlar)
    {
        var bulunanlar = new List<Aday>();
        var yigin = new Stack<string>();
        yigin.Push(kok);

        while (yigin.Count > 0)
        {
            if (belirtec.IsCancellationRequested)
            {
                break;
            }

            string suan = yigin.Pop();
            try
            {
                foreach (FileSystemInfo giris in new DirectoryInfo(suan).EnumerateFileSystemInfos())
                {
                    if (giris is DirectoryInfo alt)
                    {
                        // Kendi cop klasorumuz taranmaz: silinmis dosyalarin
                        // referanslari "bu parcayi biri kullaniyor" dedirtirdi.
                        if (!string.Equals(alt.Name, Cop.KlasorAdi, StringComparison.Ordinal))
                        {
                            yigin.Push(alt.FullName);
                        }

                        continue;
                    }

                    if (giris is not FileInfo dosya || !SwReferans.TasiyabilirMi(dosya.FullName))
                    {
                        continue;
                    }

                    bulunanlar.Add(Bilgisiyle(dosya));
                }
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                okunamayanlar.Add(suan);
            }
        }

        return bulunanlar;
    }

    /// <summary>
    /// Numaralandirmadan gelen bilgiyi okur. Dosya tam o sirada silinirse
    /// alanlar patlayabiliyor; o zaman "bilgi okunamadi" olarak isaretlenir
    /// ve arama sessizce dogru sanilan bir kayit uretmez (CLAUDE.md 3).
    /// </summary>
    private static Aday Bilgisiyle(FileInfo dosya)
    {
        try
        {
            return new Aday(dosya.FullName, dosya.Length, dosya.LastWriteTime, BilgiOkundu: true);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return new Aday(dosya.FullName, 0, default, BilgiOkundu: false);
        }
    }
}
