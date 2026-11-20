using Demos.Domain.Economy;

namespace Demos.Domain.Map;

/// <summary>
/// Ce qu'une zone de la carte supporte d'emplois (décidé le 20 novembre 2026) : autour d'un bâtiment d'extraction, on
/// ne compte que la partie de sa zone à moins de <see cref="BalanceSettings.ZoneRadius"/> ; cette surface supporte au
/// plus une densité d'emplois par km² selon la sorte de zone, partagés entre tous les bâtiments de cette zone dans ce
/// rayon, quelle que soit leur commune.
/// </summary>
public static class ZoneCapacity
{
    /// <summary>Le pas de la grille qui mesure la surface, en km (200 m).</summary>
    public const double CellKm = 0.2;

    /// <summary>Emplois par km² qu'une sorte de zone supporte (sans limite pour une sorte inconnue).</summary>
    public static double DensityOf(string kind) => kind switch
    {
        "fertile" => Balance.Current.ZoneDensityFertile,
        "foret" => Balance.Current.ZoneDensityForest,
        "pierre" or "charbon" or "fer" => Balance.Current.ZoneDensityDeposit,
        _ => double.PositiveInfinity,
    };

    /// <summary>La surface, en km², des zones de ces sortes à moins du rayon de ce point (mesurée sur une grille de 200 m).</summary>
    public static double AreaWithin(IReadOnlyList<string> kinds, Point2D center, IEnumerable<PolygonArea> zones)
    {
        var radius = MapScale.ToUnits(Balance.Current.ZoneRadius);
        var step = MapScale.ToUnits(CellKm);
        // Seules les zones dont le cadre touche le cercle comptent.
        var near = zones.Where(z => kinds.Contains(z.Kind)
            && z.Boundary.Min(p => p.X) <= center.X + radius && z.Boundary.Max(p => p.X) >= center.X - radius
            && z.Boundary.Min(p => p.Y) <= center.Y + radius && z.Boundary.Max(p => p.Y) >= center.Y - radius).ToList();
        if (near.Count == 0)
            return 0;

        var cells = 0;
        for (var x = center.X - radius; x <= center.X + radius; x += step)
        {
            for (var y = center.Y - radius; y <= center.Y + radius; y += step)
            {
                if ((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y) > radius * radius)
                    continue;
                var point = new Point2D(x, y);
                if (near.Any(z => ExtractionSites.Contains(z, point)))
                    cells++;
            }
        }

        return cells * CellKm * CellKm;
    }

    /// <summary>Les emplois que la zone supporte autour de ce point : sa surface dans le rayon × la densité de sa sorte.</summary>
    public static double CapacityAt(IReadOnlyList<string> kinds, Point2D center, IEnumerable<PolygonArea> zones) =>
        kinds.Count == 0 ? double.PositiveInfinity : AreaWithin(kinds, center, zones) * kinds.Min(DensityOf);

    /// <summary>
    /// Les emplois déjà posés autour de ce point : ceux des bâtiments d'une même sorte de zone, à moins du rayon.
    /// </summary>
    public static double UsedAt(IReadOnlyList<string> kinds, Point2D center, IEnumerable<(Point2D Position, int Jobs, IReadOnlyList<string> Kinds)> buildings)
    {
        var radius = Balance.Current.ZoneRadius;
        return buildings
            .Where(b => b.Kinds.Any(kinds.Contains) && MapScale.KmBetween(b.Position, center) <= radius)
            .Sum(b => b.Jobs);
    }
}
