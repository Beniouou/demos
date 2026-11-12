using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les biomes naturels (forêts, terres fertiles, marais, plages, falaises, déserts, glaciers) et les arbres des forêts.
/// Vue Monde : une teinte légère posée sur le terrain coloré, estompée sous les filtres Politique et Production et
/// transport. Filtre Biomes : une couleur nette par type de biome (<see cref="MapPalette.BiomeCrisp"/>).
/// Les arbres (feuillus, et conifères en altitude) sont posés une fois pour la partie, en unités de carte, et dessinés d'un coup
/// par la carte graphique avec leur ombre (<see cref="MapSprites"/>) : un déplacement ou un zoom ne les recalcule pas.
/// </summary>
public sealed class BiomesLayer : MapLayer
{
    private readonly List<MultiMeshInstance2D> _shadows = [];
    private readonly List<MultiMeshInstance2D> _trees = [];

    public override string Name => "Biomes";
    public override string Content => "Les biomes naturels, et les arbres des forêts avec leur ombre";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters;

    public override void CreateNodes(Node2D node, MapDrawContext c)
    {
        var (leafy, conifers) = PlaceTrees(c);
        foreach (var (image, items) in new[] { ("arbre-feuillu", leafy), ("arbre-conifere", conifers) })
        {
            if (items.Count == 0 || MapSprites.Texture(image) is not { } texture)
                continue;
            var trees = MapSprites.Instances(texture, items);
            _trees.Add(trees);
            _shadows.Add(MapSprites.Shadow(trees, MapPalette.TreeShadowStrength));
        }
        // Toutes les ombres sous tous les arbres.
        foreach (var shadow in _shadows)
            node.AddChild(shadow);
        foreach (var trees in _trees)
            node.AddChild(trees);
    }

    public override void UpdateNodes(MapDrawContext c)
    {
        var faded = !c.Has(MapFilters.Biomes) && c.Has(MapFilters.Politics | MapFilters.ProductionTransport);
        foreach (var shadow in _shadows)
        {
            MapSprites.PlaceOnMap(shadow, c.Camera, MapPalette.TreeShadowOffset);
            shadow.Visible = !faded;
        }
        foreach (var trees in _trees)
        {
            MapSprites.PlaceOnMap(trees, c.Camera);
            trees.Modulate = faded ? MapPalette.TreesFaded : Colors.White;
        }
    }

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

    /// <summary>
    /// La place des arbres, une fois pour la partie : une grille d'un point tous les <see cref="MapPalette.TreeSpacing"/>
    /// unités, gardé s'il est dans une forêt et hors de ses clairières, puis un peu déplacé au hasard (toujours le même) ;
    /// un conifère au-dessus de <see cref="MapPalette.ConiferAltitude"/> m, un feuillu dessous. Pour aller vite, chaque
    /// ligne de la grille est balayée une fois : on calcule où elle coupe le bord de la forêt et de ses clairières (règle
    /// pair-impair), et les points entre deux coupures sont dans la forêt.
    /// </summary>
    private static (List<(Vector2, float)> Leafy, List<(Vector2, float)> Conifers) PlaceTrees(MapDrawContext c)
    {
        var leafy = new List<(Vector2, float)>();
        var conifers = new List<(Vector2, float)>();
        var forests = (c.VectorMap?.AllBiomes ?? []).Where(b => b.Kind is "foret" or "forest" && b.Boundary.Count >= 3);
        var random = new Random(1900);
        var step = MapPalette.TreeSpacing;
        var crossings = new List<float>();
        foreach (var forest in forests)
        {
            var rings = new List<Vector2[]> { c.Geometry.World(forest.Boundary) };
            rings.AddRange((forest.Holes ?? []).Where(h => h.Count >= 3).Select(c.Geometry.World));
            var box = MapGeometry.BoundsOf(rings[0]);
            for (var y = box.Position.Y + step * 0.5f; y < box.End.Y; y += step)
            {
                crossings.Clear();
                foreach (var ring in rings)
                    for (var i = 0; i < ring.Length; i++)
                    {
                        var p = ring[i];
                        var q = ring[(i + 1) % ring.Length];
                        if ((p.Y <= y && q.Y > y) || (q.Y <= y && p.Y > y))
                            crossings.Add(p.X + (y - p.Y) / (q.Y - p.Y) * (q.X - p.X));
                    }
                crossings.Sort();
                for (var k = 0; k + 1 < crossings.Count; k += 2)
                {
                    // Les points de la grille (alignés sur la boîte de la forêt) entre deux coupures.
                    var first = box.Position.X + step * (MathF.Ceiling((crossings[k] - box.Position.X) / step - 0.5f) + 0.5f);
                    for (var x = first; x <= crossings[k + 1]; x += step)
                    {
                        var point = new Vector2(x + (random.NextSingle() - 0.5f) * step * 0.7f, y + (random.NextSingle() - 0.5f) * step * 0.7f);
                        var size = MapPalette.TreeSize * (0.8f + random.NextSingle() * 0.4f);
                        var conifer = c.Data.Relief is { } relief && relief.AltitudeAt(point) > MapPalette.ConiferAltitude;
                        (conifer ? conifers : leafy).Add((point, conifer ? size * 0.85f : size));
                    }
                }
            }
        }
        return (leafy, conifers);
    }
}
