using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// Les calculs du dessin de la carte gardés d'un dessin à l'autre, pour la vitesse :
/// - chaque tracé de la carte (courbe, fleuve, forêt, mer, gisement) mis à l'échelle du zoom une fois par zoom ; un point
///   à l'écran reste « déplacement + point × zoom », le même calcul qu'avant ;
/// - ses triangles (le remplissage), les morceaux des forêts à trous et la place des bosquets, calculés une seule fois
///   pour toute la partie, en coordonnées de la carte ; chaque dessin les met à l'échelle du zoom ;
/// - un tracé entièrement hors de l'écran n'est pas dessiné du tout.
/// Les points mis à l'échelle se recalculent quand le zoom change ; un déplacement ne recalcule rien.
/// </summary>
public partial class MapCanvas
{
    private const float MinZoom = 0.4f;
    private const float MaxZoom = 3.5f;

    /// <summary>Un tracé au zoom courant : ses points (monde × zoom), sa boîte englobante, et le tracé d'origine.</summary>
    private sealed class ScaledShape(IReadOnlyList<Point2D> source, Vector2[] points, Rect2 bounds)
    {
        public IReadOnlyList<Point2D> Source { get; } = source;
        public Vector2[] Points { get; } = points;
        public Rect2 Bounds { get; } = bounds;
    }

    private readonly Dictionary<object, ScaledShape> _scaledShapes = new(ReferenceEqualityComparer.Instance);
    private float _cachedZoom = float.NaN;

    // Gardés pour toute la partie (en coordonnées de la carte, sans zoom ni déplacement).
    private readonly Dictionary<object, Vector2[]> _worldPoints = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, List<Vector2[]>> _worldTriangles = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, List<List<Vector2[]>>> _worldForestPieces = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, TreeGrid> _treeGrids = new(ReferenceEqualityComparer.Instance);

    /// <summary>Les points d'un tracé de la carte au zoom courant (calculés une fois par zoom).</summary>
    private ScaledShape Scaled(IReadOnlyList<Point2D> source)
    {
        ForgetOtherZoom();
        if (_scaledShapes.TryGetValue(source, out var shape))
            return shape;

        var points = new Vector2[source.Count];
        for (var i = 0; i < points.Length; i++)
            points[i] = new Vector2((float)source[i].X, (float)source[i].Y) * _zoom;
        shape = new ScaledShape(source, points, BoundsOf(points));
        _scaledShapes[source] = shape;
        return shape;
    }

    private void ForgetOtherZoom()
    {
        if (_cachedZoom == _zoom)
            return;
        _scaledShapes.Clear();
        _cachedZoom = _zoom;
    }

    /// <summary>Les points d'un tracé en coordonnées de la carte (calculés une fois).</summary>
    private Vector2[] World(IReadOnlyList<Point2D> source)
    {
        if (_worldPoints.TryGetValue(source, out var points))
            return points;
        points = new Vector2[source.Count];
        for (var i = 0; i < points.Length; i++)
            points[i] = new Vector2((float)source[i].X, (float)source[i].Y);
        return _worldPoints[source] = points;
    }

