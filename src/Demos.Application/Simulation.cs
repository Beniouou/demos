using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Point d'entrée du moteur : il appelle AdvanceDay au rythme choisi par le joueur.
/// La simulation calcule l'économie chaque jour, les variables lentes chaque saison
/// et les bilans chaque année. Chaque commune est une petite économie : son marché, son stock d'État,
/// son stockage, et des échanges avec les autres communes.
/// </summary>
public sealed class Simulation(World world, GameClock clock)
{
    /// <summary>Type du catalogue qui fonde une commune : ses emplois et ses places attirent les premiers habitants.</summary>
    public const string BaseCampTypeId = "camp-de-base";

    /// <summary>
    /// Proposition : la dotation d'une nouvelle commune couvre 60 jours de salaires de son camp de base, le temps
    /// que ses premiers bâtiments de production produisent.
    /// </summary>
    public const int FoundingGrantDays = 60;

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
            RunYearlyCalculations();
    }

    /// <summary>
    /// Action du joueur : construire une structure en payant ses matériaux avec le stock de l'État et le marché
    /// de sa commune, l'argent du trésor, ou les deux. Renvoie false, sans rien construire ni dépenser, si c'est
    /// impossible.
    /// </summary>
    public bool TryBuild(Structure structure, IReadOnlyList<MaterialCost> cost, PaymentMode mode)
    {
        if (World.Structures.ContainsKey(structure.Id)
            || structure.ParentId is not { } parentId
            || !World.Structures.TryGetValue(parentId, out var parent)
            || World.CommuneOf(parent) is not { } communeId
            || !Construction.TryPay(cost, mode, World.StateStockOf(communeId), World.Treasury, World.MarketOf(communeId)))
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
    /// Il coûte des ressources, prises dans la commune qui l'envoie, de la légitimité (dans le pôle choisi), et la
    /// dotation que l'État verse du trésor à la caisse de la nouvelle commune.
    /// Renvoie la nouvelle commune, ou null, sans rien dépenser, si c'est impossible.
    /// </summary>
    public Structure? TryFoundCommune(
        string name,
        string payingCommuneId,
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
        var grant = FoundingGrantOf(campType);

        if (World.Structures.ContainsKey(commune.Id)
            || World.Legitimacy[legitimacyPole] < legitimacyCost
            || !World.Treasury.TrySpend(grant))
            return null;

        if (!Construction.TryPay(cost, mode, World.StateStockOf(payingCommuneId), World.Treasury, World.MarketOf(payingCommuneId)))
        {
            World.Treasury.Deposit(grant);
            return null;
        }

        World.Legitimacy.TrySpend(legitimacyPole, legitimacyCost);
        World.CommuneFundOf(commune.Id).Deposit(grant);
        World.AddStructure(commune);
        World.AddStructure(camp);
        return commune;
    }

    /// <summary>Dotation versée à la fondation : 60 jours de salaires de production du camp de base.</summary>
    public static double FoundingGrantOf(BuildingType camp) => FoundingGrantDays * camp.Jobs * camp.DailyWage;

    // Ordre du document de règles : emplois, production et salaires, échanges entre communes, logement, école,
    // consommation, ventes, impôts, légitimité, puis perte de ce qui déborde du stockage.
    // Les décisions du joueur et les événements restent à écrire.
    private void RunDailyCalculations()
    {
        var communeIds = World.Communes.Select(c => c.Id).ToList();

        World.Workers = Employment.Assign(World.Structures, World.Classes);
        World.PollutionByCommune = Pollution.PerResidentByCommune(World.Structures, World.Workers, World.Classes);
        WorkDay.Run(World.Structures, World.Workers, World.Classes, World.Laws, World.Treasury, World.StateStockOf, World.MarketOf, World.CommuneFundOf);
        Trade.RunDaily(communeIds, World.Classes, World.Products, World.MarketOf,
            (communeId, productId) => World.StorageOf(communeId).FreeFor(productId));
        Housing.RunDaily(World.Structures, World.Classes, World.Treasury);
        Education.RunDaily(World.Structures, World.Classes, World.Treasury);

        foreach (var communeId in communeIds)
            Consumption.RunDaily(World.Classes.Where(c => c.CommuneId == communeId).ToList(), World.Products, World.MarketOf(communeId));

        PaySellers();
        Taxation.CollectDaily(World.Classes, World.StateTaxRate, World.CommuneTaxRateOf, World.Treasury, World.CommuneFundOf, World.Laws, World.Structures);
        LawEffects.ApplyDaily(World.Classes, World.Laws, World.Structures, World.Legitimacy);
        Storage.ApplyDaily(communeIds, World.Structures, World.Products, World.MarketOf, World.StateStockOf);
    }

    private void PaySellers()
    {
        foreach (var market in World.Markets.Values)
        {
            foreach (var (sellerId, amount) in market.CollectEarnings())
            {
                if (World.ClassesById.TryGetValue(sellerId, out var seller))
                    seller.ReceiveIncome(amount);
            }
        }
    }

    // Promotions par l'éducation, puis bilan fiscal et statistique (à écrire).
    private void RunYearlyCalculations()
    {
        foreach (var created in ClassTransitions.RunYearly(World.Classes))
            World.AddClass(created);

        YearPassed?.Invoke();
    }

    // Passages entre classes, commune par commune, puis migration. Prévu : climat, revendications, recherche.
    private void RunSeasonalCalculations()
    {
        foreach (var socialClass in World.Classes)
            socialClass.Grow();

        foreach (var created in ClassTransitions.RunSeasonal(World.Classes, World.Structures, World.PollutionByCommune))
            World.AddClass(created);

        foreach (var created in Migration.RunSeasonal(World.Structures, World.Classes))
            World.AddClass(created);

        SeasonPassed?.Invoke();
    }
}
