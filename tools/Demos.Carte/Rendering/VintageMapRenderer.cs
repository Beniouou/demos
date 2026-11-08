using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Demos.Domain.Map;

namespace Demos.Carte.Rendering;

/// <summary>
/// Moteur de rendu cartographique Parchemin Ancien (1874) :
/// lavis à l'aquarelle, lignes de rhumb, rose des vents, hachures d'arpentage,
/// gravures à la plume et calligraphie cursive d'époque.
/// </summary>
public static class VintageMapRenderer
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
        // 1. Fond parchemin vieilli avec pliures et patine d'âge
        DrawAgedParchmentBackground(g, (float)map.Width, (float)map.Height);

        // 2. Mer avec estuaire, lignes de rhumb et hachures de rive
        DrawSea(g, map, heightmap);

        // 3. Biomes naturels avec aquarelles et motifs gravés à la plume
        DrawBiomes(g, map);

        // 4. Courbes de niveau gravées à l'encre bistre avec dégradé bleu glacier au-delà de 2 500 m
        if (showContours)
        {
            DrawContours(g, map, showLabels);
        }

        // 5. Fleuves et cours d'eau (rives doubles et sinuosités)
        DrawRivers(g, map);

        // 6. Liseré d'arpentage et voile TERRA INCOGNITA (maquette historique)
        if (map.Width <= 1100.0 && map.Height <= 750.0 && map.AllCommunes.Count <= 1)
        {
            DrawTerraIncognita(g, (float)map.Width, (float)map.Height);
        }

        // 7. Gisements naturels en style d'époque
        if (showResources)
        {
            DrawResources(g, map, showLabels);
        }

        // 8. Lieux nommés & toponymie d'époque
        if (showLabels)
        {
            DrawSpotElevations(g, map);
        }

        // 9. Communes & localités rurales
        if (showCadastre)
        {
            DrawCommunes(g, map);
        }

        // 10. Rose des vents marine et échelle graphique
        DrawCompassRoseAndScale(g, (float)map.Height);

        // 11. Cartouche de titre ouvragé
        DrawVintageCartouche(g, (float)map.Width);
    }

    private static void DrawAgedParchmentBackground(Graphics g, float w, float h)
    {
        using (var brush = new LinearGradientBrush(
            new PointF(0, 0), new PointF(w, h),
            Color.FromArgb(245, 237, 218), Color.FromArgb(233, 219, 192)))
        {
            g.FillRectangle(brush, 0, 0, w, h);
        }

        var foldX = w * 0.5f;
        using (var foldShadow = new LinearGradientBrush(
            new PointF(foldX - 8, 0), new PointF(foldX, 0),
            Color.FromArgb(0, 80, 60, 40), Color.FromArgb(35, 80, 60, 40)))
        {
            g.FillRectangle(foldShadow, foldX - 8, 0, 8, h);
        }
        using (var foldLight = new LinearGradientBrush(
            new PointF(foldX, 0), new PointF(foldX + 8, 0),
            Color.FromArgb(30, 255, 255, 240), Color.FromArgb(0, 255, 255, 240)))
        {
            g.FillRectangle(foldLight, foldX, 0, 8, h);
        }

        var foldY = h * 0.5f;
        using (var foldHShadow = new LinearGradientBrush(
            new PointF(0, foldY - 6), new PointF(0, foldY),
            Color.FromArgb(0, 80, 60, 40), Color.FromArgb(25, 80, 60, 40)))
        {
            g.FillRectangle(foldHShadow, 0, foldY - 6, w, 6);
        }
        using (var foldHLight = new LinearGradientBrush(
            new PointF(0, foldY), new PointF(0, foldY + 6),
            Color.FromArgb(25, 255, 255, 240), Color.FromArgb(0, 255, 255, 240)))
        {
            g.FillRectangle(foldHLight, 0, foldY, w, 6);
        }

        DrawCornerStain(g, 0, 0, 180, 140);
        DrawCornerStain(g, w, 0, -180, 140);
        DrawCornerStain(g, 0, h, 180, -140);
        DrawCornerStain(g, w, h, -180, -140);
    }

    private static void DrawCornerStain(Graphics g, float x, float y, float dx, float dy)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(x - Math.Abs(dx), y - Math.Abs(dy), Math.Abs(dx) * 2, Math.Abs(dy) * 2);
        using var pgb = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(40, 120, 90, 50),
            SurroundColors = [Color.FromArgb(0, 120, 90, 50)]
        };
        g.FillPath(pgb, path);
    }

    private static void DrawSea(Graphics g, VectorMapDefinition map, Heightmap? heightmap)
    {
        using (var brush = new SolidBrush(Color.FromArgb(215, 226, 224)))
        {
            foreach (var body in map.WaterBodies)
            {
                if (body.Boundary.Count < 3) continue;
                if (body.IsErroneousGlobalSea(map.Width, map.Height)) continue;
                var pts = body.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                g.FillPolygon(brush, pts);
            }
        }

        using (var pen = new Pen(Color.FromArgb(120, 100, 75), 1.6f) { LineJoin = LineJoin.Round })
        {
            if (heightmap is not null && heightmap.MinElevation < 0)
            {
                var coastSegments = ContourExtractor.ExtractSegmentsForLevel(heightmap, 0.0);
                foreach (var seg in coastSegments)
                {
                    g.DrawLine(pen, (float)seg.P1.X, (float)seg.P1.Y, (float)seg.P2.X, (float)seg.P2.Y);
                }
            }
            else
            {
                foreach (var body in map.WaterBodies)
                {
                    if (body.Boundary.Count < 3) continue;
                    if (body.IsErroneousGlobalSea(map.Width, map.Height)) continue;
                    var pts = body.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                    g.DrawPolygon(pen, pts);
                }
            }
        }

        foreach (var body in map.WaterBodies)
        {
            if (body.Boundary.Count < 3) continue;
            if (body.IsErroneousGlobalSea(map.Width, map.Height)) continue;
            var pts = body.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();

            var sx = (float)(map.Width / 1000.0);
            var sy = (float)(map.Height / 640.0);
            var seaCenter = new PointF(100 * sx, (float)map.Height - 80 * sy);
            using var rhumbPen = new Pen(Color.FromArgb(70, 100, 80, 60), 0.8f);
            var maxRay = Math.Max((float)map.Width, (float)map.Height) * 0.7f;
            for (var angle = 0; angle < 360; angle += 15)
            {
                var rad = angle * Math.PI / 180.0;
                var rx = seaCenter.X + (float)(Math.Cos(rad) * maxRay);
                var ry = seaCenter.Y + (float)(Math.Sin(rad) * maxRay);
                g.DrawLine(rhumbPen, seaCenter.X, seaCenter.Y, rx, ry);
            }

            using var hatchPen = new Pen(Color.FromArgb(100, 140, 125, 100), 1.0f);
            for (var i = 0; i < pts.Length - 1; i++)
            {
                var p1 = pts[i];
                var p2 = pts[i + 1];
                var count = (int)(Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2)) / 8.0);
                for (var k = 0; k < count; k++)
                {
                    var t = (float)k / count;
                    var bx = p1.X + t * (p2.X - p1.X);
                    var by = p1.Y + t * (p2.Y - p1.Y);
                    g.DrawLine(hatchPen, bx, by, bx - 6, by + 4);
                }
            }
        }
    }

    private static void DrawBiomes(Graphics g, VectorMapDefinition map)
    {
        var biomes = map.AllBiomes;
        if (biomes.Count == 0) return;

        using var cursiveFont = new Font("Georgia", 10.5f, FontStyle.Italic);
        using var textBrush = new SolidBrush(Color.FromArgb(90, 70, 45));
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        foreach (var biome in biomes)
        {
            if (biome.Boundary.Count < 3) continue;
            var pts = biome.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();

            var (washColor, strokeColor, penColor) = biome.Kind.ToLowerInvariant() switch
            {
                "foret" or "forest" => (Color.FromArgb(218, 226, 206), Color.FromArgb(130, 115, 85), Color.FromArgb(90, 80, 55)),
                "fertile" or "zone fertile" => (Color.FromArgb(235, 225, 190), Color.FromArgb(160, 140, 100), Color.FromArgb(120, 95, 55)),
                "marais" => (Color.FromArgb(210, 225, 215), Color.FromArgb(130, 150, 135), Color.FromArgb(80, 105, 90)),
                "plage" => (Color.FromArgb(236, 223, 188), Color.FromArgb(160, 145, 115), Color.FromArgb(140, 115, 80)),
                "falaise" => (Color.FromArgb(205, 200, 190), Color.FromArgb(100, 85, 70), Color.FromArgb(80, 65, 50)),
                "desert" => (Color.FromArgb(240, 220, 180), Color.FromArgb(170, 140, 95), Color.FromArgb(135, 105, 65)),
                "glacier" => (Color.FromArgb(225, 235, 245), Color.FromArgb(110, 130, 155), Color.FromArgb(70, 95, 125)),
                _ => (Color.FromArgb(218, 226, 206), Color.FromArgb(130, 115, 85), Color.FromArgb(90, 80, 55))
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
            using (var pen = new Pen(strokeColor, 1.1f))
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

            using var inkPen = new Pen(penColor, 1.0f);
            using var inkBrush = new SolidBrush(penColor);

            const float stepX = 24f;
            const float stepY = 20f;
            var row = 0;
            var symbolCount = 0;
            PointF? labelCandidate = null;
            var bestDistToCenter = float.MaxValue;
            var cx = pts.Average(p => p.X);
            var cy = pts.Average(p => p.Y);

            for (var y = minY + 12f; y <= maxY - 8f; y += stepY)
            {
                var rowOffset = (row % 2 == 1) ? stepX * 0.5f : 0f;
                row++;

                for (var x = minX + 14f + rowOffset; x <= maxX - 8f; x += stepX)
                {
                    var jx = x + (float)(Math.Sin(x * 12.7 + y * 31.1) * 3.5);
                    var jy = y + (float)(Math.Cos(x * 23.3 + y * 17.9) * 3.0);

                    if (CartoSymbols.IsPointInPolygon(jx, jy, pts) && !IsPointInHoles(jx, jy, biome.Holes))
                    {
                        DrawBiomeSymbol(g, biome.Kind, inkBrush, inkPen, jx, jy);
                        symbolCount++;

                        var d = (jx - cx) * (jx - cx) + (jy - cy) * (jy - cy);
                        if (d < bestDistToCenter)
                        {
                            bestDistToCenter = d;
                            labelCandidate = new PointF(jx, jy);
                        }
                    }
                }
            }

            if (symbolCount == 0)
            {
                var candidate = FindInternalPoint(biome.Boundary, biome.Holes);
                if (candidate.HasValue)
                {
                    DrawBiomeSymbol(g, biome.Kind, inkBrush, inkPen, candidate.Value.X, candidate.Value.Y);
                    labelCandidate = candidate.Value;
                }
            }

            g.Restore(state);

            if (!string.IsNullOrWhiteSpace(biome.Name) && labelCandidate.HasValue && (maxX - minX > 80f || maxY - minY > 60f))
            {
                var labelPos = labelCandidate.Value;
                g.DrawString(biome.Name, cursiveFont, textBrush, labelPos.X, labelPos.Y, sf);
            }
        }
    }

    private static void DrawBiomeSymbol(Graphics g, string kind, Brush brush, Pen pen, float x, float y)
    {
        switch (kind.ToLowerInvariant())
        {
            case "foret" or "forest":
                CartoSymbols.DrawHandDrawnTree(g, pen, x, y, 6.5f);
                break;
            case "fertile" or "zone fertile":
                CartoSymbols.DrawWheatStalk(g, pen, x, y, 6.0f);
                break;
            case "marais":
                CartoSymbols.DrawReedTuft(g, pen, x, y, 6.5f);
                break;
            case "plage":
                CartoSymbols.DrawSandStipples(g, brush, x, y, 5.0f);
                break;
            case "falaise":
                CartoSymbols.DrawCliffRock(g, pen, x, y, 7.0f);
                break;
            case "desert":
                CartoSymbols.DrawDuneRipple(g, pen, x, y, 7.0f);
                break;
            case "glacier":
                CartoSymbols.DrawGlacierCrevasse(g, pen, x, y, 7.0f);
                break;
            default:
                CartoSymbols.DrawHandDrawnTree(g, pen, x, y, 6.5f);
                break;
        }
    }

    private static void DrawContours(Graphics g, VectorMapDefinition map, bool showLabels)
    {
        using var penIndex = new Pen(Color.FromArgb(125, 95, 65), 1.4f);
        using var penStd = new Pen(Color.FromArgb(160, 130, 95), 0.8f);
        using var penGlacierIndex = new Pen(Color.FromArgb(85, 125, 165), 1.4f);
        using var penGlacierStd = new Pen(Color.FromArgb(130, 165, 195), 0.8f);
        using var font = new Font("Georgia", 8f, FontStyle.Italic);
        using var textBrush = new SolidBrush(Color.FromArgb(105, 80, 50));
        using var textGlacierBrush = new SolidBrush(Color.FromArgb(70, 105, 145));

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

            if (c.IsIndex && showLabels && pts.Length >= 6)
            {
                var mid = pts[pts.Length / 2];
                g.DrawString($"{c.Altitude:F0} m", font, isGlacierAlt ? textGlacierBrush : textBrush, mid.X + 2, mid.Y - 10);
            }
        }
    }

    private static void DrawRivers(Graphics g, VectorMapDefinition map)
    {
        foreach (var river in map.AllRivers)
        {
            if (river.Points.Count < 2) continue;
            var pts = river.Points.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();

            using var riverFillPen = new Pen(Color.FromArgb(205, 218, 215), (float)river.Width);
            riverFillPen.StartCap = LineCap.Round;
            riverFillPen.EndCap = LineCap.Round;
            g.DrawCurve(riverFillPen, pts, 0.45f);

            using var bankPen = new Pen(Color.FromArgb(90, 70, 48), 1.0f);
            g.DrawCurve(bankPen, pts, 0.45f);

            if (pts.Length >= 4 && !string.IsNullOrWhiteSpace(river.Name))
            {
                var p = pts[pts.Length / 2];
                using var font = new Font("Georgia", 10.5f, FontStyle.Italic);
                using var brush = new SolidBrush(Color.FromArgb(70, 50, 32));
                g.DrawString(river.Name, font, brush, p.X + 8, p.Y - 14);
            }
        }
    }

    private static void DrawTerraIncognita(Graphics g, float w, float h)
    {
        var sx = w / 1000f;
        var sy = h / 640f;

        var exploredPoly = new PointF[]
        {
            new(220 * sx, 240 * sy), new(280 * sx, 160 * sy), new(460 * sx, 110 * sy),
            new(640 * sx, 120 * sy), new(740 * sx, 180 * sy), new(800 * sx, 280 * sy),
            new(780 * sx, 430 * sy), new(580 * sx, 510 * sy), new(340 * sx, 530 * sy),
            new(210 * sx, 470 * sy), new(180 * sx, 350 * sy)
        };

        using var dashedPen = new Pen(Color.FromArgb(120, 90, 60), 1.4f)
        {
            DashStyle = DashStyle.DashDot
        };
        g.DrawPolygon(dashedPen, exploredPoly);

        using var hatchPen = new Pen(Color.FromArgb(80, 110, 85, 55), 1.0f);
        for (var i = 0; i < exploredPoly.Length; i++)
        {
            var p1 = exploredPoly[i];
            var p2 = exploredPoly[(i + 1) % exploredPoly.Length];
            var steps = (int)(Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2)) / 10.0);
            for (var k = 0; k < steps; k++)
            {
                var t = (float)k / steps;
                var x = p1.X + t * (p2.X - p1.X);
                var y = p1.Y + t * (p2.Y - p1.Y);
                g.DrawLine(hatchPen, x, y, x + 4, y - 4);
            }
        }

        using var fontTitle = new Font("Georgia", 16f, FontStyle.Bold);
        using var inkBrush = new SolidBrush(Color.FromArgb(110, 75, 45));

        DrawCurvedText(g, "T E R R A   I N C O G N I T A", fontTitle, inkBrush, new PointF(180 * sx, 90 * sy), -22);
        DrawCurvedText(g, "T E R R A   I N C O G N I T A", fontTitle, inkBrush, new PointF(620 * sx, 65 * sy), 18);
        DrawCurvedText(g, "C O N T R É E S   I N E X P L O R É E S", fontTitle, inkBrush, new PointF(460 * sx, h - 45 * sy), -8);
    }

    private static void DrawCurvedText(Graphics g, string text, Font font, Brush brush, PointF center, float angleDeg)
    {
        var state = g.Save();
        g.TranslateTransform(center.X, center.Y);
        g.RotateTransform(angleDeg);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, 0, 0, sf);
        g.Restore(state);
    }

    private static void DrawResources(Graphics g, VectorMapDefinition map, bool showLabels)
    {
        using var font = new Font("Georgia", 8.5f, FontStyle.Italic);
        using var inkBrush = new SolidBrush(Color.FromArgb(70, 45, 25));

        if (map.AllResourceZones.Count > 0)
        {
            foreach (var zone in map.AllResourceZones)
            {
                if (zone.Boundary.Count < 3) continue;
                var pts = zone.Boundary.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();

                var (tint, stroke, icon) = zone.Kind.ToLowerInvariant() switch
                {
                    "pierre" => (Color.FromArgb(40, 120, 130, 140), Color.FromArgb(140, 90, 95, 105), "🪨"),
                    "charbon" => (Color.FromArgb(55, 60, 55, 50), Color.FromArgb(160, 50, 45, 40), "⛏️"),
                    "fer" => (Color.FromArgb(50, 150, 90, 60), Color.FromArgb(150, 120, 70, 45), "⛓️"),
                    "fertile" or "zone fertile" or "ble" or "lin" => (Color.FromArgb(45, 200, 175, 110), Color.FromArgb(130, 140, 110, 65), "🌾"),
                    _ => (Color.FromArgb(40, 130, 110, 80), Color.FromArgb(120, 90, 70, 50), "•")
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

                using (var fillBrush = new SolidBrush(tint))
                using (var antiquePen = new Pen(stroke, 1.2f) { DashStyle = DashStyle.Dash })
                {
                    g.FillPath(fillBrush, zonePath);
                    g.DrawPath(antiquePen, zonePath);
                }

                if (showLabels)
                {
                    var labelPt = FindInternalPoint(zone.Boundary, zone.Holes) ?? new PointF(pts.Average(p => p.X), pts.Average(p => p.Y));
                    var cx = labelPt.X;
                    var cy = labelPt.Y;
                    g.DrawString($"{icon} {zone.Name}", font, inkBrush, cx, cy, new StringFormat { Alignment = StringAlignment.Center });
                }
            }
            return;
        }

        using var pen = new Pen(Color.FromArgb(100, 70, 45), 1.2f);
        foreach (var r in map.Resources)
        {
            var x = (float)r.Position.X;
            var y = (float)r.Position.Y;

            g.DrawEllipse(pen, x - 4, y - 4, 8, 8);
            g.DrawLine(pen, x - 2, y, x + 2, y);

            if (showLabels)
            {
                g.DrawString(r.Region, font, inkBrush, x + 7, y - 6);
            }
        }
    }

    private static void DrawSpotElevations(Graphics g, VectorMapDefinition map)
    {
        using var pen = new Pen(Color.FromArgb(90, 60, 35), 1.2f);
        using var font = new Font("Georgia", 8f, FontStyle.Italic);
        using var brush = new SolidBrush(Color.FromArgb(90, 60, 35));

        foreach (var s in map.SpotElevations)
        {
            var x = (float)s.Position.X;
            var y = (float)s.Position.Y;
            const float size = 4f;

            g.DrawLine(pen, x - size, y, x + size, y);
            g.DrawLine(pen, x, y - size, x, y + size);

            var cat = PlaceCategories.Get(s.Category);
            var label = s.ShowAltitude
                ? $"{cat.Icon} {s.Name} ({s.Altitude:F0} m)"
                : $"{cat.Icon} {s.Name}";

            g.DrawString(label, font, brush, x + 6, y - 6);
        }
    }

    private static void DrawCommunes(Graphics g, VectorMapDefinition map)
    {
        var communes = map.AllCommunes;
        if (communes.Count == 0) return;

        using var communeFont = new Font("Georgia", 9.5f, FontStyle.Italic | FontStyle.Bold);
        using var inkBrush = new SolidBrush(Color.FromArgb(50, 32, 18));
        using var antiquePen = new Pen(Color.FromArgb(85, 60, 40), 1.2f);
        using var influencePen = new Pen(Color.FromArgb(50, 110, 80, 50), 1.0f) { DashStyle = DashStyle.Dash };
        using var fillBrush = new SolidBrush(Color.FromArgb(235, 222, 202));
        using var centerDot = new SolidBrush(Color.FromArgb(85, 60, 40));
        var sf = new StringFormat { Alignment = StringAlignment.Center };

        foreach (var c in communes)
        {
            var cx = (float)c.Position.X;
            var cy = (float)c.Position.Y;

            const float radius = 25f;
            g.DrawEllipse(influencePen, cx - radius, cy - radius, radius * 2, radius * 2);

            g.FillEllipse(fillBrush, cx - 4, cy - 4, 8, 8);
            g.DrawEllipse(antiquePen, cx - 4, cy - 4, 8, 8);
            g.FillEllipse(centerDot, cx - 1.5f, cy - 1.5f, 3, 3);
            g.DrawString(c.Name, communeFont, inkBrush, cx, cy + 7, sf);
        }
    }

    private static void DrawCompassRoseAndScale(Graphics g, float h)
    {
        var cx = 110f;
        var cy = h - 130f;
        const float r = 44f;

        using var penDark = new Pen(Color.FromArgb(70, 48, 28), 1.2f);
        using var brushDark = new SolidBrush(Color.FromArgb(70, 48, 28));
        using var brushLight = new SolidBrush(Color.FromArgb(240, 230, 210));

        g.DrawEllipse(penDark, cx - r, cy - r, r * 2, r * 2);
        g.DrawEllipse(penDark, cx - r * 0.5f, cy - r * 0.5f, r, r);

        DrawCompassRay(g, cx, cy, 0, -r * 1.15f, r * 0.22f, brushDark, brushLight, penDark);
        DrawCompassRay(g, cx, cy, r * 1.15f, 0, r * 0.22f, brushDark, brushLight, penDark);
        DrawCompassRay(g, cx, cy, 0, r * 1.15f, r * 0.22f, brushDark, brushLight, penDark);
        DrawCompassRay(g, cx, cy, -r * 1.15f, 0, r * 0.22f, brushDark, brushLight, penDark);

        using var fontLys = new Font("Georgia", 11f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(60, 40, 22));
        g.DrawString("N", fontLys, textBrush, cx - 6, cy - r * 1.5f);

        var barX = cx - 55f;
        var barY = cy + r + 26f;
        const float barW = 110f;
        const float barH = 5f;

        using var barFont = new Font("Georgia", 8f, FontStyle.Regular);
        var sf = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString("ÉCHELLE DE 10 KILOMÈTRES", barFont, textBrush, cx, barY - 14, sf);

        var segW = barW / 5.0f;
        for (var i = 0; i < 5; i++)
        {
            var b = (i % 2 == 0) ? brushDark : brushLight;
            g.FillRectangle(b, barX + i * segW, barY, segW, barH);
        }
        g.DrawRectangle(penDark, barX, barY, barW, barH);
        g.DrawString("0", barFont, textBrush, barX, barY + 6, sf);
        g.DrawString("2", barFont, textBrush, barX + segW, barY + 6, sf);
        g.DrawString("4", barFont, textBrush, barX + segW * 2, barY + 6, sf);
        g.DrawString("6", barFont, textBrush, barX + segW * 3, barY + 6, sf);
        g.DrawString("8", barFont, textBrush, barX + segW * 4, barY + 6, sf);
        g.DrawString("10", barFont, textBrush, barX + barW, barY + 6, sf);
    }

    private static void DrawCompassRay(Graphics g, float cx, float cy, float dx, float dy, float w, Brush bDark, Brush bLight, Pen pen)
    {
        var px = -dy / Math.Sqrt(dx * dx + dy * dy) * w;
        var py = dx / Math.Sqrt(dx * dx + dy * dy) * w;

        var tip = new PointF(cx + dx, cy + dy);
        var pLeft = new PointF(cx + (float)px, cy + (float)py);
        var pRight = new PointF(cx - (float)px, cy - (float)py);

        g.FillPolygon(bDark, [new PointF(cx, cy), tip, pLeft]);
        g.FillPolygon(bLight, [new PointF(cx, cy), tip, pRight]);
        g.DrawPolygon(pen, [new PointF(cx, cy), tip, pLeft]);
        g.DrawPolygon(pen, [new PointF(cx, cy), tip, pRight]);
    }

    private static void DrawVintageCartouche(Graphics g, float w)
    {
        var cartoucheW = 215f;
        var cartoucheH = 110f;
        var cx = w - cartoucheW - 35f;
        var cy = 30f;

        var rect = new RectangleF(cx, cy, cartoucheW, cartoucheH);

        using var fillBrush = new SolidBrush(Color.FromArgb(248, 243, 230));
        using var penFrame = new Pen(Color.FromArgb(80, 52, 30), 1.6f);
        using var penInner = new Pen(Color.FromArgb(120, 85, 55), 0.9f);

        g.FillRectangle(fillBrush, rect);
        g.DrawRectangle(penFrame, rect.X, rect.Y, rect.Width, rect.Height);
        g.DrawRectangle(penInner, rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);

        DrawScrollworkCorner(g, penFrame, rect.X, rect.Y, 12, 12);
        DrawScrollworkCorner(g, penFrame, rect.Right, rect.Y, -12, 12);
        DrawScrollworkCorner(g, penFrame, rect.X, rect.Bottom, 12, -12);
        DrawScrollworkCorner(g, penFrame, rect.Right, rect.Bottom, -12, -12);

        using var fontSmall = new Font("Georgia", 7.5f, FontStyle.Regular);
        using var fontTitle = new Font("Georgia", 9.5f, FontStyle.Bold);
        using var inkBrush = new SolidBrush(Color.FromArgb(50, 32, 18));
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        g.DrawString("CARTE GÉOGRAPHIQUE", fontSmall, inkBrush, new RectangleF(cx, cy + 10, cartoucheW, 14), sf);
        g.DrawString("DE LA", fontSmall, inkBrush, new RectangleF(cx, cy + 24, cartoucheW, 12), sf);
        g.DrawString("PREMIÈRE COMMUNE", fontTitle, inkBrush, new RectangleF(cx, cy + 38, cartoucheW, 16), sf);
        g.DrawString("ET SES ENVIRONS", fontSmall, inkBrush, new RectangleF(cx, cy + 54, cartoucheW, 12), sf);
        g.DrawString("DRESSÉE PAR L'EXPLORATEUR L. DUBOIS,", fontSmall, inkBrush, new RectangleF(cx, cy + 70, cartoucheW, 12), sf);
        g.DrawString("1874", fontTitle, inkBrush, new RectangleF(cx, cy + 84, cartoucheW, 14), sf);
    }

    private static void DrawScrollworkCorner(Graphics g, Pen pen, float x, float y, float dx, float dy)
    {
        g.DrawArc(pen, x - Math.Abs(dx), y - Math.Abs(dy), Math.Abs(dx) * 2, Math.Abs(dy) * 2, 0, 360);
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
