using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les calculs du dessin de la carte gardés d'un dessin à l'autre, partagés par tous les calques, pour la vitesse :
/// - chaque tracé de la carte (courbe, fleuve, forêt, mer, gisement) mis à l'échelle du zoom une fois par zoom ; un point
///   à l'écran reste « déplacement + point × zoom », le même calcul qu'avant ;
/// - ses triangles (le remplissage), les morceaux des forêts à trous, calculés une seule fois
///   pour toute la partie, en coordonnées de la carte ; chaque dessin les met à l'échelle du zoom ;
/// - un tracé entièrement hors de l'écran n'est pas dessiné du tout.
/// Les points mis à l'échelle se recalculent quand le zoom change ; un déplacement ne recalcule rien.
/// </summary>
public sealed class MapGeometry(MapCamera camera)
{
    /// <summary>Un tracé au zoom courant : ses points (monde × zoom), sa boîte englobante, et le tracé d'origine.</summary>
    public sealed class ScaledShape(IReadOnlyList<Point2D> source, Vector2[] points, Rect2 bounds)
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

    private float Zoom => camera.Zoom;
    private Vector2 PanOffset => camera.PanOffset;

    /// <summary>Les points d'un tracé de la carte au zoom courant (calculés une fois par zoom).</summary>
    public ScaledShape Scaled(IReadOnlyList<Point2D> source)
    {
        ForgetOtherZoom();
        if (_scaledShapes.TryGetValue(source, out var shape))
            return shape;

        var points = new Vector2[source.Count];
        for (var i = 0; i < points.Length; i++)
            points[i] = new Vector2((float)source[i].X, (float)source[i].Y) * Zoom;
        shape = new ScaledShape(source, points, BoundsOf(points));
        _scaledShapes[source] = shape;
        return shape;
    }

    private void ForgetOtherZoom()
    {
        if (_cachedZoom == Zoom)
            return;
        _scaledShapes.Clear();
        _cachedZoom = Zoom;
    }

    /// <summary>Les points d'un tracé en coordonnées de la carte (calculés une fois).</summary>
    public Vector2[] World(IReadOnlyList<Point2D> source)
    {
        if (_worldPoints.TryGetValue(source, out var points))
            return points;
        points = new Vector2[source.Count];
        for (var i = 0; i < points.Length; i++)
            points[i] = new Vector2((float)source[i].X, (float)source[i].Y);
        return _worldPoints[source] = points;
    }

    public static Rect2 BoundsOf(Vector2[] points)
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
    public bool IsOnScreen(ScaledShape shape, float margin) =>
        new Rect2(shape.Bounds.Position + PanOffset, shape.Bounds.Size).Grow(margin).Intersects(new Rect2(Vector2.Zero, camera.Size));

    /// <summary>Les points d'un tracé à l'écran : déplacement + point × zoom, comme <see cref="MapCamera.ToScreen"/>.</summary>
    public Vector2[] ToScreen(ScaledShape shape)
    {
        var scaled = shape.Points;
        var screen = new Vector2[scaled.Length];
        for (var i = 0; i < screen.Length; i++)
            screen[i] = PanOffset + scaled[i];
        return screen;
    }

    // La triangulation et le découpage des trous se font une fois, sur la carte agrandie au zoom le plus fort : leurs
    // seuils (sommets trop proches, triangles plats) sont en pixels, et ce sont ceux du zoom où ils se voient le plus.
    // Le résultat est gardé en coordonnées de la carte.

    private static Vector2[] AtMaxZoom(Vector2[] world) => world.Select(p => p * MapCamera.MaxZoom).ToArray();

    private static List<Vector2[]> FromMaxZoom(List<Vector2[]> polygons)
    {
        foreach (var polygon in polygons)
            for (var i = 0; i < polygon.Length; i++)
                polygon[i] /= MapCamera.MaxZoom;
        return polygons;
    }

