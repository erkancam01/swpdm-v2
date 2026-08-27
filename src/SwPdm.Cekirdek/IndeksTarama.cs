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
        List<string> dosyalar = Dosyalari_Topla(indeks.Kok, belirtec, okunamayanKlasorler);

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

            string yol = dosyalar[i];
            gorulen.Add(yol);
            ilerleme?.Invoke(i, dosyalar.Count, WindowsYolu.DosyaAdi(yol));

            long boyut;
            DateTime degistirme;
            try
            {
                var bilgi = new FileInfo(yol);
                boyut = bilgi.Length;
                degistirme = bilgi.LastWriteTime;
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                okunamayan++;
                indeks.Koy(new IndeksKaydi(
                    yol, 0, default, [], null, Okundu: false, "Dosya bilgisi okunamadı."));
                continue;
            }

            // ARTIMLILIK: boyut ve tarih aynysa dosya ACILMAZ.
            IndeksKaydi? eski = indeks.Kayit(yol);
            if (eski is not null && eski.Boyut == boyut && eski.Degistirme == degistirme)
            {
                atlanan++;
                if (!eski.Okundu)
                {
                    okunamayan++;
                }

                continue;
            }

            SwReferanslar r = SwReferans.Oku(yol);
            okunan++;
            if (!r.Okundu)
            {
                okunamayan++;
            }

            indeks.Koy(new IndeksKaydi(
                yol, boyut, degistirme, r.Dogrudan, r.KendiYolu, r.Okundu, r.Sebep));
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
    private static List<string> Dosyalari_Topla(
        string kok, CancellationToken belirtec, List<string> okunamayanlar)
    {
        var bulunanlar = new List<string>();
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
                foreach (string dosya in Directory.GetFiles(suan))
                {
                    if (SwReferans.TasiyabilirMi(dosya))
                    {
                        bulunanlar.Add(dosya);
                    }
                }

                foreach (string alt in Directory.GetDirectories(suan))
                {
                    // Kendi cop klasorumuz taranmaz: silinmis dosyalarin
                    // referanslari "bu parcayi biri kullaniyor" dedirtirdi.
                    if (string.Equals(
                            WindowsYolu.DosyaAdi(alt), Cop.KlasorAdi, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yigin.Push(alt);
                }
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                okunamayanlar.Add(suan);
            }
        }

        return bulunanlar;
    }
}
