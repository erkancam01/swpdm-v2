namespace SwPdm.Cekirdek;

/// <summary>
/// Hedefte ayni adda bir sey varsa ne yapilacagi.
///
/// Once tek secenek vardi: islem yapilmaz, "zaten var" denir. Guvenliydi ama
/// 50 dosya yapistirirken cakisan 3'u icin kullaniciya secim birakmiyordu.
/// </summary>
public enum Cakisma
{
    /// <summary>Islem yapilmaz, cagirana ZatenVar donulur - cagiran sorar.</summary>
    Sor,

    /// <summary>Bu oge atlanir. Hata degildir.</summary>
    Atla,

    /// <summary>Yeni ad uretilir: "Parca (2).SLDPRT". Hicbir sey kaybolmaz.</summary>
    IkisiniDeTut,

    /// <summary>
    /// Var olan DEGISTIRILIR. Uzerine yazilan dosya YOK EDILMEZ - cagiran
    /// onu once cope tasir (<c>eskisiniKurtar</c>). Kurtarma tutmazsa islem
    /// YAPILMAZ. CLAUDE.md 1a: bu uygulamada geri alinamayan islem yazilmaz.
    ///
    /// KLASORDE gecerli degildir: bir klasoru "degistirmek" icini silmek
    /// demektir ve kullanicinin gormedigi alt dosyalar yok olur.
    /// </summary>
    Degistir,
}
