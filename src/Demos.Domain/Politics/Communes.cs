namespace Demos.Domain.Politics;

/// <summary>
/// Les communes sont les entités politiques de base sous l'État, placées directement sous lui ou dans une préfecture.
/// Tout habitant vit dans une commune, et chaque bâtiment ou organisme se trouve dans une commune (ceux d'une
/// préfecture, dans son chef-lieu).
/// </summary>
public static class Communes
{
    public static bool IsCommune(Structure structure, IReadOnlyDictionary<string, Structure> all) => structure.IsCommune;

    public static IEnumerable<Structure> All(IReadOnlyDictionary<string, Structure> all) =>
        all.Values.Where(s => s.IsCommune);

    /// <summary>
    /// Commune qui contient une structure (ou la structure elle-même si c'est une commune). Un bâtiment ou un organisme
    /// placé directement dans une préfecture se trouve dans son chef-lieu. Null pour l'État et pour une préfecture.
    /// </summary>
    public static string? CommuneOf(Structure structure, IReadOnlyDictionary<string, Structure> all)
    {
        for (Structure? current = structure; current is not null;
             current = current.ParentId is { } parentId ? all.GetValueOrDefault(parentId) : null)
        {
            if (current.IsCommune)
                return current.Id;
            if (current.Kind == StructureKind.PoliticalEntity)
                return current.IsPrefecture && !ReferenceEquals(current, structure) ? current.SeatId : null;
        }

        return null;
    }

    /// <summary>Les communes contenues dans une entité politique, à toute profondeur (elle-même si c'est une commune).</summary>
    public static IEnumerable<Structure> Within(Structure entity, IReadOnlyDictionary<string, Structure> all) =>
        All(all).Where(c => StructureTree.IsWithin(c, entity.Id, all));
}
