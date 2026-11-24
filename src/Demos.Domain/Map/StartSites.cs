using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Economy;

namespace Demos.Domain.Map;

/// <summary>
/// Une terre d'exil marquée sur la carte (décidé le 23 novembre 2026) : son nom et l'emplacement du camp de base. Sa
/// zone se lit autour du camp : le ressort de la commune (8 km) et la portée d'extraction (20 km).
/// </summary>
public sealed record ExileSite(string Name, Point2D Position);

/// <summary>Ce que vaut une terre d'exil pour un départ d'exil.</summary>
public enum ExileVerdict
{
    /// <summary>Pauvre, mais on peut y survivre et en sortir.</summary>
    Playable,

    /// <summary>Il manque de quoi survivre ou de quoi s'étendre.</summary>
    Unplayable,

    /// <summary>Trop riche pour une terre d'exil.</summary>
    TooEasy,
}

/// <summary>
/// Les seuils qui jugent une terre d'exil (propositions du 23 novembre 2026, à régler par la simulation du départ
/// d'exil). Ce ne sont pas des règles de la partie : l'éditeur de carte s'en sert pour prévenir.
/// </summary>
public sealed record ExileThresholds
{
    /// <summary>Emplois de ferme qu'il faut au moins dans le ressort de la commune (sans loi d'extension).</summary>
    public double MinFarmJobs { get; init; } = 100;

    /// <summary>Au-delà, la terre est trop fertile pour une terre d'exil.</summary>
    public double MaxFarmJobs { get; init; } = 500;

    /// <summary>Le fer doit être au-delà de cette distance (hors de portée du camp), en km.</summary>
    public double IronMinKm { get; init; } = 20;

    /// <summary>… et en deçà de celle-ci (atteignable en fondant une commune), en km.</summary>
    public double IronMaxKm { get; init; } = 40;

    /// <summary>Une forêt et de la pierre doivent être à moins de cette distance, en km.</summary>
    public double WoodAndStoneMaxKm { get; init; } = 20;

    /// <summary>Distance à la mer ou à un cours d'eau pour compter comme « au bord », en km.</summary>
    public double ShoreKm { get; init; } = 3;
}

/// <summary>La fiche d'un départ : ce que la carte offre autour du camp.</summary>
/// <param name="NearKm">Le ressort de la commune (km), où l'on exploite sans loi d'extension.</param>
/// <param name="FarKm">La portée d'extraction (km).</param>
/// <param name="FarmJobs">Emplois de ferme possibles sur la terre fertile la plus proche dans le ressort (zones limitées).</param>
/// <param name="ForestJobs">Emplois de bûcheron possibles sur la forêt la plus proche dans le ressort.</param>
/// <param name="DepositKm">Distance au gisement le plus proche de chaque sorte (pierre, charbon, fer) ; null s'il n'y en a pas.</param>
public sealed record StartSiteReport(
    double NearKm,
    double FarKm,
    double FertileNearKm2,
    double FertileFarKm2,
    double ForestNearKm2,
    double ForestFarKm2,
    double FarmJobs,
    double ForestJobs,
    IReadOnlyDictionary<string, double?> DepositKm,
    bool OnCoast,
    bool NearRiver,
    double? Altitude)
{
    public double? KmTo(string deposit) => DepositKm.TryGetValue(deposit, out var km) ? km : null;
}

/// <summary>Un bâtiment de départ qui se pose sur une zone : son nom, les sortes de zone qu'il demande, ses emplois.</summary>
public sealed record ZoneDemand(string Name, IReadOnlyList<string> Kinds, int Jobs);

/// <summary>
/// Les départs d'une carte (décidé le 23 novembre 2026) : la fiche de ce que la carte offre autour d'un camp, le verdict
/// d'une terre d'exil, et le contrôle du départ riche (le départ préréglé y tient-il ?).
/// </summary>
public static class StartSites
{
    /// <summary>Les sortes de gisement que la fiche mesure.</summary>
    public static readonly IReadOnlyList<string> DepositKinds = ["pierre", "charbon", "fer"];

    /// <summary>Le pas de la grille qui mesure les surfaces dans un grand rayon, en km.</summary>
    public const double AreaCellKm = 0.5;

