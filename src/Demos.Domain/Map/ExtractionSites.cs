using System;
using System.Collections.Generic;
using System.Linq;

namespace Demos.Domain.Map;

/// <summary>
/// Où se posent les bâtiments d'extraction (décidé le 10 novembre 2026) : sur une zone de la carte qui correspond à ce
/// qu'ils extraient (forêt, terre fertile, pierre, charbon, fer), à une distance limitée de leur commune. Plusieurs
/// bâtiments peuvent partager une zone ; la distance n'a pas d'effet sur la production pour l'instant. Les bâtiments
/// sans zone sur la carte (tour de forage, puits…) se posent sans contrainte.
/// </summary>
public static class ExtractionSites
{
    /// <summary>Les zones de la carte qu'un type de bâtiment demande, selon sa recette ; vide s'il n'en demande pas.</summary>
    public static IReadOnlyList<string> ZonesFor(string? typeId, string? productId) => typeId switch
    {
        "camp-forestier" => ["foret"],
        "ferme" or "entreprise-agricole" => ["fertile"],
        "carriere" => ["pierre"],
        "mine" or "mine-souterraine" => productId switch
        {
            "charbon" => ["charbon"],
            "minerai-fer" => ["fer"],
            // Les autres minerais n'ont pas encore de zone : charbon ou fer.
            _ => ["charbon", "fer"],
        },
        _ => [],
    };

    /// <summary>Le nom d'une zone pour le joueur (« une forêt », « un gisement de charbon »).</summary>
    public static string ZoneName(string kind) => kind switch
    {
        "foret" => "une forêt",
        "fertile" => "une terre fertile",
        "pierre" => "un gisement de pierre",
        "charbon" => "un gisement de charbon",
        "fer" => "un gisement de fer",
        _ => $"une zone « {kind} »",
    };

    /// <summary>
    /// Ce qui empêche de poser le bâtiment à cette position, ou null si c'est permis : hors de portée de la commune, ou
    /// pas sur une zone qui correspond.
    /// </summary>
    public static string? ProblemWith(IReadOnlyList<string> kinds, Point2D position, Point2D communePosition, double maxDistance,
        IEnumerable<PolygonArea> zones)
    {
        if (kinds.Count == 0)
            return null;
        if (position.DistanceTo(communePosition) > maxDistance)
            return $"trop loin de la commune ({position.DistanceTo(communePosition):0} lieues ; {maxDistance:0} au plus)";
        return zones.Any(z => kinds.Contains(z.Kind) && Contains(z, position))
            ? null
            : $"il faut le poser sur {string.Join(" ou ", kinds.Select(ZoneName))}";
    }

    /// <summary>
    /// L'emplacement permis le plus proche de la commune : la commune elle-même si elle est sur une zone qui correspond,
    /// sinon le sommet le plus proche d'une telle zone, s'il est à portée ; null s'il n'y en a pas.
    /// </summary>
    public static Point2D? NearestSite(IReadOnlyList<string> kinds, Point2D communePosition, double maxDistance, IEnumerable<PolygonArea> zones)
    {
        var matching = zones.Where(z => kinds.Contains(z.Kind)).ToList();
        if (matching.Any(z => Contains(z, communePosition)))
            return communePosition;

        // Un sommet du contour, rapproché un peu du centre de la zone pour être sûr d'être dedans.
        var candidates = matching
            .SelectMany(z => z.Boundary.Select(p => (Zone: z, Point: Inside(z, p))))
            .Where(c => Contains(c.Zone, c.Point))
            .Select(c => (c.Point, Distance: c.Point.DistanceTo(communePosition)))
            .Where(c => c.Distance <= maxDistance)
            .OrderBy(c => c.Distance)
            .ToList();
        return candidates.Count == 0 ? null : candidates[0].Point;
    }

    /// <summary>Vrai si le point est dans la zone, et pas dans un de ses trous.</summary>
    public static bool Contains(PolygonArea zone, Point2D point) =>
        InPolygon(zone.Boundary, point) && !(zone.Holes ?? []).Any(hole => InPolygon(hole, point));

    private static bool InPolygon(IReadOnlyList<Point2D> polygon, Point2D point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Y > point.Y) != (b.Y > point.Y)
                && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static Point2D Inside(PolygonArea zone, Point2D vertex)
    {
        var cx = zone.Boundary.Average(p => p.X);
        var cy = zone.Boundary.Average(p => p.Y);
        return new Point2D(vertex.X + (cx - vertex.X) * 0.02, vertex.Y + (cy - vertex.Y) * 0.02);
    }
}
