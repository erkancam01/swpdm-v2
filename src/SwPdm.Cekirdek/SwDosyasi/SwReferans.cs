using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SwPdm.Cekirdek;

/// <summary>
/// Bir SOLIDWORKS belgesinin ICINDE YAZAN referanslar.
/// </summary>
/// <param name="Dogrudan">
/// Belgenin DOGRUDAN kullandigi belgelerin yollari (montajin parcalari,
/// teknik resmin modeli). Kendi yolu bu listede YOKTUR.
/// </param>
/// <param name="KendiYolu">Belgenin en son kaydedildigi yol; bilinmiyorsa null.</param>
/// <param name="Okundu">Referanslar gercekten okunabildi mi.</param>
/// <param name="Sebep">Okunamadiysa SEBEP - bos liste "referans yok" DEMEK DEGIL.</param>
public sealed record SwReferanslar(
    IReadOnlyList<string> Dogrudan,
    string? KendiYolu,
    bool Okundu,
    string? Sebep)
{
    /// <summary>Okunamamis sonuc.</summary>
    public static SwReferanslar Okunamadi(string sebep)
        => new([], null, Okundu: false, sebep);
}

/// <summary>
/// SOLIDWORKS REFERANSLARINI OKUR - SOLIDWORKS KURULU OLMADAN.
///
/// OLCULDU (28.08.2026, SOLIDWORKS 2022, yedi gercek dosya):
///
///   Montaj1.SLDASM  -> Parça1.SLDPRT · Yeni klasör\Parça2.SLDPRT
///   Montaj2.SLDASM  -> Montaj1.SLDASM            (alt montaj)
///   Parça1.SLDDRW   -> Parça1.SLDPRT             (teknik resim -> model)
///   Montaj2.SLDDRW  -> Montaj2.SLDASM
///   Parça1/2.SLDPRT -> (yok, yaprak)
///
/// HANGI AKIS: "Header2". Bu, DOGRUDAN referanslari tutuyor. Ayrimi yapan
/// sey bu: "Contents/DisplayLists" ve "Contents/Definition" akislari butun
/// AGACI (torunlari da) yaziyor, "Header2" yalnizca dogrudan kullanilani.
/// Once yollari akis ayrimi yapmadan taramak denendi ve dolayli olanlari
/// dogrudan gibi gosteriyordu; akis adiyla ayirmak bunu cozdu.
///
/// DIZE BICIMI: MFC'nin Unicode CString'i -
///   FF FE FF  &lt;uzunluk: 1 bayt&gt;  &lt;uzunluk adet UTF-16LE karakter&gt;
/// Yedi dosyadaki 52 yolun 52'sinde tuttu. SOLIDWORKS bir MFC uygulamasi,
/// yani okudugumuz sey onlarin kendi serilestirme bicimi.
///
/// OLCULMEDI - UZUN YOL: uzunluk oneki 1 bayt, yani en fazla 254 karakter.
/// MFC daha uzun dizeleri kacisla yaziyor; o bicim BURADA GORULMEDI (ornek
/// yollarin hepsi kisaydi). Uzun bir ag yolu (\\sunucu\ortak\...) bunu
/// asabilir. Kod kacis baytini GORURSE o dizeyi atlar ve sonucu EKSIK
/// isaretler - sessizce yanlis cevap vermez (CLAUDE.md 3).
///
/// YOL BAYAT OLABILIR: butun yollar MUTLAK yaziliyor (C:\...). Klasor
/// tasininca dosyanin icindeki yol eskir ama SOLIDWORKS yanindaki kopyayi
/// bulup acmaya devam ediyor (CLAUDE.md 5'te olculdu). Yani buradan okunan
/// yol bir IPUCU; "dosya burada" garantisi degil.
/// </summary>
public static class SwReferans
{
    /// <summary>Referanslarin durdugu akis.</summary>
    private const string DogrudanAkis = "Header2";

    /// <summary>Belgenin kendi yolunun durdugu akis.</summary>
    private const string GecmisAkis = "_MO_VERSION_15000/Biography";

    /// <summary>Bu uzantilardan biriyle biten dizeler yol sayilir.</summary>
    private static readonly string[] Uzantilar = [".SLDPRT", ".SLDASM", ".SLDDRW"];