    private static Rect2 BoundsOf(Vector2[] points)
    {
        if (points.Length == 0)
            return new Rect2();
        var min = points[0];
        var max = points[0];
        foreach (var p in points)
        {
            min = new Vector2(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y));
            max = new Vector2(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y));
        }
        return new Rect2(min, max - min);
    }

    /// <summary>Vrai si le tracé, agrandi de <paramref name="margin"/> pixels (épaisseur, étiquettes), touche l'écran.</summary>
    private bool IsOnScreen(ScaledShape shape, float margin) =>
        new Rect2(shape.Bounds.Position + _panOffset, shape.Bounds.Size).Grow(margin).Intersects(new Rect2(Vector2.Zero, Size));

    private bool IsOnScreen(Vector2 point, float margin) =>
        new Rect2(Vector2.Zero, Size).Grow(margin).HasPoint(point);

    /// <summary>Les points d'un tracé à l'écran : déplacement + point × zoom, comme <see cref="ToScreen(Vector2)"/>.</summary>
    private Vector2[] ToScreen(ScaledShape shape) => Translated(shape.Points);

    private Vector2[] Translated(Vector2[] scaled)
    {
        var screen = new Vector2[scaled.Length];
        for (var i = 0; i < screen.Length; i++)
            screen[i] = _panOffset + scaled[i];
        return screen;
    }

    // La triangulation et le découpage des trous se font une fois, sur la carte agrandie au zoom le plus fort : leurs
    // seuils (sommets trop proches, triangles plats) sont en pixels, et ce sont ceux du zoom où ils se voient le plus.
    // Le résultat est gardé en coordonnées de la carte.

    private static Vector2[] AtMaxZoom(Vector2[] world) => world.Select(p => p * MaxZoom).ToArray();

    private static List<Vector2[]> FromMaxZoom(List<Vector2[]> polygons)
    {
        foreach (var polygon in polygons)
            for (var i = 0; i < polygon.Length; i++)
                polygon[i] /= MaxZoom;
        return polygons;
    }

    /// <summary>Les triangles du remplissage d'un tracé, en coordonnées de la carte (calculés une fois).</summary>
    private List<Vector2[]> Triangles(ScaledShape shape)
    {
        if (!_worldTriangles.TryGetValue(shape.Source, out var triangles))
            _worldTriangles[shape.Source] = triangles = FromMaxZoom(SafeTriangulate(AtMaxZoom(World(shape.Source))));
        return triangles;
    }

    /// <summary>
    /// Dessine des triangles gardés en coordonnées de la carte, à leur place à l'écran (déplacement + point × zoom), en
    /// un seul appel à Godot.
    /// </summary>
    private void FillTriangles(List<Vector2[]> triangles, Color color)
    {
        var count = 0;
        foreach (var triangle in triangles)
            count += triangle.Length - 2;
        if (count == 0)
            return;

        var points = new Vector2[count * 3];
        var colors = new Color[points.Length];
        var indices = new int[points.Length];
        var next = 0;
        foreach (var triangle in triangles)
        {
            // Un triangle a trois sommets ; par prudence, un polygone convexe plus long est coupé en éventail.
            for (var k = 1; k + 1 < triangle.Length; k++)
            {
                points[next] = _panOffset + triangle[0] * _zoom;
                points[next + 1] = _panOffset + triangle[k] * _zoom;
                points[next + 2] = _panOffset + triangle[k + 1] * _zoom;
                next += 3;
            }
        }
        for (var i = 0; i < points.Length; i++)
        {
            colors[i] = color;
            indices[i] = i;
        }
        RenderingServer.CanvasItemAddTriangleArray(_pen.GetCanvasItem(), indices, points, colors);
    }

    /// <summary>
    /// Les morceaux d'une forêt à trous (le contour moins chaque trou), triangulés, en coordonnées de la carte
    /// (calculés une fois).
    /// </summary>
    private List<List<Vector2[]>> ForestPieceTriangles(PolygonArea biome)
    {
        if (_worldForestPieces.TryGetValue(biome, out var cached))
            return cached;

        var pieces = new List<Vector2[]> { AtMaxZoom(World(biome.Boundary)) };
        foreach (var hole in biome.Holes!)
        {
            if (hole.Count < 3) continue;
            var holePoints = AtMaxZoom(World(hole));
            var nextPieces = new List<Vector2[]>();
            foreach (var piece in pieces)
            {
                var clipped = Geometry2D.ClipPolygons(piece, holePoints);
                if (clipped != null && clipped.Count > 0)
                {
                    foreach (var cp in clipped) nextPieces.Add(cp);
                }
                else
                {
                    nextPieces.Add(piece);
                }
            }
            pieces = nextPieces;
        }

        return _worldForestPieces[biome] = pieces.Where(piece => piece.Length >= 3).Select(piece => FromMaxZoom(SafeTriangulate(piece))).ToList();
    }

    /// <summary>
    /// Les bosquets d'une forêt, en coordonnées de la carte : une grille d'un point tous les 32 pixels × zoom (au moins 14
    /// pixels), soit tous les 32 lieues sauf aux plus petits zooms, dans la boîte englobante ; chaque point gardé s'il est
    /// dans la forêt et hors de ses trous. Seuls les points visibles sont testés, une fois, quand il le faut.
    /// </summary>
    private sealed class TreeGrid
    {
        public required float Step { get; init; }                // le pas de la grille, en lieues
        public required Vector2[] Outline { get; init; }         // la forêt, en coordonnées de la carte
        public required List<(Vector2[] Points, Rect2 Box)> Holes { get; init; }
        public required Vector2 Origin { get; init; }            // le premier point de la grille
        public required int Rows { get; init; }
        public required int Columns { get; init; }
        public required sbyte[,] Inside { get; init; }           // 0 pas encore testé, 1 un bosquet, -1 rien
        public bool AllTested { get; set; }
        public Vector2? Center { get; set; }                     // le bosquet du centre, s'il n'y en a aucun ailleurs

        public Vector2 Point(int row, int column) => Origin + new Vector2(column, row) * Step;
    }

    private TreeGrid ForestTreeGrid(PolygonArea biome)
    {
        var step = Math.Max(32f * _zoom, 14f) / _zoom;
        if (_treeGrids.TryGetValue(biome, out var cached) && cached.Step == step)
            return cached;

        var outline = World(biome.Boundary);
        var bounds = BoundsOf(outline);
        var columns = (int)Math.Floor((bounds.Size.X - step * 0.5f) / step) + 1;
        var rows = (int)Math.Floor((bounds.Size.Y - step * 0.5f) / step) + 1;
        columns = Math.Max(0, columns);
        rows = Math.Max(0, rows);

        return _treeGrids[biome] = new TreeGrid
        {
            Step = step,
            Outline = outline,
            // Les trous et leur boîte : un point hors de la boîte n'est pas dans le trou (sans appeler Godot).
            Holes = (biome.Holes ?? []).Select(World).Select(h => (h, BoundsOf(h).Grow(1f))).ToList(),
            Origin = bounds.Position + new Vector2(step, step) * 0.5f,
            Rows = rows,
            Columns = columns,
            Inside = new sbyte[rows, columns],
        };
    }

    private static bool InForest(TreeGrid grid, Vector2 point) =>
        Geometry2D.IsPointInPolygon(point, grid.Outline)
        && !grid.Holes.Any(h => h.Box.HasPoint(point) && Geometry2D.IsPointInPolygon(point, h.Points));

    private static bool HasTree(TreeGrid grid, int row, int column)
    {
        if (grid.Inside[row, column] == 0)
            grid.Inside[row, column] = InForest(grid, grid.Point(row, column)) ? (sbyte)1 : (sbyte)-1;
        return grid.Inside[row, column] == 1;
    }

    /// <summary>
    /// Dessine les bosquets visibles d'une forêt, ligne par ligne ; s'il n'y en a aucun dans toute la forêt, celui du
    /// centre (s'il est dans la forêt).
    /// </summary>
    private void DrawForestTrees(PolygonArea biome)
    {
        var grid = ForestTreeGrid(biome);
        var margin = 16f * Math.Max(0.6f, _zoom) + 2f;
        var view = new Rect2(Vector2.Zero, Size).Grow(margin);

        // Les lignes et colonnes qui peuvent être visibles (une de plus de chaque côté, le test exact suit).
        var first = (view.Position - _panOffset) / _zoom - grid.Origin;
        var last = (view.End - _panOffset) / _zoom - grid.Origin;
        var row0 = Math.Max(0, (int)Math.Floor(first.Y / grid.Step) - 1);
        var row1 = Math.Min(grid.Rows - 1, (int)Math.Ceiling(last.Y / grid.Step) + 1);
        var column0 = Math.Max(0, (int)Math.Floor(first.X / grid.Step) - 1);
        var column1 = Math.Min(grid.Columns - 1, (int)Math.Ceiling(last.X / grid.Step) + 1);

        var drawn = 0;
        for (var row = row0; row <= row1; row++)
            for (var column = column0; column <= column1; column++)
            {
                var point = _panOffset + grid.Point(row, column) * _zoom;
                if (!view.HasPoint(point) || !HasTree(grid, row, column))
                    continue;
                DrawTreeCluster(point);
                drawn++;
            }

        if (drawn > 0)
            return;

        // Aucun bosquet visible : le bosquet du centre n'existe que si la grille entière n'en a aucun.
        if (!grid.AllTested)
        {
            var any = false;
            for (var row = 0; row < grid.Rows && !any; row++)
                for (var column = 0; column < grid.Columns && !any; column++)
                    any = HasTree(grid, row, column);
            grid.AllTested = true;
            if (!any && grid.Outline.Length > 0)
            {
                var center = grid.Outline.Aggregate(Vector2.Zero, (acc, p) => acc + p) / grid.Outline.Length;
                if (InForest(grid, center))
                    grid.Center = center;
            }
        }

        if (grid.Center is { } tree)
        {
            var point = _panOffset + tree * _zoom;
            if (view.HasPoint(point))
                DrawTreeCluster(point);
        }
    }
}
