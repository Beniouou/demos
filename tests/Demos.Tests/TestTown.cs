using Demos.Domain.Politics;

namespace Demos.Tests;

/// <summary>Une petite nation de test : deux communes, chacune avec une ferme de 1 000 emplois.</summary>
public static class TestTown
{
    public static readonly Dictionary<string, Structure> Structures = new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        new("ferme", "Ferme", StructureKind.Building, "commune", Jobs: 1_000),
        new("commune-2", "Commune 2", StructureKind.PoliticalEntity, "etat"),
        new("ferme-2", "Ferme 2", StructureKind.Building, "commune-2", Jobs: 1_000),
    }.ToDictionary(s => s.Id);

    public static readonly Dictionary<string, double> NoPollution = [];
}
