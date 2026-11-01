namespace Demos.Domain.Politics;

/// <summary>
/// Les communes sont les entités politiques placées juste sous l'État. Tout habitant vit dans une commune,
/// et chaque bâtiment ou organisme se trouve dans une commune.
/// </summary>
public static class Communes
{
    public static bool IsCommune(Structure structure, IReadOnlyDictionary<string, Structure> all) =>
        structure is { Kind: StructureKind.PoliticalEntity, ParentId: { } parentId }
        && all.TryGetValue(parentId, out var parent)
        && parent.IsNational;

    public static IEnumerable<Structure> All(IReadOnlyDictionary<string, Structure> all) =>
        all.Values.Where(s => IsCommune(s, all));

    /// <summary>Commune qui contient une structure (ou la structure elle-même si c'est une commune) ; null pour l'État.</summary>
    public static string? CommuneOf(Structure structure, IReadOnlyDictionary<string, Structure> all)
    {
        var current = structure;
        while (current.ParentId is { } parentId && all.TryGetValue(parentId, out var parent))
        {
            if (parent.IsNational)
                return current.Id;
            current = parent;
        }

        return null;
    }
}
