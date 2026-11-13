using System.Collections.Generic;
using System.Linq;

namespace Demos.Domain.Map;

/// <summary>
/// Où se fonde une commune (décidé le 12 novembre 2026) : sur la carte, pas sur l'eau (altitude sous 0), ni sur une
/// falaise, ni sur un glacier, et à une distance minimale de toute autre commune (deux ressorts de commune, réglage
/// <c>communeMinDistance</c>). Pas de distance maximale à la commune qui paie.
/// </summary>
public static class CommuneSites
{
    /// <summary>Les zones de la carte où l'on ne fonde pas de commune.</summary>
    public static readonly IReadOnlyList<string> ForbiddenZones = ["falaise", "glacier"];

    /// <summary>
    /// Ce qui empêche de fonder une commune à cette position, ou null si c'est permis.
    /// </summary>
    /// <param name="relief">La carte d'altitude ; sans elle, ni l'eau ni le bord de la carte ne sont contrôlés.</param>
    /// <param name="communes">Les autres communes qui ont une position : leur nom et leur position.</param>
    public static string? ProblemWith(Point2D position, IEnumerable<PolygonArea> zones, ReliefGrid? relief,
        IEnumerable<(string Name, Point2D Position)> communes, double minDistance)
    {
        if (relief is not null)
        {
            if (position.X < 0 || position.Y < 0 || position.X > relief.Width * relief.CellSize || position.Y > relief.Height * relief.CellSize)
                return "hors de la carte";
            if (relief.Sample(position.X, position.Y) < 0)
                return "sur l'eau";
        }

        if (zones.FirstOrDefault(z => ForbiddenZones.Contains(z.Kind) && ExtractionSites.Contains(z, position)) is { } zone)
            return zone.Kind == "glacier" ? "sur un glacier" : "sur une falaise";

        var nearest = communes
            .Select(c => (c.Name, Distance: MapScale.KmBetween(position, c.Position)))
            .OrderBy(c => c.Distance)
            .FirstOrDefault();
        return nearest.Name is not null && nearest.Distance < minDistance
            ? $"trop près de {nearest.Name} ({MapScale.Format(nearest.Distance)} ; {MapScale.Format(minDistance)} au moins)"
            : null;
    }
}
