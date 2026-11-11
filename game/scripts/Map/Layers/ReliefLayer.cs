using System;
using System.Collections.Generic;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Le relief : les courbes de niveau (Marching Squares), estompées dans la vue Monde, et l'estompage de l'ancienne
/// maquette. Filtre Géographie physique : courbes nettes, points cotés (croix) ; écritures : les cotes des courbes
/// maîtresses et le nom des points cotés.
/// </summary>
public sealed class ReliefLayer : MapLayer
{
    public override string Name => "Relief";
    public override string Content => "Les courbes de niveau, les points cotés et l'estompage";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters;

    public override void Draw(MapDrawContext c)
    {
        // Estompage topographique (réservé à l'ancienne maquette historique 1000x640 sans courbes réelles)
        var map = c.VectorMap;
        if (map is null || (map.Width <= 1100.0 && map.Height <= 750.0 && map.Contours.Count == 0))
            DrawHillshading(c);

        // Courbes de niveau IGN continues, avec le filtre Géographie physique (le terrain ombré montre le relief)
        if (c.Has(MapFilters.Physical))
        {
            foreach (var (contour, screenPoints, color) in VisibleContours(c))
            {
                var width = (contour.IsIndex ? 1.4f : 0.75f) * Math.Clamp(c.Zoom, 0.7f, 1.8f);
                c.Pen.DrawPolyline(screenPoints, color, width, antialiased: true);
            }
        }

        // Points cotés d'altitude (croix IGN), avec le filtre Géographie physique
        if (!c.Has(MapFilters.Physical) || map?.SpotElevations is not { Count: > 0 } spots)
            return;
        foreach (var spot in spots)
        {
            var pos = SpotPosition(c, spot);
            const float crossSize = 4f;
            c.Pen.DrawLine(pos + new Vector2(-crossSize, 0), pos + new Vector2(crossSize, 0), MapPalette.SpotElevation, 1.2f);
            c.Pen.DrawLine(pos + new Vector2(0, -crossSize), pos + new Vector2(0, crossSize), MapPalette.SpotElevation, 1.2f);
        }
    }

    public override void DrawLabels(MapDrawContext c)
    {
        if (!c.Has(MapFilters.Physical))
            return;

        // Cotes altimétriques sur les courbes maîtresses (100, 200, 300, 400, 500)
        foreach (var (contour, screenPoints, color) in VisibleContours(c))
        {
            if (contour.IsIndex && c.Zoom >= 0.85f && screenPoints.Length >= 4)
            {
                var mid = screenPoints[screenPoints.Length / 2];
                var label = $"{contour.Altitude:F0}";
                c.Pen.DrawString(c.Font, mid + new Vector2(0, -2), label, HorizontalAlignment.Left, -1, 10, color);
            }
        }

        // Nom (et altitude) des points cotés
        if (c.VectorMap?.SpotElevations is not { Count: > 0 } spots)
            return;
        foreach (var spot in spots)
        {
            var pos = SpotPosition(c, spot);
            var cat = PlaceCategories.Get(spot.Category);
            var label = spot.ShowAltitude
                ? $"{cat.Icon} {spot.Name} ({spot.Altitude:F0} m)"
                : $"{cat.Icon} {spot.Name}";
            c.Pen.DrawString(c.Font, pos + new Vector2(6, 4), label, HorizontalAlignment.Left, -1, 11, MapPalette.SpotElevation);
        }
    }

    private static Vector2 SpotPosition(MapDrawContext c, SpotElevation spot) =>
        c.ToScreen(new Vector2((float)spot.Position.X, (float)spot.Position.Y));

    /// <summary>Les courbes à dessiner (rien hors de l'écran, étiquette comprise), leurs points à l'écran et leur couleur.</summary>
    private static IEnumerable<(ContourLine Contour, Vector2[] ScreenPoints, Color Color)> VisibleContours(MapDrawContext c)
    {
        if (c.VectorMap?.Contours is not { Count: > 0 } contours)
            yield break;

        var physical = c.Has(MapFilters.Physical);
        // Sous le filtre Politique (sans le relief), seules les courbes maîtresses restent.
        var masterOnly = !physical && c.Has(MapFilters.Politics);

        foreach (var contour in contours)
        {
            if (contour.Points.Count < 2)
                continue;

            if (masterOnly && !contour.IsIndex)
                continue;

            var shape = c.Scaled(contour.Points);
            if (!c.IsOnScreen(shape, 120f))
                continue;

            var color = contour.IsIndex ? MapPalette.ContourIndex : MapPalette.ContourStandard;
            // Vue Monde : courbes estompées ; filtre Géographie physique : courbes nettes.
            if (!physical)
                color = new Color(color.R, color.G, color.B, color.A * 0.45f);

            yield return (contour, c.ToScreen(shape), color);
        }
    }

    private static void DrawHillshading(MapDrawContext c)
    {
        // Masse d'ombrage continue au sud-est des crêtes nord-est
        var p1 = c.ToScreen(new Vector2(580, 120));
        var p2 = c.ToScreen(new Vector2(720, 70));
        var p3 = c.ToScreen(new Vector2(860, 100));
        var p4 = c.ToScreen(new Vector2(940, 160));
        var p5 = c.ToScreen(new Vector2(900, 240));
        var p6 = c.ToScreen(new Vector2(700, 200));

        c.Pen.DrawColoredPolygon([p1, p2, p3, p4, p5, p6], MapPalette.Hillshade);
    }
}
