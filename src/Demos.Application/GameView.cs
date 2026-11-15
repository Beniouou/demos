using Demos.Domain.Economy;
using Demos.Domain.Map;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Port d'entrée en lecture seule : ce que l'interface du joueur peut voir de la partie. Chaque valeur est une copie
/// prise au moment de l'appel (nombres, textes, records) : l'interface ne reçoit aucun objet du moteur, elle ne peut
/// donc rien y changer. Lire ne crée rien non plus (ni caisse, ni marché). Les actions du joueur passent par
/// <see cref="Simulation"/>.
/// </summary>
public sealed class GameView(Simulation simulation)
{
    private World World => simulation.World;

    public DateOnly Today => simulation.Clock.Today;
    public Season Season => simulation.Clock.Season;

    public double TreasuryBalance => World.Treasury.Balance;
    public double StateTaxRate => World.StateTaxRate;

    public double LegitimacyOf(Orientation pole) => World.Legitimacy[pole];

    public IReadOnlyList<CommuneView> Communes() => World.Communes.Select(DescribeCommune).ToList();

    /// <summary>Positions cartographiques géoréférencées des communes dans le monde vectoriel.</summary>
    public IReadOnlyDictionary<string, Point2D> CommunePositions() => World.CommunePositions;

    /// <summary>La capitale : le chef-lieu de l'État, ou null s'il n'en a pas.</summary>
    public string? CapitalId => World.CapitalId;

    /// <summary>Les positions des bâtiments posés sur la carte (bâtiments d'extraction), chantiers compris.</summary>
    public IReadOnlyDictionary<string, Point2D> BuildingPositions() => new Dictionary<string, Point2D>(World.BuildingPositions);

    /// <summary>Les sentiers, chemins et routes : les deux communes, le niveau, le tracé (en unités de carte), la longueur (km) et la structure qui paie.</summary>
    public IReadOnlyList<RoadLink> Roads() => World.Roads.Links.ToList();

    /// <summary>
    /// Ce que les routes ont changé au dernier bilan (1er du mois : construction ; 1er janvier : aussi l'entretien). La liste
    /// est remplacée à chaque bilan : une nouvelle liste veut dire un nouveau bilan.
    /// </summary>
    public IReadOnlyList<RoadChange> RoadChanges => simulation.RoadChanges;

    /// <summary>La commune d'une structure (un bâtiment, un organisme), ou null.</summary>
    public string? CommuneOf(string structureId) =>
        World.Structures.TryGetValue(structureId, out var structure) ? World.CommuneOf(structure) : null;

    /// <summary>
    /// La charte de fondation d'une commune : le pôle payé à sa fondation, qui attire les classes de ce pôle et repousse
    /// celles du pôle opposé dans la migration. Null pour une commune de départ.
    /// </summary>
    public Orientation? CharterOf(string communeId) => World.CharterOf(communeId);

    /// <summary>Le nombre d'habitants de toutes les communes.</summary>
    public double TotalResidents => World.Communes.Sum(c => World.Residents(c.Id));

    /// <summary>Le nom d'une structure (commune, préfecture, État), ou son identifiant si elle n'existe plus.</summary>
    public string NameOf(string structureId) => World.Structures.TryGetValue(structureId, out var structure) ? structure.Name : structureId;

    /// <summary>
    /// Les échanges entre communes : pour chaque paire, ce qui a circulé (dans les deux sens) le dernier mois fini, ou
    /// depuis le début du mois si aucun mois n'est encore fini, et la perte de transport entre elles (route comprise).
    /// </summary>
    public IReadOnlyList<TradeFlowView> TradeFlows()
    {
        var traffic = World.RoadTraffic.LastMonth.Count > 0 ? World.RoadTraffic.LastMonth : World.RoadTraffic.ThisMonth;
        // Le trafic entre un bâtiment et sa commune fait des routes, mais ce n'est pas un échange entre communes.
        var communes = World.Communes.Select(c => c.Id).ToHashSet();
        return traffic.Where(t => communes.Contains(t.Key.Item1) && communes.Contains(t.Key.Item2)).Select(t => new TradeFlowView(t.Key.Item1, t.Key.Item2, t.Value,
                GeographicEconomics.TransportLoss(t.Key.Item1, t.Key.Item2, World.CommunePositions) * World.Roads.LossFactor(t.Key.Item1, t.Key.Item2)))
            .ToList();
    }

