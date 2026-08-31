using System;
using System.Collections.Generic;
using System.Threading;

namespace SwPdm.Cekirdek;

/// <summary>Parca listesindeki BIR satir - agactaki bir gorunus.</summary>
/// <param name="Seviye">Girinti: 0 = secilen belge, 1 = dogrudan cocugu.</param>
/// <param name="Ad">Dosya adi (uzantiyla).</param>
/// <param name="Yol">Diskteki yol; bulunamadiysa dosyada YAZAN yol.</param>
/// <param name="Bulundu">
/// Yol diskte gercekten bulundu mu. Bulunmayan satirin yolu bir HEDEF
/// degildir: ona "git" denemez, CSV'de de yalnizca dosyada yazani gosterir.
/// </param>
/// <param name="Tur">Parca · Montaj · Teknik resim · …</param>
/// <param name="KacYerde">
/// Bu dosya agacta kac AYRI ebeveynin altinda geciyor. Kok satirinda 0.
/// ADET DEGILDIR - asagidaki nota bakin.
/// </param>
/// <param name="Yapilandirma">SW-Configuration Name; bilinmiyorsa null.</param>
/// <param name="SonKaydeden">Belgeyi en son kaydeden; bilinmiyorsa null.</param>
/// <param name="Degistirme">Belgenin icinde yazan son kaydetme zamani.</param>
/// <param name="Ozel">Kullanicinin ozellikleri (Malzeme, Ağırlık…).</param>
/// <param name="Durum">Bir sorun ya da aciklama varsa cumlesi; yoksa null.</param>
public sealed record ParcaSatiri(
    int Seviye,
    string Ad,
    string Yol,
    bool Bulundu,
    DosyaTuru Tur,
    int KacYerde,
    string? Yapilandirma,
    string? SonKaydeden,
    DateTime? Degistirme,
    IReadOnlyDictionary<string, string> Ozel,
    string? Durum);

/// <summary>Bir parca listesinin tamami.</summary>
/// <param name="Satirlar">Agac sirasiyla satirlar.</param>
/// <param name="OzelSutunlar">
/// Dosyalarda GERCEKTEN gorulen ozel ozellik adlari, ilk gorulme sirasiyla.
/// Sabit liste YOK (CLAUDE.md 1b): yeni bir ozellik eklenince kod degismez.
/// </param>
/// <param name="Sorunlu">Bulunamayan ya da icine bakilamayan satir sayisi.</param>
/// <param name="Tam">Liste sonuna kadar gidildi mi (iptal edilmediyse true).</param>
/// <param name="Sebep">Tam degilse ya da hic satir yoksa SEBEBI.</param>
public sealed record ParcaListesiSonucu(
    IReadOnlyList<ParcaSatiri> Satirlar,
    IReadOnlyList<string> OzelSutunlar,
    int Sorunlu,
    bool Tam,
    string? Sebep);

