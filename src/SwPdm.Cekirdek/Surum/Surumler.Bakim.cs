using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>
/// VERSIYON KAYDININ BAKIMI - sil ve notu degistir (Erkan, 31.08.2026:
/// "versiyon silme ve not düzenlemeyi ekle"). Ayri dosya, cunku Surumler.cs
/// 502 satirdi ve boyut kapisinin siniri 600 (CLAUDE.md 11); ayrica bu iki
/// islem "arsivi olustur/don" cekirdeginden BAGIMSIZ olarak silinebilir
/// olmali (CLAUDE.md 1b).
///
/// IKISI DE KAYIT DOSYASINI YENIDEN YAZIYOR ve tek bir kural paylasiyorlar:
/// COZULEMEYEN SATIR SILINMEZ. Anlamadigimiz bir satiri yeniden yazarken
/// atmak, kullanicinin gecmisini sessizce kirpardi (CLAUDE.md 3); okunamayan
/// satir oldugu gibi kalir, panelde zaten "kayit bozuk" diye sayiliyor.
/// </summary>
public static partial class Surumler
{
    /// <summary>
    /// Bir versiyonu KALICI olarak siler: once arsiv kopyasi, sonra kayit
    /// satiri. Cop kutusuna GITMEZ - copten geri gelen dosya kayitsiz kalir
    /// ve listede gorunmez; "geri alinabilir" demek orada yalan olurdu
    /// (CLAUDE.md 3). Onayi arayuz sorar.
    ///
    /// SIRA ONEMLI (dosya once, kayit sonra): tersi olsaydi, kayit silinip
    /// dosya silinemedigi durumda ortada SAHIPSIZ bir "v7.SLDPRT" kalirdi ve
    /// numara yeniden dagitildiginda yeni v7 o dosyaya carpardi. Bu sirayla
    /// dosya silinemezse kayda HIC dokunulmaz: liste eskisi gibi dogru.
    ///
    /// NUMARA YENIDEN KULLANILABILIR: en yeni versiyon silinirse sonraki
    /// "yeni versiyon" ayni numarayi alir (numaralar kayittaki en buyukten
    /// turetiliyor). Bilincli: bos numara birakmak kullaniciya "bir versiyon
    /// kayip mi" dedirtiyordu; silinen versiyonun dosyasi zaten yok.
    /// </summary>
    public static IslemRaporu Sil(string kok, string yol, int no)
    {
        (SurumKaydi? hedef, string? yuva, IslemRaporu? engel) = Hedefi(kok, yol, no);
        if (engel is not null)
        {
            return engel;
        }

        try
        {
            // YENI DUZENDE ARSIV BIR KLASOR ("v3\" + cocuklari): tamami
            // gider. ESKI duzende tek dosya - o zaman yalniz o silinir.
            // Iki duzen de yasiyor (bkz. ArsivBul).
            string klasor = WindowsYolu.Klasor(hedef!.ArsivYolu);
            bool klasorArsivi = string.Equals(
                WindowsYolu.DosyaAdi(klasor),
                "v" + no.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);

            // Arsiv kopyalari salt-okunur; Windows oznitelik konmus dosyayi
            // sildirmez (CLAUDE.md 4).
            if (klasorArsivi)
            {
                foreach (string dosya in Directory.GetFiles(klasor))
                {
                    File.SetAttributes(dosya, FileAttributes.Normal);
                }

                Directory.Delete(klasor, recursive: true);
            }
            else
            {
                File.SetAttributes(hedef.ArsivYolu, FileAttributes.Normal);
                File.Delete(hedef.ArsivYolu);
            }

            // BILESIM KAYDI DA GIDER (02.09.2026): oksuz kalan bir
            // "vN.cocuklar.txt", silinmis bir versiyonun parca listesini
            // diskte tutar ve elle bakan kullaniciya var olmayan bir
            // versiyonu anlatir (CLAUDE.md 3).
            //
            // ICERIK DEPOSUNA DOKUNULMAZ - bilerek: ayni icerigi BASKA
            // versiyonlar da gosteriyor olabilir ve yanlis bir silme,
            // gecmisi geri getirilemez bicimde yok ederdi (CLAUDE.md 1a).
            // Artakalan icerik yer kaplar, zarar vermez.
            BilesimKaydiniSil(klasor);
        }
        catch (Exception hata)
        {
            return IslemSonuclari.HatayiCevir(hata);
        }

        string? yazmaHatasi = KaydiYenidenYaz(
            yuva!, satir => SatirNosu(satir) == no ? null : satir);

        return yazmaHatasi is null
            ? IslemRaporu.Basarili(yol)

            // Yarim kalan hal SESSIZ BIRAKILMAZ: dosya gitti, satir kaldi;
            // liste bunu "arsiv kopyasi kayip" diye zaten sayiyor ama
            // SEBEBINI yalniz bu cumle soyluyor (CLAUDE.md 3).
            : new IslemRaporu(
                IslemSonucu.Bilinmeyen, null,
                $"v{no} kopyası silindi ama kayıt güncellenemedi "
                + "— listede \"kayıt bozuk\" görünecek: " + yazmaHatasi);
    }