    /// <summary>
    /// Les totaux de chaque produit dans chaque commune depuis le début de la partie : produit, utilisé (achats des
    /// habitants, matières et énergie des bâtiments, chantiers), envoyé vers une autre commune, perdu ; et le stock et le
    /// prix du moment. Seuls les produits qui ont servi ou qui sont en stock y sont. Ne crée aucun marché.
    /// </summary>
    public IReadOnlyList<ProductFlowView> ProductFlows()
    {
        var flows = new List<ProductFlowView>();
        foreach (var commune in World.Communes)
        {
            var produced = World.ProducedTotalsOf(commune.Id);
            World.Markets.TryGetValue(commune.Id, out var market);
            foreach (var product in World.Products)
            {
                var tally = market?.TallyOf(product.Id) ?? default;
                var stock = market?.QuantityOf(product.Id) ?? 0;
                var made = produced.GetValueOrDefault(product.Id);
                if (made <= 0 && stock <= 0 && tally.Used <= 0 && tally.Sent <= 0 && tally.Lost <= 0)
                    continue;
                flows.Add(new ProductFlowView(commune.Id, product.Id, product.Name, made, tally.Used, tally.Sent, tally.Lost, stock,
                    market?.PriceOf(product.Id) ?? product.BasePrice));
            }
        }
        return flows;
    }

    /// <summary>
    /// La chaîne de production d'une commune : chaque bâtiment qui a une recette, ce qu'il produit, ses matières (par
    /// unité produite, ce qu'il en faudrait à plein régime le dernier jour, et le stock de la commune), ses travailleurs
    /// de production, sa production du dernier jour, et la part que ses matières et son énergie ont permise.
    /// </summary>
    public IReadOnlyList<ChainBuildingView> ProductionChain(string communeId)
    {
        World.Markets.TryGetValue(communeId, out var market);
        var names = World.Products.ToDictionary(p => p.Id, p => p.Name);
        string NameOfProduct(string id) => names.GetValueOrDefault(id, id);

        return World.Structures.Values
            .Where(s => s.Production is not null && s.Jobs > 0 && World.CommuneOf(s) == communeId)
            .Select(building =>
            {
                var recipe = building.Production!.Value;
                var workers = World.Workers.GetValueOrDefault(building.Id);
                var fullOutput = recipe.QuantityPerWorker * workers;
                return new ChainBuildingView(
                    building.Id,
                    building.Name,
                    building.Ownership,
                    recipe.ProductId,
                    NameOfProduct(recipe.ProductId),
                    World.Products.FirstOrDefault(p => p.Id == recipe.ProductId)?.Need?.Id,
                    recipe.Materials.Select(m => new ChainInputView(m.ProductId, NameOfProduct(m.ProductId), m.Quantity,
                        fullOutput * m.Quantity, market?.QuantityOf(m.ProductId) ?? 0)).ToList(),
                    workers,
                    World.Produced.GetValueOrDefault(building.Id),
                    World.SupplyShares.GetValueOrDefault(building.Id, 1),
                    World.EnergyShareOf(building));
            })
            .OrderBy(b => b.Name)
            .ToList();
    }

    /// <summary>Distance maximale, en km, entre un bâtiment d'extraction et sa commune.</summary>
    public double ExtractionMaxDistance => Balance.Current.ExtractionMaxDistance;

    /// <summary>Ressort d'une commune : rayon en km du territoire qu'elle tient.</summary>
    public double CommuneJurisdictionRadius => Balance.Current.CommuneJurisdictionRadius;

