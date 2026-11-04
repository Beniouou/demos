using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Application;

/// <summary>
/// État complet d'une partie. Il n'y a pas de régime : le joueur est l'esprit de la nation,
/// et chaque classe juge les lois qui s'appliquent à elle.
/// </summary>
public sealed class World
{
    private readonly Dictionary<string, Structure> _structures;
    private readonly List<SocialClass> _classes;
    private readonly Dictionary<string, SocialClass> _classesById;
    private readonly Dictionary<string, Market> _markets = [];
    private readonly Dictionary<string, double> _priceOverrides = [];

    public World(
        IReadOnlyList<ProductDefinition> products,
        IReadOnlyList<BuildingType> buildingTypes,
        IReadOnlyList<Structure> structures,
        IReadOnlyList<SocialClass> classes,
        IReadOnlyList<Law> laws,
        LawCatalogue? lawCatalogue = null)
    {
        Products = products;
        BuildingTypes = buildingTypes.ToDictionary(t => t.Id);
        _structures = structures.ToDictionary(s => s.Id);
        _classes = classes.ToList();
        _classesById = _classes.ToDictionary(c => c.Id);
        _laws = laws.ToList();
        LawCatalogue = lawCatalogue ?? LawCatalogue.Empty;
        _needOfProduct = products.ToDictionary(p => p.Id, p => p.Need?.Id);
    }

    public IReadOnlyList<ProductDefinition> Products { get; }

    /// <summary>Catalogue des types de bâtiments, par identifiant.</summary>
    public IReadOnlyDictionary<string, BuildingType> BuildingTypes { get; }

    public IReadOnlyDictionary<string, Structure> Structures => _structures;
    public IReadOnlyList<SocialClass> Classes => _classes;
    public IReadOnlyDictionary<string, SocialClass> ClassesById => _classesById;
    public IReadOnlyList<Law> Laws => _laws;

    private readonly List<Law> _laws;

    public void AddLaw(Law law) => _laws.Add(law);

    public void RemoveLaw(string lawId) => _laws.RemoveAll(l => l.Id == lawId);

    /// <summary>Remplace une loi en vigueur par sa nouvelle version (mêmes identifiant et niveau).</summary>
    public void ReplaceLaw(Law law)
    {
        var index = _laws.FindIndex(l => l.Id == law.Id);
        if (index >= 0)
            _laws[index] = law;
    }

    /// <summary>Le catalogue des lois : domaines, briques et modèles.</summary>
    public LawCatalogue LawCatalogue { get; }

    /// <summary>La commission des lois : brouillons et changements votés en attente du 1er du mois.</summary>
    public LawCommission LawCommission { get; } = new();

    /// <summary>Les lois en vigueur, lues avec le catalogue : valeur d'une brique à un endroit, valeur héritée, membres touchés.</summary>
    public LawBook LawBook => new(LawCatalogue, _laws, _structures,
        building => building.TypeId is { } typeId ? BuildingTypes.GetValueOrDefault(typeId)?.Category : null,
        building => building.Production is { } production ? _needOfProduct.GetValueOrDefault(production.ProductId) : null);

    private readonly Dictionary<string, string?> _needOfProduct;

    /// <summary>Recalcule ce que les lois en vigueur imposent à chaque bâtiment (salaires, production, loyer…).</summary>
    public void ApplyLaws(LawBook laws)
    {
        foreach (var building in _structures.Values.Where(s => s.Kind == StructureKind.Building).ToList())
        {
            var rules = laws.RulesFor(building);
            _structures[building.Id] = building with { Laws = rules == AppliedLaws.None ? null : rules };
        }
    }

    /// <summary>
    /// Charte de fondation d'une commune : le pôle payé pour la fonder. Elle attire les classes de ce pôle et repousse
    /// celles du pôle opposé dans la migration ; ce n'est pas une loi.
    /// </summary>
    public Orientation? CharterOf(string communeId) => _charters.TryGetValue(communeId, out var pole) ? pole : null;

