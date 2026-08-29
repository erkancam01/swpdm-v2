using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>Surukle-birak ile aktarma istegi.</summary>
/// <param name="Yollar">Aktarilacak ogelerin yollari.</param>
/// <param name="HedefKlasor">Birakilan klasor.</param>
/// <param name="Kopyala">Ctrl basiliydi: tasima degil KOPYALAMA.</param>
internal sealed record TasimaIstegi(
    IReadOnlyList<string> Yollar, string HedefKlasor, bool Kopyala);

/// <summary>
/// SURUKLE-BIRAKIN TEK KAPISI. Agaca takilir; agac bunu bilmez.
///
/// CLAUDE.md 1b: "surukleyerek tasima"yi kaldirmak = bu dosyayi sil +
/// AnaForm'daki bir satiri kes. Menuden Kes/Yapistir bundan bagimsiz calismaya
/// devam eder.
///
/// TASIMAYI KENDISI YAPMAZ - yalnizca istegi bildirir. Tasimanin karari
/// (onay, uyari, kismi basarisizlik) <see cref="Tasi"/>'da, tek yerde.
///
/// Surukleme yalnizca DUGUM uzerinden baslar.
///
/// CTRL BASILI SURUKLEME KOPYALAR (Gezgin kurali); basili degilse tasir.
/// </summary>
internal sealed class SurukleBirak
{
    private readonly SecimliAgac _agac;

    internal SurukleBirak(SecimliAgac agac)
    {
        _agac = agac;
        _agac.AllowDrop = true;
        _agac.ItemDrag += Basladi;
        _agac.DragOver += Uzerinde;
        _agac.DragDrop += Birakildi;
    }

    /// <summary>Secim bir klasorun uzerine birakildi.</summary>
    internal event EventHandler<TasimaIstegi>? Tasindi;

    private void Basladi(object? gonderen, ItemDragEventArgs e)
    {
        // Suruklenen dugum secili DEGILSE once o secilir. Yoksa kullanici
        // gorunurde bir seyi surukleyip BASKA seyleri tasirdi - sessiz ve
        // geri alinamaz bir hata (CLAUDE.md 3).
        if (e.Item is TreeNode suruklenen && !_agac.KumedeMi(suruklenen))
        {
            _agac.YalnizSec(suruklenen);
        }

        var yollar = new List<string>();
        foreach (TreeNode dugum in _agac.Secililer)
        {
            if (Yolu(dugum) is string yol)
            {
                yollar.Add(yol);
            }
        }

        if (yollar.Count > 0)
        {
            // IKI ETKI DE VERILIR: hangisinin secilecegini Ctrl belirler.
            // Yalnizca Move verilirse Ctrl'e basmak imleci "yasak"a cevirir.
            _agac.DoDragDrop(yollar, DragDropEffects.Move | DragDropEffects.Copy);
        }
    }

    /// <summary>
    /// CTRL BASILIYSA KOPYALA - Gezgin'in kurali.
    ///
    /// NEDEN EKLENDI (29.08.2026): burada etki her zaman <c>Move</c>'du.
    /// Gezgin aliskanligiyla Ctrl basili surukleyen kullanici KOPYALADIGINI
    /// sanarken dosyayi TASIYORDU; imlecte de hicbir fark yoktu ve hicbir
    /// yerde sorulmuyordu. Sessiz ve yanlis (CLAUDE.md 3).
    /// </summary>
    private static bool KopyaMi(DragEventArgs e)
        => (e.KeyState & CtrlBiti) != 0;

    /// <summary>Surukleme sirasinda Ctrl'un <c>KeyState</c>'teki biti.</summary>
    private const int CtrlBiti = 8;

    private void Uzerinde(object? gonderen, DragEventArgs e)
        => e.Effect = Hedef(e) is null
            ? DragDropEffects.None
            : KopyaMi(e) ? DragDropEffects.Copy : DragDropEffects.Move;

    private void Birakildi(object? gonderen, DragEventArgs e)
    {
        string? hedef = Hedef(e);
        if (hedef is not null && e.Data?.GetData(typeof(List<string>)) is List<string> yollar)
        {
            Tasindi?.Invoke(this, new TasimaIstegi(yollar, hedef, KopyaMi(e)));
        }
    }

    /// <summary>Farenin altindaki KLASORUN yolu; klasor degilse null.</summary>
    private string? Hedef(DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(List<string>)) != true)
        {
            return null;
        }

        TreeNode? dugum = _agac.GetNodeAt(_agac.PointToClient(new Point(e.X, e.Y)));
        return dugum?.Tag is KlasorOgesi klasor ? klasor.Yol : null;
    }

    private static string? Yolu(TreeNode? dugum) => dugum?.Tag switch
    {
        DosyaOgesi dosya => dosya.Yol,
        KlasorOgesi klasor => klasor.Yol,
        _ => null,
    };
}