    /// <summary>Ressort d'une préfecture : rayon en km autour de son chef-lieu.</summary>
    public double PrefectureJurisdictionRadius => Balance.Current.PrefectureJurisdictionRadius;

    /// <summary>
    /// Les zones de la carte (forêt, terre fertile, pierre, charbon, fer) où se pose un bâtiment neuf de ce type, avec sa
    /// recette de départ ; vide s'il se construit sans emplacement (ou sans carte).
    /// </summary>
    public IReadOnlyList<string> ExtractionZonesFor(string typeId) =>
        World.BuildingTypes.TryGetValue(typeId, out var type) && World.NeedsExtractionSite(typeId, type.DefaultProduction?.ProductId)
            ? ExtractionSites.ZonesFor(typeId, type.DefaultProduction?.ProductId)
            : [];

    /// <summary>Ce qui empêche de poser un bâtiment neuf de ce type à cette position pour cette commune, ou null si c'est permis.</summary>
    public string? ExtractionSiteProblem(string typeId, string communeId, Point2D position) =>
        World.BuildingTypes.TryGetValue(typeId, out var type)
            ? World.ExtractionSiteProblem(typeId, type.DefaultProduction?.ProductId, communeId, position)
            : "type de bâtiment inconnu";

    /// <summary>Les chantiers en cours : le bâtiment, son type, sa commune et les jours qu'il reste.</summary>
    public IReadOnlyList<ConstructionSiteView> ConstructionSites() => World.ConstructionSites
        .Select(site => new ConstructionSiteView(
            site.Building.Id,
            site.Building.Name,
            site.Building.TypeId,
            World.CommuneOf(site.Building),
            site.DaysLeft))
        .ToList();

    /// <summary>Position géographique d'une commune par son identifiant, ou null si non géoréférencée.</summary>
    public Point2D? CommunePosition(string communeId) =>
        World.CommunePositions.TryGetValue(communeId, out var pos) ? pos : null;

    /// <summary>
    /// Extension et bureaucratie : la charge et l'efficacité de chaque entité politique (État, préfectures, communes),
    /// calculées le matin de la journée, de la plus large à la plus étroite.
    /// </summary>
    public IReadOnlyList<EntityLoadView> EntityLoads()
    {
        var extension = World.Extension;
        var state = World.Structures.Values.FirstOrDefault(s => s.IsNational);
        return World.Structures.Values
            .Where(s => s.Kind == StructureKind.PoliticalEntity)
            .OrderBy(s => s.Level)
            .ThenBy(s => s.Name)
            .Select(entity =>
            {
                var load = extension.Loads.GetValueOrDefault(entity.Id);
                return new EntityLoadView(
                    entity.Id,
                    entity.Name,
                    entity.Level!.Value,
                    entity.ParentId,
                    load?.CountedPopulation ?? 0,
                    load?.DirectBuildings ?? 0,
                    load?.AutonomousBuildings ?? 0,
                    load?.Charge ?? 0,
                    load?.Efficiency ?? 1,
                    state is null ? 0 : extension.LevelsBetween(state.Id, entity),
                    state is null ? 1 : extension.EfficiencyFor(state.Id, entity));
            })
            .ToList();
    }

    /// <summary>Les préfectures : ce qu'elles regroupent, leur chef-lieu, leur bâtiment (ou son chantier) et ce qu'il coûte.</summary>
    public IReadOnlyList<PrefectureView> Prefectures() => World.Structures.Values
        .Where(s => s.IsPrefecture)
        .OrderBy(s => s.Name)
        .Select(prefecture =>
        {
            var building = World.Structures.Values.FirstOrDefault(s => s.ParentId == prefecture.Id && s.TypeId == Simulation.PrefectureTypeId);
            var site = World.ConstructionSites.FirstOrDefault(s => s.Building.ParentId == prefecture.Id && s.Building.TypeId == Simulation.PrefectureTypeId);
            var seat = prefecture.SeatId is { } seatId ? World.Structures.GetValueOrDefault(seatId) : null;
            var jobs = building?.JobCapacity ?? site?.Building.JobCapacity ?? 0;
            var wageBill = building is not null ? Staffing.WageBillPerJob(building) * building.Jobs : 0;
            return new PrefectureView(
                prefecture.Id,
                prefecture.Name,
                prefecture.ParentId,
                prefecture.SeatId,
                seat?.Name,
                World.Structures.Values.Where(s => s.ParentId == prefecture.Id && s.Kind == StructureKind.PoliticalEntity)
                    .Select(s => s.Id).ToList(),
                building?.Id,
                site?.DaysLeft ?? 0,
                jobs,
                wageBill);
        })
        .ToList();

