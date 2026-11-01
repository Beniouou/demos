using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Application;

/// <summary>
/// État complet d'une partie. Il n'y a pas de régime : le joueur est l'esprit de la nation,
/// et chaque classe juge les lois qui s'appliquent à elle.
/// </summary>
public sealed class World(
    IReadOnlyList<ProductDefinition> products,
    IReadOnlyList<Structure> structures,
    IReadOnlyList<SocialClass> classes,
    IReadOnlyList<Law> laws)
{
    public IReadOnlyList<ProductDefinition> Products { get; } = products;
    public IReadOnlyDictionary<string, Structure> Structures { get; } = structures.ToDictionary(s => s.Id);
    public IReadOnlyList<SocialClass> Classes { get; } = classes;
    public IReadOnlyList<Law> Laws { get; } = laws;
    public LegitimacyPool Legitimacy { get; } = new();
}
