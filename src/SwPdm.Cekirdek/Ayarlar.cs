using System;
using System.Collections.Generic;
using System.IO;

namespace SwPdm.Cekirdek;

/// <summary>
/// KALICI AYARLAR - uygulama kapanip acilinca hatirlanan her sey.
///
/// Dosya bicimi DUZ METIN "anahtar=deger": bozulursa ELLE onarilabilir ve
/// bozuk bir satir uygulamayi dusurmez, atlanir (cop kutusu kaydiyla ayni
/// gerekce). Ikili ya da serilestirilmis bicim, bozuldugunda kullaniciyi
/// caresiz birakirdi.
///
/// Yol: %APPDATA%\SwPdm\ayarlar.txt - kullanicinin kendi profilinde, yani
/// ag surucusune ya da yonetici iznine BAGLI DEGIL.
/// </summary>
public sealed class Ayarlar
{
    /// <summary>Son acilanlar listesinde en fazla kac kok tutulur.</summary>
    public const int GecmisSiniri = 10;

    private readonly List<string> _sonKokler = [];

    /// <summary>Ayar dosyasinin tam yolu.</summary>
    public static string Yolu { get; } = VarsayilanYol();

    /// <summary>
    /// Uygulama acilinca kendiliginden acilacak kok. Yoksa null.
    /// </summary>
    public string? SonKok => _sonKokler.Count > 0 ? _sonKokler[0] : null;

    /// <summary>Son acilan kokler, en yeni once.</summary>
    public IReadOnlyList<string> SonKokler => _sonKokler;

    /// <summary>
    /// Cop klasorlerinin konacagi UST klasor. null ise varsayilan: kokun
    /// kendi icinde.
    /// </summary>
    public string? CopUstKlasoru { get; set; }

    /// <summary>Diskten okur. Dosya yoksa bos ayarlar doner - hata degildir.</summary>
    public static Ayarlar Oku(string? dosya = null)
    {
        string yol = dosya ?? Yolu;
        var ayarlar = new Ayarlar();

        string[] satirlar;
        try
        {
            if (!File.Exists(yol))
            {
                return ayarlar;
            }

            satirlar = File.ReadAllLines(yol);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return ayarlar;   // ayar okunamamasi uygulamayi durdurmaz
        }

        foreach (string satir in satirlar)
        {
            int esittir = satir.IndexOf('=');
            if (esittir <= 0)
            {
                continue;   // bozuk satir ATLANIR
            }

            string anahtar = satir[..esittir].Trim();
            string deger = satir[(esittir + 1)..].Trim();

            if (deger.Length == 0)
            {
                continue;
            }

            switch (anahtar)
            {
                case "kok":
                    ayarlar.KokEkle(deger);
                    break;

                case "copUstKlasoru":
                    ayarlar.CopUstKlasoru = deger;
                    break;
            }
        }

        return ayarlar;
    }

    /// <summary>
    /// Diske yazar. Yazilamazsa SESSIZCE gecer ve false doner - ayar
    /// kaydedilememesi kullanicinin isini durdurmamali, ama cagiran bilmeli
    /// (CLAUDE.md 3: sessiz basarisizlik yok, sessiz COKME de yok).
    /// </summary>
    public bool Yaz(string? dosya = null)
    {
        string yol = dosya ?? Yolu;

        try
        {
            string klasor = WindowsYolu.Klasor(yol);
            if (klasor.Length > 0)
            {
                Directory.CreateDirectory(klasor);
            }

            var satirlar = new List<string>(_sonKokler.Count + 1);
            foreach (string kok in _sonKokler)
            {
                satirlar.Add("kok=" + kok);
            }

            if (!string.IsNullOrWhiteSpace(CopUstKlasoru))
            {
                satirlar.Add("copUstKlasoru=" + CopUstKlasoru);
            }

            File.WriteAllLines(yol, satirlar);
            return true;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Bir koku gecmisin BASINA koyar. Ayni kok zaten varsa tekrar eklenmez,
    /// one alinir. Karsilastirma buyuk/kucuk harf duyarsiz: Windows yollari
    /// oyle.
    /// </summary>
    public void KokEkle(string yol)
    {
        if (string.IsNullOrWhiteSpace(yol))
        {
            return;
        }

        _sonKokler.RemoveAll(v => string.Equals(v, yol, StringComparison.OrdinalIgnoreCase));
        _sonKokler.Insert(0, yol);

        while (_sonKokler.Count > GecmisSiniri)
        {
            _sonKokler.RemoveAt(_sonKokler.Count - 1);
        }
    }

    /// <summary>Bir koku gecmisten cikarir (artik yoksa).</summary>
    public void KokCikar(string yol)
        => _sonKokler.RemoveAll(v => string.Equals(v, yol, StringComparison.OrdinalIgnoreCase));

    private static string VarsayilanYol()
    {
        string taban = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Linux'ta (testlerde) ApplicationData bos donebiliyor; o zaman
        // gecici klasore dusuyoruz - test kosabilsin diye.
        if (string.IsNullOrEmpty(taban))
        {
            taban = Path.GetTempPath();
        }

        return WindowsYolu.Birlestir(WindowsYolu.Birlestir(taban, "SwPdm"), "ayarlar.txt");
    }
}
