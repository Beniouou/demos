using Demos.Application.Ports;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Partie de départ, décrite dans game/data/partie-depart.json : un État, ses communes, des bâtiments tirés du
/// catalogue (buildings.json), des classes, et les lois de départ choisies à la création de la partie. Les propriétaires sont arbitraires, pour tester les trois
/// circuits d'argent, et se changent dans le menu d'administration.
/// </summary>
public static class PrototypeScenario
{
    public const string FirstCommuneId = "commune-1";

    public const string StateId = "etat";

    /// <param name="startingLaws">Les lois de départ : celles du départ préréglé si rien n'est donné.</param>
    public static Simulation Create(IContentSource content, StartingLaws? startingLaws = null)
    {
        // Les réglages d'équilibrage d'abord : le catalogue, les prix et le monde en dépendent.
        Balance.Current = content.LoadBalance();

        var types = content.LoadBuildingTypes().ToDictionary(t => t.Id);
        var scenario = content.LoadScenario();

        var structures = new List<Structure> { new(StateId, "État", StructureKind.PoliticalEntity, null) };
        structures.AddRange(scenario.Communes.Select(c => new Structure(c.Id, c.Name, StructureKind.PoliticalEntity, StateId)));
        structures.AddRange(scenario.Buildings.Select(b => BuildingOf(b, types, scenario.HeatingFuel)));

        var classes = scenario.Classes.Select(c =>
        {
            var socialClass = new SocialClass(c.Id, c.Name, c.Rank, new Ideology(c.Hierarchy, c.Production, c.Property), c.Population)
            {
                Wealth = c.Wealth,
            };
            socialClass.SetResidence(c.Commune, 1);
            return socialClass;
        }).ToList();

        // Prix de départ : 30 % de bénéfice pour le bâtiment de référence de chaque produit, à plein régime.
        var catalogue = types.Values.ToList();
        var world = new World(Pricing.WithReferencePrices(content.LoadProducts(), catalogue), catalogue, structures, classes, [],
            content.LoadLawCatalogue());

        // Trésor de départ gonflé à la main, en attendant de régler les recettes de l'État : il permet de payer la
        // dotation d'une première nouvelle commune.
        world.Treasury.Deposit(scenario.StartingTreasury);

        // Chaque bâtiment commence avec sa réserve de chauffage pleine (120 jours de chauffage normal) : la partie
        // commence le 1er janvier, en plein hiver.
        foreach (var building in world.Structures.Values.Where(s => s.Kind == StructureKind.Building && world.TierOf(s) == 1))
            world.FuelStockOf(building).Add(building.HeatingProductId ?? Energy.DefaultHeating, Energy.FuelReserveOf(building));

        // Chaque entreprise privée ou coopérative part avec sa réserve (30 jours de salaires et d'énergie), prise à ses
        // propriétaires.
        foreach (var building in world.Structures.Values.Where(Companies.HasCompanyFund))
        {
            if (building.OwnerClassId is not { } ownerId || !world.ClassesById.TryGetValue(ownerId, out var owner))
                continue;

            var reserve = Math.Min(Companies.ReserveOf(building, world.PriceOf, Balance.Current.StartingReserveDays), Math.Max(0, owner.Wealth));
            owner.Wealth -= reserve;
            world.CompanyFundOf(building).Deposit(reserve);
        }

        var simulation = new Simulation(world, new GameClock(new DateOnly(1900, 1, 1)));
        // Les communes de départ reçoivent la même réserve de chantier qu'une commune fondée en cours de partie.
        foreach (var commune in world.Communes)
            simulation.GiveFoundingMaterials(commune.Id);

        var chosen = (startingLaws ?? StartingLaws.Preset) switch
        {
            { Mode: StartingLawsMode.Preset } => scenario.PresetLaws ?? [],
            { Mode: StartingLawsMode.Chosen, Chosen: { } laws } => laws,
            _ => [],
        };
        foreach (var law in chosen)
        {
            if (world.LawCatalogue.TemplateOf(law.Template) is not { } template
                || !simulation.EnactAtStart(template.ToLaw(world.LawCommission.NewId("loi"), law.Level)))
                throw new InvalidDataException($"Loi de départ impossible : « {law.Template} ».");
        }

        // Sans loi qui les autorise, les entreprises privées de départ sont nationalisées dès le début.
        simulation.NationalizeForbiddenCompanies();

        return simulation;
    }

    /// <summary>Un bâtiment de départ : son type du catalogue, avec ce que la partie de départ change.</summary>
    private static Structure BuildingOf(ScenarioBuilding building, IReadOnlyDictionary<string, BuildingType> types, string? heatingFuel)
    {
        if (!types.TryGetValue(building.Type, out var type))
            throw new InvalidDataException($"partie-depart.json : le bâtiment « {building.Id} » a un type inconnu, « {building.Type} ».");

        var structure = building.Product is { } product
            ? type.BuildFor(product, building.Id, building.Commune, building.Ownership, building.Owner, building.Name)
            : type.Build(building.Id, building.Commune, building.Ownership, building.Owner, building.Name);

        return structure with
        {
            Jobs = building.Jobs ?? structure.Jobs,
            HousingPlaces = building.HousingPlaces ?? structure.HousingPlaces,
            DailyRent = building.DailyRent ?? structure.DailyRent,
            HeatingProductId = heatingFuel ?? structure.HeatingProductId,
        };
    }
}