    /// <summary>
    /// Bir versiyonun NOTUNU degistirir. Icerige, numaraya, zamana ve boyuta
    /// DOKUNMAZ - not, kaydin tek "fikir" alanidir; otekiler olcumdur.
    /// </summary>
    public static IslemRaporu NotDegistir(string kok, string yol, int no, string yeniNot)
    {
        (SurumKaydi? hedef, string? yuva, IslemRaporu? engel) = Hedefi(kok, yol, no);
        if (engel is not null)
        {
            return engel;
        }

        string temiz = (yeniNot ?? string.Empty)
            .Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

        string? yazmaHatasi = KaydiYenidenYaz(
            yuva!,
            satir =>
            {
                if (SatirNosu(satir) != no)
                {
                    return satir;
                }

                // Satirin KENDI zamani ve boyutu korunur (ayni No'dan birden
                // cok satir varsa her biri kendi olcumuyle kalir); yalniz
                // dorduncu alan degisir.
                string[] p = satir.Split('\t');
                if (p.Length < 4)
                {
                    return satir;   // cozulemeyen satira dokunulmaz
                }

                p[3] = temiz;
                return string.Join('\t', p);
            });

        return yazmaHatasi is null
            ? IslemRaporu.Basarili(hedef!.ArsivYolu)
            : new IslemRaporu(
                IslemSonucu.Bilinmeyen, null, "Not yazılamadı: " + yazmaHatasi);
    }

    /// <summary>
    /// Iki islemin de ilk uc adimi: yuvayi bul, kaydi guvenilir oku, hedef
    /// versiyonu bul. Ikinci kopya yazilmasin diye burada (CLAUDE.md 8).
    /// </summary>
    private static (SurumKaydi? Hedef, string? Yuva, IslemRaporu? Engel) Hedefi(
        string kok, string yol, int no)
    {
        string? yuva = Yuvasi(kok, yol);
        if (yuva is null)
        {
            return (null, null, new IslemRaporu(
                IslemSonucu.Bilinmeyen, null, "Dosya açık kökün altında değil."));
        }

        SurumDurumu durum = Listele(kok, yol);
        if (!durum.Guvenilir)
        {
            // Kayit okunamiyorsa yeniden YAZMAK butun gecmisi silebilirdi.
            return (null, null, new IslemRaporu(
                IslemSonucu.Bilinmeyen, null, durum.Okunamadi));
        }

        foreach (SurumKaydi kayit in durum.Ogeler)
        {
            if (kayit.No == no)
            {
                return (kayit, yuva, null);
            }
        }

        return (null, null, new IslemRaporu(
            IslemSonucu.Bulunamadi, null, $"v{no} arşivde yok."));
    }

    /// <summary>
    /// Kayit dosyasini satir satir donusturerek yeniden yazar; null donen
    /// satir DUSER. Once geciciye yazilip <see cref="File.Replace(string,
    /// string, string)"/> ile oturuyor: yarim yazma butun versiyon gecmisini
    /// bozardi (CLAUDE.md 1a - KOPYALA/ONAR/SIL kalibinin ayni mantigi).
    /// </summary>
    /// <returns>Hata sebebi; her sey olduysa null.</returns>
    private static string? KaydiYenidenYaz(string yuva, Func<string, string?> donustur)
    {
        string kayitYolu = KayitYolu(yuva);
        string gecici = kayitYolu + ".yeni";

        try
        {
            var kalanlar = new List<string>();
            foreach (string satir in File.ReadAllLines(kayitYolu))
            {
                if (satir.Length == 0)
                {
                    continue;
                }

                if (donustur(satir) is string yeni)
                {
                    kalanlar.Add(yeni);
                }
            }

            // Bos kalan kayit dosyasi SILINMEZ: varligi "bu dosya bir kez
            // versiyonlanmisti" bilgisidir ve Listele onu bos+guvenilir
            // okuyor.
            File.WriteAllText(
                gecici,
                kalanlar.Count == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine, kalanlar) + Environment.NewLine);

            File.Replace(gecici, kayitYolu, destinationBackupFileName: null);
            return null;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            TemizlemeyeCalis(gecici);
            return hata.Message;
        }
    }
}
