using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Point d'entrée du moteur : il appelle AdvanceDay au rythme choisi par le joueur.
/// La simulation calcule l'économie chaque jour, les variables lentes chaque saison
/// et les bilans chaque année. Chaque commune est une petite économie : son marché et son stockage, où
/// arrive aussi la production de l'État, sa caisse, et des échanges avec les autres communes.
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
    /// Action du joueur : construire une structure en payant ses matériaux avec ce que l'État a produit dans le
    /// stockage de sa commune, en les achetant avec l'argent du trésor, ou les deux. Renvoie false, sans rien construire ni dépenser, si c'est
    /// impossible.
    /// </summary>
    public bool TryBuild(Structure structure, IReadOnlyList<MaterialCost> cost, PaymentMode mode)
    {
        if (World.Structures.ContainsKey(structure.Id)
            || structure.ParentId is not { } parentId
            || !World.Structures.TryGetValue(parentId, out var parent)
            || World.CommuneOf(parent) is not { } communeId
            || !Construction.TryPay(cost, mode, Market.StateSellerId(communeId), World.Treasury, World.MarketOf(communeId)))
            return false;

        World.AddStructure(structure);
        return true;
    }

    /// <summary>
    /// Outil d'administration : remplace les paramètres d'un bâtiment (propriétaire, emplois, salaire, production,
    /// places, loyer). Les effets se voient dès le jour suivant.
    /// </summary>
    public void UpdateBuilding(Structure building) => World.UpdateStructure(building);

    /// <summary>Proposition : vendre un bâtiment public coûte 10 de légitimité libérale (LI).</summary>
    public const double SaleLegitimacyCost = 10;

    /// <summary>Pôle d'une vente au privé : libéral (LI).</summary>
    public static readonly Orientation SalePole = new(IdeologicalAxis.Property, Pole.B);

    /// <summary>Bénéfice attendu par jour d'un bâtiment, avec son personnel actuel.</summary>
    public double DailyProfitOf(Structure building) =>
        Privatization.DailyProfitOf(building, World.Workers.GetValueOrDefault(building.Id), World.Classes, World.PriceOf);

    /// <summary>
    /// Action du joueur : vendre un bâtiment public à une classe de sa commune, au prix choisi. L'argent va au trésor,
    /// la classe devient propriétaire (privé ou coopérative), et l'entreprise ouvre sa propre caisse, vide au départ :
    /// ses ventes la remplissent et elle paie désormais les salaires. La vente coûte de la légitimité LI, et reste dans
    /// la commune comme une loi libérale : les habitants libéraux l'acceptent, les protectionnistes la rejettent.
    /// Ne change rien si la vente est impossible.
    /// </summary>
    public SaleOutcome TrySellBuilding(string buildingId, string buyerClassId, double price)
    {
        var building = World.Structures[buildingId];
        var buyer = World.ClassesById[buyerClassId];
        var communeId = World.CommuneOf(building);

        var outcome = Privatization.Check(building, communeId, buyer, price, DailyProfitOf(building));
        if (outcome != SaleOutcome.Sold)
            return outcome;
        if (!World.Legitimacy.TrySpend(SalePole, SaleLegitimacyCost))
            return SaleOutcome.NotEnoughLegitimacy;

        buyer.Wealth -= price;
        World.Treasury.Deposit(price);
        World.UpdateStructure(building with { Ownership = Privatization.OwnershipFor(buyer), OwnerClassId = buyer.Id });
        World.Wages.Forget(building.Id);
        World.RemoveLaw($"nationalisation-{building.Id}");
        World.AddLaw(new Law($"privatisation-{building.Id}", $"Privatisation : {building.Name}", communeId!, [SalePole]));
        return SaleOutcome.Sold;
    }

    /// <summary>Pôle d'une nationalisation : protectionniste (PR).</summary>
    public static readonly Orientation NationalizationPole = new(IdeologicalAxis.Property, Pole.A);

    /// <summary>Une entreprise est en difficulté quand elle a versé moins de 80 % des salaires dus sur 30 jours.</summary>
    public bool IsStruggling(Structure building) => World.Wages.IsStruggling(building.Id);

    public double NationalizationPriceOf(Structure building, NationalizationMode mode) =>
        Nationalization.PriceOf(mode, IsStruggling(building), DailyProfitOf(building));

    public double NationalizationLegitimacyCostOf(Structure building, NationalizationMode mode) =>
        Nationalization.LegitimacyCostOf(mode, IsStruggling(building));

    /// <summary>
    /// Action du joueur : reprendre un bâtiment privé ou coopératif, par rachat (le trésor paie le propriétaire) ou
    /// par expropriation (sans payer). Le propriétaire ne peut pas refuser. Le bâtiment redevient public, et la
    /// nationalisation reste dans la commune comme une loi protectionniste ; la loi de privatisation, s'il y en avait
    /// une, disparaît. La caisse de l'entreprise ferme : en cas de rachat, ce qu'il y restait revient aux
    /// propriétaires ; en cas d'expropriation, à la caisse de la commune. Ne change rien si c'est impossible.
    /// </summary>
    public NationalizationOutcome TryNationalize(string buildingId, NationalizationMode mode)
    {
        var building = World.Structures[buildingId];
        if (building.Ownership == Ownership.Public || World.CommuneOf(building) is not { } communeId)
            return NationalizationOutcome.NotPrivate;

        var price = NationalizationPriceOf(building, mode);
        var legitimacyCost = NationalizationLegitimacyCostOf(building, mode);
        if (World.Treasury.Balance < price)
            return NationalizationOutcome.TreasuryTooPoor;
        if (!World.Legitimacy.TrySpend(NationalizationPole, legitimacyCost))
            return NationalizationOutcome.NotEnoughLegitimacy;

        World.Treasury.TrySpend(price);
        var owner = building.OwnerClassId is { } ownerId ? World.ClassesById.GetValueOrDefault(ownerId) : null;
        if (owner is not null)
            owner.Wealth += price;

        // La caisse de l'entreprise ferme : rachetée, son argent revient aux propriétaires ; expropriée, à la commune.
        var remaining = World.CloseCompanyFund(building.Id);
        if (mode == NationalizationMode.Buyout && owner is not null)
            owner.Wealth += remaining;
        else
            World.CommuneFundOf(communeId).Deposit(remaining);

        World.UpdateStructure(building with { Ownership = Ownership.Public, OwnerClassId = null });
        World.Wages.Forget(building.Id);
        World.RemoveLaw($"privatisation-{building.Id}");
        World.RemoveLaw($"nationalisation-{building.Id}");
        World.AddLaw(new Law($"nationalisation-{building.Id}", $"Nationalisation : {building.Name}", communeId, [NationalizationPole]));
        return NationalizationOutcome.Done;
    }

    /// <summary>
    /// Action du joueur : bâtir un camp de base, qui fonde une nouvelle commune rattachée à l'État.
    /// Il coûte des ressources, prises dans la commune qui l'envoie, de la légitimité (dans le pôle choisi), et la
    /// dotation que l'État verse du trésor à la caisse de la nouvelle commune. Le pôle choisi devient la charte de
    /// fondation de la commune : une loi de ce pôle, qui attire les classes du même pôle et repousse celles du pôle opposé.
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

        if (!Construction.TryPay(cost, mode, Market.StateSellerId(payingCommuneId), World.Treasury, World.MarketOf(payingCommuneId)))
        {
            World.Treasury.Deposit(grant);
            return null;
        }

        World.Legitimacy.TrySpend(legitimacyPole, legitimacyCost);
        World.CommuneFundOf(commune.Id).Deposit(grant);
        World.AddStructure(commune);
        World.AddStructure(camp);
        World.AddLaw(new Law($"charte-{commune.Id}", $"Charte de fondation : {name}", commune.Id, [legitimacyPole], [LawRule.FoundingCharter]));
        return commune;
    }

    /// <summary>Dotation versée à la fondation : 60 jours de salaires de production du camp de base.</summary>
    public static double FoundingGrantOf(BuildingType camp) => FoundingGrantDays * camp.Jobs * camp.DailyWage;

    // Ordre du document de règles : emplois, production et salaires, énergie des bâtiments, échanges entre communes,
    // logement, école, consommation, ventes, bénéfices des entreprises versés aux propriétaires, impôts, légitimité,
    // puis perte de ce qui déborde du stockage ou se périme. L'énergie prise après la production d'un jour règle la
    // production du lendemain, et le logement et l'école du jour même.
    // Les décisions du joueur et les événements restent à écrire.
    private void RunDailyCalculations()
    {
        var communeIds = World.Communes.Select(c => c.Id).ToList();

        World.Workers = Employment.Assign(World.Structures, World.Classes);
        World.PollutionByCommune = Pollution.PerResidentByCommune(World.Structures, World.Workers, World.Classes);
        WorkDay.Run(World.Structures, World.Workers, World.Classes, World.Laws, World.Treasury, World.MarketOf, World.CommuneFundOf, World.Wages,
            World.CompanyFundOf, World.EnergyEffectOf);
        World.EnergyShares = Energy.RunDaily(World.Structures, World.TierOf, Clock.Season, World.MarketOf, World.Treasury, World.CommuneFundOf,
            World.CompanyFundOf, World.ClassesById);
        Trade.RunDaily(communeIds, World.Classes, World.Products, World.MarketOf,
            (communeId, productId) => World.StorageOf(communeId).FreeFor(productId));
        Housing.RunDaily(World.Structures, World.Classes, World.Treasury, World.CompanyFundOf, World.EnergyEffectOf);
        Education.RunDaily(World.Structures, World.Classes, World.Treasury, World.CompanyFundOf, World.EnergyEffectOf);

        foreach (var communeId in communeIds)
            Consumption.RunDaily(World.Classes.Where(c => c.CommuneId == communeId).ToList(), World.Products, World.MarketOf(communeId));

        PaySellers();
        Companies.PayDividends(World.Structures, World.ClassesById, World.CompanyFundOf, World.PriceOf);
        Taxation.CollectDaily(World.Classes, World.StateTaxRate, World.CommuneTaxRateOf, World.Treasury, World.CommuneFundOf, World.Laws, World.Structures);
        LawEffects.ApplyDaily(World.Classes, World.Laws, World.Structures, World.Legitimacy);
        Storage.ApplyDaily(communeIds, World.Structures, World.Products, World.MarketOf);
    }

    private void PaySellers()
    {
        foreach (var market in World.Markets.Values)
        {
            foreach (var (sellerId, amount) in market.CollectEarnings())
            {
                // Ce que l'État a produit et vendu dans une commune revient à la caisse de cette commune.
                if (Market.CommuneOfStateSeller(sellerId) is { } communeId)
                    World.CommuneFundOf(communeId).Deposit(amount);
                // Une entreprise touche ses ventes dans sa caisse ; si elle est devenue publique entre-temps, elles
                // vont à la caisse de sa commune.
                else if (Market.BuildingOfCompanySeller(sellerId) is { } buildingId && World.Structures.TryGetValue(buildingId, out var building))
                {
                    if (Companies.HasCompanyFund(building))
                        World.CompanyFundOf(building).Deposit(amount);
                    else if (World.CommuneOf(building) is { } buildingCommune)
                        World.CommuneFundOf(buildingCommune).Deposit(amount);
                }
                else if (World.ClassesById.TryGetValue(sellerId, out var seller))
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

        foreach (var created in Migration.RunSeasonal(World.Structures, World.Classes, World.Laws))
            World.AddClass(created);

        SeasonPassed?.Invoke();
    }
}
