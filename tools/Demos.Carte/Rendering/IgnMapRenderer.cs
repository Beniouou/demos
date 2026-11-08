using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Demos.Domain.Map;

namespace Demos.Carte.Rendering;

/// <summary>
/// Moteur de rendu cartographique IGN Pastel :
/// style technique, pastel doux, haute lisibilité, isohypses glacées en altitude.
/// </summary>
public static class IgnMapRenderer
{
    public static void Render(
        Graphics g,
        VectorMapDefinition map,
        Heightmap heightmap,
        bool showContours,
        bool showLabels,
        bool showResources,
        bool showCadastre)
    {
        // 1. Fond papier vélin IGN doux
        using (var brush = new SolidBrush(Color.FromArgb(250, 246, 237)))
        {
            g.FillRectangle(brush, 0, 0, (float)map.Width, (float)map.Height);
        }

        // 2. Mer et littoral
        DrawSea(g, map, heightmap);

        // 3. Biomes naturels (Forêts, Marais, Terres fertiles, Plages, Falaises, Déserts, Glaciers)
        DrawBiomes(g, map);

        // 4. Estompage topographique (maquette historique sans courbes réelles)
        if (map.Width <= 1100.0 && map.Height <= 750.0 && map.Contours.Count == 0)
        {
            DrawHillshading(g, map);
        }

        // 5. Courbes de niveau IGN continues
        if (showContours)
        {
            DrawContours(g, map, showLabels);
        }

        // 6. Fleuves et cours d'eau
        DrawRivers(g, map, showLabels);

        // 7. Points cotés d'altitude
        if (showLabels)
        {
            DrawSpotElevations(g, map);
        }

        // 8. Liseré de juridiction / souveraineté
        DrawSovereignty(g, map);

        // 9. Gisements naturels
        if (showResources)
        {
            DrawResources(g, map, showLabels);
        }

        // 10. Communes & Villages du pays
        if (showCadastre)
        {
            DrawCommunes(g, map);
        }
    }

