using System.Drawing;
using System.Drawing.Drawing2D;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// 16x16 simgeler koda ciziliyor; depoda ikili varlik yok.
///
/// GECICI: bunlar SOLIDWORKS'un gercek simgeleri DEGIL, ayirt edilebilir
/// yer tutuculardir. Gercek simge dosyalari geldiginde bu sinif tumuyle
/// degistirilecek - cagri yerleri degismeyecek.
/// </summary>
internal static class Simgeler
{
    internal const int Boy = 16;

    private static Bitmap Tuval(out Graphics g)
    {
        var bmp = new Bitmap(Boy, Boy);
        g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        return bmp;
    }

    internal static Bitmap Klasor()
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            Point[] govde =
            [
                new(1, 4), new(6, 4), new(7, 6), new(14, 6),
                new(14, 13), new(1, 13)
            ];
            using var dolgu = new SolidBrush(Color.FromArgb(0xE8, 0xB4, 0x4A));
            using var kalem = new Pen(Color.FromArgb(0xB0, 0x82, 0x1E));
            g.FillPolygon(dolgu, govde);
            g.DrawPolygon(kalem, govde);
        }
        return bmp;
    }

    internal static Bitmap Parca()
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var dolgu = new SolidBrush(Color.FromArgb(0xD4, 0xB0, 0x3A));
            using var kalem = new Pen(Color.FromArgb(0x8A, 0x6D, 0x14));
            Point[] blok = [new(3, 5), new(8, 2), new(13, 5), new(13, 11), new(8, 14), new(3, 11)];
            g.FillPolygon(dolgu, blok);
            g.DrawPolygon(kalem, blok);
            g.DrawLine(kalem, 3, 5, 8, 8);
            g.DrawLine(kalem, 13, 5, 8, 8);
            g.DrawLine(kalem, 8, 8, 8, 14);
        }
        return bmp;
    }

    internal static Bitmap Montaj()
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var mavi = new SolidBrush(Color.FromArgb(0x3F, 0x6F, 0xB5));
            using var sari = new SolidBrush(Color.FromArgb(0xD4, 0xB0, 0x3A));
            using var kalem = new Pen(Color.FromArgb(0x2A, 0x3F, 0x5C));
            g.FillRectangle(mavi, 1, 5, 8, 8);
            g.DrawRectangle(kalem, 1, 5, 8, 8);
            g.FillRectangle(sari, 7, 2, 7, 7);
            g.DrawRectangle(kalem, 7, 2, 7, 7);
        }
        return bmp;
    }

    internal static Bitmap TeknikResim()
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var kagit = new SolidBrush(Color.White);
            using var kenar = new Pen(Color.FromArgb(0x2E, 0x6D, 0xA4));
            using var cizgi = new Pen(Color.FromArgb(0x6E, 0x8F, 0xAE));
            g.FillRectangle(kagit, 2, 1, 12, 14);
            g.DrawRectangle(kenar, 2, 1, 12, 14);
            g.DrawRectangle(cizgi, 4, 3, 5, 4);
            g.DrawLine(cizgi, 4, 10, 12, 10);
            g.DrawLine(cizgi, 4, 12, 12, 12);
            g.DrawRectangle(kenar, 9, 12, 3, 2);
        }
        return bmp;
    }

    internal static Bitmap Pdf()
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var kagit = new SolidBrush(Color.White);
            using var kirmizi = new SolidBrush(Color.FromArgb(0xC4, 0x30, 0x2B));
            using var kenar = new Pen(Color.FromArgb(0x8A, 0x8A, 0x8A));
            g.FillRectangle(kagit, 2, 1, 12, 14);
            g.DrawRectangle(kenar, 2, 1, 12, 14);
            g.FillRectangle(kirmizi, 2, 8, 12, 5);
        }
        return bmp;
    }

    internal static Bitmap Ac()
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var dolgu = new SolidBrush(Color.FromArgb(0xF0, 0xC4, 0x6A));
            using var kalem = new Pen(Color.FromArgb(0xA8, 0x7C, 0x1E));
            Point[] arka = [new(1, 4), new(6, 4), new(7, 6), new(13, 6), new(13, 9), new(1, 9)];
            g.FillPolygon(dolgu, arka);
            g.DrawPolygon(kalem, arka);
            Point[] on = [new(3, 8), new(15, 8), new(12, 14), new(1, 14)];
            using var acikDolgu = new SolidBrush(Color.FromArgb(0xFA, 0xDD, 0x9A));
            g.FillPolygon(acikDolgu, on);
            g.DrawPolygon(kalem, on);
        }
        return bmp;
    }

    internal static Bitmap Cop()
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var kalem = new Pen(Color.FromArgb(0x55, 0x55, 0x55));
            using var dolgu = new SolidBrush(Color.FromArgb(0xC9, 0xCF, 0xD6));
            g.DrawLine(kalem, 2, 4, 14, 4);
            g.DrawRectangle(kalem, 6, 2, 4, 2);
            Point[] govde = [new(4, 5), new(12, 5), new(11, 14), new(5, 14)];
            g.FillPolygon(dolgu, govde);
            g.DrawPolygon(kalem, govde);
            g.DrawLine(kalem, 6, 7, 6, 12);
            g.DrawLine(kalem, 8, 7, 8, 12);
            g.DrawLine(kalem, 10, 7, 10, 12);
        }
        return bmp;
    }

    internal static Bitmap GeriAl()
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var kalem = new Pen(Color.FromArgb(0x33, 0x66, 0x99), 2f);
            g.DrawArc(kalem, 3, 3, 10, 10, 40, 280);
            using var uc = new SolidBrush(Color.FromArgb(0x33, 0x66, 0x99));
            Point[] okUcu = [new(11, 1), new(15, 5), new(9, 6)];
            g.FillPolygon(uc, okUcu);
        }
        return bmp;
    }

    internal static Bitmap Raptiye(Color renk)
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var dolgu = new SolidBrush(renk);
            using var kalem = new Pen(renk);
            Point[] bas = [new(5, 1), new(11, 1), new(10, 7), new(13, 9), new(3, 9), new(6, 7)];
            g.FillPolygon(dolgu, bas);
            g.DrawLine(kalem, 8, 9, 8, 15);
        }
        return bmp;
    }

    internal static Bitmap Disli(Color renk)
    {
        var bmp = Tuval(out Graphics g);
        using (g)
        {
            using var dolgu = new SolidBrush(renk);
            const float merkez = 8f;
            for (int i = 0; i < 8; i++)
            {
                float aci = i * 45f;
                var durum = g.Save();
                g.TranslateTransform(merkez, merkez);
                g.RotateTransform(aci);
                g.FillRectangle(dolgu, -1.6f, -7.5f, 3.2f, 4f);
                g.Restore(durum);
            }
            g.FillEllipse(dolgu, 2.5f, 2.5f, 11f, 11f);
            using var delik = new SolidBrush(Color.Transparent);
            var eskiMod = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.FillEllipse(delik, 5.5f, 5.5f, 5f, 5f);
            g.CompositingMode = eskiMod;
        }
        return bmp;
    }
}