    public void SetCharter(string communeId, Orientation pole) => _charters[communeId] = pole;

    private readonly Dictionary<string, Orientation> _charters = [];

    /// <summary>Salaires dus et versés par chaque bâtiment sur les derniers jours.</summary>
    public WageLedger Wages { get; } = new();

    /// <summary>Ressource « légitimité » du joueur, un stock par pôle.</summary>
    public LegitimacyPool Legitimacy { get; } = new();

    /// <summary>Ressources financières du joueur.</summary>
    public Treasury Treasury { get; } = new();

    /// <summary>
    /// Caisse d'une commune : la dotation versée par l'État à sa fondation. Elle paie d'abord les salaires des
    /// bâtiments publics de la commune ; plus tard, elle pourra devenir un budget communal.
    /// </summary>
    public Treasury CommuneFundOf(string communeId)
    {
        if (!_communeFunds.TryGetValue(communeId, out var fund))
            _communeFunds[communeId] = fund = new Treasury();
        return fund;
    }

    private readonly Dictionary<string, Treasury> _communeFunds = [];

    /// <summary>Solde de la caisse d'une commune, sans la créer si elle n'existe pas encore (0 dans ce cas).</summary>
    public double CommuneFundBalanceOf(string communeId) => _communeFunds.TryGetValue(communeId, out var fund) ? fund.Balance : 0;

    /// <summary>
    /// Caisse d'une entreprise privée ou coopérative, distincte de la richesse de sa classe propriétaire : ventes,
    /// loyers et frais y arrivent, salaires et bénéfices versés aux propriétaires en sortent.
    /// </summary>
    public Treasury CompanyFundOf(Structure building) => CompanyFundOf(building.Id);

    public Treasury CompanyFundOf(string buildingId)
    {
        if (!_companyFunds.TryGetValue(buildingId, out var fund))
            _companyFunds[buildingId] = fund = new Treasury();
        return fund;
    }

    /// <summary>Ferme la caisse d'une entreprise (quand elle devient publique) et renvoie ce qu'il y restait.</summary>
    public double CloseCompanyFund(string buildingId)
    {
        var balance = _companyFunds.TryGetValue(buildingId, out var fund) ? fund.Balance : 0;
        _companyFunds.Remove(buildingId);
        return balance;
    }

    private readonly Dictionary<string, Treasury> _companyFunds = [];

    /// <summary>
    /// Ressources matérielles du joueur dans une commune : ce que ses structures publiques ont produit, rangé dans le
    /// stockage de la commune et en vente au nom de l'État. Les habitants peuvent l'acheter ; l'État peut le reprendre.
    /// </summary>
    public IReadOnlyDictionary<string, double> StateHoldingsOf(string communeId) =>
        _markets.TryGetValue(communeId, out var market) ? market.ContentsOf(Market.StateSellerId(communeId)) : NoProducts;

    public double StateQuantityOf(string communeId, string productId) =>
        _markets.TryGetValue(communeId, out var market) ? market.QuantityOf(productId, Market.StateSellerId(communeId)) : 0;

    /// <summary>Tout ce qui est en vente dans une commune, sans créer son marché s'il n'existe pas encore.</summary>
    public IReadOnlyDictionary<string, double> StockOf(string communeId) =>
        _markets.TryGetValue(communeId, out var market) ? market.Contents : NoProducts;

    private static readonly IReadOnlyDictionary<string, double> NoProducts = new Dictionary<string, double>();

    /// <summary>Marché d'une commune : on vend et on achète là où l'on est.</summary>
    public Market MarketOf(string communeId)
    {
        if (_markets.TryGetValue(communeId, out var market))
            return market;

        market = new Market(Products);
        foreach (var (productId, price) in _priceOverrides)
            market.SetPrice(productId, price);
        return _markets[communeId] = market;
    }

