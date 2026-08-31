using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SwPdm.Cekirdek;

/// <summary>
/// Bir klasorun olculen boyutu.
/// </summary>
/// <param name="Bayt">Toplam bayt.</param>
/// <param name="DosyaSayisi">Sayilan dosya.</param>
/// <param name="KlasorSayisi">Icindeki alt klasor sayisi (klasorun KENDISI sayilmaz).</param>
/// <param name="OkunamayanKlasorler">Izin verilmeyen ya da okunamayan klasorler.</param>
/// <param name="Iptal">Kullanici yarida kesti mi.</param>
public sealed record BoyutSonucu(
    long Bayt,
    int DosyaSayisi,
    int KlasorSayisi,
    IReadOnlyList<string> OkunamayanKlasorler,
    bool Iptal)
{
    /// <summary>
    /// Sayi TAM mi. Iptal edildiyse ya da okunamayan klasor varsa DEGIL -
    /// ve bu SOYLENMEK zorunda, yoksa kullanici eksik bir sayiya bakip
    /// karar verir (CLAUDE.md 3).
    /// </summary>
    public bool Tam => !Iptal && OkunamayanKlasorler.Count == 0;

    /// <summary>Ekranda gosterilecek cumle.</summary>
    public string Yaz()
    {
        string temel = $"{Boyut.Yaz(Bayt)}  ·  {DosyaSayisi} dosya  ·  {KlasorSayisi} klasör";

        if (Iptal)
        {
            return temel + "  (YARIM — hesaplama durduruldu)";
        }

        if (OkunamayanKlasorler.Count > 0)
        {
            return temel + $"  (EKSİK — {OkunamayanKlasorler.Count} klasör okunamadı)";
        }

        return temel;
    }
}

/// <summary>
/// KLASOR BOYUTU HESABI. Bilerek ISTEK UZERINE: bir klasorun boyutu ancak
/// icindeki her sey gezilerek bulunur ve ag surucusunde bu dakikalar surebilir.
/// Her klasor secilince kendiliginden hesaplamak uygulamayi kullanilmaz yapardi.
///
/// Iptal edilebilir ve ilerleme bildirir - CLAUDE.md 3: sayilabilir ilerleme
/// varken uydurma yuzde gosterilmez, GERCEK sayi gosterilir.
/// </summary>
public static class KlasorBoyutu
{
    /// <summary>
    /// Klasoru gezip boyutunu toplar.
    /// </summary>
    /// <param name="klasorYolu">Olculecek klasor.</param>
    /// <param name="belirtec">Iptal.</param>
    /// <param name="ilerleme">
    /// Her klasor bitiminde cagrilir: (gezilen klasor, o ana kadarki bayt).
    /// </param>
    public static BoyutSonucu Hesapla(
        string klasorYolu,
        CancellationToken belirtec = default,
        Action<int, long>? ilerleme = null)
    {
        long bayt = 0;
        int dosya = 0;

        // Gezilen klasor sayisi klasorun KENDISINI de kapsiyor; kullaniciya
        // gosterilen "kac klasor" ise ICINDEKILER. Aradaki 1 farki tek yerde
        // duruyor: Ic(...) (CLAUDE.md 8 - ayni mantigin ikinci kopyasi yok).
        int gezilen = 0;
        var okunamayan = new List<string>();

        static int Ic(int gezilen) => gezilen > 0 ? gezilen - 1 : 0;

        var yigin = new Stack<string>();
        yigin.Push(klasorYolu);

        while (yigin.Count > 0)
        {
            if (belirtec.IsCancellationRequested)
            {
                return new BoyutSonucu(bayt, dosya, Ic(gezilen), okunamayan, Iptal: true);
            }

            string su_an = yigin.Pop();
            gezilen++;

            try
            {
                foreach (string yol in Directory.GetFiles(su_an))
                {
                    try
                    {
                        bayt += new FileInfo(yol).Length;
                        dosya++;
                    }
                    catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
                    {
                        // Tek bir dosya okunamadi; klasoru komple kaybetmeyiz.
                        dosya++;
                    }
                }

                foreach (string alt in Directory.GetDirectories(su_an))
                {
                    // Kendi cop ve versiyon klasorlerimiz sayilmaz: kullanici
                    // "bu klasor kac GB" derken sildiklerini ve arsivi
                    // kastetmiyor.
                    if (string.Equals(
                            WindowsYolu.DosyaAdi(alt), Cop.KlasorAdi, StringComparison.Ordinal)
                        || string.Equals(
                            WindowsYolu.DosyaAdi(alt), Surumler.KlasorAdi, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yigin.Push(alt);
                }
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                // Okunamayan klasor GIZLENMEZ - sayilir ve sonucun eksik
                // oldugu soylenir.
                okunamayan.Add(su_an);
            }

            ilerleme?.Invoke(gezilen, bayt);
        }

        return new BoyutSonucu(bayt, dosya, Ic(gezilen), okunamayan, Iptal: false);
    }
}