    /// <summary>Bu dosya turu referans tasiyabilir mi.</summary>
    public static bool TasiyabilirMi(string? yol)
    {
        string uzanti = WindowsYolu.Uzanti(yol);
        foreach (string u in Uzantilar)
        {
            if (string.Equals(uzanti, u, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Belgenin icindeki dogrudan referanslari okur.
    /// Basarisizlikta bos liste DEGIL, sebebi olan bir sonuc doner.
    /// </summary>
    public static SwReferanslar Oku(string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu))
        {
            return SwReferanslar.Okunamadi("Yol boş.");
        }

        try
        {
            using SwPaket? paket = SwPaket.Ac(dosyaYolu);
            if (paket is null)
            {
                return SwReferanslar.Okunamadi(
                    "Dosya SOLIDWORKS paketi gibi görünmüyor (biçim tanınmadı).");
            }

            byte[]? basliklar = paket.AkisiOku(DogrudanAkis);
            if (basliklar is null)
            {
                return SwReferanslar.Okunamadi(
                    $"\"{DogrudanAkis}\" akışı okunamadı; referanslar bilinmiyor.");
            }

            string kendiAdi = WindowsYolu.DosyaAdi(dosyaYolu);
            string? kendi = KendiYolunuBul(paket, dosyaYolu);

            // BELGENIN KENDI ADI IKI TANE OLABILIR: diskteki BUGUNKU ad ve
            // dosyanin ICINDE yazan (en son kaydedildigi) ad. Dosya disarida
            // yeniden adlandirilmissa bunlar AYRISIR ve ikincisi elenmezse
            // BIR REFERANS SANILIR (01.09.2026'da olculdu: "1-" oneki almis
            // alti dosyanin her biri KIRIK sekmesinde kendi eski adini
            // gosteriyordu; dokuz "kirik referans" bulgusunun altisi buydu).
            // Yanlis "kirik" bilgisi bu uygulamada saglam dosya sildirir
            // (CLAUDE.md 3) - iki ad da elenir.
            string? kendiEskiAdi = kendi is null ? null : WindowsYolu.DosyaAdi(kendi);

            var yollar = new List<string>();
            var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool kacisGoruldu = Dizeler(basliklar, dize =>
            {
                if (!YolMu(dize))
                {
                    return;
                }

                // Belge kendi yolunu da yaziyor; o bir referans DEGIL.
                // Hem bugunku ad hem dosyanin icinde yazan eski ad elenir.
                string adi = WindowsYolu.DosyaAdi(dize);
                if (string.Equals(adi, kendiAdi, StringComparison.OrdinalIgnoreCase)
                    || (kendiEskiAdi is not null
                        && string.Equals(adi, kendiEskiAdi, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                if (gorulen.Add(dize))
                {
                    yollar.Add(dize);
                }
            });

            return new SwReferanslar(
                yollar,
                kendi,
                Okundu: true,
                kacisGoruldu ? "Bazı yollar 254 karakterden uzun; onlar okunamadı." : null);
        }
        catch (FileNotFoundException)
        {
            return SwReferanslar.Okunamadi("Dosya bulunamadı.");
        }
        catch (DirectoryNotFoundException)
        {
            return SwReferanslar.Okunamadi("Klasör bulunamadı.");
        }
        catch (UnauthorizedAccessException)
        {
            return SwReferanslar.Okunamadi("Dosya okunamadı: erişim reddedildi.");
        }
        catch (IOException hata)
        {
            return SwReferanslar.Okunamadi("Dosya okunamadı: " + hata.Message);
        }
    }

    /// <summary>
    /// Belgenin en son kaydedildigi yolu bulur; yoksa null.
    ///
    /// ADI DEGISMIS DOSYA DA BULUNUR (01.09.2026): eski hal yalnizca
    /// diskteki BUGUNKU adla eslesen yolu kabul ediyordu, yani dosya
    /// disarida yeniden adlandirildiysa "kendi yolu bilinmiyor" diyordu.
    /// Bedeli ikiydi: (1) "Tasinmis dosyalar" raporu o dosyayi hic
    /// gormuyordu, (2) dosyanin icinde yazan eski adi elenemedigi icin
    /// KIRIK sekmesinde bir referans gibi cikiyordu (CLAUDE.md 3).
    ///
    /// SIRA: once bugunku adla tam eslesme (adi degismemis olagan hal),
    /// yoksa AYNI UZANTIYI tasiyan SON yol. Uzanti sarti bilerek: gecmis
    /// akisinda beklenmedik bir yol dursa bile bir montajin parcasi
    /// (.SLDPRT) montajin (.SLDASM) "kendi yolu" sayilamaz - yanlis eleme,
    /// gercek bir referansi gizlerdi.
    /// </summary>
    private static string? KendiYolunuBul(SwPaket paket, string dosyaYolu)
    {
        byte[]? gecmis = paket.AkisiOku(GecmisAkis);
        if (gecmis is null)
        {
            return null;
        }

        string kendiAdi = WindowsYolu.DosyaAdi(dosyaYolu);
        string kendiUzanti = WindowsYolu.Uzanti(dosyaYolu);
        string? adiTutan = null;
        string? uzantisiTutan = null;

        Dizeler(gecmis, dize =>
        {
            if (!YolMu(dize))
            {
                return;
            }

            if (adiTutan is null
                && string.Equals(WindowsYolu.DosyaAdi(dize), kendiAdi, StringComparison.OrdinalIgnoreCase))
            {
                adiTutan = dize;
                return;
            }

            // EN SONUNCUSU tutulur: gecmis akisi kayit sirasina gore yaziliyor,
            // yani sondaki en son kaydedilen yoldur.
            if (string.Equals(WindowsYolu.Uzanti(dize), kendiUzanti, StringComparison.OrdinalIgnoreCase))
            {
                uzantisiTutan = dize;
            }
        });

        return adiTutan ?? uzantisiTutan;
    }

    /// <summary>
    /// Tampondaki MFC Unicode dizelerini tek tek verir.
    /// Doner deger: olculmemis KACIS bicimi goruldu mu (o dizeler atlandi).
    /// </summary>
    /// <summary>
    /// Tampondaki dizeleri gezer. TARAMA <see cref="MfcDize"/>'DE, burada
    /// degil: ayni bicimi yazan bir kod da var (<see cref="SwYazici"/>) ve
    /// iki kopya gunun birinde ayrisirdi (CLAUDE.md 8).
    /// </summary>
    private static bool Dizeler(byte[] tampon, Action<string> her)
        => MfcDize.Tara(tampon, bulgu => her(bulgu.Deger));

    private static bool YolMu(string dize)
    {
        foreach (string u in Uzantilar)
        {
            if (dize.EndsWith(u, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