    public IReadOnlyDictionary<string, Market> Markets => _markets;

    /// <summary>Fixe le prix d'un produit à la main, dans toutes les communes (en attendant la formation des prix).</summary>
    public void SetPrice(string productId, double price)
    {
        _priceOverrides[productId] = price;
        foreach (var market in _markets.Values)
            market.SetPrice(productId, price);
    }

    public double PriceOf(string productId) =>
        _priceOverrides.TryGetValue(productId, out var price) ? price : Products.FirstOrDefault(p => p.Id == productId)?.BasePrice ?? 0;

    /// <summary>Commune où se trouve une structure (la sienne si c'est une commune).</summary>
    public string? CommuneOf(Structure structure) => Domain.Politics.Communes.CommuneOf(structure, _structures);

    /// <summary>Capacité de stockage d'une commune : la somme de ses bâtiments de stockage.</summary>
    public int StorageCapacity(string communeId) => Storage.CapacityOf(communeId, _structures);

    /// <summary>Rangement actuel des produits de la commune dans ses bâtiments de stockage.</summary>
    public StorageAllocation StorageOf(string communeId) =>
        Storage.Allocate(communeId, _structures, Products, StockOf(communeId));

    /// <summary>
    /// Impôt d'État : part de sa richesse que chaque groupe verse chaque jour au trésor. À l'essai : 0,3 %, soit avec
    /// l'impôt communal 0,5 % en tout. Les lois peuvent le plafonner ou l'augmenter.
    /// </summary>
    public double StateTaxRate { get; set; } = Balance.Current.StateTaxRate;

    /// <summary>Impôt communal de départ de chaque commune (0,2 % par jour, à l'essai), versé à sa caisse.</summary>
    public double DefaultCommuneTaxRate { get; set; } = Balance.Current.CommuneTaxRate;

    private readonly Dictionary<string, double> _communeTaxRates = [];

    /// <summary>Impôt communal d'une commune, réglé commune par commune.</summary>
    public double CommuneTaxRateOf(string communeId) => _communeTaxRates.GetValueOrDefault(communeId, DefaultCommuneTaxRate);

    public void SetCommuneTaxRate(string communeId, double rate) => _communeTaxRates[communeId] = rate;

    /// <summary>Nombre de travailleurs de production de chaque structure, recalculé chaque jour.</summary>
    public IReadOnlyDictionary<string, double> Workers { get; set; } = new Dictionary<string, double>();

    /// <summary>Ce que chaque lieu de travail a produit le dernier jour (après le manque d'énergie et de matières).</summary>
    public IReadOnlyDictionary<string, double> Produced { get; set; } = new Dictionary<string, double>();

    /// <summary>
    /// Part de ses besoins en énergie que chaque bâtiment a couverte le dernier jour (1 s'il n'en a pas besoin).
    /// </summary>
    public IReadOnlyDictionary<string, double> EnergyShares { get; set; } = new Dictionary<string, double>();

    public double EnergyShareOf(Structure building) => EnergyShares.GetValueOrDefault(building.Id, 1);

    /// <summary>Réserve de combustible de chaque bâtiment (voir <see cref="Energy.FuelReserveDays"/>).</summary>
    public Inventory FuelStockOf(Structure building)
    {
        if (!_fuelStocks.TryGetValue(building.Id, out var stock))
            _fuelStocks[building.Id] = stock = new Inventory();
        return stock;
    }

    private readonly Dictionary<string, Inventory> _fuelStocks = [];

    /// <summary>Effet de son énergie sur un bâtiment : 1 s'il a tout, la moitié s'il n'a rien.</summary>
    public double EnergyEffectOf(Structure building) => Energy.EffectOf(EnergyShareOf(building));

