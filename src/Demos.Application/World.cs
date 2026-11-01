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
        Laws = laws;
        Market = new Market(products);
    }

    public IReadOnlyList<ProductDefinition> Products { get; }

    /// <summary>Catalogue des types de bâtiments, par identifiant.</summary>
    public IReadOnlyDictionary<string, BuildingType> BuildingTypes { get; }

    public IReadOnlyDictionary<string, Structure> Structures => _structures;
    public IReadOnlyList<SocialClass> Classes => _classes;
    public IReadOnlyDictionary<string, SocialClass> ClassesById => _classesById;
    public IReadOnlyList<Law> Laws { get; }

    /// <summary>Ressource « légitimité » du joueur, un stock par pôle.</summary>
    public LegitimacyPool Legitimacy { get; } = new();

    /// <summary>Ressources financières du joueur.</summary>
    public Treasury Treasury { get; } = new();

    /// <summary>Ressources matérielles du joueur, produites par les structures publiques.</summary>
    public Inventory StateStock { get; } = new();

    public Market Market { get; }

    /// <summary>Part de sa richesse que chaque classe verse chaque jour. Plus tard : fixée par les lois.</summary>
    public double DailyTaxRate { get; set; } = 0.001;

    /// <summary>Nombre de travailleurs de production de chaque structure, recalculé chaque jour.</summary>
    public IReadOnlyDictionary<string, double> Workers { get; set; } = new Dictionary<string, double>();

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
