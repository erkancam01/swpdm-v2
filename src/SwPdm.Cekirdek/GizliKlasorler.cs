using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>
/// UYGULAMANIN KENDI KLASORLERI - kokun icinde duran, kullanicinin dosyasi
/// OLMAYAN klasorler. TEK KAYNAK (CLAUDE.md 1b).
///
/// NEDEN VAR - OLCULDU (31.08.2026): bu listeyi DORT tarayici elle
/// biliyordu (<see cref="KlasorTarayici"/> · <see cref="IndeksTarama"/> ·
/// <see cref="KlasorBoyutu"/> · DiskIzleyici) ve iki sabit vardi. UCUNCU bir
/// klasor eklemek 4 dosyaya satir ekletecekti, KALDIRMAK da 4 dosyadan satir
/// sildirecekti - ve biri unutulunca hata SESSIZ olurdu: kilit klasoru
/// agacta bir "klasor" gibi gorunur, indekste taranir, boyuta katilirdi.
///
/// Simdi liste burada; otekiler <see cref="Bizim"/> ile SORUYOR. Yeni gizli
/// klasor = asagiya BIR SATIR.
///
/// NEDEN "GIZLI" DEGIL DE "BIZIM": bu klasorler Windows'ta gizli oznitelikli
/// olmak zorunda degil; ayirt eden sey SAHIPLIGI - onlari biz yaziyoruz.
/// Kullanicinin kendi gizli klasoru agacta GORUNMEYE devam eder.
/// </summary>
public static class GizliKlasorler
{
    /// <summary>
    /// Uygulamanin kendi klasorlerinin adlari. Sira onemsiz.
    /// Sahibi olan dosya bu adi SABIT olarak yayinlar; burada ikinci kez
    /// metin yazilmaz (CLAUDE.md 8).
    /// </summary>
    public static readonly IReadOnlyList<string> Tumu =
    [
        Cop.KlasorAdi,
        Surumler.KlasorAdi,
        KlasorKilidi.KlasorAdi,
    ];

    /// <summary>Bu ad uygulamanin kendi klasorlerinden biri mi.</summary>
    public static bool Bizim(string? klasorAdi)
    {
        if (string.IsNullOrEmpty(klasorAdi))
        {
            return false;
        }

        foreach (string ad in Tumu)
        {
            // Ordinal SART: bu bir MAKINE karsilastirmasi (klasor adi bizim
            // yazdigimiz sabit), insan metni degil. Kulture bagli
            // karsilastirma Turkce yerelinde noktali/noktasiz I yuzunden
            // sasar (CLAUDE.md 4'teki DosyaTurleri.Tani ile ayni gerekce).
            if (string.Equals(klasorAdi, ad, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Verilen YOL uygulamanin kendi klasorlerinden birine mi ait.</summary>
    public static bool BizimYol(string? yol) => Bizim(WindowsYolu.DosyaAdi(yol));
}
