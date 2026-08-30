using System.Collections.Generic;
using System.Drawing;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// UC BOLUMUN ICERIGI - "hangi satir hangi bolumde, ne yaziyor" sorusunun
/// cevabi. <see cref="ReferansSurucusu"/>'nun ayni ozelligi; ayri dosyada
/// olmasinin TEK sebebi boyut kapisi (600 satir): indeks yonetimi ile bolum
/// cizimi bir arada 638 satir ediyordu.
///
/// EN SERT KURAL BURADA GECERLI (CLAUDE.md 3): bos bir bolum tek basina
/// "yok" demek DEGILDIR. Uc bolumun de kendi bosluk cumlesi var ve
/// "taranmadi" ile "yok" ASLA ayni kelimeyle yazilmiyor - taranmamis kokte
/// "kullanan yok" demek kullaniciya saglam dosya sildirir.
/// </summary>
internal sealed partial class ReferansSurucusu
{
    /// <summary>ASAGI bolumu: bu dosyanin ICINDEKILER.</summary>
    private void Asagiyi(ReferansListesi liste, string yol)
    {
        IndeksKaydi? kayit = _indeks!.Kayit(yol);
        if (kayit is null)
        {
            Aciklama(liste, "Bu kök henüz taranmadı.", "Ctrl+Shift+R", Renkler.ReferansAsagiYazi);
            return;
        }

        if (!kayit.Okundu)
        {
            Aciklama(
                liste, kayit.Sebep ?? "Dosyanın referansları okunamadı.", "hata",
                Renkler.ReferansAsagiYazi);
            return;
        }

        List<(string Yazilan, Cozum Cozum)> gorunen = Icindekiler(yol);

        if (gorunen.Count == 0)
        {
            // IKI AYRI BOSLUK, IKI AYRI CUMLE (CLAUDE.md 3). Ikisini ayni
            // kelimeyle yazmak, referanslarinin HEPSI kirik olan bir dosyayi
            // "hicbir sey kullanmiyor" diye gosterirdi - ve bu uygulamada
            // oyle bir yanlis okuma saglam dosya sildirir.
            //
            // KISA CUMLE SART: ad sutunu dar ve uzun cumle KIRPILIYOR -
            // "Bu dosya başka dosya kull..." ekranda tam TERSI anlama
            // ("kullanıyor") okunabiliyordu (olculdu, 30.08.2026).
            Aciklama(
                liste,
                kayit.YazilanYollar.Count == 0
                    ? "Başka dosya kullanmıyor."
                    : "Hepsi kırık — KIRIK bölümünde.",
                Ilgisiz,
                Renkler.ReferansAsagiYazi);
            return;
        }

        foreach ((string yazilan, Cozum cozum) in gorunen)
        {
            // IPUCUNDA HANGI YOL: bulunduysa dosyanin GERCEK yeri (kullanici
            // "hangi dosya" diye ona bakiyor), bulunamadiysa dosyanin ICINDE
            // yazan yol (aranan seyin ne oldugunu ancak o soyluyor).
            liste.Ekle(
                WindowsYolu.DosyaAdi(yazilan),
                AsagiRol(cozum),
                Simge(yazilan),
                Renkler.ReferansAsagiYazi,
                cozum.Durum == CozumDurumu.Bulundu ? cozum.Yol : null,
                cozum.Yol ?? yazilan);
        }

        // LISTENIN ALTINDAKI "N kırık referans — KIRIK bölümünde" SATIRI
        // KALKTI (Erkan, 30.08.2026: "gerek yok, zaten kırık dosyalar diye
        // bölüm var"). Guvenli, cunku ayni bilgi seritte HER AN duruyor
        // ("KIRIK 29 dosya") ve serit sarmali - gizlenmiyor. Ustelik
        // sekmedeki sayi artik listedeki satir sayisiyla BIREBIR ayni;
        // "43 yaziyor, 14 satir var" celiskisi de bu turda kapandi.
    }

