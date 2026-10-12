// Génère src\Archiveur.ico : dossier violet avec fermeture éclair (dossier zippé).
// Usage : GenererIcone.exe <fichier.ico>
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class GenererIcone
{
    static readonly int[] Tailles = { 16, 20, 24, 32, 40, 48, 64, 256 };

    static int Main(string[] args)
    {
        var images = new List<byte[]>();
        // PNG seulement pour 256 px : les petites tailles en DIB, lues par tous les outils (csc, System.Drawing).
        foreach (int t in Tailles) images.Add(t >= 256 ? Png(Dessiner(t)) : Dib(Dessiner(t)));

        using (var w = new BinaryWriter(File.Create(args[0])))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)images.Count);
            int offset = 6 + 16 * images.Count;
            for (int i = 0; i < images.Count; i++)
            {
                int t = Tailles[i];
                w.Write((byte)(t >= 256 ? 0 : t)); w.Write((byte)(t >= 256 ? 0 : t));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] img in images) w.Write(img);
        }
        return 0;
    }

    static byte[] Png(Bitmap b)
    {
        using (b)
        using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); return ms.ToArray(); }
    }

    // Entrée d'icône classique : BITMAPINFOHEADER, pixels BGRA de bas en haut, masque AND (vide, l'alpha suffit).
    static byte[] Dib(Bitmap b)
    {
        using (b)
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            int t = b.Width, ligneMasque = ((t + 31) / 32) * 4;
            w.Write(40); w.Write(t); w.Write(2 * t); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(t * t * 4 + ligneMasque * t); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = t - 1; y >= 0; y--)
                for (int x = 0; x < t; x++)
                {
                    Color c = b.GetPixel(x, y);
                    w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                }
            w.Write(new byte[ligneMasque * t]);
            return ms.ToArray();
        }
    }

    // Dessin sur une grille de 32 unités, mise à l'échelle de la taille demandée.
    static Bitmap Dessiner(int taille)
    {
        var b = new Bitmap(taille, taille, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(taille / 32f, taille / 32f);
            bool petit = taille < 24;

            // Dossier : dos avec onglet, puis face avant.
            using (var dos = Arrondi(1.5f, 5f, 29f, 22f, 2f))
            {
                dos.FillMode = FillMode.Winding;
                dos.AddPath(Arrondi(1.5f, 3f, 11f, 5f, 1.5f), false);
                using (var p = new SolidBrush(Color.FromArgb(0x5B, 0x2A, 0x8C))) g.FillPath(p, dos);
            }
            using (var face = Arrondi(1.5f, 8.5f, 29f, 18.5f, 2f))
            using (var p = new LinearGradientBrush(new PointF(0, 8.5f), new PointF(0, 27f),
                       Color.FromArgb(0xB0, 0x7C, 0xE8), Color.FromArgb(0x80, 0x45, 0xC8)))
            using (var bord = new Pen(Color.FromArgb(0x4A, 0x1F, 0x78), 0.8f))
            {
                g.FillPath(p, face);
                g.DrawPath(bord, face);
            }

            // Fermeture éclair : bande verticale avec dents alternées et curseur.
            float x = 16f;
            using (var bande = new SolidBrush(Color.FromArgb(0x3A, 0x1A, 0x5A)))
            using (var dent = new SolidBrush(Color.FromArgb(0xE8, 0xE8, 0xE8)))
            {
                g.FillRectangle(bande, x - 1.8f, 8.5f, 3.6f, 18.5f);
                if (!petit)
                    for (float y = 9.5f; y < 25f; y += 2f)
                        g.FillRectangle(dent, (y % 4f < 2f) ? x - 1.4f : x, y, 1.4f, 1.1f);
                g.FillRectangle(dent, x - 1.6f, 9f, 3.2f, petit ? 5f : 4f);
                g.FillRectangle(bande, x - 0.6f, petit ? 10.5f : 10f, 1.2f, petit ? 2f : 2f);
            }

        }
        return b;
    }

    static GraphicsPath Arrondi(float x, float y, float l, float h, float r)
    {
        var p = new GraphicsPath();
        float d = 2f * r;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + l - d, y, d, d, 270, 90);
        p.AddArc(x + l - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
