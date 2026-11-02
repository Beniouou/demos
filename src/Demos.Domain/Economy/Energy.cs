using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Domain.Economy;

/// <summary>
/// Énergie des bâtiments, selon leur niveau technique (leur « liste ») : les bâtiments de la liste 1 ont besoin de
/// chauffage (bois ou charbon, au choix de chaque bâtiment) et d'eau ; ceux de la liste 2 auront besoin d'électricité, et
/// ceux de la liste 3 d'internet (à écrire). Ce sont les bâtiments qui consomment, pas les habitants : un logement se
/// chauffe pour ses occupants, et son propriétaire paie. Chaque jour, après la production, chaque bâtiment prend son
/// énergie dans le stockage de sa commune, comme ses matières (voir <see cref="Supply"/>). Quand elle manque, il
/// tourne moins, jusqu'à la moitié sans aucune énergie : un atelier produit moins le lendemain, un logement ou une
/// école n'offre qu'une partie de ses places.
/// </summary>
public static class Energy
{
    /// <summary>Proposition : 0,01 de bois ou de charbon par emploi ou par place, et par jour, au printemps et en automne.</summary>
    public const double HeatingPerUnit = 0.01;

    /// <summary>Proposition : 0,1 d'eau par emploi ou par place, et par jour.</summary>
    public const double WaterPerUnit = 0.1;

    /// <summary>
    /// Proposition : sans énergie, un bâtiment tourne à moitié (on travaille au froid, on porte l'eau) ; il ne s'arrête
    /// jamais tout à fait, ce qui évite qu'un puits et une mine qui ont besoin l'un de l'autre se bloquent.
    /// </summary>
    public const double OutputWithoutEnergy = 0.5;

    /// <summary>Effet de l'énergie couverte sur un bâtiment : de la moitié (rien) à tout (tous ses besoins couverts).</summary>
    public static double EffectOf(double coveredShare) => OutputWithoutEnergy + (1 - OutputWithoutEnergy) * coveredShare;

    /// <summary>
    /// Proposition : chaque bâtiment garde une réserve de combustible pour 120 jours de chauffage normal. Il puise
    /// dedans d'abord, et la remplit dès qu'il le peut, même en été : sa consommation d'hiver est lissée sur l'année.
    /// </summary>
    public const double FuelReserveDays = 120;

    /// <summary>Taille de la réserve de combustible d'un bâtiment.</summary>
    public static double FuelReserveOf(Structure building) => UnitsOf(building) * HeatingPerUnit * FuelReserveDays;

    public const string Water = "eau";

    public const string DefaultHeating = "bois";

    /// <summary>Les combustibles possibles pour le chauffage, au début de la partie.</summary>
    public static readonly IReadOnlyList<string> HeatingFuels = ["bois", "charbon"];

    /// <summary>Le chauffage double en hiver et diminue de moitié en été (propositions).</summary>
    public static double HeatingFactor(Season season) => season switch
    {
        Season.Winter => 2,
        Season.Summer => 0.5,
        _ => 1,
    };

    /// <summary>Ce qui consomme de l'énergie dans un bâtiment : ses emplois de production et ses places.</summary>
    public static double UnitsOf(Structure building) => building.Jobs + building.HousingPlaces + building.SchoolPlaces;

    /// <summary>L'énergie dont un bâtiment a besoin par emploi ou par place, un jour de cette saison.</summary>
    public static IReadOnlyList<RecipeInput> NeedsOf(Structure building, int tier, Season season)
    {
        if (tier != 1)
            return [];

        var needs = new List<RecipeInput>();
        var heating = HeatingPerUnit * HeatingFactor(season);
        if (heating > 0)
            needs.Add(new RecipeInput(building.HeatingProductId ?? DefaultHeating, heating));

        // Un puits ou un château d'eau ne s'achète pas son eau.
        if (building.Production?.ProductId != Water)
            needs.Add(new RecipeInput(Water, WaterPerUnit));

        return needs;
    }

    /// <summary>
    /// Approvisionne chaque bâtiment en énergie et renvoie, pour chacun, la part de ses besoins couverte (entre 0 et 1).
    /// </summary>
    public static Dictionary<string, double> RunDaily(
        IReadOnlyDictionary<string, Structure> structures,
        Func<Structure, int> tierOf,
        Season season,
        Func<string, Market> marketOf,
        Treasury treasury,
        Func<string, Treasury>? communeFundOf,
        Func<Structure, Treasury>? companyFundOf,
        IReadOnlyDictionary<string, SocialClass> classesById,
        Func<Structure, Inventory>? fuelStockOf = null)
    {
        var shares = new Dictionary<string, double>();
        foreach (var building in structures.Values.Where(s => s.Kind == StructureKind.Building))
        {
            var units = UnitsOf(building);
            var tier = tierOf(building);
            if (units <= 0 || tier != 1)
                continue;

            var needs = NeedsOf(building, tier, season).ToList();

            var communeId = Communes.CommuneOf(building, structures) ?? "";
            var market = marketOf(communeId);
            var communeFund = communeFundOf?.Invoke(communeId);
            var fuel = building.HeatingProductId ?? DefaultHeating;
            var stock = fuelStockOf?.Invoke(building);

            // Le chauffage du jour vient d'abord de la réserve du bâtiment.
            if (stock is not null)
            {
                needs = needs.Select(need =>
                {
                    if (need.ProductId != fuel)
                        return need;

                    var fromStock = Math.Min(stock.QuantityOf(fuel), need.Quantity * units);
                    stock.TryRemove(fuel, fromStock);
                    return need with { Quantity = need.Quantity - fromStock / units };
                }).ToList();
            }

            shares[building.Id] = needs.Count == 0 ? 1 : Supply.Acquire(
                building, new Production("", 1, needs), units, market, communeId, treasury, communeFund, companyFundOf, classesById);

            // Puis il remplit sa réserve, s'il trouve du combustible et l'argent pour l'acheter.
            var missing = stock is null ? 0 : FuelReserveOf(building) - stock.QuantityOf(fuel);
            if (stock is not null && missing > 0)
            {
                var refill = new Production("", 1, [new RecipeInput(fuel, missing)]);
                var bought = Supply.Acquire(building, refill, 1, market, communeId, treasury, communeFund, companyFundOf, classesById);
                stock.Add(fuel, missing * bought);
            }
        }

        return shares;
    }
}
