using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les lacs (décidé le 24 novembre 2026) : l'eau à plat, au-dessus du relief et sous les fleuves, avec la rive ; les îles
/// (trous du contour) restent à terre. Écritures (filtre Géographie physique) : leur nom, de près.
/// </summary>
public sealed class LakesLayer : MapLayer
{
    // Le même polygone d'un dessin à l'autre, pour que MapGeometry garde ses morceaux triangulés.
    private readonly Dictionary<LakeData, PolygonArea> _areas = [];

    public override string Name => "Lacs";
    public override string Content => "Les lacs";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters;

    public override void Draw(MapDrawContext c)
    {
        foreach (var lake in c.VectorMap?.AllLakes ?? [])
        {
            var outline = c.Scaled(lake.Boundary);
            if (outline.Points.Length < 3 || !c.IsOnScreen(outline, 4f))
                continue;

            if (lake.Holes is { Count: > 0 } holes)
            {
                foreach (var piece in c.Geometry.ForestPieceTriangles(AreaOf(lake)))
                    c.FillTriangles(piece, MapPalette.Water);
                foreach (var hole in holes.Where(h => h.Count >= 3))
                    c.Pen.DrawPolyline(Closed(c.ToScreen(c.Scaled(hole))), MapPalette.WaterShallow, 2.0f, antialiased: true);
            }
            else
            {
                c.FillTriangles(c.Triangles(outline), MapPalette.Water);
            }
            c.Pen.DrawPolyline(Closed(c.ToScreen(outline)), MapPalette.WaterShallow, 2.0f, antialiased: true);
        }
    }

    public override void DrawLabels(MapDrawContext c)
    {
        if (!c.Has(MapFilters.Physical) || c.Zoom < 0.9f)
            return;

        foreach (var lake in c.VectorMap?.AllLakes ?? [])
        {
            var outline = c.Scaled(lake.Boundary);
            if (outline.Points.Length < 3 || string.IsNullOrWhiteSpace(lake.Name) || !c.IsOnScreen(outline, 4f))
                continue;
            var points = c.ToScreen(outline);
            var centre = points.Aggregate(Vector2.Zero, (sum, p) => sum + p) / points.Length;
            c.Pen.DrawString(c.Font, centre + new Vector2(-40, 4), lake.Name, HorizontalAlignment.Center, 80, 11, MapPalette.River);
        }
    }

    private PolygonArea AreaOf(LakeData lake)
    {
        if (!_areas.TryGetValue(lake, out var area))
            _areas[lake] = area = new PolygonArea(lake.Name, "lac", lake.Boundary, lake.Holes);
        return area;
    }

    private static Vector2[] Closed(Vector2[] points) => points.Length > 0 ? [.. points, points[0]] : points;
}
