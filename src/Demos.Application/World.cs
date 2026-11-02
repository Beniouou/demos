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
        IReadOnlyList<Law> laws)
    {
        Products = products;
        BuildingTypes = buildingTypes.ToDictionary(t => t.Id);
        _structures = structures.ToDictionary(s => s.Id);
        _classes = classes.ToList();
        _classesById = _classes.ToDictionary(c => c.Id);
        _laws = laws.ToList();
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
    public IReadOnlyDictionary<string, double> StateHoldingsOf(string communeId) => MarketOf(communeId).ContentsOf(Market.StateSellerId(communeId));

    public double StateQuantityOf(string communeId, string productId) => MarketOf(communeId).QuantityOf(productId, Market.StateSellerId(communeId));

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
        Storage.Allocate(communeId, _structures, Products, MarketOf(communeId).Contents);

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