    /// <summary>Ce que coûte une préfecture : sa fondation, ses emplois, et le rattachement d'une entité.</summary>
    public PrefectureCostView PrefectureCost()
    {
        var settings = Balance.Current;
        var type = World.BuildingTypes.GetValueOrDefault(Simulation.PrefectureTypeId);
        return new PrefectureCostView(
            type?.Construction?.ProductId,
            type?.Construction?.Quantity ?? 0,
            type?.Construction?.Days ?? 0,
            settings.PrefectureLegitimacyCost,
            settings.AttachLegitimacyCost,
            settings.PrefectureMinimumMembers,
            type?.Jobs ?? 0,
            settings.PrefectureJobsPerEntity,
            type is null ? 0 : Staffing.WageBillPerJob(type));
    }

    /// <summary>Vrai si l'entité peut être rattachée là, sans compter la légitimité (voir <see cref="Simulation.CanAttach"/>).</summary>
    public bool CanAttach(string entityId, string newParentId) => simulation.CanAttach(entityId, newParentId);

    private CommuneView DescribeCommune(Structure commune)
    {
        var jobs = World.Structures.Values
            .Where(s => s.Jobs > 0 && World.CommuneOf(s) == commune.Id)
            .ToList();
        var storage = World.StorageOf(commune.Id);
        var laws = World.LawBook;

        return new CommuneView(
            commune.Id,
            commune.Name,
            World.Residents(commune.Id),
            World.HousingPlaces(commune.Id),
            jobs.Sum(s => World.Workers.GetValueOrDefault(s.Id)),
            jobs.Sum(s => s.Jobs),
            World.PollutionByCommune.GetValueOrDefault(commune.Id),
            World.IsGhostTown(commune.Id),
            World.CommuneFundBalanceOf(commune.Id),
            World.CommuneTaxRateOf(commune.Id),
            storage.Stored.Values.Sum(),
            World.StorageCapacity(commune.Id),
            new Dictionary<string, double>(World.StockOf(commune.Id)),
            new Dictionary<string, double>(World.StateHoldingsOf(commune.Id)),
            World.Classes.Where(c => c.CommuneId == commune.Id && c.Population > 0).Select(c => new SocialClassView(
                c.Id, c.Name, c.Population, c.Wealth, c.NeedsSatisfaction, c.HousingCoverage, c.Adhesion,
                c.EducationPoints, c.AdvancedEducationPoints, c.Rank, laws.TaxFactorOf(c), c.SeasonTaxes,
                Enum.GetValues<IdeologicalAxis>().Select(axis => new Orientation(axis, c.Ideology[axis])).ToList(),
                c.Ideology.Name, c.UnemployedMembers, c.SeasonIncome, c.SeasonMoneyShortfall, c.SeasonSupplyShortage,
                c.HousingPlaceShortage, c.WealthPerMember,
                c.Employment.Where(e => e.Value > 0).OrderByDescending(e => e.Value)
                    .Select(e => new WorkplaceView(e.Key, NameOf(e.Key), c.Population * e.Value)).ToList())).ToList());
    }

    /// <summary>
    /// Les structures emboîtées, de l'État (la racine) aux communes, puis à leurs organismes et bâtiments, chacune
    /// avec les lois créées chez elle.
    /// </summary>
    public IReadOnlyList<StructureNode> StructureTree() =>
        World.Structures.Values.Where(s => s.IsNational).Select(DescribeNode).ToList();

