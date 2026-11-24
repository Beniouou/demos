using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les arbres de près, par tuiles (24 novembre 2026, étape 2 après la canopée) : de loin, la canopée de
/// <see cref="ForestsLayer"/> suffit ; quand on zoome assez, les arbres (feuillus, conifères en altitude, avec leur ombre)
/// apparaissent en fondu, seulement dans les tuiles de 20 × 20 km visibles. Une tuile est préparée la première fois qu'on la
/// voit et gardée tant qu'on n'en a pas vu trop d'autres. Chaque arbre est tiré de sa place sur une grille commune à toute la
/// carte : on retrouve les mêmes arbres en revenant, et les tuiles se raccordent sans couture.
/// </summary>
public sealed class ForestTrees
{
    /// <summary>Côté d'une tuile, en unités de carte (200 = 20 km).</summary>
    private const float TileSize = 200f;

    /// <summary>Les arbres apparaissent à partir de ce zoom (pixels par unité) et sont pleins à partir du suivant.</summary>
    private const float ShowZoom = 2.0f;
    private const float FullZoom = 3.0f;

    /// <summary>Tuiles gardées au plus ; au-delà, les moins récemment vues sont libérées.</summary>
    private const int MaxTiles = 48;

    private sealed record Tile(List<MultiMeshInstance2D> Trees, List<MultiMeshInstance2D> Shadows)
    {
        public long LastSeen { get; set; }
    }

    private sealed record Forest(Vector2[][] Rings, Rect2 Box);

    private readonly Dictionary<(int X, int Y), Tile> _tiles = [];
    private readonly Node2D _shadowRoot = new() { Name = "OmbresDesArbres" };
    private readonly Node2D _treeRoot = new() { Name = "Arbres" };
    private List<Forest>? _forests;
    private long _updates;

    /// <summary>Le nombre d'arbres posés dans les tuiles gardées (pour les mesures).</summary>
    public int TreeCount => _tiles.Values.Sum(t => t.Trees.Sum(n => n.Multimesh.InstanceCount));

    public ForestTrees(Node2D host)
    {
        // Toutes les ombres sous tous les arbres.
        host.AddChild(_shadowRoot);
        host.AddChild(_treeRoot);
    }

    public void Update(MapDrawContext c)
    {
        _updates++;
        var zoom = c.Zoom;
        if (zoom < ShowZoom || c.VectorMap is null)
        {
            _shadowRoot.Visible = _treeRoot.Visible = false;
            return;
        }

        var faded = !c.Has(MapFilters.Biomes) && c.Has(MapFilters.Politics | MapFilters.ProductionTransport);
        var appear = Mathf.SmoothStep(ShowZoom, FullZoom, zoom);
        _treeRoot.Visible = true;
        _treeRoot.Modulate = new Color(1, 1, 1, appear * (faded ? MapPalette.TreesFaded.A : 1f));
        _shadowRoot.Visible = !faded;
        _shadowRoot.Modulate = new Color(1, 1, 1, appear);
        MapSprites.PlaceOnMap(_treeRoot, c.Camera);
        MapSprites.PlaceOnMap(_shadowRoot, c.Camera, MapPalette.TreeShadowOffset);

        // Les tuiles à l'écran (avec un arbre de marge) : préparées si besoin, montrées ; les autres, cachées.
        var topLeft = c.Camera.ToWorld(Vector2.Zero) - new Vector2(MapPalette.TreeSize, MapPalette.TreeSize);
        var bottomRight = c.Camera.ToWorld(c.Size) + new Vector2(MapPalette.TreeSize, MapPalette.TreeSize);
        var (x0, y0) = ((int)MathF.Floor(topLeft.X / TileSize), (int)MathF.Floor(topLeft.Y / TileSize));
        var (x1, y1) = ((int)MathF.Floor(bottomRight.X / TileSize), (int)MathF.Floor(bottomRight.Y / TileSize));
        foreach (var tile in _tiles.Values)
            SetVisible(tile, false);
        for (var ty = y0; ty <= y1; ty++)
            for (var tx = x0; tx <= x1; tx++)
            {
                if (!_tiles.TryGetValue((tx, ty), out var tile))
                    _tiles[(tx, ty)] = tile = Build(c, tx, ty);
                tile.LastSeen = _updates;
                SetVisible(tile, true);
            }

        // Trop de tuiles gardées : les moins récemment vues sont libérées.
        if (_tiles.Count > MaxTiles)
            foreach (var old in _tiles.OrderBy(t => t.Value.LastSeen).Take(_tiles.Count - MaxTiles).ToList())
            {
                foreach (var node in old.Value.Trees.Concat(old.Value.Shadows))
                    node.QueueFree();
                _tiles.Remove(old.Key);
            }
    }