    /// <summary>La « liste » d'un bâtiment, d'après son type du catalogue ; 0 (pas d'énergie) hors catalogue.</summary>
    public int TierOf(Structure building) =>
        building.TypeId is { } typeId && BuildingTypes.TryGetValue(typeId, out var type) ? type.Tier : 0;

    /// <summary>Vrai pour un bâtiment qui chauffe autant toute l'année (la forge).</summary>
    public bool HeatsAllYear(Structure building) =>
        building.TypeId is { } typeId && BuildingTypes.TryGetValue(typeId, out var type) && type.ConstantHeating;

    /// <summary>Pollution de chaque commune par habitant et par jour, recalculée chaque jour.</summary>
    public IReadOnlyDictionary<string, double> PollutionByCommune { get; set; } = new Dictionary<string, double>();

    public IEnumerable<Structure> Communes => Domain.Politics.Communes.All(_structures);

    public double Residents(string communeId) => Classes.Sum(c => c.MembersLivingIn(communeId));

    public int HousingPlaces(string communeId) => _structures.Values
        .Where(s => s.HousingPlaces > 0 && Domain.Politics.Communes.CommuneOf(s, _structures) == communeId)
        .Sum(s => s.HousingPlaces);

    /// <summary>Une commune sans habitants est une ville fantôme ; ses bâtiments sont des ruines.</summary>
    public bool IsGhostTown(string communeId) => Residents(communeId) < 1;

    public void AddStructure(Structure structure) => _structures.Add(structure.Id, structure);

    private readonly Dictionary<string, Inventory> _constructionReserves = [];

    /// <summary>
    /// Réserve de chantier d'une commune : les matériaux reçus à sa fondation. Hors du stockage, elle ne se perd ni ne
    /// se vend ; ses chantiers la prennent avant le stock de l'État et le marché.
    /// </summary>
    public Inventory ConstructionReserveOf(string communeId)
    {
        if (!_constructionReserves.TryGetValue(communeId, out var reserve))
            _constructionReserves[communeId] = reserve = new Inventory();
        return reserve;
    }

    private readonly List<ConstructionSite> _constructionSites = [];

    /// <summary>Bâtiments en chantier : ils ne sont pas encore dans <see cref="Structures"/>.</summary>
    public IReadOnlyList<ConstructionSite> ConstructionSites => _constructionSites;

    public void AddConstructionSite(ConstructionSite site) => _constructionSites.Add(site);

    /// <summary>Un identifiant est pris par une structure ou par un bâtiment en chantier.</summary>
    public bool IsIdTaken(string id) => _structures.ContainsKey(id) || _constructionSites.Any(s => s.Building.Id == id);

    /// <summary>
    /// Avance chaque chantier d'un jour ; les bâtiments dont le chantier est fini rejoignent la partie. Renvoie ces
    /// bâtiments.
    /// </summary>
    public IReadOnlyList<Structure> AdvanceConstructionSites()
    {
        var finished = new List<Structure>();
        for (var i = _constructionSites.Count - 1; i >= 0; i--)
        {
            var site = _constructionSites[i] with { DaysLeft = _constructionSites[i].DaysLeft - 1 };
            if (site.DaysLeft > 0)
            {
                _constructionSites[i] = site;
                continue;
            }

            _constructionSites.RemoveAt(i);
            AddStructure(site.Building);
            finished.Insert(0, site.Building);
        }

        return finished;
    }

    /// <summary>Ajoute une classe née en cours de partie (par exemple d'un passage entre classes).</summary>
    public void AddClass(SocialClass socialClass)
    {
        _classesById.Add(socialClass.Id, socialClass);
        _classes.Add(socialClass);
    }

    /// <summary>Remplace une structure existante par sa version modifiée (même identifiant).</summary>
    public void UpdateStructure(Structure structure)
    {
        if (!_structures.ContainsKey(structure.Id))
            throw new KeyNotFoundException($"Structure inconnue : {structure.Id}");
        _structures[structure.Id] = structure;
    }
}
