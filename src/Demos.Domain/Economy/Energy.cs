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

    public const string Water = "eau";

    public const string DefaultHeating = "bois";

    /// <summary>Les combustibles possibles pour le chauffage, au début de la partie.</summary>
    public static readonly IReadOnlyList<string> HeatingFuels = ["bois", "charbon"];

    /// <summary>Le chauffage double en hiver et s'arrête en été (proposition).</summary>
    public static double HeatingFactor(Season season) => season switch
    {
        Season.Winter => 2,
        Season.Summer => 0,
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
        IReadOnlyDictionary<string, SocialClass> classesById)
    {
        var shares = new Dictionary<string, double>();
        foreach (var building in structures.Values.Where(s => s.Kind == StructureKind.Building))
        {
            var units = UnitsOf(building);
            var needs = NeedsOf(building, tierOf(building), season);
            if (units <= 0 || needs.Count == 0)
                continue;

            var communeId = Communes.CommuneOf(building, structures) ?? "";
            var asRecipe = new Production("", 1, needs);
            shares[building.Id] = Supply.Acquire(
                building, asRecipe, units, marketOf(communeId), communeId, treasury, communeFundOf?.Invoke(communeId), companyFundOf, classesById);
        }

        return shares;
    }
}
