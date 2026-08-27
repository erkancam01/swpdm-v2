using System.Drawing;
using System.Windows.Forms;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Sag alt panel: secili dosyanin referanslari. Solda dosya adi, sagda rolu
/// (ornegin "Baz aldığı model").
///
/// Yalnizca GORUNUM; referans cozmez. CLAUDE.md 3 geregi bos liste tek basina
/// "referansi yok" anlamina GELMEZ - o ayrimi dolduran kod yapacak, bu sinif
/// kendiliginden hicbir sey iddia etmez.
/// </summary>
internal sealed class ReferansListesi : ListView
{
    private readonly ColumnHeader _adSutunu;
    private readonly ColumnHeader _rolSutunu;

    internal ReferansListesi()
    {
        View = View.Details;
        HeaderStyle = ColumnHeaderStyle.None;
        FullRowSelect = true;
        MultiSelect = false;
        BorderStyle = BorderStyle.None;
        BackColor = Renkler.OnizlemeArkaPlan;
        Dock = DockStyle.Fill;

        _adSutunu = new ColumnHeader { Text = "Dosya", Width = 170 };
        _rolSutunu = new ColumnHeader
        {
            Text = "Rol",
            Width = 110,
            TextAlign = HorizontalAlignment.Right,
        };
        Columns.AddRange([_adSutunu, _rolSutunu]);
    }

    /// <summary>Bir referans satiri ekler.</summary>
    internal void Ekle(string dosyaAdi, string rol, int simgeSirasi)
    {
        var satir = new ListViewItem(dosyaAdi) { ImageIndex = simgeSirasi };
        satir.SubItems.Add(rol);
        Items.Add(satir);
    }

    /// <summary>Sutun genisliklerini panelin genisligine gore paylastirir.</summary>
    protected override void OnResize(System.EventArgs e)
    {
        base.OnResize(e);
        int kullanilabilir = ClientSize.Width - 4;
        if (kullanilabilir <= 0)
        {
            return;
        }

        _rolSutunu.Width = (int)(kullanilabilir * 0.42);
        _adSutunu.Width = kullanilabilir - _rolSutunu.Width;
    }
}