    /// <summary>
    /// ICINDEKILER bolumunde GORUNECEK satirlar - kirik olmayanlar.
    ///
    /// TEK KAYNAK (CLAUDE.md 8): hem <see cref="Asagiyi"/> bunu ciziyor hem
    /// seritteki sayi bunu sayiyor. Once sayi yazilan yollarin TAMAMINDAN
    /// geliyordu ve ayrismisti: Erkan'in ekraninda sekme "43 dosya" derken
    /// listede 14 satir vardi (29'u KIRIK bolumunde).
    ///
    /// "Belirsiz" (n aday) satirlar BURADA KALIR - orada gercek bir karar
    /// var ve karari kullanici verecek; kirik degil.
    /// </summary>
    private List<(string Yazilan, Cozum Cozum)> Icindekiler(string yol)
    {
        var sonuc = new List<(string, Cozum)>();

        foreach ((string yazilan, Cozum cozum) in _indeks!.KullandiklariGorunur(yol).Gosterilecekler)
        {
            if (!ReferansIndeksi.BayatMi(yol, yazilan, cozum))
            {
                sonuc.Add((yazilan, cozum));
            }
        }

        return sonuc;
    }

    /// <summary>
    /// ICINDEKILER sekmesinin sayisi - <see cref="Icindekiler"/>'i sayar,
    /// yani ekranda GERCEKTEN duran satirlari. <see cref="KirikMetni"/>'nin
    /// birebir kardesi; "0" ile "bilmiyoruz" ayni kelimeyle yazilmaz
    /// (CLAUDE.md 3).
    /// </summary>
    private string IcindekilerMetni(string yol)
    {
        IndeksKaydi? kayit = _indeks!.Kayit(yol);
        if (kayit is null)
        {
            return "taranmadı";
        }

        if (!kayit.Okundu)
        {
            return "okunamadı";
        }

        int adet = Icindekiler(yol).Count;
        return adet == 0 ? "yok" : $"{adet} dosya";
    }

    /// <summary>
    /// KIRIK bolumu: SOLIDWORKS'un acamayacagi referanslar. Iki hal, ikisi de
    /// ayri kelimeyle (Erkan'in karari, 30.08.2026):
    ///   BULUNAMADI - bu adda dosya taranan agacta YOK (satirda dosyanin
    ///                ICINDE yazan yol durur; aranan seyi ancak o soyluyor)
    ///   yol BAYAT  - dosya duruyor ama belgedeki yol baska yeri gosteriyor
    ///
    /// Bunlar once ICINDEKILER'de karisik duruyordu ve BULUNAMADI olanlar
    /// hic gosterilmiyordu (43 referansin hepsi BULUNAMADI cikinca panel
    /// okunamaz oluyordu). Ayri bolum ikisini de cozuyor: liste okunur
    /// kaliyor VE onarilacaklar gorunur oluyor.
    /// </summary>
    private void Kiriklari(ReferansListesi liste, string yol)
    {
        IndeksKaydi? kayit = _indeks!.Kayit(yol);
        if (kayit is null)
        {
            Aciklama(liste, "Bu kök henüz taranmadı.", "Ctrl+Shift+R", Renkler.YolBayatYazi);
            return;
        }

        if (!kayit.Okundu)
        {
            Aciklama(
                liste, kayit.Sebep ?? "Dosyanın referansları okunamadı.", "hata",
                Renkler.YolBayatYazi);
            return;
        }

        List<(string Yazilan, Cozum Cozum, bool Bayat)> kirikler = Kirikler(yol);
        if (kirikler.Count == 0)
        {
            Aciklama(liste, "Kırık referans yok.", Ilgisiz, Renkler.YolBayatYazi);
            return;
        }

        foreach ((string yazilan, Cozum cozum, bool bayat) in kirikler)
        {
            liste.Ekle(
                WindowsYolu.DosyaAdi(yazilan),
                bayat ? "yol BAYAT" : "BULUNAMADI",
                Simge(yazilan),
                Renkler.YolBayatYazi,
                bayat ? cozum.Yol : null,
                bayat ? cozum.Yol ?? yazilan : yazilan);
        }
    }