    /// <summary>
    /// La fiche d'un camp posé à cette position. <paramref name="elevation"/> donne l'altitude d'un point (sans elle,
    /// ni la côte ni l'altitude ne sont mesurées).
    /// </summary>
    public static StartSiteReport Report(Point2D camp, IReadOnlyList<PolygonArea> zones, IReadOnlyList<RiverData> rivers,
        Func<Point2D, double>? elevation, ExileThresholds? thresholds = null)
    {
        thresholds ??= new ExileThresholds();
        var near = Balance.Current.CommuneJurisdictionRadius;
        var far = Balance.Current.ExtractionMaxDistance;

        var deposits = DepositKinds.ToDictionary(kind => kind, kind => NearestKm(kind, camp, zones));
        var shore = MapScale.ToUnits(thresholds.ShoreKm);

        return new StartSiteReport(
            near,
            far,
            AreaWithin(["fertile"], camp, near, zones),
            AreaWithin(["fertile"], camp, far, zones),
            AreaWithin(["foret"], camp, near, zones),
            AreaWithin(["foret"], camp, far, zones),
            JobsAtNearestSite(["fertile"], camp, near, zones),
            JobsAtNearestSite(["foret"], camp, near, zones),
            deposits,
            elevation is not null && SeaWithin(camp, shore, elevation),
            rivers.Any(r => DistanceToLine(r.Points, camp) <= shore),
            elevation?.Invoke(camp));
    }

    /// <summary>Le verdict d'une terre d'exil, et ce qui le justifie (une ligne par raison).</summary>
    public static (ExileVerdict Verdict, IReadOnlyList<string> Reasons) Judge(StartSiteReport report, ExileThresholds? thresholds = null)
    {
        thresholds ??= new ExileThresholds();
        var missing = new List<string>();
        var easy = new List<string>();

        if (report.FarmJobs < thresholds.MinFarmJobs)
            missing.Add($"trop peu de terre fertile : {report.FarmJobs:N0} emplois de ferme dans le ressort, {thresholds.MinFarmJobs:N0} au moins");
        else if (report.FarmJobs > thresholds.MaxFarmJobs)
            easy.Add($"terre trop fertile : {report.FarmJobs:N0} emplois de ferme dans le ressort, {thresholds.MaxFarmJobs:N0} au plus");

        var iron = report.KmTo("fer");
        if (iron is null)
            missing.Add("aucun gisement de fer sur la carte");
        else if (iron > thresholds.IronMaxKm)
            missing.Add($"fer trop loin : {MapScale.Format(iron.Value)} ({MapScale.Format(thresholds.IronMaxKm)} au plus, pour l'atteindre en une fondation)");
        else if (iron <= thresholds.IronMinKm)
            easy.Add($"fer à portée du camp : {MapScale.Format(iron.Value)} (au-delà de {MapScale.Format(thresholds.IronMinKm)} demandé)");

        if (report.ForestFarKm2 <= 0)
            missing.Add($"aucune forêt à moins de {MapScale.Format(thresholds.WoodAndStoneMaxKm)}");
        if (report.KmTo("pierre") is not { } stone || stone > thresholds.WoodAndStoneMaxKm)
            missing.Add($"pas de pierre à moins de {MapScale.Format(thresholds.WoodAndStoneMaxKm)}");

        if (missing.Count > 0)
            return (ExileVerdict.Unplayable, missing);
        return easy.Count > 0 ? (ExileVerdict.TooEasy, easy) : (ExileVerdict.Playable, []);
    }

    /// <summary>
    /// Ce qui empêche le départ préréglé de tenir autour de ce camp : chaque bâtiment est posé, comme au début de la
    /// partie, sur la zone qui convient la plus proche (à portée d'extraction), puis les emplois de chaque zone sont
    /// comparés à ce qu'elle supporte (zones limitées). Vide si tout tient.
    /// </summary>
    public static IReadOnlyList<string> PresetProblems(IEnumerable<ZoneDemand> demands, Point2D camp, IReadOnlyList<PolygonArea> zones)
    {
        var problems = new List<string>();
        var placed = new List<(ZoneDemand Demand, Point2D Site)>();
        var maxDistance = Balance.Current.ExtractionMaxDistance;
        foreach (var demand in demands.Where(d => d.Kinds.Count > 0))
        {
            if (ExtractionSites.NearestSite(demand.Kinds, camp, maxDistance, zones) is { } site)
                placed.Add((demand, site));
            else
                problems.Add($"{demand.Name} : pas de {string.Join(" ni de ", demand.Kinds.Select(KindName))} à moins de {MapScale.Format(maxDistance)}");
        }

        var radius = Balance.Current.ZoneRadius;
        foreach (var (demand, site) in placed)
        {
            var used = placed
                .Where(p => p.Demand.Kinds.Any(demand.Kinds.Contains) && MapScale.KmBetween(p.Site, site) <= radius)
                .Sum(p => p.Demand.Jobs);
            var capacity = ZoneCapacity.CapacityAt(demand.Kinds, site, zones);
            if (used > capacity + 1e-6)
                problems.Add($"{demand.Name} : zone pleine, {used:N0} emplois pour {capacity:N0} possibles à moins de {radius:N0} km");
        }

        return problems;
    }