    private StructureNode DescribeNode(Structure structure) => new(
        structure.Id,
        structure.Name,
        structure.Kind switch
        {
            StructureKind.PoliticalEntity => structure.Level switch
            {
                EntityLevel.State => StructureNodeKind.State,
                EntityLevel.Prefecture => StructureNodeKind.Prefecture,
                _ => StructureNodeKind.Commune,
            },
            StructureKind.Organization => StructureNodeKind.Organization,
            _ => StructureNodeKind.Building,
        },
        World.Laws.Where(l => l.StructureId == structure.Id).Select(DescribeLaw).ToList(),
        World.Structures.Values.Where(s => s.ParentId == structure.Id).Select(DescribeNode).ToList(),
        structure.Ownership,
        structure.TypeId is { } typeId && World.BuildingTypes.TryGetValue(typeId, out var type) ? type.Category : null,
        structure.TypeId,
        structure.Production?.ProductId);

    /// <summary>
    /// Les lois qui s'appliquent à une structure : une loi créée dans une structure s'applique à tout ce qu'elle
    /// contient. De la plus large (l'État) à la structure elle-même.
    /// </summary>
    public IReadOnlyList<LawView> LawsApplyingTo(string structureId)
    {
        var chain = new List<string>();
        for (var current = World.Structures.GetValueOrDefault(structureId); current is not null;
             current = current.ParentId is { } parentId ? World.Structures.GetValueOrDefault(parentId) : null)
            chain.Insert(0, current.Id);

        return chain.SelectMany(id => World.Laws.Where(l => l.StructureId == id)).Select(DescribeLaw).ToList();
    }

    private LawView DescribeLaw(Law law)
    {
        var book = World.LawBook;
        var bricks = law.Bricks.Select(setting => DescribeBrick(book, law, setting)).ToList();
        return new LawView(
            law.Id,
            law.Name,
            law.StructureId,
            World.Structures.TryGetValue(law.StructureId, out var structure) ? structure.Name : law.StructureId,
            bricks.SelectMany(b => b.Poles).Distinct().ToList(),
            law.Domain,
            World.LawCatalogue.DomainOf(law.Domain)?.Regalian == true && structure is { IsNational: false },
            bricks);
    }

    private static BrickView DescribeBrick(LawBook book, Law law, BrickSetting setting)
    {
        var movement = book.MovementOf(law, setting);
        return new BrickView(
            setting.Brick,
            movement?.Brick.Name ?? setting.Brick,
            setting.Subject,
            setting.Value,
            movement?.Reference ?? setting.Value,
            movement?.Intensity ?? 0,
            setting.Locked,
            movement is { Intensity: > 0 } m ? m.Poles.ToList() : []);
    }

    /// <summary>Les modèles de lois du catalogue, à proposer dans le menu des modèles.</summary>
    public IReadOnlyList<LawTemplate> LawTemplates() => World.LawCatalogue.Templates.ToList();

    /// <summary>Toutes les lois en vigueur, de tous les niveaux (le code civil).</summary>
    public IReadOnlyList<LawView> Laws() => World.Laws.Select(DescribeLaw).ToList();

    /// <summary>Les briques du catalogue : famille, bornes, valeur par défaut, poids et description de chaque curseur.</summary>
    public IReadOnlyList<BrickDefinition> LawBricks() => World.LawCatalogue.Bricks.ToList();

    /// <summary>Les domaines du droit, avec ceux qui sont régaliens.</summary>
    public IReadOnlyList<LawDomain> LawDomains() => World.LawCatalogue.Domains.ToList();

    /// <summary>Les brouillons de la commission des lois, avec leur coût s'ils étaient votés maintenant.</summary>
    public IReadOnlyList<LawDraftView> LawDrafts() => World.LawCommission.Drafts
        .Select(d => new LawDraftView(d.Id, d.Kind, DescribeLaw(d.Law), new Dictionary<Orientation, double>(simulation.CostOf(d.Id)), false))
        .ToList();

