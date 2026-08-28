using System.Collections.Generic;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// AGAC ISLEMLERININ TEK LISTESI.
///
/// CLAUDE.md 1b: menu, kisayollar ve arac cubugu bu listeden URETILIR. Yeni
/// bir islem eklemek = bir dosya yaz + buraya bir satir. Kaldirmak = dosyayi
/// sil + o satiri sil. Baska hicbir dosya degismez.
///
/// SIRA, kullanicinin menude gordugu siradir.
/// </summary>
internal static class AgacIslemleri
{
    /// <summary>Menuye bu sirayla girer. null bir AYRAC demektir.</summary>
    internal static readonly IReadOnlyList<IAgacIslemi?> Tumu =
    [
        new YeniKlasorIslemi(),
        new YenidenAdlandirIslemi(),
        new SilIslemi(),
        null,
        new KesIslemi(),
        new KopyalaIslemi(),
        new YapistirIslemi(),
        null,
        new GeriAlIslemi(),
        null,
        new BoyutHesaplaIslemi(),
        null,
        new ReferansTaramaIslemi(),
        new ElleBaglaIslemi(),
        new RaporIslemi(),
        null,
        new YenileIslemi(),
        new AgaciKapatIslemi(),
    ];
}
