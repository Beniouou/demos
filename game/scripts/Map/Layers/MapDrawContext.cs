using System;
using System.Collections.Generic;
using Demos.Application;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Ce qu'un calque a sous la main pour dessiner : le crayon (l'élément de Godot sur lequel il dessine), la caméra, les
/// calculs gardés (<see cref="MapGeometry"/>), les données de la carte et de la partie (<see cref="MapData"/>), la vue
/// choisie, et quelques dessins communs.
/// </summary>
public sealed class MapDrawContext(CanvasItem pen, MapCamera camera, MapGeometry geometry, MapData data, Func<MapFilters> filters)
{
    public CanvasItem Pen { get; } = pen;
    public MapCamera Camera { get; } = camera;
    public MapGeometry Geometry { get; } = geometry;
    public MapData Data { get; } = data;
    public MapFilters Filters => filters();

    /// <summary>Vrai si ce filtre d'information est coché.</summary>
    public bool Has(MapFilters filter) => (filters() & filter) != 0;

    public Font Font => ThemeDB.FallbackFont;
    public float Zoom => Camera.Zoom;
    public Vector2 Size => Camera.Size;
    public VectorMapDefinition? VectorMap => Data.VectorMap;
    public GameView? GameView => Data.View;

    public Vector2 ToScreen(Vector2 worldPos) => Camera.ToScreen(worldPos);
    public Vector2[] ToScreen(MapGeometry.ScaledShape shape) => Geometry.ToScreen(shape);
    public MapGeometry.ScaledShape Scaled(IReadOnlyList<Point2D> source) => Geometry.Scaled(source);
    public bool IsOnScreen(MapGeometry.ScaledShape shape, float margin) => Geometry.IsOnScreen(shape, margin);
    public List<Vector2[]> Triangles(MapGeometry.ScaledShape shape) => Geometry.Triangles(shape);

    /// <summary>
    /// Dessine des triangles gardés en coordonnées de la carte, à leur place à l'écran (déplacement + point × zoom), en
    /// un seul appel à Godot.
    /// </summary>
    public void FillTriangles(List<Vector2[]> triangles, Color color)
    {
        var count = 0;
        foreach (var triangle in triangles)
            count += triangle.Length - 2;
        if (count == 0)
            return;

        var panOffset = Camera.PanOffset;
        var zoom = Camera.Zoom;
        var points = new Vector2[count * 3];
        var colors = new Color[points.Length];
        var indices = new int[points.Length];
        var next = 0;
        foreach (var triangle in triangles)
        {
            // Un triangle a trois sommets ; par prudence, un polygone convexe plus long est coupé en éventail.
            for (var k = 1; k + 1 < triangle.Length; k++)
            {
                points[next] = panOffset + triangle[0] * zoom;
                points[next + 1] = panOffset + triangle[k] * zoom;
                points[next + 2] = panOffset + triangle[k + 1] * zoom;
                next += 3;
            }
        }
        for (var i = 0; i < points.Length; i++)
        {
            colors[i] = color;
            indices[i] = i;
        }
        RenderingServer.CanvasItemAddTriangleArray(Pen.GetCanvasItem(), indices, points, colors);
    }

    /// <summary>Remplit un polygone (à l'écran) découpé en triangles sûrs.</summary>
    public void SafeDrawColoredPolygon(Vector2[] points, Color color)
    {
        foreach (var triangle in MapGeometry.SafeTriangulate(points))
            Pen.DrawColoredPolygon(triangle, color);
    }

    /// <summary>Un petit bâtiment vu du ciel : murs, toit à deux versants et faîtage.</summary>
    public void DrawBuildingFootprint(Vector2 center, Vector2 size, Color roofColor)
    {
        var half = size * 0.5f;
        var rect = new Rect2(center - half, size);

        // Empreinte murale
        Pen.DrawRect(rect, MapPalette.BuildingWall);
        Pen.DrawRect(rect, MapPalette.BuildingStroke, filled: false, width: 1.0f);

        // Toit à deux versants avec arête de faîtage
        var pBottom = new Vector2(center.X, rect.Position.Y + size.Y);

        Pen.DrawColoredPolygon([rect.Position, new Vector2(rect.Position.X + size.X, rect.Position.Y), pBottom, new Vector2(rect.Position.X, rect.Position.Y + size.Y)], roofColor);
        Pen.DrawLine(new Vector2(rect.Position.X, center.Y), new Vector2(rect.Position.X + size.X, center.Y), MapPalette.BuildingStroke, 1.2f);
    }
}
