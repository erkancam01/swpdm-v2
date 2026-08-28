using System;
using System.Collections.Generic;

namespace SwPdm.Cekirdek;

/// <summary>Indekste bir dosyanin kaydi.</summary>
/// <param name="Yol">Dosyanin GERCEK yolu (bu makinede).</param>
/// <param name="Boyut">Bayt. Artimli taramada "degisti mi" bunun icin tutuluyor.</param>
/// <param name="Degistirme">Son degistirme zamani. Ayni sebeple.</param>
/// <param name="YazilanYollar">Dosyanin ICINDE yazan referans yollari (ham).</param>
/// <param name="KendiYolu">Dosyanin en son kaydedildigi yol; bilinmiyorsa null.</param>
/// <param name="Okundu">Referanslar okunabildi mi.</param>
/// <param name="Sebep">Okunamadiysa sebep.</param>
public sealed record IndeksKaydi(
    string Yol,
    long Boyut,
    DateTime Degistirme,
    IReadOnlyList<string> YazilanYollar,
    string? KendiYolu,
    bool Okundu,
    string? Sebep);

/// <summary>"Bu dosyayi kim kullaniyor" sorusunun cevabi.</summary>
/// <param name="Kullananlar">Bu dosyaya referans veren dosyalarin yollari.</param>
/// <param name="Guvenilir">
/// Cevap TAM mi. false ise bos liste "kimse kullanmiyor" ANLAMINA GELMEZ.
/// </param>
/// <param name="Sebep">Guvenilir degilse neden.</param>
public sealed record KullanimSonucu(
    IReadOnlyList<string> Kullananlar, bool Guvenilir, string? Sebep);

/// <summary>
/// REFERANS INDEKSI - "bu parcayi kim kullaniyor" sorusunun tek kapisi.
///
/// NEDEN VAR: kullanici bir dosyayi silmeden ya da tasimadan once ona kimin
/// dokundugunu bilmeli. Dosyanin kendisine sormak yetmez - soru TERS
/// yonde: "beni kim kullaniyor". Bunun cevabi ancak butun agac taranarak
/// bulunur, o yuzden bir indeks var.
///
/// EN ONEMLI KURAL (CLAUDE.md 3): BOS LISTE "YOK" DEMEK DEGILDIR.
/// Taranmamis bir kokte sorgu bos doner; bunu "bu parcayi kimse
/// kullanmiyor" diye gostermek SAGLAM DOSYA SILDIRIR. Bu yuzden her cevap
/// <see cref="KullanimSonucu.Guvenilir"/> tasiyor ve arayuz guvenilir
/// olmayan bir cevabi SAYI olarak gosteremez.
///
/// Bu sinif yalnizca VERI ve SORGU. Diskten doldurmak
/// <see cref="IndeksTarama"/>'da, dosyaya yazmak <see cref="IndeksDosyasi"/>'nda
/// (CLAUDE.md 1b: bir konu, bir dosya).
/// </summary>
public sealed class ReferansIndeksi
{
    private readonly Dictionary<string, IndeksKaydi> _kayitlar
        = new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, List<string>>? _adaGore;
    private Dictionary<string, List<string>>? _kullananlar;

    /// <summary>Yeni, bos indeks.</summary>
    public ReferansIndeksi(string kok) => Kok = kok;

    /// <summary>Indeksin ait oldugu kok klasor.</summary>
    public string Kok { get; }

    /// <summary>Son taramanin bittigi an; hic taranmadiysa null.</summary>
    public DateTime? TaramaZamani { get; private set; }

    /// <summary>Son tarama SONUNA KADAR gitti mi (iptal/hata yok).</summary>
    public bool Tam { get; private set; }

    /// <summary>Indeksteki dosya sayisi.</summary>
    public int DosyaSayisi => _kayitlar.Count;

    /// <summary>Butun kayitlar.</summary>
    public IReadOnlyCollection<IndeksKaydi> Kayitlar => _kayitlar.Values;

    /// <summary>Bir dosyanin kaydi; indekste yoksa null.</summary>
    public IndeksKaydi? Kayit(string yol)
        => _kayitlar.TryGetValue(yol, out IndeksKaydi? k) ? k : null;

    /// <summary>Kaydi ekler ya da gunceller.</summary>
    public void Koy(IndeksKaydi kayit)
    {
        ArgumentNullException.ThrowIfNull(kayit);
        _kayitlar[kayit.Yol] = kayit;
        Bozuldu();
    }

    /// <summary>Kaydi siler. Doner: gercekten vardi mi.</summary>
    public bool Sil(string yol)
    {
        bool vardi = _kayitlar.Remove(yol);
        if (vardi)
        {
            Bozuldu();
        }

        return vardi;
    }

    /// <summary>Taramanin bittigini isaretler.</summary>
    public void TaramayiBitir(bool tam, DateTime zaman)
    {
        Tam = tam;
        TaramaZamani = zaman;
    }

    /// <summary>Ayni ada sahip taranmis dosyalar.</summary>
    public IReadOnlyList<string> AdaGoreAdaylar(string ad)
    {
        AdDizinini_Kur();
        return _adaGore!.TryGetValue(ad, out List<string>? yollar) ? yollar : [];
    }

    /// <summary>Bir kaydin yazdigi yolu gercek dosyaya cozer.</summary>
    public Cozum Coz(IndeksKaydi kaynak, string yazilanYol)
    {
        ArgumentNullException.ThrowIfNull(kaynak);
        return ReferansCozucu.Coz(
            yazilanYol, kaynak.Yol, AdaGoreAdaylar(WindowsYolu.DosyaAdi(yazilanYol)));
    }