    /// <summary>Les changements votés, en attente du 1er du mois, avec ce qu'ils ont coûté.</summary>
    public IReadOnlyList<LawDraftView> VotedLawChanges() => World.LawCommission.Voted
        .Select(v => new LawDraftView(v.Draft.Id, v.Draft.Kind, DescribeLaw(v.Draft.Law), new Dictionary<Orientation, double>(v.Paid), true))
        .ToList();
}

public enum StructureNodeKind
{
    State,
    Commune,
    Organization,
    Building,

    /// <summary>Entité intermédiaire entre l'État et les communes ; contient des communes ou des préfectures.</summary>
    Prefecture,
}

/// <summary>
/// Une structure dans l'arbre de l'État, ses lois, ce qu'elle contient, son propriétaire (État, SCOP ou privé) et, pour
/// un bâtiment, la catégorie de son type (Extraction, Production…).
/// </summary>
public sealed record StructureNode(
    string Id,
    string Name,
    StructureNodeKind Kind,
    IReadOnlyList<LawView> Laws,
    IReadOnlyList<StructureNode> Children,
    Ownership Ownership,
    string? Category = null,
    string? TypeId = null,
    string? ProductId = null);

/// <summary>
/// Une loi vue par le joueur : où elle a été créée, ses pôles idéologiques (ceux de ses briques, selon le sens de leurs
/// curseurs), son domaine, la marque d'une loi régalienne votée sous l'État, et ses briques.
/// </summary>
public sealed record LawView(
    string Id,
    string Name,
    string StructureId,
    string StructureName,
    IReadOnlyList<Orientation> Orientations,
    string Domain = "",
    bool RegalianBelowState = false,
    IReadOnlyList<BrickView>? Bricks = null);

/// <summary>Une brique d'une loi : son sujet, son curseur, la valeur héritée des niveaux au-dessus, son écart et ses pôles.</summary>
public sealed record BrickView(
    string Id,
    string Name,
    LawSubject Subject,
    double Value,
    double Reference,
    double Intensity,
    bool Locked,
    IReadOnlyList<Orientation> Poles);

/// <summary>Un brouillon de la commission, ou un changement voté en attente du 1er du mois, et son coût par pôle.</summary>
public sealed record LawDraftView(
    string Id,
    LawChangeKind Kind,
    LawView Law,
    IReadOnlyDictionary<Orientation, double> Cost,
    bool Voted);

/// <summary>Une commune vue par le joueur, à un instant donné.</summary>
public sealed record CommuneView(
    string Id,
    string Name,
    double Residents,
    int HousingPlaces,
    double JobsFilled,
    int Jobs,
    double PollutionPerResident,
    bool IsGhostTown,
    double Fund,
    double TaxRate,
    double Stored,
    int StorageCapacity,
    IReadOnlyDictionary<string, double> Stock,
    IReadOnlyDictionary<string, double> StateStock,
    IReadOnlyList<SocialClassView> Classes);

/// <summary>Un chantier en cours : le bâtiment à venir, son type, sa commune et les jours qu'il reste.</summary>
public sealed record ConstructionSiteView(string BuildingId, string Name, string? TypeId, string? CommuneId, int DaysLeft);

/// <summary>
/// Un groupe local (une classe dans sa commune) vu par le joueur, à un instant donné : son rang, le multiplicateur de
/// son impôt fixé par les lois (1,5 = +50 %), et les impôts (État et commune) payés depuis le début de la saison ; son
/// idéologie (un pôle par axe, et son nom), ses chômeurs, ses revenus de la saison, la part moyenne de ses besoins non
/// couverte faute d'argent et faute de produits depuis le début de la saison, la part de ses membres sans logement faute
/// de places, sa richesse par membre et ses lieux de travail.
/// </summary>
public sealed record SocialClassView(
    string Id,
    string Name,
    long Population,
    double Wealth,
    double NeedsSatisfaction,
    double HousingCoverage,
    double Adhesion,
    double EducationPoints,
    double AdvancedEducationPoints,
    Rank Rank = Rank.Workers,
    double TaxFactor = 1,
    double SeasonTaxes = 0,
    IReadOnlyList<Orientation>? Poles = null,
    string IdeologyName = "",
    double Unemployed = 0,
    double SeasonIncome = 0,
    double MoneyShortfall = 0,
    double SupplyShortage = 0,
    double HousingPlaceShortage = 0,
    double WealthPerMember = 0,
    IReadOnlyList<WorkplaceView>? Workplaces = null);

