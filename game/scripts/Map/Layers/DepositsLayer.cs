using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les ressources minières et les sols (filtre Ressources minières et sols) : les 66 gisements (pierre, charbon, fer,
/// terres fertiles), ou les sites d'extraction ponctuels des anciennes cartes. Écritures : le nom de chaque gisement.
/// </summary>
public sealed class DepositsLayer : MapLayer
{
    public override string Name => "Gisements";
    public override string Content => "Les gisements de pierre, de charbon et de fer, et les terres fertiles";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters;

    public override void Draw(MapDrawContext c)
    {
        if (c.VectorMap == null || !c.Has(MapFilters.Deposits))
            return;

        if (c.VectorMap.AllResourceZones.Count > 0)
        {
            foreach (var (_, shape, screenPoints, fill, stroke, _) in VisibleZones(c))
            {
                c.FillTriangles(c.Triangles(shape), fill);
                c.Pen.DrawPolyline(screenPoints, stroke, 2.0f * c.SymbolScale, antialiased: true);
            }
            return;
        }

        if (c.VectorMap.Resources is not { Count: > 0 } res)
            return;

        foreach (var r in res)
        {
            var (_, roofColor) = Site(r);
            c.DrawBuildingFootprint(SitePosition(c, r), new Vector2(24, 16) * c.Zoom, roofColor);
        }
    }

    public override void DrawLabels(MapDrawContext c)
    {
        if (c.VectorMap == null || !c.Has(MapFilters.Deposits))
            return;
        var s = c.SymbolScale;

        if (c.VectorMap.AllResourceZones.Count > 0)
        {
            foreach (var (zone, _, screenPoints, _, stroke, icon) in VisibleZones(c))
            {
                var center = screenPoints.Aggregate(Vector2.Zero, (acc, p) => acc + p) / screenPoints.Length;
                var label = $"{icon} {zone.Name}";
                var fontSize = (int)Math.Clamp(10 * s, 9, 13);
                c.Pen.DrawString(c.Font, center, label, HorizontalAlignment.Center, -1, fontSize, stroke);
            }
            return;
        }

        if (c.VectorMap.Resources is not { Count: > 0 } res)
            return;

        foreach (var r in res)
        {
            var (name, _) = Site(r);
            var label = $"{name} ({r.Resource})";
            c.Pen.DrawString(c.Font, SitePosition(c, r) + new Vector2(-30, 22 * s), label, HorizontalAlignment.Center, -1, 10, MapPalette.InkText);
        }
    }

    /// <summary>Les gisements à dessiner (rien hors de l'écran, nom compris), leurs points et leurs couleurs.</summary>
    private static IEnumerable<(PolygonArea Zone, MapGeometry.ScaledShape Shape, Vector2[] ScreenPoints, Color Fill, Color Stroke, string Icon)> VisibleZones(MapDrawContext c)
    {
        foreach (var zone in c.VectorMap!.AllResourceZones)
        {
            if (zone.Boundary.Count < 3) continue;
            var shape = c.Scaled(zone.Boundary);
            if (!c.IsOnScreen(shape, 300f))
                continue;

            var (fill, stroke, icon) = MapPalette.Deposit(zone.Kind);
            yield return (zone, shape, c.ToScreen(shape), fill, stroke, icon);
        }
    }

    private static Vector2 SitePosition(MapDrawContext c, ResourceDeposit r) =>
        c.ToScreen(new Vector2((float)r.Position.X, (float)r.Position.Y));

    private static (string Name, Color RoofColor) Site(ResourceDeposit r) => r.Resource.ToLowerInvariant() switch
    {
        "pierre" => ("Carrière de pierre", MapPalette.RoofSlate),
        "charbon" => ("Mine de charbon", new Color("#333333")),
        "fer" => ("Gisement de fer", new Color("#8B3A2A")),
        "fertile" or "ble" or "lin" => ("Zone fertile", MapPalette.RoofTile),
        _ => ("Site d'extraction", MapPalette.RoofTile),
    };
}
