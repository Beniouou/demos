using System;
using System.Drawing;
using System.Linq;
using Demos.Domain.Map;

namespace Demos.Carte.Controls;

/// <summary>
/// Gestionnaire d'affichage de la minimap globale dans l'angle inférieur droit du canvas,
/// avec rendu de la silhouette des terres, biomes, gisements, fleuves, communes et du viewport.
/// </summary>
public static class CanvasMinimapRenderer
{
    public static Rectangle Render(
        Graphics g,
        int canvasWidth,
        int canvasHeight,
        VectorMapDefinition map,
        RectangleF visibleWorldRect)
    {
        const int miniW = 180;
        var miniH = (int)Math.Round(miniW * (map.Height / map.Width));
        const int margin = 14;

        var minimapRect = new Rectangle(canvasWidth - miniW - margin, canvasHeight - miniH - margin, miniW, miniH);

        // Fond et cadre de la minimap
        using (var bgBrush = new SolidBrush(Color.FromArgb(235, 248, 245, 238)))
        using (var borderPen = new Pen(Color.FromArgb(100, 75, 45), 1.5f))
        {
            g.FillRectangle(bgBrush, minimapRect);
            g.DrawRectangle(borderPen, minimapRect);
        }

        var state = g.Save();
        g.SetClip(minimapRect);

        var miniScale = (float)miniW / (float)map.Width;
        g.TranslateTransform(minimapRect.X, minimapRect.Y);
        g.ScaleTransform(miniScale, miniScale);

        // 1. Masses d'eau
        using var waterBrush = new SolidBrush(Color.FromArgb(148, 181, 197));
        foreach (var body in map.WaterBodies)
        {
            if (body.Boundary.Count >= 3 && !body.IsErroneousGlobalSea(map.Width, map.Height))
            {
                var pts = body.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                g.FillPolygon(waterBrush, pts);
            }
        }

        // 2. Zones de gisements
        foreach (var zone in map.AllResourceZones)
        {
            if (zone.Boundary.Count >= 3)
            {
                var color = zone.Kind.ToLowerInvariant() switch
                {
                    "pierre" => Color.FromArgb(140, 160, 175),
                    "charbon" => Color.FromArgb(50, 50, 52),
                    "fer" => Color.FromArgb(185, 95, 60),
                    "fertile" or "zone fertile" or "ble" or "lin" => Color.FromArgb(220, 195, 110),
                    _ => Color.FromArgb(160, 160, 160)
                };
                using var resZoneBrush = new SolidBrush(Color.FromArgb(120, color));
                var pts = zone.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                g.FillPolygon(resZoneBrush, pts);
            }
        }

        // 3. Biomes naturels
        foreach (var biome in map.AllBiomes)
        {
            if (biome.Boundary.Count >= 3)
            {
                var color = biome.Kind.ToLowerInvariant() switch
                {
                    "foret" or "forest" => Color.FromArgb(197, 214, 191),
                    "fertile" or "zone fertile" => Color.FromArgb(235, 225, 185),
                    "marais" => Color.FromArgb(195, 218, 205),
                    "plage" => Color.FromArgb(242, 230, 188),
                    "falaise" => Color.FromArgb(195, 197, 200),
                    "desert" => Color.FromArgb(240, 220, 175),
                    "glacier" => Color.FromArgb(230, 242, 250),
                    _ => Color.FromArgb(197, 214, 191)
                };
                using var bBrush = new SolidBrush(color);
                var pts = biome.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                g.FillPolygon(bBrush, pts);
            }
        }

        // 4. Fleuves
        using var riverPen = new Pen(Color.FromArgb(104, 144, 160), 4.0f);
        foreach (var r in map.AllRivers)
        {
            if (r.Points.Count >= 2)
            {
                var pts = r.Points.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                g.DrawCurve(riverPen, pts, 0.4f);
            }
        }

        // 5. Communes
        using var townBrush = new SolidBrush(Color.FromArgb(178, 34, 34));
        using var townBorder = new Pen(Color.FromArgb(50, 20, 20), 1.0f);
        foreach (var c in map.AllCommunes)
        {
            var cx = (float)c.Position.X;
            var cy = (float)c.Position.Y;
            g.FillEllipse(townBrush, cx - 3.5f, cy - 3.5f, 7, 7);
            g.DrawEllipse(townBorder, cx - 3.5f, cy - 3.5f, 7, 7);
        }

        // 6. Cadre rouge du viewport visible
        using var viewPen = new Pen(Color.FromArgb(200, 200, 30, 30), 2.0f / miniScale);
        using var viewBrush = new SolidBrush(Color.FromArgb(35, 200, 30, 30));
        g.FillRectangle(viewBrush, visibleWorldRect);
        g.DrawRectangle(viewPen, visibleWorldRect.X, visibleWorldRect.Y, visibleWorldRect.Width, visibleWorldRect.Height);

        g.Restore(state);
        return minimapRect;
    }
}