/// <summary>
/// PARCA LISTESI (BOM) - "bu montaj neyi kullaniyor", butun agac tek tabloda.
///
/// Agaci <see cref="BelgeAgaci"/> yuruyor, ozellikleri
/// <see cref="SwBelgeBilgisi"/> okuyor; burada olan tek sey ikisini satira
/// cevirmek. Ozelligi kaldirmak = bu dosya + ParcaListesiCsv + iki arayuz
/// dosyasi + AgacIslemleri'nde bir satir (CLAUDE.md 1b).
///
/// ADET SUTUNU YOK - VE BU BILEREK (CLAUDE.md 3). Bir parcanin montajda kac
/// KEZ kullanildigi olculemedi: elimizdeki gercek dosyalarda (Montaj1.SLDASM)
/// her parca birer kez geciyor, yani "yol iki kez yazilir mi" sorusu
/// ORNEKTEN CEVAPLANAMIYOR. Olculen sey su: Montaj1'in dort akisinda da
/// (Header2 · Contents/Config-0-ModelHeader · Contents/DisplayLists ·
/// SwDocContentMgr/SwDocContentMgrInfo) her yol TAM BIR KEZ yaziyor. Bu,
/// bicimin "kullanilan BELGE" yazdigini dusundurur ama KANITLAMAZ. Uydurma
/// bir adet, tekliflerde yanlis fiyat demektir; o yuzden bu sutun ancak iki
/// kez kullanilmis gercek bir parcayla olculdukten sonra acilir.
///
/// YERINE "KAC YERDE GECIYOR" var ve o OLCULEBILIR: agacta kac ayri
/// ebeveynin altinda gorundugu. Tanimi dar ama DOGRU.
///
/// AGIRLIK BAYAT OLABILIR - SwBelgeBilgisi'nde olculdu: dosyada duran deger
/// bir denklemin EN SON HESAPLANMIS sonucu. Yeniden hesaplamiyoruz; uydurma
/// bir sayi yerine dosyada yazani gostermek dogru, ama bunun bayat
/// olabilecegi tabloda ve CSV'de YAZAR.
/// </summary>
public static class ParcaListesi
{
    /// <summary>
    /// Listeyi uretir. Iki gecis: once agac yurunur, sonra her AYRI dosyanin
    /// ozellikleri bir kez okunur (ayni dosya iki satirda gorunse bile ikinci
    /// kez acilmaz).
    /// </summary>
    /// <param name="kok">Listesi cikarilacak belge.</param>
    /// <param name="belirtec">Iptal.</param>
    /// <param name="adim">
    /// Ilerleme: (yapilan, toplam, ad). Toplam ancak AGAC YURUNDUKTEN sonra
    /// bilinir; o yuzden ilk gecis boyunca cagrilmaz - uydurma yuzde YOK
    /// (CLAUDE.md 3).
    /// </param>
    public static ParcaListesiSonucu Uret(
        string? kok,
        CancellationToken belirtec = default,
        Action<int, int, string>? adim = null)
    {
        IReadOnlyList<AgacDugumu> dugumler = BelgeAgaci.Yur(kok, belirtec);
        if (dugumler.Count == 0)
        {
            return new ParcaListesiSonucu(
                [], [], 0, Tam: false,
                string.IsNullOrWhiteSpace(kok) ? "Dosya seçilmedi." : "Belge okunamadı.");
        }

        Dictionary<string, HashSet<string>> ebeveynler = Ebeveynler(dugumler);

        var bilgiler = new Dictionary<string, SwBelgeBilgileri>(StringComparer.OrdinalIgnoreCase);
        var sutunlar = new List<string>();
        var sutunAdlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var satirlar = new List<ParcaSatiri>(dugumler.Count);
        int sorunlu = 0;
        bool iptal = false;

        for (int i = 0; i < dugumler.Count; i++)
        {
            if (belirtec.IsCancellationRequested)
            {
                iptal = true;
                break;
            }

            AgacDugumu dugum = dugumler[i];
            string ad = WindowsYolu.DosyaAdi(dugum.Yol);
            adim?.Invoke(i, dugumler.Count, ad);

            if (dugum.Sorunlu)
            {
                sorunlu++;
            }

            // OZELLIKLER DOSYA BASINA BIR KEZ: ayni parca on montajda
            // geciyorsa dosya on kez acilmaz.
            SwBelgeBilgileri bilgi;
            if (!dugum.Bulundu)
            {
                bilgi = SwBelgeBilgileri.Okunamadi("Dosya bulunamadı.");
            }
            else if (!bilgiler.TryGetValue(dugum.Yol, out SwBelgeBilgileri? onceki))
            {
                bilgi = SwBelgeBilgisi.Oku(dugum.Yol);
                bilgiler[dugum.Yol] = bilgi;
            }
            else
            {
                bilgi = onceki;
            }

            var ozel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> c in bilgi.Ozel)
            {
                ozel[c.Key] = c.Value;

                // SUTUNLAR TURETILIYOR: dosyada gorulen ad sutun olur, ilk
                // gorulme sirasiyla. Kodda sabit bir "Malzeme" listesi YOK.
                if (sutunAdlari.Add(c.Key))
                {
                    sutunlar.Add(c.Key);
                }
            }

            satirlar.Add(new ParcaSatiri(
                dugum.Seviye,
                ad,
                dugum.Yol,
                dugum.Bulundu,
                DosyaTurleri.Tani(ad),
                ebeveynler.TryGetValue(dugum.Yol, out HashSet<string>? kume) ? kume.Count : 0,
                bilgi.Yapilandirma,
                bilgi.SonKaydeden,
                bilgi.Degistirme,
                ozel,
                Durum(dugum, bilgi)));
        }

        return new ParcaListesiSonucu(
            satirlar, sutunlar, sorunlu, Tam: !iptal,
            iptal ? "İptal edildi — liste yarım." : null);
    }

    /// <summary>
    /// Her yolun AYRI ebeveynleri. Yuruyus derinlemesine sirali oldugu icin
    /// bir dugumun ebeveyni, ondan once gelen ve bir ust seviyede duran son
    /// dugumdur; ayri bir alan tutmaya gerek yok.
    /// </summary>
    private static Dictionary<string, HashSet<string>> Ebeveynler(
        IReadOnlyList<AgacDugumu> dugumler)
    {
        var sonuc = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var dal = new List<string>();

        foreach (AgacDugumu dugum in dugumler)
        {
            while (dal.Count > dugum.Seviye)
            {
                dal.RemoveAt(dal.Count - 1);
            }

            if (dugum.Seviye > 0 && dal.Count == dugum.Seviye)
            {
                if (!sonuc.TryGetValue(dugum.Yol, out HashSet<string>? kume))
                {
                    kume = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    sonuc[dugum.Yol] = kume;
                }

                kume.Add(dal[dugum.Seviye - 1]);
            }

            dal.Add(dugum.Yol);
        }

        return sonuc;
    }

    /// <summary>
    /// Satirin durum cumlesi. Agactaki not once gelir (bulunamadi, icine
    /// bakilamadi); yoksa ozellikler okunamadiysa onun sebebi yazar. Ikisi de
    /// temizse null - bos hucre "sorun yok" demektir.
    /// </summary>
    private static string? Durum(AgacDugumu dugum, SwBelgeBilgileri bilgi)
    {
        if (dugum.Not is not null)
        {
            return dugum.Not;
        }

        return bilgi.Okundu ? null : "Özellikleri okunamadı — " + (bilgi.Sebep ?? "sebep bilinmiyor");
    }
}
