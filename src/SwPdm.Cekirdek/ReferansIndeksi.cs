using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

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

/// <summary>Panelde gosterilecek referans satirlari ve gizlenenlerin sayisi.</summary>
/// <param name="Gosterilecekler">Cozulmus, belirsiz ya da kok disindaki satirlar.</param>
/// <param name="Gizlenen">
/// Gizlenen satir sayisi: ne taranan agacta ne diskteki yazili yerde bulunanlar.
/// </param>
public sealed record PanelSatirlari(
    IReadOnlyList<(string YazilanYol, Cozum Cozum)> Gosterilecekler, int Gizlenen);

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

    // "Yazili yol diskte var mi" cevaplari. ConcurrentDictionary: arka plan
    // taramasi yazarken arayuz okuyor.
    private readonly ConcurrentDictionary<string, bool> _diskteVar
        = new(StringComparer.OrdinalIgnoreCase);

    private int? _okunamayan;

    /// <summary>Yeni, bos indeks.</summary>
    public ReferansIndeksi(string kok) => Kok = kok;

    /// <summary>Indeksin ait oldugu kok klasor.</summary>
    public string Kok { get; }

    /// <summary>Son taramanin bittigi an; hic taranmadiysa null.</summary>
    public DateTime? TaramaZamani { get; private set; }

    /// <summary>Son tarama SONUNA KADAR gitti mi (iptal/hata yok).</summary>
    public bool Tam { get; private set; }

    /// <summary>
    /// Son diske yazmadan bu yana indeks DEGISTI mi.
    ///
    /// NEDEN VAR: indeks her taramadan sonra BASTAN yaziliyordu (5000 dosyada
    /// ~2,5 MB) - hicbir sey degismese bile. Islem oncesi/sonrasi tarama
    /// geldiginde bu, tek bir adlandirma icin dort yazim demeye basladi.
    /// </summary>
    public bool Degisti { get; private set; } = true;

    /// <summary>Diske yazildi: bir sonraki degisikliğe kadar yeniden yazilmaz.</summary>
    public void YazildiIsaretle() => Degisti = false;

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
        Degisti = true;
        Bozuldu();
    }

    /// <summary>Kaydi siler. Doner: gercekten vardi mi.</summary>
    public bool Sil(string yol)
    {
        bool vardi = _kayitlar.Remove(yol);
        if (vardi)
        {
            Degisti = true;
            Bozuldu();
        }

        return vardi;
    }

    /// <summary>Taramanin bittigini isaretler.</summary>
    public void TaramayiBitir(bool tam, DateTime zaman)
    {
        // ILK TARAMA ve GUVENILIRLIK DEGISIMI diske yazilmali: ikisi de
        // sonraki oturumun "taranmadi mi, eksik mi" cevabini belirliyor.
        // Yalnizca zamanin ilerlemesi bir yazim sebebi DEGIL.
        if (TaramaZamani is null || Tam != tam)
        {
            Degisti = true;
        }

        Tam = tam;
        TaramaZamani = zaman;
    }

    /// <summary>Ayni ada sahip taranmis dosyalar.</summary>
    public IReadOnlyList<string> AdaGoreAdaylar(string ad)
    {
        AdDizinini_Kur();
        return _adaGore!.TryGetValue(ad, out List<string>? yollar) ? yollar : [];
    }

    /// <summary>
    /// Bir kaydin yazdigi yolu gercek dosyaya cozer.
    ///
    /// KOK DISINDA AYRIMI BURADA yapiliyor, cozucude degil: cozucu yalnizca
    /// elindeki adaylara bakar, taramanin nereye kadar gittigini indeks bilir
    /// (<see cref="ReferansCozucu"/> belgesi). Adaylarda yoksa AMA yazili yol
    /// diskte gercek bir dosyayi gosteriyorsa bu "kayip" degil "taranmamis
    /// yerdeki dosya"dir - SOLIDWORKS onu acar. Ikisini ayni kelimeyle anlatmak
    /// kullaniciya saglam referansi kirik gosteriyordu (CLAUDE.md 3).
    ///
    /// Yalnizca MUTLAK yol diskte aranir: goreli bir yol calisma klasorune
    /// gore bakilirdi ve o cevap yalan olurdu. Kokun ALTINDAKI ama indekste
    /// olmayan dosya da "kok disinda" SAYILMAZ - orasi taramanin isi.
    ///
    /// BU METOT DISKE HIC DOKUNMAZ - yalnizca <see cref="DiskiYokla"/>'nin
    /// doldurdugu onbellegi okur. Sebebi asagida, o metodun belgesinde:
    /// Erkan'in makinesinde arayuz DONDU (30.08.2026).
    /// </summary>
    public Cozum Coz(IndeksKaydi kaynak, string yazilanYol)
    {
        ArgumentNullException.ThrowIfNull(kaynak);
        Cozum cozum = ReferansCozucu.Coz(
            yazilanYol, kaynak.Yol, AdaGoreAdaylar(WindowsYolu.DosyaAdi(yazilanYol)));

        if (cozum.Durum == CozumDurumu.Bulunamadi
            && MutlakMi(yazilanYol)
            && !WindowsYolu.AltindaMi(yazilanYol, Kok)
            && DiskteVar(yazilanYol))
        {
            return new Cozum(CozumDurumu.KokDisinda, yazilanYol, []);
        }

        return cozum;
    }

    /// <summary>Surucu ("C:"), UNC ("\\sunucu") ya da kokten baslayan yol.</summary>
    private static bool MutlakMi(string yol)
        => yol.Length >= 2 && (yol[1] == ':' || WindowsYolu.AyiriciMi(yol[0]));

    /// <summary>Onbellekteki cevap; hic bakilmamis yol "yok" sayilir.</summary>
    private bool DiskteVar(string yol) => _diskteVar.TryGetValue(yol, out bool var) && var;

    /// <summary>
    /// "Kok disinda" sorusunun DISK YOKLAMASI - ARKA PLANDA cagrilmali;
    /// tarama kendi sonunda cagiriyor.
    ///
    /// NEDEN COZUMDE DEGIL - ERKAN'IN MAKINESINDE OLCULDU (30.08.2026):
    /// ilk hal File.Exists'i cozum aninda cagiriyordu. Ilk secimde ters
    /// dizin kurulurken BUTUN indeksin cozulemeyen yollari arayuz is
    /// parcaciginda tek tek yoklandi ve olu/erisilemez yollarda uygulama
    /// DONDU - "boyle kaldi". O yuzden cozum yalnizca onbellegi okur;
    /// diske dokunan tek yer burasi ve tarama gibi arka planda kosar.
    ///
    /// HENUZ BAKILMAMIS yol "bulunamadi" sayilir - yanlis yonde degil:
    /// dosya taranan agacta gercekten yok, yalnizca "kok disinda" incelmesi
    /// bir tarama gec gelir ve satir o zamana kadar gizli kalir.
    ///
    /// AYNI YOL IKINCI KEZ YOKLANMAZ (bu indeks nesli boyunca): olu bir
    /// sunucu adi her taramada yeniden dakikalar yedirirdi. Bedeli durustce
    /// soylenen bir bayatlik: kok disindaki dosya sonradan silinirse cevap
    /// kok yeniden acilana kadar eski kalir - kok disini zaten hicbir sey
    /// izlemiyor, indeksin oradaki bilgisi her turlu "en son bakildiginda".
    /// </summary>
    public void DiskiYokla(CancellationToken belirtec = default)
    {
        // Kopya uzerinde geziliyor: yoklama surerken baska bir is parcacigi
        // Koy/Sil cagirirsa numaralandirma patlardi (dusen-kayit dongusuyle
        // ayni kalip).
        foreach (IndeksKaydi kayit in new List<IndeksKaydi>(_kayitlar.Values))
        {
            foreach (string yazilan in kayit.YazilanYollar)
            {
                if (belirtec.IsCancellationRequested)
                {
                    return;
                }

                if (_diskteVar.ContainsKey(yazilan)
                    || !MutlakMi(yazilan)
                    || WindowsYolu.AltindaMi(yazilan, Kok))
                {
                    continue;
                }

                // Adaylari olan yol zaten cozuluyor; diske sormaya deger
                // olan yalniz COZULEMEYEN.
                Cozum cozum = ReferansCozucu.Coz(
                    yazilan, kayit.Yol, AdaGoreAdaylar(WindowsYolu.DosyaAdi(yazilan)));
                if (cozum.Durum != CozumDurumu.Bulunamadi)
                {
                    continue;
                }

                _diskteVar[yazilan] = File.Exists(yazilan);
            }
        }
    }

    /// <summary>
    /// YAZILI YOL BAYAT MI - yani dosya duruyor ama SOLIDWORKS ACAMAZ.
    ///
    /// BIZ dosyayi ADA ve KOMSULUGA gore buluyoruz; SOLIDWORKS ise dosya
    /// ebeveynin YANINDA DEGILSE dosyanin icindeki YAZILI YOLA bakiyor
    /// (CLAUDE.md 5). Ikisi ayrisinca hata GORUNMEZ kaliyor - Erkan'in
    /// dosyasi tam bu yuzden acilmadi (28.08.2026).
    ///
    /// Uc sart birden:
    ///   1. dosya BULUNDU (bulunamayan zaten ayri bir sorun)
    ///   2. ebeveynin YANINDA DEGIL (yanindaysa komsuluk kurali kurtarir)
    ///   3. yazili yol, ebeveyne gore cozuldugunde gercek dosyayi GOSTERMIYOR
    ///
    /// TEK KOPYA (CLAUDE.md 8): hem referans paneli, hem "Bayat yollar"
    /// raporu, hem toplu onarim buna soruyor.
    /// </summary>
    public static bool BayatMi(string ebeveynYolu, string yazilan, Cozum? cozum)
    {
        if (cozum is null || cozum.Durum != CozumDurumu.Bulundu || cozum.Yol is not string gercek)
        {
            return false;
        }

        string ebeveynKlasoru = WindowsYolu.Klasor(ebeveynYolu);
        if (string.Equals(
                WindowsYolu.Klasor(gercek), ebeveynKlasoru, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.Equals(
            WindowsYolu.Cozumle(ebeveynKlasoru, yazilan),
            WindowsYolu.Cozumle(null, gercek),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Panelde GOSTERILECEK satirlar ve GIZLENEN sayisi.
    ///
    /// NEDEN GIZLIYORUZ (Erkan, 29.08.2026 - gercek veriyle): 43 referansli
    /// bir montajda satirlarin neredeyse tamami "BULUNAMADI" cikiyordu ve
    /// panel okunamaz hale geliyordu.
    ///
    /// O satirlarin cogu aslinda "kayip dosya" degil "taranmamis yerdeki
    /// dosya"ydi; artik <see cref="CozumDurumu.KokDisinda"/> olarak AYRILIYOR
    /// ve GIZLENMIYOR - dosya gercek, onizlenebilir ve SOLIDWORKS acar.
    /// Gizlenen yalnizca GERCEKTEN bulunamayan: ne agacta ne diskte.
    ///
    /// GIZLEMEK SESSIZ DEGIL: sayi cagirana DONUYOR ve ekranda yaziliyor.
    /// Kisalmis bir liste "bu dosya bunlari kullanmiyor" diye okunursa
    /// saglam dosya sildirir (CLAUDE.md 3).
    ///
    /// BELIRSIZ SATIRLAR GIZLENMEZ: orada gercek bir karar var (n aday) ve
    /// karari kullanici verecek.
    /// </summary>
    public PanelSatirlari KullandiklariGorunur(string yol)
    {
        var gosterilecek = new List<(string, Cozum)>();
        int gizlenen = 0;

        foreach ((string yazilan, Cozum cozum) in Kullandiklari(yol))
        {
            if (cozum.Durum == CozumDurumu.Bulunamadi)
            {
                gizlenen++;
                continue;
            }

            gosterilecek.Add((yazilan, cozum));
        }

        return new PanelSatirlari(gosterilecek, gizlenen);
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

        if (!Tam && Okunamayanlar() == 0)
        {
            // Tam degil ama okunamayan dosya da yok: tarama iptal edilmis ya
            // da bir KLASOR okunamamis.
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
        //
        // SEBEP DOGRU YAZILMALI - OLCULDU (28.08.2026): burasi okunamayan
        // dosya varken de "Tarama yarım kaldı" diyordu, oysa tarama
        // BITMISTI. Kullanici ekranda yanlis sebep goruyordu (CLAUDE.md 3:
        // hata sebebi gosterilir - yanlis sebep gostermek daha kotusu).
        int okunamayan = Okunamayanlar();

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
            // "Altinda mi" TEK kopyadan sorulur (CLAUDE.md 8). Esitlik de
            // dahil - cagiran zaten Contains ile eledigi icin zararsiz.
            if (WindowsYolu.AltindaMi(yol, s))
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

    // _diskteVar burada TEMIZLENMIYOR: o kayitlardan degil DISKTEN turuyor
    // ve yeniden doldurmasi pahali (DiskiYokla belgesi). Kayit degisimi
    // diskteki dis dosyalar hakkinda yeni bir sey soylemez.
    private void Bozuldu()
    {
        _adaGore = null;
        _kullananlar = null;
        _okunamayan = null;
    }

    /// <summary>
    /// Okunamamis kayit sayisi - ONBELLEKLI.
    ///
    /// NEDEN ONBELLEK: bu sayi her <see cref="Kullananlar"/> cagrisinda butun
    /// kayitlar gezilerek bulunuyordu, yani agactaki her SECIM DEGISIMINDE
    /// dosya sayisi kadar dongu. Ters dizinle ayni omre sahip; kayit
    /// degisince ikisi birden dusuyor.
    /// </summary>
    private int Okunamayanlar()
    {
        if (_okunamayan is int hazir)
        {
            return hazir;
        }

        int sayi = 0;
        foreach (IndeksKaydi k in _kayitlar.Values)
        {
            if (!k.Okundu)
            {
                sayi++;
            }
        }

        _okunamayan = sayi;
        return sayi;
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
