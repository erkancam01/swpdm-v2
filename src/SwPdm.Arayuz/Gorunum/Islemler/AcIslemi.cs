using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// DOSYAYI AC (Erkan, 31.08.2026: "sağ tık aç diye kısayol ekle").
///
/// Acma yetenegi zaten vardi - cift tik ve Enter - ama MENUDE YOKTU. Menuye
/// bakan biri icin var olmayan bir ozellikti; sag tik, bu uygulamada
/// "ne yapabilirim" sorusunun cevabinin durdugu yer.
///
/// ACMA MANTIGI BURADA DEGIL: <see cref="DosyaAcici"/>. Hata metni, kutu,
/// calisma klasoru tuzagi - hepsi orada, tek kopya (CLAUDE.md 8). Bu dosya
/// yalnizca menuye bir kapi aciyor.
///
/// KISAYOL YALNIZ ETIKET: menude "Enter" yaziyor ama tus KAYDEDILMIYOR
/// (<see cref="Kisayol"/> = None, yazi <see cref="YazilanTus"/>'tan turetiliyor).
/// Enter'i agacta <see cref="AgacTuslari"/>, panelde
/// <see cref="ReferansPaneliTuslari"/> zaten sahipleniyor; yani cift acma
/// yok. Kaydetmek ZATEN MUMKUN DEGIL: tek basina Enter'i ShortcutKeys'e
/// yazmak uygulamayi acilmaz yapiyor (31.08.2026, kapi yakaladi).
///
/// YAZAR = false: dosyayi ACMAK onu degistirmez. Kilitli bir klasordeki
/// bitmis isi GORMEK serbest; kilit degistirmeyi engeller, bakmayi degil.
/// </summary>
internal sealed class AcIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Aç";

    /// <inheritdoc/>
    /// <remarks>
    /// KAYDEDILMEZ, YALNIZ YAZILIR - ve bu bir tercih degil ZORUNLULUK:
    /// tek basina Enter gecerli bir menu kisayolu degil, ShortcutKeys'e
    /// yazilinca InvalidEnumArgumentException atiyor ve uygulama HIC
    /// ACILMIYOR (31.08.2026, calistirma kapisi yakaladi - derleme temizdi).
    /// </remarks>
    public Keys Kisayol => Keys.None;

    /// <inheritdoc/>
    public Keys YazilanTus => Keys.Enter;

    /// <inheritdoc/>
    public bool Yazar => false;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        if (secim.TekOge is DosyaOgesi)
        {
            nedenOlmaz = string.Empty;
            return true;
        }

        // KLASORDE GRI + SEBEP (CLAUDE.md 3): "Aç" bir klasorde de mantikli
        // gorunuyor ama bu uygulamada klasoru "acmak" dali acmaktir ve onu
        // Enter/cift tik zaten yapiyor. Gizlemek "boyle bir sey yok" demek
        // olurdu; gri durup sebebini soylemek dogru.
        nedenOlmaz = secim.TekOge is KlasorOgesi
            ? "Klasör için Enter ya da çift tık dalı açar."
            : "Tek bir dosya seçin.";
        return false;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        if (baglam.Secim.TekOge is DosyaOgesi dosya)
        {
            baglam.Bildir(DosyaAcici.Ac(baglam.Sahip, dosya));
        }
    }
}
