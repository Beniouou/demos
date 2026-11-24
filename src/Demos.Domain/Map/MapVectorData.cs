using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Demos.Domain.Map;

public readonly record struct Point2D(double X, double Y)
{
    public double DistanceTo(Point2D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return System.Math.Sqrt(dx * dx + dy * dy);
    }
}

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
    IReadOnlyList<Point2D> Boundary,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<IReadOnlyList<Point2D>>? Holes = null)
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

/// <summary>
/// Un lac (décidé le 24 novembre 2026) : une cuvette remplie jusqu'à <paramref name="Level"/> (m, au-dessus de la mer). Le
/// contour est la rive à ce niveau, les trous sont les îles. Un lac rempli jusqu'à son seuil déborde : il a un fleuve de
/// sortie (<paramref name="Outlet"/> : son nom, un fleuve ordinaire de la carte) ; un lac baissé sous son seuil est fermé. Dans le
/// jeu, un lac est de l'eau : pas de commune dessus, les routes le contournent ; sa rive n'est pas une côte.
/// </summary>
public sealed record LakeData(
    string Name,
    Point2D Seed,
    double Level,
    IReadOnlyList<Point2D> Boundary,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<IReadOnlyList<Point2D>>? Holes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Outlet = null)
{
    /// <summary>Le point est-il dans l'eau du lac (dans le contour, hors des îles) ?</summary>
    public bool Contains(Point2D point) =>
        ExtractionSites.Contains(new PolygonArea(Name, "lac", Boundary, Holes), point);
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
    IReadOnlyList<PolygonArea>? Biomes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ExileSite>? ExileSites = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<LakeData>? Lakes = null)
{
    /// <summary>Les lacs de la carte (vide pour une carte qui n'en a pas).</summary>
    public IReadOnlyList<LakeData> AllLakes => Lakes ?? [];

    /// <summary>Les terres d'exil marquées sur la carte (vide pour une carte qui n'en a pas).</summary>
    public IReadOnlyList<ExileSite> AllExileSites => ExileSites ?? [];

    public IReadOnlyList<RiverData> AllRivers =>
        Rivers is { Count: > 0 } ? Rivers : (River.Points.Count > 0 ? [River] : []);

    public IReadOnlyList<CommuneData> AllCommunes =>
        Communes is { Count: > 0 } ? Communes : (DefaultCommune is not null ? [DefaultCommune] : []);

    public IReadOnlyList<PolygonArea> AllResourceZones =>
        ResourceZones ?? [];

    public IReadOnlyList<PolygonArea> AllBiomes =>
        Biomes is { Count: > 0 } ? Biomes : Forests;
}