    private static void DrawSea(Graphics g, VectorMapDefinition map, Heightmap? heightmap)
    {
        using (var brush = new SolidBrush(Color.FromArgb(148, 181, 197)))
        {
            foreach (var body in map.WaterBodies)
            {
                if (body.Boundary.Count < 3) continue;
                if (body.IsErroneousGlobalSea(map.Width, map.Height)) continue;
                var pts = body.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                g.FillPolygon(brush, pts);
            }
        }

        if (heightmap is not null && heightmap.MinElevation < 0)
        {
            var coastSegments = ContourExtractor.ExtractSegmentsForLevel(heightmap, 0.0);
            using var shallowPen = new Pen(Color.FromArgb(90, 255, 255, 255), 5.0f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var coastPen = new Pen(Color.FromArgb(105, 145, 170), 2.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };

            foreach (var seg in coastSegments)
            {
                var x1 = (float)seg.P1.X;
                var y1 = (float)seg.P1.Y;
                var x2 = (float)seg.P2.X;
                var y2 = (float)seg.P2.Y;
                g.DrawLine(shallowPen, x1, y1, x2, y2);
                g.DrawLine(coastPen, x1, y1, x2, y2);
            }
        }
        else
        {
            using var coastPen = new Pen(Color.FromArgb(105, 145, 170), 2.2f) { LineJoin = LineJoin.Round };
            foreach (var body in map.WaterBodies)
            {
                if (body.Boundary.Count < 3) continue;
                if (body.IsErroneousGlobalSea(map.Width, map.Height)) continue;
                var pts = body.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                g.DrawPolygon(coastPen, pts);
            }
        }
    }

    private static void DrawBiomes(Graphics g, VectorMapDefinition map)
    {
        var biomes = map.AllBiomes;
        if (biomes.Count == 0) return;

        foreach (var biome in biomes)
        {
            if (biome.Boundary.Count < 3) continue;
            var pts = biome.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();

            var (washColor, strokeColor, patternColor) = biome.Kind.ToLowerInvariant() switch
            {
                "foret" or "forest" => (Color.FromArgb(197, 214, 191), Color.FromArgb(142, 164, 135), Color.FromArgb(127, 153, 118)),
                "fertile" or "zone fertile" => (Color.FromArgb(238, 228, 185), Color.FromArgb(200, 185, 130), Color.FromArgb(180, 150, 80)),
                "marais" => (Color.FromArgb(195, 218, 205), Color.FromArgb(135, 175, 155), Color.FromArgb(100, 150, 125)),
                "plage" => (Color.FromArgb(242, 230, 188), Color.FromArgb(215, 195, 145), Color.FromArgb(195, 170, 120)),
                "falaise" => (Color.FromArgb(200, 202, 205), Color.FromArgb(120, 125, 130), Color.FromArgb(100, 105, 110)),
                "desert" => (Color.FromArgb(245, 222, 170), Color.FromArgb(215, 175, 115), Color.FromArgb(215, 165, 95)),
                "glacier" => (Color.FromArgb(225, 240, 248), Color.FromArgb(140, 180, 210), Color.FromArgb(105, 155, 195)),
                _ => (Color.FromArgb(197, 214, 191), Color.FromArgb(142, 164, 135), Color.FromArgb(127, 153, 118))
            };

            using var path = new GraphicsPath(FillMode.Alternate);
            path.AddPolygon(pts);
            if (biome.Holes is { Count: > 0 } holes)
            {
                foreach (var hole in holes)
                {
                    if (hole.Count >= 3)
                    {
                        path.AddPolygon(hole.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray());
                    }
                }
            }

            using (var brush = new SolidBrush(washColor))
            using (var pen = new Pen(strokeColor, 1.2f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            var minX = pts.Min(p => p.X);
            var maxX = pts.Max(p => p.X);
            var minY = pts.Min(p => p.Y);
            var maxY = pts.Max(p => p.Y);

            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect);

            using var patternBrush = new SolidBrush(patternColor);
            using var patternPen = new Pen(patternColor, 1.1f);

            const float stepX = 32f;
            const float stepY = 28f;
            var row = 0;
            var symbolCount = 0;

            for (var y = minY + 14f; y <= maxY - 8f; y += stepY)
            {
                var rowOffset = (row % 2 == 1) ? stepX * 0.5f : 0f;
                row++;

                for (var x = minX + 16f + rowOffset; x <= maxX - 8f; x += stepX)
                {
                    var jx = x + (float)(Math.Sin(x * 11.3 + y * 27.7) * 4.0);
                    var jy = y + (float)(Math.Cos(x * 19.1 + y * 13.9) * 4.0);

                    if (CartoSymbols.IsPointInPolygon(jx, jy, pts) && !IsPointInHoles(jx, jy, biome.Holes))
                    {
                        DrawBiomeSymbol(g, biome.Kind, patternBrush, patternPen, jx, jy);
                        symbolCount++;
                    }
                }
            }

            if (symbolCount == 0)
            {
                var candidate = FindInternalPoint(biome.Boundary, biome.Holes);
                if (candidate.HasValue)
                {
                    DrawBiomeSymbol(g, biome.Kind, patternBrush, patternPen, candidate.Value.X, candidate.Value.Y);
                }
            }

            g.Restore(state);
        }
    }

    private static void DrawBiomeSymbol(Graphics g, string kind, Brush brush, Pen pen, float x, float y)
    {
        switch (kind.ToLowerInvariant())
        {
            case "foret" or "forest":
                CartoSymbols.DrawTreeCluster(g, brush, x, y, 8.5f);
                break;
            case "fertile" or "zone fertile":
                CartoSymbols.DrawWheatStalk(g, pen, x, y, 7.5f);
                break;
            case "marais":
                CartoSymbols.DrawReedTuft(g, pen, x, y, 8.0f);
                break;
            case "plage":
                CartoSymbols.DrawSandStipples(g, brush, x, y, 6.0f);
                break;
            case "falaise":
                CartoSymbols.DrawCliffRock(g, pen, x, y, 8.0f);
                break;
            case "desert":
                CartoSymbols.DrawDuneRipple(g, pen, x, y, 8.0f);
                break;
            case "glacier":
                CartoSymbols.DrawGlacierCrevasse(g, pen, x, y, 8.0f);
                break;
            default:
                CartoSymbols.DrawTreeCluster(g, brush, x, y, 8.5f);
                break;
        }
    }

    private static void DrawHillshading(Graphics g, VectorMapDefinition map)
    {
        var sx = (float)(map.Width / 1000.0);
        var sy = (float)(map.Height / 640.0);

        using var penShade = new Pen(Color.FromArgb(20, 70, 50, 30), 8.0f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        var ridge1 = new PointF[] { new(680 * sx, 55 * sy), new(760 * sx, 42 * sy), new(820 * sx, 45 * sy), new(890 * sx, 78 * sy) };
        var ridge2 = new PointF[] { new(700 * sx, 105 * sy), new(780 * sx, 95 * sy), new(850 * sx, 120 * sy) };
        var ridge3 = new PointF[] { new(760 * sx, 140 * sy), new(820 * sx, 160 * sy), new(890 * sx, 175 * sy) };

        g.DrawCurve(penShade, ridge1, 0.4f);
        g.DrawCurve(penShade, ridge2, 0.4f);
        g.DrawCurve(penShade, ridge3, 0.4f);
    }

    private static void DrawContours(Graphics g, VectorMapDefinition map, bool showLabels)
    {
        using var penIndex = new Pen(Color.FromArgb(117, 88, 59), 1.6f);
        using var penStd = new Pen(Color.FromArgb(156, 127, 96), 0.9f);
        using var penGlacierIndex = new Pen(Color.FromArgb(90, 160, 215), 1.6f);
        using var penGlacierStd = new Pen(Color.FromArgb(140, 195, 235), 0.9f);
        using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(117, 88, 59));
        using var textGlacierBrush = new SolidBrush(Color.FromArgb(70, 140, 195));

        foreach (var c in map.Contours)
        {
            if (c.Points.Count < 2) continue;
            var pts = c.Points.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
            var isGlacierAlt = c.Altitude >= 2500.0;
            var pen = isGlacierAlt
                ? (c.IsIndex ? penGlacierIndex : penGlacierStd)
                : (c.IsIndex ? penIndex : penStd);

            if (c.IsClosed && pts.Length >= 3)
            {
                g.DrawPolygon(pen, pts);
            }
            else
            {
                g.DrawLines(pen, pts);
            }

            if (c.IsIndex && showLabels && pts.Length >= 5)
            {
                var mid = pts[pts.Length / 2];
                var label = $"{c.Altitude:F0}";
                g.DrawString(label, font, isGlacierAlt ? textGlacierBrush : textBrush, mid.X + 2, mid.Y - 12);
            }
        }
    }

    private static void DrawRivers(Graphics g, VectorMapDefinition map, bool showLabels)
    {
        foreach (var river in map.AllRivers)
        {
            if (river.Points.Count < 2) continue;
            var pts = river.Points.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();

            using var penRiver = new Pen(Color.FromArgb(104, 144, 160), (float)river.Width);
            penRiver.StartCap = LineCap.Round;
            penRiver.EndCap = LineCap.Round;
            penRiver.LineJoin = LineJoin.Round;
            g.DrawCurve(penRiver, pts, 0.4f);

            using var penCore = new Pen(Color.FromArgb(144, 181, 196), Math.Max(1.5f, (float)river.Width * 0.45f));
            penCore.StartCap = LineCap.Round;
            penCore.EndCap = LineCap.Round;
            penCore.LineJoin = LineJoin.Round;
            g.DrawCurve(penCore, pts, 0.4f);

            if (showLabels && pts.Length >= 4 && !string.IsNullOrWhiteSpace(river.Name))
            {
                var p = pts[pts.Length / 2];
                using var font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                using var brush = new SolidBrush(Color.FromArgb(60, 95, 110));
                g.DrawString(river.Name, font, brush, p.X + 8, p.Y - 14);
            }
        }
    }

    private static void DrawSpotElevations(Graphics g, VectorMapDefinition map)
    {
        using var pen = new Pen(Color.FromArgb(74, 52, 32), 1.4f);
        using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(74, 52, 32));

        foreach (var s in map.SpotElevations)
        {
            var x = (float)s.Position.X;
            var y = (float)s.Position.Y;
            const float size = 4.5f;

            g.DrawLine(pen, x - size, y, x + size, y);
            g.DrawLine(pen, x, y - size, x, y + size);

            var cat = PlaceCategories.Get(s.Category);
            var label = s.ShowAltitude
                ? $"{cat.Icon} {s.Name} ({s.Altitude:F0} m)"
                : $"{cat.Icon} {s.Name}";

            g.DrawString(label, font, brush, x + 6, y - 6);
        }
    }

    private static void DrawSovereignty(Graphics g, VectorMapDefinition map)
    {
        if (map.Width == 1000 && map.Height == 640 && map.AllCommunes.Count <= 1)
        {
            var sx = (float)(map.Width / 1000.0);
            var sy = (float)(map.Height / 640.0);

            var poly = new PointF[]
            {
                new(270 * sx, 160 * sy), new(480 * sx, 120 * sy), new(660 * sx, 170 * sy),
                new(700 * sx, 440 * sy), new(480 * sx, 510 * sy), new(220 * sx, 430 * sy), new(200 * sx, 230 * sy)
            };

            using var pen = new Pen(Color.FromArgb(168, 76, 66), 1.8f)
            {
                DashStyle = DashStyle.Dash
            };
            g.DrawPolygon(pen, poly);
        }
    }

    private static void DrawResources(Graphics g, VectorMapDefinition map, bool showLabels)
    {
        using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        if (map.AllResourceZones.Count > 0)
        {
            foreach (var zone in map.AllResourceZones)
            {
                if (zone.Boundary.Count < 3) continue;
                var pts = zone.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();

                var (fillColor, strokeColor, icon) = zone.Kind.ToLowerInvariant() switch
                {
                    "pierre" => (Color.FromArgb(70, 140, 160, 175), Color.FromArgb(180, 90, 110, 125), "🪨"),
                    "charbon" => (Color.FromArgb(85, 45, 45, 48), Color.FromArgb(190, 30, 30, 32), "⛏️"),
                    "fer" => (Color.FromArgb(75, 185, 95, 60), Color.FromArgb(190, 140, 65, 40), "⛓️"),
                    "fertile" or "zone fertile" or "ble" or "lin" => (Color.FromArgb(70, 225, 200, 115), Color.FromArgb(160, 175, 140, 65), "🌾"),
                    _ => (Color.FromArgb(60, 150, 150, 150), Color.FromArgb(160, 100, 100, 100), "💎")
                };

                using var zonePath = new GraphicsPath(FillMode.Alternate);
                zonePath.AddPolygon(pts);
                if (zone.Holes is { Count: > 0 } zHoles)
                {
                    foreach (var h in zHoles)
                    {
                        if (h.Count >= 3)
                            zonePath.AddPolygon(h.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray());
                    }
                }

                using (var fillBrush = new SolidBrush(fillColor))
                using (var strokePen = new Pen(strokeColor, 1.4f))
                {
                    g.FillPath(fillBrush, zonePath);
                    g.DrawPath(strokePen, zonePath);
                }

                if (showLabels)
                {
                    var labelPt = FindInternalPoint(zone.Boundary, zone.Holes) ?? new PointF(pts.Average(p => p.X), pts.Average(p => p.Y));
                    var cx = labelPt.X;
                    var cy = labelPt.Y;
                    var label = $"{icon} {zone.Name}";
                    var sz = g.MeasureString(label, font);
                    var rect = new RectangleF(cx - sz.Width * 0.5f - 4, cy - sz.Height * 0.5f - 2, sz.Width + 8, sz.Height + 4);

                    using var tagBg = new SolidBrush(Color.FromArgb(235, 255, 253, 245));
                    using var tagBorder = new Pen(strokeColor, 1.0f);
                    using var textBrush = new SolidBrush(Color.FromArgb(40, 30, 20));

                    g.FillRectangle(tagBg, rect);
                    g.DrawRectangle(tagBorder, rect.X, rect.Y, rect.Width, rect.Height);
                    g.DrawString(label, font, textBrush, rect, sf);
                }
            }
            return;
        }

        // Rétrocompatibilité : anciens points de gisements
        using var legacyFont = new Font("Segoe UI", 8f, FontStyle.Regular);
        using var legacyTextBrush = new SolidBrush(Color.FromArgb(61, 41, 22));

        foreach (var r in map.Resources)
        {
            var x = (float)r.Position.X;
            var y = (float)r.Position.Y;

            var (color, label) = r.Resource.ToLowerInvariant() switch
            {
                "pierre" => (Color.FromArgb(106, 128, 144), "Carrière de pierre"),
                "charbon" => (Color.FromArgb(50, 50, 50), "Mine de charbon"),
                "fer" => (Color.FromArgb(175, 90, 60), "Gisement de fer"),
                "fertile" or "ble" or "lin" => (Color.FromArgb(200, 160, 60), "Zone fertile"),
                _ => (Color.FromArgb(120, 120, 120), r.Resource),
            };

            using (var fillBrush = new SolidBrush(color))
            using (var strokePen = new Pen(Color.FromArgb(40, 30, 20), 1.2f))
            {
                g.FillRectangle(fillBrush, x - 7, y - 5, 14, 10);
                g.DrawRectangle(strokePen, x - 7, y - 5, 14, 10);
            }

            if (showLabels)
            {
                g.DrawString(label, legacyFont, legacyTextBrush, x + 9, y - 6);
            }
        }
    }

    private static void DrawCommunes(Graphics g, VectorMapDefinition map)
    {
        var communes = map.AllCommunes;
        if (communes.Count == 0) return;

        using var fontCommune = new Font("Segoe UI", 9.0f, FontStyle.Bold);
        using var fillCommune = new SolidBrush(Color.FromArgb(175, 45, 35));
        using var textBrush = new SolidBrush(Color.FromArgb(40, 25, 15));
        using var cartoucheBg = new SolidBrush(Color.FromArgb(235, 255, 253, 245));
        using var cartoucheBorder = new Pen(Color.FromArgb(160, 130, 95), 1.0f);
        using var borderPen = new Pen(Color.FromArgb(40, 25, 15), 1.2f);
        using var influencePen = new Pen(Color.FromArgb(60, 168, 76, 66), 1.2f) { DashStyle = DashStyle.Dash };
        using var dotBrush = new SolidBrush(Color.FromArgb(240, 230, 215));

        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        foreach (var c in communes)
        {
            var cx = (float)c.Position.X;
            var cy = (float)c.Position.Y;

            const float radius = 25f;
            g.DrawEllipse(influencePen, cx - radius, cy - radius, radius * 2, radius * 2);

            g.FillEllipse(fillCommune, cx - 5, cy - 5, 10, 10);
            g.DrawEllipse(borderPen, cx - 5, cy - 5, 10, 10);
            g.FillEllipse(dotBrush, cx - 2, cy - 2, 4, 4);

            var textSize = g.MeasureString(c.Name, fontCommune);
            var labelRect = new RectangleF(cx - textSize.Width * 0.5f - 4, cy + 8, textSize.Width + 8, textSize.Height + 2);

            g.FillRectangle(cartoucheBg, labelRect);
            g.DrawRectangle(cartoucheBorder, labelRect.X, labelRect.Y, labelRect.Width, labelRect.Height);
            g.DrawString(c.Name, fontCommune, textBrush, labelRect, sf);
        }
    }

    private static bool IsPointInHoles(float x, float y, IReadOnlyList<IReadOnlyList<Point2D>>? holes)
    {
        if (holes is null || holes.Count == 0) return false;
        foreach (var hole in holes)
        {
            if (hole.Count >= 3 && CartoSymbols.IsPointInPolygon(x, y, hole))
                return true;
        }
        return false;
    }

    private static PointF? FindInternalPoint(IReadOnlyList<Point2D> outer, IReadOnlyList<IReadOnlyList<Point2D>>? holes)
    {
        if (outer.Count < 3) return null;
        var avgX = outer.Average(p => p.X);
        var avgY = outer.Average(p => p.Y);
        if (CartoSymbols.IsPointInPolygon(avgX, avgY, outer) && !IsPointInHoles((float)avgX, (float)avgY, holes))
        {
            return new PointF((float)avgX, (float)avgY);
        }

        for (var i = 0; i < outer.Count - 1; i++)
        {
            var p1 = outer[i];
            var p2 = outer[i + 1];
            var mx = (p1.X + p2.X) * 0.5;
            var my = (p1.Y + p2.Y) * 0.5;
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4) continue;
            var nx = -dy / len;
            var ny = dx / len;

            foreach (var d in new[] { 4.0, 8.0, 14.0 })
            {
                var t1 = new Point2D(mx + nx * d, my + ny * d);
                if (CartoSymbols.IsPointInPolygon(t1.X, t1.Y, outer) && !IsPointInHoles((float)t1.X, (float)t1.Y, holes))
                    return new PointF((float)t1.X, (float)t1.Y);

                var t2 = new Point2D(mx - nx * d, my - ny * d);
                if (CartoSymbols.IsPointInPolygon(t2.X, t2.Y, outer) && !IsPointInHoles((float)t2.X, (float)t2.Y, holes))
                    return new PointF((float)t2.X, (float)t2.Y);
            }
        }
        return null;
    }
}