    /// <summary>
    /// KIRIK bolumun satirlari - TEK YERDE. Uc musterisi var: bolumun
    /// kendisi, seritteki sayi ve ICINDEKILER'in altindaki "kaci ayrildi"
    /// satiri. Ikinci bir kopya yazilsa biri gunun birinde otekinden farkli
    /// sayi gosterirdi (CLAUDE.md 8).
    /// </summary>
    private List<(string Yazilan, Cozum Cozum, bool Bayat)> Kirikler(string yol)
    {
        var sonuc = new List<(string, Cozum, bool)>();

        foreach ((string yazilan, Cozum cozum) in _indeks!.Kullandiklari(yol))
        {
            if (cozum.Durum == CozumDurumu.Bulunamadi)
            {
                sonuc.Add((yazilan, cozum, false));
            }
            else if (ReferansIndeksi.BayatMi(yol, yazilan, cozum))
            {
                sonuc.Add((yazilan, cozum, true));
            }
        }

        return sonuc;
    }

    /// <summary>
    /// KIRIK bolumunun seritteki sayisi. "0" ile "bilmiyoruz" AYNI KELIMEYLE
    /// yazilamaz (CLAUDE.md 3): taranmamis bir kokte "yok" demek, kirik
    /// referansi olan bir dosyayi temiz gostermek olurdu.
    /// </summary>
    private string KirikMetni(string yol)
    {
        IndeksKaydi? kayit = _indeks!.Kayit(yol);
        if (kayit is null)
        {
            return "taranmadı";
        }

        if (!kayit.Okundu)
        {
            return "okunamadı";
        }

        int adet = Kirikler(yol).Count;
        return adet == 0 ? "yok" : $"{adet} dosya";
    }

    /// <summary>YUKARI bolumu: bu dosyayi KIM KULLANIYOR.</summary>
    private void Yukariyi(ReferansListesi liste, string yol)
    {
        KullanimSonucu sonuc = _indeks!.Kullananlar(yol);

        foreach (string kullanan in sonuc.Kullananlar)
        {
            liste.Ekle(
                WindowsYolu.DosyaAdi(kullanan), "kullanan", Simge(kullanan),
                Renkler.ReferansYukariYazi, kullanan, kullanan);
        }

        // SEBEP SATIRI YALNIZCA LISTE BOSKEN - Erkan, 30.08.2026: dolu bir
        // listenin altindaki "17 dosya okunamadı" satiri "gerek yok".
        //
        // BU ARTIK TEK ISARET (30.08.2026, ikinci tur): sekmedeki "· eksik"
        // de Erkan'in karariyla kalkti. Yani DOLU bir listede eksiklik
        // yalnizca durum cubugundaki tarama cumlesinde yaziyor
        // ("EKSİK — 15 dosya okunamadı") - riski soylendi, karar onun.
        //
        // LISTE BOSKEN SATIR SART (CLAUDE.md 3): guvenilir olmayan bos bir
        // listeye "Bunu kullanan dosya yok." yazmak, taranmamis kokte
        // "bu parcayi kimse kullanmiyor" demektir ve SAGLAM DOSYA SILDIRIR.
        // Sekmedeki sayi da o halde "yok" DEMIYOR, "taranmadı" diyor.
        if (sonuc.Kullananlar.Count > 0)
        {
            return;
        }

        Aciklama(
            liste,
            sonuc.Guvenilir ? "Bunu kullanan dosya yok." : sonuc.Sebep ?? "Liste eksik olabilir.",
            sonuc.Guvenilir ? Ilgisiz : "eksik",
            Renkler.ReferansYukariYazi);
    }

    /// <summary>
    /// Bolumun bos ya da eksik olma SEBEBINI yazan satir.
    /// Simgesi YOK (-1): bir dosya satiri gibi gorunmemeli, cunku degil.
    /// </summary>
    private static void Aciklama(ReferansListesi liste, string cumle, string rol, Color yazi)
        => liste.Ekle(cumle, rol, -1, yazi, hedefYol: null, tamMetin: cumle);

    /// <summary>
    /// Asagi yondeki satirin rol kelimesi. BELIRSIZ olan SAKLANMAZ: tek bir
    /// cevap uydurmak yanlis dosyayi sildirir (CLAUDE.md 5).
    /// </summary>
    private static string AsagiRol(Cozum cozum) => cozum.Durum switch
    {
        CozumDurumu.Bulundu => "içinde",
        CozumDurumu.Belirsiz => $"içinde? {cozum.Adaylar.Count} aday",
        _ => "BULUNAMADI",
    };

    private static int Simge(string yol) => TurSimgeleri.Sira(DosyaTurleri.Tani(yol));
}
