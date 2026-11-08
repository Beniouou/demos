using System.Collections.Generic;

namespace Demos.Domain.Map;

public readonly record struct Point2D(double X, double Y);

public sealed record ContourLine(
    double Altitude,
    bool IsIndex,
    bool IsClosed,
    IReadOnlyList<Point2D> Points);

public sealed record SpotElevation(
    double Altitude,
    string Name,
    Point2D Position,
    string Category = "Peak",
    bool ShowAltitude = true);

public sealed record RiverData(
    string Name,
    IReadOnlyList<Point2D> Points,
    double Width = 5.0);

public sealed record PolygonArea(
    string Name,
    string Kind,
    IReadOnlyList<Point2D> Boundary)
{
    /// <summary>
    /// Détecte si un polygone maritime est un artéfact erroné qui boucle anormalement sur les coins
    /// et recouvre la totalité du monde terrestre.
    /// </summary>
    public bool IsErroneousGlobalSea(double w, double h)
    {
        if (Boundary.Count < 4) return false;
        var corners = new Point2D[] { new(0, 0), new(w, 0), new(w, h), new(0, h) };
        var cornersOnBoundary = 0;
        foreach (var c in corners)
        {
            for (var i = 0; i < Boundary.Count; i++)
            {
                if (System.Math.Abs(Boundary[i].X - c.X) < 1.0 && System.Math.Abs(Boundary[i].Y - c.Y) < 1.0)
                {
                    cornersOnBoundary++;
                    break;
                }
            }
        }
        if (cornersOnBoundary >= 3)
        {
            var area = 0.0;
            for (var i = 0; i < Boundary.Count; i++)
            {
                var j = (i + 1) % Boundary.Count;
                area += Boundary[i].X * Boundary[j].Y - Boundary[j].X * Boundary[i].Y;
            }
            if (System.Math.Abs(area) * 0.5 > 0.70 * w * h) return true;
        }
        return false;
    }
}

public sealed record ResourceDeposit(
    string Resource,
    string Region,
    Point2D Position);

public sealed record CommuneData(
    string Id,
    string Name,
    Point2D Position,
    double Population = 1000,
    IReadOnlyList<string>? InitialBuildings = null);

public sealed record VectorMapDefinition(
    string Name,
    double Width,
    double Height,
    IReadOnlyList<ContourLine> Contours,
    IReadOnlyList<SpotElevation> SpotElevations,
    RiverData River,
    IReadOnlyList<PolygonArea> Forests,
    IReadOnlyList<PolygonArea> WaterBodies,
    IReadOnlyList<ResourceDeposit> Resources,
    CommuneData DefaultCommune,
    IReadOnlyList<RiverData>? Rivers = null,
    IReadOnlyList<CommuneData>? Communes = null,
    IReadOnlyList<PolygonArea>? ResourceZones = null,
    IReadOnlyList<PolygonArea>? Biomes = null)
{
    public IReadOnlyList<RiverData> AllRivers =>
        Rivers is { Count: > 0 } ? Rivers : (River.Points.Count > 0 ? [River] : []);

    public IReadOnlyList<CommuneData> AllCommunes =>
        Communes is { Count: > 0 } ? Communes : (DefaultCommune is not null ? [DefaultCommune] : []);

    public IReadOnlyList<PolygonArea> AllResourceZones =>
        ResourceZones ?? [];

    public IReadOnlyList<PolygonArea> AllBiomes =>
        Biomes is { Count: > 0 } ? Biomes : Forests;
}
