using System;
using System.Collections.Generic;
using System.Threading;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// REFERANS BILGISININ ARAYUZDEKI TEK KAPISI.
///
/// Indeksi tutar, diskten yukler, diske yazar, sorgular ve sag alt
/// listeyi doldurur. "Referanslar arayuzde nasil gorunur" sorusunun
/// cevabinin TAMAMI burada (CLAUDE.md 1b).
///
/// EN SERT KURAL (CLAUDE.md 3): taranmamis bir kokte "0 kullanan" YAZILMAZ.
/// Sayi yerine "taranmadı" yazilir. Bos bir liste "bu parcayi kimse
/// kullanmiyor" demek DEGILDIR ve o yanlis, kullaniciya sagam dosya
/// sildirir. Bu yuzden butun metinler
/// <see cref="KullanimSonucu.Guvenilir"/> uzerinden uretiliyor.
/// </summary>
internal sealed class ReferansSurucusu
{
    private ReferansIndeksi? _indeks;

    /// <summary>Su anki kokun indeksi; kok acilmadiysa null.</summary>
    internal ReferansIndeksi? Indeks => _indeks;

    /// <summary>Indeks taranmis ve tam mi.</summary>
    internal bool Hazir => _indeks is { TaramaZamani: not null, Tam: true };

    /// <summary>Kok degisti: o kokun indeksi diskten yuklenir.</summary>
    internal void KokuKur(string? kok)
    {
        _indeks = string.IsNullOrWhiteSpace(kok) ? null : IndeksDosyasi.Oku(kok);
    }

    /// <summary>Taramayi kosturur (ARKA PLANDA cagrilmali) ve sonucu diske yazar.</summary>
    internal TaramaSonucu? Tara(CancellationToken belirtec, Action<int, int, string> ilerleme)
    {
        ReferansIndeksi? indeks = _indeks;
        if (indeks is null)
        {
            return null;
        }

        TaramaSonucu sonuc = IndeksTarama.Tara(indeks, belirtec, ilerleme);
        IndeksDosyasi.Yaz(indeks);
        return sonuc;
    }

    /// <summary>
    /// Onizleme panelindeki "Kullanan:" satiri.
    ///
    /// Uc ayri hal, UCU DE farkli yazilir - "0" hepsini ayni gostermek olurdu:
    ///   taranmadi        -> bilmiyoruz
    ///   taranmis, 0      -> gercekten kullanan yok
    ///   taranmis, n      -> n dosya
    /// </summary>
    internal string KullananMetni(string? yol)
    {
        if (_indeks is null || string.IsNullOrWhiteSpace(yol))
        {
            return "taranmadı";
        }

        KullanimSonucu sonuc = _indeks.Kullananlar(yol);
        if (!sonuc.Guvenilir)
        {
            return sonuc.Kullananlar.Count > 0
                ? $"{sonuc.Kullananlar.Count} dosya (liste eksik olabilir)"
                : "taranmadı";
        }

        return sonuc.Kullananlar.Count == 0 ? "yok" : $"{sonuc.Kullananlar.Count} dosya";
    }

    /// <summary>
    /// Sag alt listeyi doldurur: once bu dosyanin KULLANDIKLARI, sonra onu
    /// KULLANANLAR.
    ///
    /// Liste bos kalirsa sebebi de yaziliyor - bos bir liste tek basina
    /// hicbir sey iddia etmemeli.
    /// </summary>
    internal void Doldur(ReferansListesi liste, string? yol)
    {
        ArgumentNullException.ThrowIfNull(liste);

        liste.BeginUpdate();
        try
        {
            liste.Items.Clear();

            if (_indeks is null || string.IsNullOrWhiteSpace(yol) || !SwReferans.TasiyabilirMi(yol))
            {
                return;
            }

            foreach ((string yazilan, Cozum cozum) in _indeks.Kullandiklari(yol))
            {
                liste.Ekle(WindowsYolu.DosyaAdi(yazilan), RolMetni(cozum), Simge(yazilan));
            }

            KullanimSonucu kullananlar = _indeks.Kullananlar(yol);
            foreach (string kullanan in kullananlar.Kullananlar)
            {
                liste.Ekle(WindowsYolu.DosyaAdi(kullanan), "kullanıyor", Simge(kullanan));
            }

            if (liste.Items.Count == 0)
            {
                liste.Ekle(
                    kullananlar.Guvenilir ? "Referansı yok" : "Bilinmiyor",
                    kullananlar.Guvenilir ? "—" : "taranmadı",
                    TurSimgeleri.GenelDosya);
            }
        }
        finally
        {
            liste.EndUpdate();
        }
    }

    /// <summary>
    /// Bir dosyayi kullananlarin yollari. Guvenilirlik burada DUSMEZ:
    /// cagiran once <see cref="Hazir"/>'a bakmali, yoksa bos liste
    /// "kullanan yok" diye okunur (CLAUDE.md 3).
    /// </summary>
    internal IReadOnlyList<string> Kullananlarin(string yol)
        => _indeks is null ? [] : _indeks.Kullananlar(yol).Kullananlar;

    /// <summary>Kullanicinin gordugu rol yazisi. BELIRSIZ olan SAKLANMAZ.</summary>
    private static string RolMetni(Cozum cozum) => cozum.Durum switch
    {
        CozumDurumu.Bulundu => "kullanıyor →",
        CozumDurumu.Belirsiz => $"{cozum.Adaylar.Count} aday —belirsiz",
        _ => "BULUNAMADI",
    };

    private static int Simge(string yol) => TurSimgeleri.Sira(DosyaTurleri.Tani(yol));
}