    /// <summary>La surface, en km², des zones de ces sortes à moins de <paramref name="radiusKm"/> de ce point.</summary>
    public static double AreaWithin(IReadOnlyList<string> kinds, Point2D center, double radiusKm, IEnumerable<PolygonArea> zones)
    {
        var radius = MapScale.ToUnits(radiusKm);
        var step = MapScale.ToUnits(AreaCellKm);
        // Chaque zone avec son cadre : un point hors du cadre n'a pas besoin du test complet.
        var nearZones = zones.Where(z => kinds.Contains(z.Kind) && z.Boundary.Count > 2)
            .Select(z => (Zone: z, MinX: z.Boundary.Min(p => p.X), MaxX: z.Boundary.Max(p => p.X),
                MinY: z.Boundary.Min(p => p.Y), MaxY: z.Boundary.Max(p => p.Y)))
            .Where(b => b.MinX <= center.X + radius && b.MaxX >= center.X - radius
                && b.MinY <= center.Y + radius && b.MaxY >= center.Y - radius)
            .ToList();
        if (nearZones.Count == 0)
            return 0;

        var cells = 0;
        for (var x = center.X - radius + step / 2; x <= center.X + radius; x += step)
        {
            for (var y = center.Y - radius + step / 2; y <= center.Y + radius; y += step)
            {
                if ((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y) > radius * radius)
                    continue;
                var point = new Point2D(x, y);
                foreach (var b in nearZones)
                {
                    if (x < b.MinX || x > b.MaxX || y < b.MinY || y > b.MaxY || !ExtractionSites.Contains(b.Zone, point))
                        continue;
                    cells++;
                    break;
                }
            }
        }

        return cells * AreaCellKm * AreaCellKm;
    }

    /// <summary>
    /// Les emplois qu'un bâtiment posé sur la zone de cette sorte la plus proche du camp, dans ce rayon, pourrait avoir
    /// (zones limitées) ; 0 s'il n'y en a pas. C'est là que le jeu pose les bâtiments du départ.
    /// </summary>
    public static double JobsAtNearestSite(IReadOnlyList<string> kinds, Point2D camp, double radiusKm, IReadOnlyList<PolygonArea> zones) =>
        ExtractionSites.NearestSite(kinds, camp, radiusKm, zones) is { } site
            ? ZoneCapacity.CapacityAt(kinds, site, zones)
            : 0;

    /// <summary>La distance, en km, à la zone de cette sorte la plus proche (0 si le camp est dessus) ; null s'il n'y en a pas.</summary>
    public static double? NearestKm(string kind, Point2D camp, IEnumerable<PolygonArea> zones)
    {
        double? best = null;
        foreach (var zone in zones.Where(z => z.Kind == kind && z.Boundary.Count > 2))
        {
            if (ExtractionSites.Contains(zone, camp))
                return 0;
            var km = MapScale.ToKm(DistanceToLine(zone.Boundary, camp, closed: true));
            if (best is null || km < best)
                best = km;
        }
        return best;
    }

    private static string KindName(string kind) => kind switch
    {
        "foret" => "forêt",
        "fertile" => "terre fertile",
        _ => kind,
    };

    private static bool SeaWithin(Point2D center, double radius, Func<Point2D, double> elevation)
    {
        if (elevation(center) < 0)
            return true;
        for (var ring = 1; ring <= 3; ring++)
        {
            var r = radius * ring / 3;
            for (var i = 0; i < 16; i++)
            {
                var angle = i * Math.PI / 8;
                if (elevation(new Point2D(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle))) < 0)
                    return true;
            }
        }
        return false;
    }

    /// <summary>La distance (en unités) d'un point à une ligne brisée, fermée ou non.</summary>
    private static double DistanceToLine(IReadOnlyList<Point2D> points, Point2D p, bool closed = false)
    {
        if (points.Count == 0)
            return double.PositiveInfinity;
        if (points.Count == 1)
            return points[0].DistanceTo(p);

        var best = double.PositiveInfinity;
        var count = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var lengthSquared = dx * dx + dy * dy;
            var t = lengthSquared <= 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
            var d = p.DistanceTo(new Point2D(a.X + t * dx, a.Y + t * dy));
            if (d < best)
                best = d;
        }
        return best;
    }
}
