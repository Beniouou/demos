using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Point d'entrée du moteur : il appelle AdvanceDay au rythme choisi par le joueur.
/// La simulation calcule l'économie chaque jour, les variables lentes chaque saison
/// et les bilans chaque année.
/// </summary>
public sealed class Simulation(World world, GameClock clock)
{
    /// <summary>Type du catalogue qui fonde une commune : ses emplois et ses places attirent les premiers habitants.</summary>
    public const string BaseCampTypeId = "camp-de-base";

    public World World { get; } = world;
    public GameClock Clock { get; } = clock;

    public event Action? SeasonPassed;
    public event Action? YearPassed;

    public void AdvanceDay()
    {
        var change = Clock.AdvanceDay();

        RunDailyCalculations();

        if (change.HasFlag(CalendarChange.NewSeason))
            RunSeasonalCalculations();

        if (change.HasFlag(CalendarChange.NewYear))
            YearPassed?.Invoke();
    }

    /// <summary>
    /// Action du joueur : construire une structure en payant ses matériaux avec le stock de l'État,
    /// l'argent du trésor, ou les deux. Renvoie false, sans rien construire ni dépenser, si c'est impossible.
    /// </summary>
    public bool TryBuild(Structure structure, IReadOnlyList<MaterialCost> cost, PaymentMode mode)
    {
        if (World.Structures.ContainsKey(structure.Id)
            || !Construction.TryPay(cost, mode, World.StateStock, World.Treasury, World.Market))
            return false;

        World.AddStructure(structure);
        return true;
    }

    /// <summary>
    /// Outil d'administration : remplace les paramètres d'un bâtiment (propriétaire, emplois, salaire, production,
    /// places, loyer). Les effets se voient dès le jour suivant.
    /// </summary>
    public void UpdateBuilding(Structure building) => World.UpdateStructure(building);

    /// <summary>
    /// Action du joueur : bâtir un camp de base, qui fonde une nouvelle commune rattachée à l'État.
    /// Il coûte des ressources et de la légitimité (dans le pôle choisi par le joueur).
    /// Renvoie la nouvelle commune, ou null, sans rien dépenser, si c'est impossible.
    /// </summary>
    public Structure? TryFoundCommune(
        string name,
        IReadOnlyList<MaterialCost> cost,
        PaymentMode mode,
        Orientation legitimacyPole,
        double legitimacyCost)
    {
        var state = World.Structures.Values.Single(s => s.IsNational);
        var number = World.Communes.Count() + 1;
        var commune = new Structure($"commune-{number}", name, StructureKind.PoliticalEntity, state.Id);
        var campType = World.BuildingTypes[BaseCampTypeId];
        var camp = campType.Build($"camp-de-base-{number}", commune.Id, name: $"{campType.Name} ({name})");

        if (World.Structures.ContainsKey(commune.Id)
            || World.Legitimacy[legitimacyPole] < legitimacyCost
            || !Construction.TryPay(cost, mode, World.StateStock, World.Treasury, World.Market))
            return null;

        World.Legitimacy.TrySpend(legitimacyPole, legitimacyCost);
        World.AddStructure(commune);
        World.AddStructure(camp);
        return commune;
    }

    // Ordre du document de règles : emplois, production et salaires, logement, école, consommation, ventes,
    // impôts, légitimité. Les décisions du joueur et les événements restent à écrire.
    private void RunDailyCalculations()
    {
        World.Workers = Employment.Assign(World.Structures, World.Classes);
        WorkDay.Run(World.Structures, World.Workers, World.Classes, World.Laws, World.Treasury, World.StateStock, World.Market);
        Housing.RunDaily(World.Structures, World.Classes, World.Treasury);
        Education.RunDaily(World.Structures, World.Classes, World.Treasury);
        Consumption.RunDaily(World.Classes, World.Products, World.Market);
        PaySellers();
        Taxation.CollectDaily(World.Classes, World.DailyTaxRate, World.Treasury);
        LawEffects.ApplyDaily(World.Classes, World.Laws, World.Structures, World.Legitimacy);
    }

    private void PaySellers()
    {
        foreach (var (sellerId, amount) in World.Market.CollectEarnings())
        {
            if (World.ClassesById.TryGetValue(sellerId, out var seller))
                seller.Wealth += amount;
        }
    }

    // Prévu : climat, autres passages entre classes, revendications, recherche.
    private void RunSeasonalCalculations()
    {
        foreach (var socialClass in World.Classes)
            socialClass.Grow();

        foreach (var created in ClassTransitions.RunSeasonal(World.Classes))
            World.AddClass(created);

        Migration.RunSeasonal(World.Structures, World.Classes);

        SeasonPassed?.Invoke();
    }
}