/// <summary>Un lieu de travail d'un groupe local : la structure et le nombre de membres qui y travaillent.</summary>
public sealed record WorkplaceView(string StructureId, string Name, double Members);

/// <summary>
/// Extension d'une entité politique vue par le joueur : ce qu'elle gère (population comptée, bâtiments publics en
/// direct, bâtiments autonomes), sa charge (1 = la limite de ce qu'elle gère sans perte) et son efficacité propre ; puis
/// les préfectures traversées depuis l'État et l'efficacité qu'ont ici les décisions de l'État.
/// </summary>
public sealed record EntityLoadView(
    string Id,
    string Name,
    EntityLevel Level,
    string? ParentId,
    double CountedPopulation,
    int DirectBuildings,
    int AutonomousBuildings,
    double Charge,
    double Efficiency,
    int PrefecturesFromState,
    double StateEfficiencyHere);

/// <summary>
/// Une préfecture vue par le joueur : son parent, son chef-lieu, les entités qu'elle contient, son bâtiment (null tant
/// qu'il est en chantier, avec les jours restants), ses emplois et ses salaires par jour (payés par le trésor).
/// </summary>
public sealed record PrefectureView(
    string Id,
    string Name,
    string? ParentId,
    string? SeatId,
    string? SeatName,
    IReadOnlyList<string> MemberIds,
    string? BuildingId,
    int ConstructionDaysLeft,
    int Jobs,
    double DailyWages);

/// <summary>
/// Coût d'une préfecture : matériaux et durée du chantier, légitimité de fondation et de rattachement, nombre minimal
/// d'entités, emplois (de base et par entité contenue) et salaires par emploi productif et par jour.
/// </summary>
public sealed record PrefectureCostView(
    string? MaterialProductId,
    double MaterialQuantity,
    int ConstructionDays,
    double FoundingLegitimacy,
    double AttachLegitimacy,
    int MinimumMembers,
    int BaseJobs,
    int JobsPerEntity,
    double WageBillPerJob);

/// <summary>Ce qui circule entre deux communes en un mois (produits, dans les deux sens) et la perte de transport entre elles.</summary>
public sealed record TradeFlowView(string A, string B, double MonthlyQuantity, double Loss);

/// <summary>
/// Un bâtiment de la chaîne de production d'une commune : son produit (et le besoin des habitants qu'il couvre, s'il y
/// en a un), ses matières, ses travailleurs de production, sa production du dernier jour, et la part de sa production
/// que ses matières (<see cref="SupplyShare"/>) et son énergie (<see cref="EnergyShare"/>) ont permise.
/// </summary>
public sealed record ChainBuildingView(
    string Id,
    string Name,
    Ownership Ownership,
    string ProductId,
    string ProductName,
    string? NeedId,
    IReadOnlyList<ChainInputView> Inputs,
    double Workers,
    double ProducedToday,
    double SupplyShare,
    double EnergyShare);

/// <summary>Une matière d'une recette : par unité produite, ce qu'il en faudrait à plein régime par jour, et le stock de la commune.</summary>
public sealed record ChainInputView(string ProductId, string ProductName, double PerUnit, double NeededPerDay, double Stock);

/// <summary>
/// Un produit dans une commune, en totaux depuis le début de la partie : produit, utilisé, envoyé vers une autre commune,
/// perdu ; et son stock et son prix du moment.
/// </summary>
public sealed record ProductFlowView(
    string CommuneId,
    string ProductId,
    string ProductName,
    double Produced,
    double Used,
    double Sent,
    double Lost,
    double Stock,
    double Price);
