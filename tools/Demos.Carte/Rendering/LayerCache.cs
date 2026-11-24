using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Demos.Carte.Rendering;

/// <summary>
/// Un calque gardé en image (24 novembre 2026) : les biomes ne changent pas pendant un coup de pinceau de relief, mais
/// leur dessin coûtait ~140 ms à chaque image (150 zones, motifs et symboles). Le calque est dessiné une fois dans une
/// image transparente à la taille de l'écran, puis recopié tant que ce qu'il montre (<c>content</c>, comparé par
/// référence), la vue (transformation, découpe) et le style ne changent pas.
/// </summary>
internal sealed class LayerCache
{
    private Bitmap? _image;
    private (object? Content, float M11, float M12, float M21, float M22, float Dx, float Dy, Rectangle Clip, int Style) _key;

    /// <summary>
    /// Dessine le calque dans <paramref name="g"/> (déjà placé en coordonnées de carte) : depuis l'image gardée si
    /// rien n'a changé, sinon en appelant <paramref name="draw"/> dans une nouvelle image.
    /// </summary>
    public void Draw(Graphics g, object content, int style, Action<Graphics> draw)
    {
        using var transform = g.Transform;
        var e = transform.Elements;
        var clip = DeviceClip(g, transform);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;

        var key = (content, e[0], e[1], e[2], e[3], e[4], e[5], clip, style);
        if (_image is null || !ReferenceEquals(_key.Content, content) || key != _key)
        {
            if (_image is null || _image.Width != clip.Width || _image.Height != clip.Height)
            {
                _image?.Dispose();
                _image = new Bitmap(clip.Width, clip.Height);
            }

            using (var layer = Graphics.FromImage(_image))
            {
                layer.Clear(Color.Transparent);
                layer.SmoothingMode = g.SmoothingMode;
                layer.InterpolationMode = g.InterpolationMode;
                layer.TextRenderingHint = g.TextRenderingHint;
                layer.TranslateTransform(-clip.X, -clip.Y);
                layer.MultiplyTransform(transform);
                draw(layer);
            }
            _key = key;
        }

        var state = g.Save();
        g.ResetTransform();
        g.ResetClip();
        g.DrawImageUnscaled(_image, clip.X, clip.Y);
        g.Restore(state);
    }

    /// <summary>La zone visible, en pixels de l'image où l'on dessine.</summary>
    private static Rectangle DeviceClip(Graphics g, Matrix transform)
    {
        var bounds = g.VisibleClipBounds;
        var corners = new[] { bounds.Location, new PointF(bounds.Right, bounds.Bottom) };
        transform.TransformPoints(corners);
        var left = (int)Math.Floor(Math.Min(corners[0].X, corners[1].X));
        var top = (int)Math.Floor(Math.Min(corners[0].Y, corners[1].Y));
        var right = (int)Math.Ceiling(Math.Max(corners[0].X, corners[1].X));
        var bottom = (int)Math.Ceiling(Math.Max(corners[0].Y, corners[1].Y));
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
