using Demos.Domain.Economy;

namespace Demos.Domain.Politics;

/// <summary>
/// Charge d'une entité politique : ce qu'elle gère, rapporté à ce qu'elle peut gérer sans perte.
/// </summary>
/// <param name="CountedPopulation">
/// Population comptée : les habitants pour une commune ; pour une préfecture ou l'État, une part de ce que comptent
/// les entités qu'il contient (en cascade).
/// </param>
/// <param name="DirectBuildings">Bâtiments publics que l'entité gère en direct (pas ceux de ses entités contenues).</param>
/// <param name="AutonomousBuildings">Bâtiments autonomes (SCOP, privé) placés directement chez elle.</param>
/// <param name="ChargeWithoutPopulation">Charge sans le terme population : c'est elle qui remonte à l'entité qui la contient.</param>
/// <param name="Charge">Charge totale ; à 1, l'entité est à la limite de ce qu'elle peut gérer en direct.</param>
/// <param name="Efficiency">Part des décisions bien appliquées : 1 jusqu'à une charge de 1, puis 1 / charge.</param>
/// <param name="Distance">Terme de distance : charge bureaucratique liée à l'éloignement géographique des entités administrées.</param>
public sealed record EntityLoad(
    string EntityId,
    double CountedPopulation,
    int DirectBuildings,
    int AutonomousBuildings,
    double ChargeWithoutPopulation,
    double Charge,
    double Efficiency,
    double Distance = 0);

/// <summary>
/// L'extension : plus une entité gère de choses en direct, moins elle les gère bien. Elle se mesure dans chaque entité
/// politique avec sa population comptée, ses bâtiments gérés en direct, ses bâtiments autonomes (pour une moindre part),
/// la charge de ses entités contenues et les distances (à 0 en attendant la carte). La bureaucratie s'y ajoute : chaque
/// préfecture traversée entre l'entité qui décide et la commune fait perdre une part de l'efficacité.
/// </summary>
public sealed class Extension
{
    private readonly IReadOnlyDictionary<string, Structure> _structures;
    private readonly Dictionary<string, EntityLoad> _loads = [];
    private readonly double _lossPerLevel;

    private Extension(IReadOnlyDictionary<string, Structure> structures, double lossPerLevel)
    {
        _structures = structures;
        _lossPerLevel = lossPerLevel;
    }

    /// <summary>Aucune perte : toutes les entités sont efficaces à 100 %.</summary>
    public static Extension None(IReadOnlyDictionary<string, Structure> structures) => new(structures, 0);

    public IReadOnlyDictionary<string, EntityLoad> Loads => _loads;

    /// <summary>Calcule la charge de chaque entité politique, d'après les habitants de chaque commune et les distances.</summary>
    public static Extension Compute(
        IReadOnlyDictionary<string, Structure> structures,
        Func<string, double> residentsOf,
        BalanceSettings settings,
        Func<string, double>? distanceOf = null)
    {
        var extension = new Extension(structures, settings.BureaucracyLossPerLevel);
        var children = structures.Values
            .Where(s => s.ParentId is not null)
            .GroupBy(s => s.ParentId!)
            .ToDictionary(g => g.Key, g => g.ToList());
        var buildingsByManager = structures.Values
            .Where(s => s.Kind == StructureKind.Building)
            .GroupBy(s => ManagerOf(s, structures))
            .Where(g => g.Key is not null)
            .ToDictionary(g => g.Key!, g => g.ToList());

        foreach (var root in structures.Values.Where(s => s.IsNational && s.Kind == StructureKind.PoliticalEntity))
            extension.ComputeLoad(root, children, buildingsByManager, residentsOf, settings, distanceOf);

        return extension;
    }

    /// <summary>Efficacité propre d'une entité, sans la perte des échelons ; 1 si elle est inconnue.</summary>
    public double EfficiencyOf(string entityId) => _loads.TryGetValue(entityId, out var load) ? load.Efficiency : 1;

    /// <summary>
    /// Efficacité d'une décision de l'entité <paramref name="deciderId"/> là où elle s'applique : son efficacité propre,
    /// moins la perte de chaque préfecture traversée entre elle et la structure visée.
    /// </summary>
    public double EfficiencyFor(string deciderId, Structure target) =>
        EfficiencyOf(deciderId) * Math.Pow(1 - _lossPerLevel, LevelsBetween(deciderId, target));

    /// <summary>Préfectures traversées entre l'entité qui décide (exclue) et la structure visée.</summary>
    public int LevelsBetween(string deciderId, Structure target)
    {
        var levels = 0;
        for (var current = target.ParentId is { } p ? _structures.GetValueOrDefault(p) : null; current is not null;
             current = current.ParentId is { } parentId ? _structures.GetValueOrDefault(parentId) : null)
        {
            if (current.Id == deciderId)
                return levels;
            if (current.IsPrefecture)
                levels++;
        }

        // L'entité qui décide ne contient pas la structure : sa décision ne s'y applique pas, aucune préfecture à traverser.
        return 0;
    }

    /// <summary>L'entité politique qui gère un bâtiment en direct : la plus proche au-dessus de lui.</summary>
    public static string? ManagerOf(Structure building, IReadOnlyDictionary<string, Structure> structures)
    {
        for (var current = building.ParentId is { } p ? structures.GetValueOrDefault(p) : null; current is not null;
             current = current.ParentId is { } parentId ? structures.GetValueOrDefault(parentId) : null)
        {
            if (current.Kind == StructureKind.PoliticalEntity)
                return current.Id;
        }

        return null;
    }

    private EntityLoad ComputeLoad(
        Structure entity,
        IReadOnlyDictionary<string, List<Structure>> children,
        IReadOnlyDictionary<string, List<Structure>> buildingsByManager,
        Func<string, double> residentsOf,
        BalanceSettings settings,
        Func<string, double>? distanceOf)
    {
        var subEntities = children.GetValueOrDefault(entity.Id, [])
            .Where(c => c.Kind == StructureKind.PoliticalEntity)
            .Select(c => ComputeLoad(c, children, buildingsByManager, residentsOf, settings, distanceOf))
            .ToList();

        var population = entity.IsCommune
            ? residentsOf(entity.Id)
            : settings.ExtensionCountedShare * subEntities.Sum(l => l.CountedPopulation);

        var buildings = buildingsByManager.GetValueOrDefault(entity.Id, []);
        var direct = buildings.Count(b => b.Ownership == Ownership.Public);
        var autonomous = buildings.Count - direct;

        var distance = distanceOf?.Invoke(entity.Id) ?? 0;
        var withoutPopulation = Ratio(direct, settings.ExtensionBuildingCapacity)
            + settings.ExtensionAutonomousWeight * Ratio(autonomous, settings.ExtensionBuildingCapacity)
            + settings.ExtensionChildWeight * subEntities.Sum(l => l.ChargeWithoutPopulation)
            + distance;
        var charge = Ratio(population, settings.ExtensionPopulationCapacity) + withoutPopulation;
        var load = new EntityLoad(entity.Id, population, direct, autonomous, withoutPopulation, charge,
            charge <= 1 ? 1 : 1 / charge, distance);
        _loads[entity.Id] = load;
        return load;
    }

    private static double Ratio(double value, double capacity) => capacity > 0 ? value / capacity : 0;
}
