using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Demos.Carte;

/// <summary>
/// Commande <c>--images</c> : dessine les petites images de la carte du jeu (style maquette, vues du ciel, lumière du
/// nord-ouest) et les enregistre en PNG dans <c>game/assets/carte/</c>. Une image déjà là n'est pas remplacée (sauf avec
/// <c>--forcer</c>) : pour mettre un vrai dessin, il suffit de poser un fichier de même nom et de même taille.
/// </summary>
internal static class MapSpriteImages
{
    private sealed record Sprite(string File, int Width, int Height, Action<Graphics, int, int> Draw);

    private static readonly Sprite[] Sprites =
    [
        new("arbre-feuillu.png", 64, 64, DrawLeafyTree),
        new("arbre-conifere.png", 64, 64, DrawConifer),
        new("toit-1.png", 32, 32, (g, w, h) => DrawRoof(g, w, h, Color.FromArgb(200, 104, 78), Color.FromArgb(150, 70, 52))),
        new("toit-2.png", 32, 32, (g, w, h) => DrawRoof(g, w, h, Color.FromArgb(124, 140, 152), Color.FromArgb(86, 100, 112))),
        new("toit-3.png", 32, 32, (g, w, h) => DrawRoof(g, w, h, Color.FromArgb(206, 146, 84), Color.FromArgb(160, 104, 56))),
        new("fumee.png", 32, 32, DrawSmoke),
    ];

    public static int Run(string dataDir, bool force)
    {
        var dir = Path.GetFullPath(Path.Combine(dataDir, "..", "assets", "carte"));
        Directory.CreateDirectory(dir);
        foreach (var sprite in Sprites)
        {
            var path = Path.Combine(dir, sprite.File);
            if (File.Exists(path) && !force)
            {
                Console.WriteLine($"Gardée (déjà là) : {sprite.File}");
                continue;
            }
            using var bitmap = new Bitmap(sprite.Width, sprite.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                sprite.Draw(g, sprite.Width, sprite.Height);
            }
            bitmap.Save(path, ImageFormat.Png);
            Console.WriteLine($"Dessinée : {sprite.File} ({sprite.Width} × {sprite.Height})");
        }
        return 0;
    }

    /// <summary>Un feuillu vu du dessus : une couronne de lobes, claire au nord-ouest, sombre au sud-est.</summary>
    private static void DrawLeafyTree(Graphics g, int w, int h)
    {
        var lobes = new (float X, float Y, float R)[]
        {
            (0.50f, 0.50f, 0.30f), (0.33f, 0.40f, 0.18f), (0.62f, 0.33f, 0.18f), (0.70f, 0.58f, 0.18f),
            (0.45f, 0.70f, 0.18f), (0.28f, 0.60f, 0.16f),
        };
        Fill(g, w, h, lobes, Color.FromArgb(58, 92, 46), 0, 0, 1.0f);
        Fill(g, w, h, lobes, Color.FromArgb(84, 124, 60), -0.04f, -0.04f, 0.85f);
        Fill(g, w, h, lobes, Color.FromArgb(120, 160, 82), -0.08f, -0.08f, 0.45f);
    }

    /// <summary>Un conifère vu du dessus : une étoile de branches, sombre, éclairée au nord-ouest.</summary>
    private static void DrawConifer(Graphics g, int w, int h)
    {
        PointF[] Star(float scale, float dx, float dy)
        {
            var points = new PointF[16];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = i * Math.PI * 2 / points.Length;
                var r = (i % 2 == 0 ? 0.40f : 0.24f) * scale;
                points[i] = new PointF((float)((0.5 + dx + r * Math.Cos(angle)) * w), (float)((0.5 + dy + r * Math.Sin(angle)) * h));
            }
            return points;
        }
        using var dark = new SolidBrush(Color.FromArgb(40, 74, 44));
        using var mid = new SolidBrush(Color.FromArgb(62, 104, 60));
        using var light = new SolidBrush(Color.FromArgb(96, 140, 84));
        g.FillPolygon(dark, Star(1.0f, 0, 0));
        g.FillPolygon(mid, Star(0.75f, -0.03f, -0.03f));
        g.FillPolygon(light, Star(0.35f, -0.06f, -0.06f));
    }

    /// <summary>Un toit à deux pans vu du dessus : le pan nord éclairé, le pan sud dans l'ombre, et le faîtage.</summary>
    private static void DrawRoof(Graphics g, int w, int h, Color lit, Color shaded)
    {
        var left = w * 0.12f;
        var right = w * 0.88f;
        var top = h * 0.22f;
        var bottom = h * 0.78f;
        var ridge = (top + bottom) / 2;
        using var litBrush = new SolidBrush(lit);
        using var shadedBrush = new SolidBrush(shaded);
        using var edge = new Pen(Color.FromArgb(90, 40, 30, 25), 1f);
        g.FillRectangle(litBrush, left, top, right - left, ridge - top);
        g.FillRectangle(shadedBrush, left, ridge, right - left, bottom - ridge);
        g.DrawRectangle(edge, left, top, right - left, bottom - top);
        using var ridgePen = new Pen(Color.FromArgb(160, 60, 40, 30), 1.5f);
        g.DrawLine(ridgePen, left, ridge, right, ridge);
    }

    /// <summary>Une bouffée de fumée : un disque blanc qui s'efface vers le bord.</summary>
    private static void DrawSmoke(Graphics g, int w, int h)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(1, 1, w - 2, h - 2);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(200, 245, 245, 242),
            SurroundColors = [Color.FromArgb(0, 245, 245, 242)],
        };
        g.FillEllipse(brush, 1, 1, w - 2, h - 2);
    }

    private static void Fill(Graphics g, int w, int h, (float X, float Y, float R)[] lobes, Color color, float dx, float dy, float scale)
    {
        using var brush = new SolidBrush(color);
        foreach (var (x, y, r) in lobes)
        {
            var radius = r * scale;
            g.FillEllipse(brush, (x + dx * (1 - scale) * 4 - radius) * w, (y + dy * (1 - scale) * 4 - radius) * h, radius * 2 * w, radius * 2 * h);
        }
    }
}
