using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// AGACIN ARAMA GORUNUMU - "sonuclari goster" ve "gezinmeye don".
///
/// AYRI DOSYA, cunku boyut kapisi yakaladi (620 > 600) ve konu ayrimi da
/// dogru: otekiler DISKI gosteriyor, burasi bir SORGUNUN sonucunu. Arama
/// kipinden cikinca agac oldugu gibi geri geliyor - o hafizanin sahibi de
/// burasi.
///
/// Arama motoru burada DEGIL (<see cref="KlasorTarayici.Ara"/>), ne zaman
/// baslayacagi da degil (<see cref="AramaSurucusu"/>). Burasi yalnizca
/// SONUCU cizer (CLAUDE.md 1b).
/// </summary>
internal sealed partial class AgacDoldurucu
{
    /// <summary>
    /// Arama sonucunu agaca yazar: eslesmeler bulunduklari klasore gore gruplanir.
    /// Kesilme ve sinir asimi GIZLENMEZ.
    /// </summary>
    internal void AramaSonucunuGoster(string metin, AramaSonucu sonuc)
    {
        // Arama kipine ILK geciste gezinme durumu saklanir; aramadan cikinca
        // kullanici actigi dallari acik bulur.
        _gezinmeDurumu ??= AgacDurumlari.Al(_agac);

        _aramaMetni = metin;
        _aramaSonucu = sonuc;
        _taranan.Clear();
        _agac.SecimiTemizle();

        _agac.BeginUpdate();
        _agac.Nodes.Clear();

        int gosterilen = 0;
        var gruplar = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        var kokDugum = new TreeNode(string.Empty)
        {
            ImageIndex = TurSimgeleri.Klasor,
            SelectedImageIndex = TurSimgeleri.Klasor,
        };
        _agac.Nodes.Add(kokDugum);

        // Arama sonucu birden cok klasoru kapsiyor; Kilit.Coz eslesmeyi
        // KLASOR BAZINDA yapiyor, yani A klasorundeki bir kilit B'deki bir
        // dosyayi gizleyemez.
        KilitDurumu kilit = Kilit.Coz(sonuc.Bulunanlar);

        foreach (DosyaOgesi dosya in kilit.Gosterilecek)
        {
            if (!TureUyuyorMu(dosya.Tur))
            {
                continue;
            }

            gosterilen++;
            string klasor = WindowsYolu.Klasor(dosya.Yol);
            if (!gruplar.TryGetValue(klasor, out TreeNode? grup))
            {
                grup = new TreeNode(GoreceliYol(klasor))
                {
                    ImageIndex = TurSimgeleri.Klasor,
                    SelectedImageIndex = TurSimgeleri.Klasor,
                    Tag = new KlasorOgesi(klasor, WindowsYolu.DosyaAdi(klasor), null, null, null),
                    ToolTipText = klasor,
                };
                gruplar[klasor] = grup;
                kokDugum.Nodes.Add(grup);
            }

            grup.Nodes.Add(DosyaSatiri.Dugum(dosya, kilit));
        }

        string kokAdi = Kok is null ? "Arama" : WindowsYolu.DosyaAdi(Kok);
        kokDugum.Text = $"{kokAdi}  —  \"{metin}\": {gosterilen} eşleşme";
        kokDugum.ExpandAll();
        _agac.EndUpdate();

        Durum?.Invoke(this, AramaOzeti(sonuc, gosterilen));
    }

    /// <summary>
    /// Arama kipinden gezinme kipine doner ve aramadan ONCEKI acik dallari
    /// geri yukler.
    /// </summary>
    internal void GezinmeyeDon()
    {
        if (Kok is null)
        {
            return;
        }

        AgacDurumu? geri = _gezinmeDurumu;
        _gezinmeDurumu = null;
        KokuAc(Kok, geri);
    }

    private static string AramaOzeti(AramaSonucu sonuc, int gosterilen)
    {
        string ozet = gosterilen == sonuc.Bulunanlar.Count
            ? $"{gosterilen} eşleşme"
            : $"{gosterilen} / {sonuc.Bulunanlar.Count} eşleşme (süzgeç açık)";

        ozet += $" · {sonuc.TarananKlasor} klasör tarandı";

        // Sessiz kirpma "hepsini kapsadim" gibi okunur (CLAUDE.md 9).
        if (sonuc.Iptal)
        {
            ozet += " · ARAMA YARIDA KESİLDİ";
        }
        else if (sonuc.SinirAsildi)
        {
            ozet += " · SINIRA ULAŞILDI, daha fazlası olabilir";
        }

        if (sonuc.AtlananKilitli > 0)
        {
            // SESSIZ ATLAMA YOK (CLAUDE.md 3): kullanici aradigini
            // bulamayinca "yok" sanmasin - kilitli oldugu icin atlandi.
            ozet += $" · {sonuc.AtlananKilitli} kilitli klasör atlandı";
        }

        if (sonuc.OkunamayanKlasorler.Count > 0)
        {
            ozet += $" · {sonuc.OkunamayanKlasorler.Count} klasör okunamadı";
        }

        return ozet;
    }
}