    /// <summary>Bu dosyanin KULLANDIKLARI: yazilan yol + cozumu.</summary>
    public IReadOnlyList<(string YazilanYol, Cozum Cozum)> Kullandiklari(string yol)
    {
        IndeksKaydi? kayit = Kayit(yol);
        if (kayit is null)
        {
            return [];
        }

        var sonuc = new List<(string, Cozum)>();
        foreach (string yazilan in kayit.YazilanYollar)
        {
            sonuc.Add((yazilan, Coz(kayit, yazilan)));
        }

        return sonuc;
    }

    /// <summary>
    /// Bu dosyayi KULLANANLAR. Cevabin guvenilir olup olmadigi da doner -
    /// bos liste tek basina "kimse kullanmiyor" DEMEK DEGILDIR.
    /// </summary>
    public KullanimSonucu Kullananlar(string yol)
    {
        TersDizini_Kur();
        IReadOnlyList<string> bulunanlar =
            _kullananlar!.TryGetValue(yol, out List<string>? liste) ? liste : [];

        if (TaramaZamani is null)
        {
            return new KullanimSonucu(bulunanlar, Guvenilir: false, "Bu kök henüz taranmadı.");
        }

        if (!Tam)
        {
            return new KullanimSonucu(
                bulunanlar, Guvenilir: false, "Tarama yarım kaldı; liste eksik olabilir.");
        }

        if (Kayit(yol) is null)
        {
            return new KullanimSonucu(
                bulunanlar, Guvenilir: false, "Bu dosya taranan kökün dışında.");
        }

        // Okunamamis dosya varsa cevap TAM degildir: o dosyanin icinde bu
        // parcaya bir referans olabilirdi ve bilmiyoruz.
        int okunamayan = 0;
        foreach (IndeksKaydi k in _kayitlar.Values)
        {
            if (!k.Okundu)
            {
                okunamayan++;
            }
        }

        return okunamayan == 0
            ? new KullanimSonucu(bulunanlar, Guvenilir: true, null)
            : new KullanimSonucu(
                bulunanlar, Guvenilir: false,
                $"{okunamayan} dosya okunamadı; liste eksik olabilir.");
    }

    /// <summary>
    /// Verilen secimin KULLANDIGI ama secimde OLMAYAN dosyalar - zincirin
    /// tamami (montaj -> alt montaj -> parca).
    ///
    /// Klasor secildiyse ALTINDAKILER de secimde sayilir.
    /// Cozulememis referanslar EKLENMEZ: nereye gidecegi belirsizken bir
    /// dosyayi listeye koymak uydurma olurdu (CLAUDE.md 3).
    /// </summary>
    public IReadOnlyList<string> ZincirdekiEksikler(IReadOnlyList<string>? yollar)
    {
        var eksik = new List<string>();
        if (yollar is null || yollar.Count == 0)
        {
            return eksik;
        }

        var secili = new HashSet<string>(yollar, StringComparer.OrdinalIgnoreCase);
        var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sira = new Queue<string>(yollar);

        while (sira.Count > 0)
        {
            string suan = sira.Dequeue();
            if (!gorulen.Add(suan))
            {
                continue;
            }

            foreach ((_, Cozum cozum) in Kullandiklari(suan))
            {
                if (cozum.Durum != CozumDurumu.Bulundu || cozum.Yol is not string hedef)
                {
                    continue;
                }

                if (!secili.Contains(hedef) && !AltindaMi(secili, hedef)
                    && !gorulen.Contains(hedef) && !Iceriyor(eksik, hedef))
                {
                    eksik.Add(hedef);
                }

                sira.Enqueue(hedef);
            }
        }

        return eksik;
    }

    /// <summary>Secilen bir KLASORUN altinda mi.</summary>
    private static bool AltindaMi(HashSet<string> secili, string yol)
    {
        foreach (string s in secili)
        {
            if (yol.StartsWith(s + WindowsYolu.Ayirici, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Iceriyor(List<string> liste, string aranan)
    {
        foreach (string v in liste)
        {
            if (string.Equals(v, aranan, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void Bozuldu()
    {
        _adaGore = null;
        _kullananlar = null;
    }

    private void AdDizinini_Kur()
    {
        if (_adaGore is not null)
        {
            return;
        }

        var dizin = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (IndeksKaydi k in _kayitlar.Values)
        {
            string ad = WindowsYolu.DosyaAdi(k.Yol);
            if (!dizin.TryGetValue(ad, out List<string>? liste))
            {
                liste = [];
                dizin[ad] = liste;
            }

            liste.Add(k.Yol);
        }

        _adaGore = dizin;
    }

    private void TersDizini_Kur()
    {
        if (_kullananlar is not null)
        {
            return;
        }

        AdDizinini_Kur();
        var ters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (IndeksKaydi k in _kayitlar.Values)
        {
            foreach (string yazilan in k.YazilanYollar)
            {
                Cozum cozum = Coz(k, yazilan);

                // BELIRSIZ olanlar da sayilir: hangi aday oldugunu bilmiyoruz,
                // o yuzden HEPSI icin "bunu kullanan var" denir. Fazla uyarmak
                // zararsiz, eksik uyarmak dosya sildirir (CLAUDE.md 1a).
                IEnumerable<string> hedefler = cozum.Durum switch
                {
                    CozumDurumu.Bulundu => [cozum.Yol!],
                    CozumDurumu.Belirsiz => cozum.Adaylar,
                    _ => [],
                };

                foreach (string hedef in hedefler)
                {
                    if (!ters.TryGetValue(hedef, out List<string>? liste))
                    {
                        liste = [];
                        ters[hedef] = liste;
                    }

                    if (!Iceriyor(liste, k.Yol))
                    {
                        liste.Add(k.Yol);
                    }
                }
            }
        }

        _kullananlar = ters;
    }
}