    /// <summary>Les triangles du remplissage d'un tracé, en coordonnées de la carte (calculés une fois).</summary>
    public List<Vector2[]> Triangles(ScaledShape shape)
    {
        if (!_worldTriangles.TryGetValue(shape.Source, out var triangles))
            _worldTriangles[shape.Source] = triangles = FromMaxZoom(SafeTriangulate(AtMaxZoom(World(shape.Source))));
        return triangles;
    }

    /// <summary>
    /// Les morceaux d'une forêt à trous (le contour moins chaque trou), triangulés, en coordonnées de la carte
    /// (calculés une fois).
    /// </summary>
    public List<List<Vector2[]>> ForestPieceTriangles(PolygonArea biome)
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
    /// Les triangles d'un polygone rempli, sans les sommets doublés ni les triangles plats (moins d'un pixel) ; gardés
    /// pour les tracés de la carte (<see cref="Triangles"/>).
    /// </summary>
    public static List<Vector2[]> SafeTriangulate(Vector2[] points)
    {
        var triangles = new List<Vector2[]>();
        if (points == null || points.Length < 3) return triangles;

        var cleaned = CleanPolygonVertices(points);
        if (cleaned.Length < 3) return triangles;

        if (Geometry2D.IsPolygonClockwise(cleaned))
        {
            Array.Reverse(cleaned);
        }

        if (cleaned.Length == 3)
        {
            var area = Math.Abs((cleaned[1].X - cleaned[0].X) * (cleaned[2].Y - cleaned[0].Y) - (cleaned[1].Y - cleaned[0].Y) * (cleaned[2].X - cleaned[0].X));
            if (area > 1.0f)
            {
                if (Geometry2D.IsPolygonClockwise(cleaned)) Array.Reverse(cleaned);
                triangles.Add(cleaned);
            }
            return triangles;
        }

        var indices = Geometry2D.TriangulatePolygon(cleaned);
        if (indices != null && indices.Length >= 3)
        {
            for (var i = 0; i < indices.Length; i += 3)
            {
                var tri = new[] { cleaned[indices[i]], cleaned[indices[i + 1]], cleaned[indices[i + 2]] };
                var cross = (tri[1].X - tri[0].X) * (tri[2].Y - tri[0].Y) - (tri[1].Y - tri[0].Y) * (tri[2].X - tri[0].X);
                if (Math.Abs(cross) > 1.0f)
                {
                    if (Geometry2D.IsPolygonClockwise(tri))
                    {
                        Array.Reverse(tri);
                    }
                    triangles.Add(tri);
                }
            }
            return triangles;
        }

        var convexParts = Geometry2D.DecomposePolygonInConvex(cleaned);
        if (convexParts != null && convexParts.Count > 0)
        {
            foreach (var part in convexParts)
            {
                if (part.Length >= 3)
                {
                    var p = part;
                    if (Geometry2D.IsPolygonClockwise(p))
                    {
                        var rev = (Vector2[])p.Clone();
                        Array.Reverse(rev);
                        p = rev;
                    }
                    var partIndices = Geometry2D.TriangulatePolygon(p);
                    if (partIndices != null && partIndices.Length >= 3)
                    {
                        for (var i = 0; i < partIndices.Length; i += 3)
                        {
                            var tri = new[] { p[partIndices[i]], p[partIndices[i + 1]], p[partIndices[i + 2]] };
                            if (Geometry2D.IsPolygonClockwise(tri))
                            {
                                Array.Reverse(tri);
                            }
                            triangles.Add(tri);
                        }
                    }
                }
            }
        }

        return triangles;
    }

    private static Vector2[] CleanPolygonVertices(Vector2[] pts)
    {
        if (pts.Length < 3) return pts;
        var list = new List<Vector2>(pts.Length);
        for (var i = 0; i < pts.Length; i++)
        {
            var curr = pts[i];
            if (list.Count == 0 || curr.DistanceSquaredTo(list[^1]) > 0.5f)
            {
                list.Add(curr);
            }
        }
        if (list.Count >= 3 && list[0].DistanceSquaredTo(list[^1]) <= 0.5f)
        {
            list.RemoveAt(list.Count - 1);
        }
        return list.ToArray();
    }
}
