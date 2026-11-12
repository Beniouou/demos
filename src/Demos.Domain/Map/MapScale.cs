namespace Demos.Domain.Map;

/// <summary>
/// L'échelle de la carte (décidé le 12 novembre 2026) : une unité des coordonnées de la carte vaut 100 m, la carte
/// (3 600 × 2 250 unités) fait donc 360 × 225 km. Les règles comptent en kilomètres : les réglages de distance
/// (<c>equilibrage.json</c>) sont en km, et les coordonnées sont converties ici.
/// </summary>
public static class MapScale
{
    /// <summary>Mètres par unité de carte.</summary>
    public const double MetersPerUnit = 100;

    /// <summary>Kilomètres par unité de carte.</summary>
    public const double KmPerUnit = MetersPerUnit / 1000;

    /// <summary>Une longueur de la carte (en unités), en km.</summary>
    public static double ToKm(double units) => units * KmPerUnit;

    /// <summary>Une longueur en km, en unités de la carte.</summary>
    public static double ToUnits(double km) => km / KmPerUnit;

    /// <summary>La distance entre deux points de la carte, en km.</summary>
    public static double KmBetween(Point2D a, Point2D b) => ToKm(a.DistanceTo(b));

    /// <summary>Une distance en km, écrite pour le joueur : « 850 m », « 4,2 km », « 37 km ».</summary>
    public static string Format(double km) =>
        km < 1 ? $"{km * 1000:0} m"
        : km < 10 ? $"{km:0.#} km"
        : $"{km:N0} km";
}
