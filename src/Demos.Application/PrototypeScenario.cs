using Demos.Application.Ports;
using Demos.Domain.Economy;
using Demos.Domain.Map;
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
    /// <param name="exile">
    /// Le départ d'exil (décidé le 27 novembre 2026) : la partie de partie-exil.json, sur une terre d'exil tirée au
    /// hasard, avec La Promesse si elle est donnée ; null pour le départ préréglé (départ riche du bac à sable).
    /// </param>
    public static Simulation Create(IContentSource content, StartingLaws? startingLaws = null, ExileStart? exile = null)
    {
        // Les réglages d'équilibrage d'abord : le catalogue, les prix et le monde en dépendent.
        Balance.Current = content.LoadBalance();

        var types = content.LoadBuildingTypes().ToDictionary(t => t.Id);
        var scenario = exile is null
            ? content.LoadScenario()
            : content.LoadExileScenario() ?? throw new InvalidDataException("partie-exil.json manque : pas de départ d'exil.");
        var map = content.LoadMap();
        // La terre d'exil est tirée au hasard parmi celles de la carte : on ne choisit pas où l'on échoue. Une carte sans
        // terre d'exil garde sa commune de base.
        var exileSite = exile is not null && map?.AllExileSites is { Count: > 0 } sites ? sites[new Random(exile.Seed).Next(sites.Count)] : null;

        // La capitale (chef-lieu de l'État) est la première commune de la partie de départ.
        var firstCommuneId = scenario.Communes.FirstOrDefault()?.Id;
        var structures = new List<Structure> { new(StateId, "État", StructureKind.PoliticalEntity, null, SeatId: firstCommuneId) };
        structures.AddRange(scenario.Communes.Select(c => new Structure(
            c.Id, exileSite is not null && c.Id == firstCommuneId ? exileSite.Name : c.Name, StructureKind.PoliticalEntity, StateId)));
        structures.AddRange(scenario.Buildings.Select(b => BuildingOf(b, types, scenario.HeatingFuel)));

        var classes = scenario.Classes.Select(c =>
        {
            var ideology = new Ideology(c.Hierarchy, c.Production, c.Property);
            var (id, name) = (c.Id, c.Name);
            if (exile?.Promise is { } promise)
            {
                // Au départ, tous les exilés partagent La Promesse : sur ses axes, chacun est au pôle promis ; sur les axes
                // libres, chaque rang garde son pôle (décidé le 27 novembre 2026). Le nom suit l'idéologie.
                foreach (var pole in promise.Orientations)
                    ideology = ideology.With(pole.Axis, pole.Pole);
                var named = ClassTransitions.Create(c.Rank, ideology, c.Commune);
                (id, name) = (named.Id, named.Name);
            }

            var socialClass = new SocialClass(id, name, c.Rank, ideology, c.Population)
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

        // Chaque groupe commence avec sa possession visée d'objets durables (vêtements, meubles ; décidé le 20 novembre 2026).
        Consumption.EquipFully(world.Classes, world.Products, scenario.PossessionShare);
        // Chaque bâtiment de départ qui a une recette reçoit son stock d'outils visé au complet.
        foreach (var building in world.Structures.Values.Where(s => Tools.TargetOf(s) > 0))
            Tools.Equip(building, world.ToolStockOf(building), scenario.ToolShare);

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

        if (map is not null)
        {
            foreach (var commune in map.AllCommunes)
                world.SetCommunePosition(commune.Id, commune.Position);
            if (exileSite is not null && firstCommuneId is not null)
                world.SetCommunePosition(firstCommuneId, exileSite.Position);
            world.SetMapZones([.. map.AllBiomes, .. map.AllResourceZones]);
            // Les lacs sont de l'eau pour la fondation et les routes (24 novembre 2026).
            var relief = content.LoadRelief();
            relief?.SetLakes(map.AllLakes);
            world.SetRelief(relief);
            PlaceExtractionBuildings(world);
        }

        var simulation = new Simulation(world, new GameClock(new DateOnly(1900, 1, 1)));
        // Les communes de départ reçoivent la même réserve de chantier qu'une commune fondée en cours de partie.
        foreach (var commune in world.Communes)
            simulation.GiveFoundingMaterials(commune.Id);
        if (firstCommuneId is not null)
            GiveProvisions(world, firstCommuneId, scenario);

        if (exile?.Promise is { } chosen
            && simulation.ChoosePromise(chosen, exile.Engagements, exile.PeopleName) is { } promiseProblem)
            throw new InvalidDataException($"La Promesse choisie ne va pas : {promiseProblem}.");
        if (world.PromiseState is { } sworn && exile?.Signature is { } signature)
            sworn.Signature = signature;

        var startLaws = (startingLaws ?? StartingLaws.Preset) switch
        {
            { Mode: StartingLawsMode.Preset } => scenario.PresetLaws ?? [],
            { Mode: StartingLawsMode.Chosen, Chosen: { } laws } => laws,
            _ => [],
        };
        foreach (var law in startLaws)
        {
            if (world.LawCatalogue.TemplateOf(law.Template) is not { } template)
                throw new InvalidDataException($"Loi de départ impossible : « {law.Template} ».");
            // Une loi de l'An I contraire à La Promesse ou qui romprait un engagement est refusée (grisée à l'écran) : La
            // Promesse et les lois fondatrices sont signées le même jour (décidé les 27 et 28 novembre 2026).
            if (world.PromiseState is { } promiseState
                && FoundingLawProblem(world.LawCatalogue, law, promiseState.Promise, promiseState.Engagements) is { } problem)
                throw new InvalidDataException($"La loi de départ « {template.Name} » est refusée : {problem}.");
            if (!simulation.EnactAtStart(template.ToLaw(world.LawCommission.NewId("loi"), law.Level)))
                throw new InvalidDataException($"Loi de départ impossible : « {law.Template} ».");
        }

        if (world.PromiseState is { } signed)
            signed.FoundingLaws = startLaws.Select(l => world.LawCatalogue.TemplateOf(l.Template)!.Name).ToList();

        // Sans loi qui les autorise, les entreprises privées de départ sont nationalisées dès le début.
        simulation.NationalizeForbiddenCompanies();

        return simulation;
    }

    /// <summary>
    /// Les engagements qu'une loi de départ romprait (décidé le 27 novembre 2026 : elle est alors grisée, avec la raison).
    /// Jugée comme une nouvelle loi votée sur une nation sans loi : ce que l'écran de création peut savoir avant la partie.
    /// </summary>
    public static IReadOnlyList<EngagementDefinition> FoundingLawBreaches(
        LawCatalogue catalogue, ScenarioLaw law, IEnumerable<EngagementDefinition> engagements) =>
        FoundingDraft(catalogue, law) is var (book, draft) && draft is not null
            ? Simulation.EngagementsBrokenBy(book, engagements, draft, null)
            : [];

    /// <summary>
    /// Les pôles opposés à La Promesse qu'une loi de départ favorise (décidé le 28 novembre 2026 : elle est alors grisée
    /// aussi, même sans rompre l'engagement choisi ; le jour du serment, tout le peuple partage La Promesse).
    /// </summary>
    public static IReadOnlyList<Orientation> FoundingLawContraryPoles(LawCatalogue catalogue, ScenarioLaw law, PromiseDefinition promise)
    {
        var (book, draft) = FoundingDraft(catalogue, law);
        if (draft is null)
            return [];

        var opposites = promise.Orientations.Select(p => p.Opposite).ToHashSet();
        return LawCost.MovementsOf(book, null, draft.Law).SelectMany(m => m.Poles).Where(opposites.Contains).Distinct().ToList();
    }

    /// <summary>
    /// Pourquoi une loi de l'An I est refusée (grisée à l'écran), ou null : elle favorise un pôle opposé à La Promesse,
    /// ou elle rompt un engagement choisi.
    /// </summary>
    public static string? FoundingLawProblem(
        LawCatalogue catalogue, ScenarioLaw law, PromiseDefinition promise, IEnumerable<EngagementDefinition> engagements)
    {
        if (FoundingLawContraryPoles(catalogue, law, promise) is [var contrary, ..])
            return $"contraire à La Promesse (pôle {contrary.Opposite.Code})";
        return FoundingLawBreaches(catalogue, law, engagements) is [var broken, ..] ? $"rompt l'engagement « {broken.Text} »" : null;
    }

    /// <summary>Une loi de départ jugée comme une nouvelle loi de l'État, sur une nation sans loi.</summary>
    private static (LawBook Book, LawDraft? Draft) FoundingDraft(LawCatalogue catalogue, ScenarioLaw law)
    {
        var state = new Structure(StateId, "État", StructureKind.PoliticalEntity, null);
        var book = new LawBook(catalogue, [], new Dictionary<string, Structure> { [StateId] = state });
        return catalogue.TemplateOf(law.Template) is { } template
            ? (book, new LawDraft("loi-an-1", LawChangeKind.Enact, template.ToLaw("loi-an-1", law.Level)))
            : (book, null);
    }

    /// <summary>
    /// Les vivres et les charrettes du départ d'exil : chaque produit, en jours de nourriture de tout le peuple, est mis en
    /// vente par l'État de la première commune (décidé le 27 novembre 2026 : 5 jours de farine, ce que tient le grenier).
    /// </summary>
    private static void GiveProvisions(World world, string communeId, ScenarioDefinition scenario)
    {
        var dailyFood = world.Classes.Sum(c => c.Population * Consumption.NeedsOf(c.Rank).GetValueOrDefault(NeedCategory.Food));
        foreach (var provision in scenario.Provisions ?? [])
            world.MarketOf(communeId).Offer(provision.Product, dailyFood * provision.Days, Market.StateSellerId(communeId));
        if (scenario.Carts > 0)
        {
            world.CartStockOf(communeId).Add(Carts.ProductId, scenario.Carts);
            world.SetWantedCarts(communeId, scenario.Carts);
        }
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

    /// <summary>
    /// Pose chaque bâtiment d'extraction de départ sur la zone de la carte qui correspond la plus proche de sa commune, à
    /// portée (décidé le 10 novembre 2026). Un bâtiment sans zone à portée reste sans position.
    /// </summary>
    private static void PlaceExtractionBuildings(World world)
    {
        foreach (var building in world.Structures.Values.Where(s => s.Kind == StructureKind.Building))
        {
            var kinds = ExtractionSites.ZonesFor(building.TypeId, building.Production?.ProductId);
            if (kinds.Count == 0 || world.CommuneOf(building) is not { } communeId
                || !world.CommunePositions.TryGetValue(communeId, out var commune))
                continue;

            if (ExtractionSites.NearestSite(kinds, commune, Balance.Current.ExtractionMaxDistance, world.MapZones) is { } site)
                world.SetBuildingPosition(building.Id, site);
        }
    }
}