    private static void SetVisible(Tile tile, bool visible)
    {
        foreach (var node in tile.Trees)
            node.Visible = visible;
        foreach (var node in tile.Shadows)
            node.Visible = visible;
    }

    /// <summary>Les arbres d'une tuile : un feuillu et un conifère au plus par nœud, leur ombre à part.</summary>
    private Tile Build(MapDrawContext c, int tx, int ty)
    {
        var (leafy, conifers) = Place(c, tx, ty);
        var tile = new Tile([], []);
        foreach (var (image, items) in new[] { ("arbre-feuillu", leafy), ("arbre-conifere", conifers) })
        {
            if (items.Count == 0 || MapSprites.Texture(image) is not { } texture)
                continue;
            var trees = MapSprites.Instances(texture, items);
            var shadow = MapSprites.Shadow(trees, MapPalette.TreeShadowStrength);
            tile.Trees.Add(trees);
            tile.Shadows.Add(shadow);
            _treeRoot.AddChild(trees);
            _shadowRoot.AddChild(shadow);
        }
        return tile;
    }

    /// <summary>
    /// La place des arbres d'une tuile : les points d'une grille d'un point tous les <see cref="MapPalette.TreeSpacing"/>
    /// unités (commune à toute la carte), gardés s'ils sont dans une forêt et hors de ses clairières, puis un peu déplacés ;
    /// un conifère au-dessus de <see cref="MapPalette.ConiferAltitude"/> m. Chaque ligne de la grille est balayée une fois :
    /// on calcule où elle coupe le bord de la forêt et de ses clairières (règle pair-impair).
    /// </summary>
    private (List<(Vector2, float)> Leafy, List<(Vector2, float)> Conifers) Place(MapDrawContext c, int tx, int ty)
    {
        var leafy = new List<(Vector2, float)>();
        var conifers = new List<(Vector2, float)>();
        var step = MapPalette.TreeSpacing;
        var tile = new Rect2(tx * TileSize, ty * TileSize, TileSize, TileSize);
        var crossings = new List<float>();
        foreach (var forest in Forests(c).Where(f => f.Box.Intersects(tile)))
        {
            var rowStart = (int)MathF.Ceiling(tile.Position.Y / step - 0.5f);
            var rowEnd = (int)MathF.Floor(tile.End.Y / step - 0.5f);
            for (var row = rowStart; row <= rowEnd; row++)
            {
                var y = (row + 0.5f) * step;
                if (y < forest.Box.Position.Y || y >= forest.Box.End.Y)
                    continue;
                crossings.Clear();
                foreach (var ring in forest.Rings)
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
                    var from = Math.Max(crossings[k], tile.Position.X);
                    var to = Math.Min(crossings[k + 1], tile.End.X);
                    for (var col = (int)MathF.Ceiling(from / step - 0.5f); (col + 0.5f) * step <= to; col++)
                    {
                        if ((col + 0.5f) * step >= tile.End.X)
                            break;
                        var point = new Vector2(
                            (col + 0.5f + (Noise(col, row, 1) - 0.5f) * 0.7f) * step,
                            (row + 0.5f + (Noise(col, row, 2) - 0.5f) * 0.7f) * step);
                        var size = MapPalette.TreeSize * (0.8f + Noise(col, row, 3) * 0.4f);
                        var conifer = c.Data.Relief is { } relief && relief.AltitudeAt(point) > MapPalette.ConiferAltitude;
                        (conifer ? conifers : leafy).Add((point, conifer ? size * 0.85f : size));
                    }
                }
            }
        }
        return (leafy, conifers);
    }

    /// <summary>Les forêts de la carte, avec leurs contours et leurs clairières en unités de carte, une fois pour la partie.</summary>
    private List<Forest> Forests(MapDrawContext c) => _forests ??=
        [.. (c.VectorMap?.AllBiomes ?? [])
            .Where(b => b.Kind is "foret" or "forest" && b.Boundary.Count >= 3)
            .Select(b =>
            {
                Vector2[][] rings = [c.Geometry.World(b.Boundary), .. (b.Holes ?? []).Where(h => h.Count >= 3).Select(c.Geometry.World)];
                return new Forest(rings, MapGeometry.BoundsOf(rings[0]));
            })];

    /// <summary>Un nombre entre 0 et 1 tiré de la place d'un point de la grille (toujours le même pour ce point).</summary>
    private static float Noise(int col, int row, int salt)
    {
        unchecked
        {
            var h = (uint)(col * 374761393 + row * 668265263 + salt * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
