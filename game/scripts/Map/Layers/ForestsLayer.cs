using System.Linq;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les forêts (24 novembre 2026, remplace les arbres posés un par un) : une canopée peinte par la carte graphique
/// (shader <c>foret.gdshader</c>) dans les forêts de la carte. Les couronnes, les bosquets et les massifs apparaissent selon
/// le zoom ; le coût ne dépend que de l'écran, pas de la taille de la carte (un continent avait 6,5 millions d'arbres).
/// Vue Monde : la canopée pleine ; sous les filtres Politique et Production et transport, estompée ; avec le filtre
/// Biomes, rien (la couleur nette du biome suffit). De près, les arbres un par un, par tuiles (<see cref="ForestTrees"/>).
/// </summary>
public sealed class ForestsLayer : MapLayer
{
    private ShaderMaterial? _material;
    private ForestTrees? _trees;

    public override string Name => "Forêts";
    public override string Content => "La canopée des forêts";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters;

    public override void CreateNodes(Node2D node, MapDrawContext c)
    {
        _material = MapShaders.Material("foret");
        _material.SetShaderParameter("conifer_altitude", MapPalette.ConiferAltitude);
        _material.SetShaderParameter("has_relief", c.Data.Relief is not null);
        node.Material = _material;
        // Les arbres de près, par tuiles (étape 2), au-dessus de la canopée.
        _trees = new ForestTrees(node);
    }

    public override void UpdateNodes(MapDrawContext c)
    {
        if (_material is null)
            return;
        MapShaders.SetView(_material, c);
        var faded = c.Has(MapFilters.Politics | MapFilters.ProductionTransport);
        _material.SetShaderParameter("strength", faded ? 0.55f : 1f);
        _trees?.Update(c);
    }

    public override void Draw(MapDrawContext c)
    {
        if (c.Has(MapFilters.Biomes))
            return;

        foreach (var forest in (c.VectorMap?.AllBiomes ?? []).Where(b => b.Kind is "foret" or "forest"))
        {
            var outline = c.Scaled(forest.Boundary);
            if (outline.Points.Length < 3 || !c.IsOnScreen(outline, 4f))
                continue;
            if (forest.Holes is { Count: > 0 })
                foreach (var piece in c.Geometry.ForestPieceTriangles(forest))
                    c.FillTriangles(piece, Colors.White);
            else
                c.FillTriangles(c.Triangles(outline), Colors.White);
        }
    }
}
