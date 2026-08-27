using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>Surukle-birak ile tasima istegi.</summary>
/// <param name="Yollar">Tasinacak ogelerin yollari.</param>
/// <param name="HedefKlasor">Birakilan klasor.</param>
internal sealed record TasimaIstegi(IReadOnlyList<string> Yollar, string HedefKlasor);

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
/// Surukleme yalnizca DUGUM uzerinden baslar; bos alandan baslayan surukleme
/// dikdortgen secimdir (Gezgin'in ayrimi) ve o <see cref="SecimliAgac"/>'in isi.
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
            _agac.DoDragDrop(yollar, DragDropEffects.Move);
        }
    }

    private void Uzerinde(object? gonderen, DragEventArgs e)
        => e.Effect = Hedef(e) is null ? DragDropEffects.None : DragDropEffects.Move;

    private void Birakildi(object? gonderen, DragEventArgs e)
    {
        string? hedef = Hedef(e);
        if (hedef is not null && e.Data?.GetData(typeof(List<string>)) is List<string> yollar)
        {
            Tasindi?.Invoke(this, new TasimaIstegi(yollar, hedef));
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
