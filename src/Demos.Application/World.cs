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

    public World(
        IReadOnlyList<ProductDefinition> products,
        IReadOnlyList<Structure> structures,
        IReadOnlyList<SocialClass> classes,
        IReadOnlyList<Law> laws)
    {
        Products = products;
        _structures = structures.ToDictionary(s => s.Id);
        Classes = classes;
        ClassesById = classes.ToDictionary(c => c.Id);
        Laws = laws;
        Market = new Market(products);
    }

    public IReadOnlyList<ProductDefinition> Products { get; }
    public IReadOnlyDictionary<string, Structure> Structures => _structures;
    public IReadOnlyList<SocialClass> Classes { get; }
    public IReadOnlyDictionary<string, SocialClass> ClassesById { get; }
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

    /// <summary>Nombre de travailleurs de chaque structure, recalculé chaque jour.</summary>
    public IReadOnlyDictionary<string, double> Workers { get; set; } = new Dictionary<string, double>();

    public IEnumerable<Structure> Communes => Domain.Politics.Communes.All(_structures);

    public double Residents(string communeId) => Classes.Sum(c => c.MembersLivingIn(communeId));

    /// <summary>Une commune sans habitants est une ville fantôme ; ses bâtiments sont des ruines.</summary>
    public bool IsGhostTown(string communeId) => Residents(communeId) < 1;

    public void AddStructure(Structure structure) => _structures.Add(structure.Id, structure);
}
