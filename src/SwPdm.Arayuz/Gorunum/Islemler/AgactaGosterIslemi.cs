using System;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// DOSYA AGACINDA GOSTER (Erkan, 31.08.2026: "sağ tıka önizlemede çalışacak
/// şekilde dosya ağacında göster diye seçenek ekler misin. dosyanın konumunu
/// bilmiyorum").
///
/// GITME YETENEGI ZATEN VARDI - panelde Enter - ama MENUDE YOKTU; menuye
/// bakan biri icin var olmayan bir ozellikti. CLAUDE.md 11'de yazili dersin
/// tersi: orada menusuz kalan ozellik kor noktaydi, burada kisayol vardi
/// menu yoktu.
///
/// ISTEGIN IKINCI YARISI ("konumunu bilmiyorum") gitmekle KAPANMIYOR:
/// gidis sessiz bitiyordu. Bu islem gittigi KLASORU yaziyla soyluyor -
/// sessiz basari yasak (CLAUDE.md 3).
///
/// AGACI SURMEYI KENDISI YAPMAZ: <see cref="IslemBaglami.AgactaGoster"/>
/// diye ister. Dal acma, tur suzgecini kaldirma ve sebep yazma
/// AnaForm.ReferansaGit'te tek kopya duruyor (CLAUDE.md 8).
///
/// YAZAR = false: hicbir seyi degistirmez, yalnizca gosterir. Kilitli
/// klasordeki bitmis isi GORMEK serbest.
///
/// AGAC MENUSUNDE DE GORUNUR, BILEREK: orada secili dosyayi yeniden gosterip
/// KLASORUNU durum cubuguna yazar. Gizlemek icin bagLama "hangi menudeyim"
/// alani eklemek gerekirdi - tek bir oge icin sozlesmeyi buyutmek olurdu
/// (CLAUDE.md 1b).
/// </summary>
internal sealed class AgactaGosterIslemi : IAgacIslemi
{
    /// <inheritdoc/>
    public string Ad => "Dosya ağacında göster";

    /// <inheritdoc/>
    public Keys Kisayol => Keys.Control | Keys.Shift | Keys.G;

    /// <inheritdoc/>
    public bool Yazar => false;

    /// <inheritdoc/>
    public bool Uygulanabilir(SecimBaglami secim, out string nedenOlmaz)
    {
        ArgumentNullException.ThrowIfNull(secim);

        if (SecimBaglami.Yolu(secim.TekOge) is not null)
        {
            nedenOlmaz = string.Empty;
            return true;
        }

        // VERSIYONLAR SATIRINDA GRI KALIR - VE BU SART. Arsiv yolu
        // "kokun altinda" testini GECER ama agacta dugumu ASLA yoktur
        // (.SwPdmSurum gizli): gitme denenseydi "acik kokun disinda
        // olabilir" derdi - YANLIS SEBEP, sebep gostermemekten kotudur
        // (CLAUDE.md 3). ReferansMenusu o satirda zaten bos secim veriyor.
        nedenOlmaz = "Tek bir dosya ya da klasör seçin.";
        return false;
    }

    /// <inheritdoc/>
    public void Uygula(IslemBaglami baglam)
    {
        ArgumentNullException.ThrowIfNull(baglam);

        if (SecimBaglami.Yolu(baglam.Secim.TekOge) is not string yol)
        {
            baglam.Bildir("Bu satırda gösterilecek bir dosya yok.");
            return;
        }

        // GIDILEMEDIYSE SUSULUR: sebebi AgactaGoster'in kendisi yaziyor
        // (kok disinda mi, suzgec mi) - ustune "gosterildi" yazmak yalan
        // olurdu (CLAUDE.md 3).
        if (baglam.AgactaGoster(yol))
        {
            baglam.Bildir("Ağaçta gösterildi — " + WindowsYolu.Klasor(yol));
        }
    }
}
