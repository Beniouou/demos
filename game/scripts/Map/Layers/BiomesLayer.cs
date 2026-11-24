using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les biomes naturels (forêts, terres fertiles, marais, plages, falaises, déserts, glaciers).
/// Vue Monde : une teinte légère posée sur le terrain coloré, estompée sous les filtres Politique et Production et
/// transport. Filtre Biomes : une couleur nette par type de biome (<see cref="MapPalette.BiomeCrisp"/>).
/// La canopée des forêts est dessinée par <see cref="ForestsLayer"/> (24 novembre 2026 : plus d'arbres posés un par un).
/// </summary>
public sealed class BiomesLayer : MapLayer
{
    public override string Name => "Biomes";
    public override string Content => "Les biomes naturels";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters;

    public override void Draw(MapDrawContext c)
    {
        var biomes = c.VectorMap?.AllBiomes ?? c.VectorMap?.Forests;
        if (biomes is not { Count: > 0 })
            return;

        var crisp = c.Has(MapFilters.Biomes);
        // Sous les informations politiques ou de transport, les biomes s'effacent un peu.
        var faded = !crisp && c.Has(MapFilters.Politics | MapFilters.ProductionTransport);

        foreach (var biome in biomes)
        {
            // Points, morceaux et triangles gardés d'un dessin à l'autre (MapGeometry) ; rien hors de l'écran.
            var outline = c.Scaled(biome.Boundary);
            if (outline.Points.Length < 3 || !c.IsOnScreen(outline, 4f))
                continue;

            var screenPoints = c.ToScreen(outline);
            var (fill, stroke) = crisp ? MapPalette.BiomeCrisp(biome.Kind) : MapPalette.BiomeSoft(biome.Kind);
            // Vue Monde : une teinte légère sur le terrain coloré, sans contour.
            if (!crisp)
            {
                fill = new Color(fill, fill.A * MapPalette.BiomeTint);
                stroke = Colors.Transparent;
            }
            if (faded)
                fill = new Color(fill, fill.A * 0.55f);

            if (biome.Holes is { Count: > 0 } holes)
            {
                // Le contour moins chaque trou, découpé et triangulé une fois.
                foreach (var piece in c.Geometry.ForestPieceTriangles(biome))
                    c.FillTriangles(piece, fill);
                if (stroke.A > 0)
                {
                    c.Pen.DrawPolyline(screenPoints, stroke, 1.2f, antialiased: true);
                    foreach (var hole in holes.Where(h => h.Count >= 3))
                        c.Pen.DrawPolyline(c.ToScreen(c.Scaled(hole)), stroke, 1.2f, antialiased: true);
                }
            }
            else
            {
                c.FillTriangles(c.Triangles(outline), fill);
                if (stroke.A > 0)
                    c.Pen.DrawPolyline(screenPoints, stroke, 1.2f, antialiased: true);
            }
        }
    }
}
