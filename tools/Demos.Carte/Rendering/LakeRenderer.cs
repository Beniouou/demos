using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Demos.Domain.Map;

namespace Demos.Carte.Rendering;

/// <summary>
/// Les lacs (24 novembre 2026) : l'eau à plat, dessinée au-dessus du relief estompé et des courbes de niveau (qui ne se
/// voient pas sous l'eau), avec la rive et le nom. Les îles (trous du contour) restent à terre.
/// </summary>
public static class LakeRenderer
{
    public static void Draw(Graphics g, VectorMapDefinition map, Color water, Color shore, Color label, bool showLabels)
    {
        foreach (var lake in map.AllLakes)
        {
            if (lake.Boundary.Count < 3) continue;
            using var path = new GraphicsPath(FillMode.Alternate);
            path.AddPolygon(lake.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray());
            foreach (var hole in lake.Holes ?? [])
                if (hole.Count >= 3)
                    path.AddPolygon(hole.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray());

            using (var brush = new SolidBrush(water))
                g.FillPath(brush, path);
            using (var pen = new Pen(shore, 1.6f) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, path);

            if (showLabels && !string.IsNullOrWhiteSpace(lake.Name))
            {
                var cx = (float)lake.Boundary.Average(p => p.X);
                var cy = (float)lake.Boundary.Average(p => p.Y);
                using var font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                using var brush = new SolidBrush(label);
                var size = g.MeasureString(lake.Name, font);
                g.DrawString(lake.Name, font, brush, cx - size.Width / 2, cy - size.Height / 2);
            }
        }
    }
}
