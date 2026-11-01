namespace Demos.Domain.Politics;

public static class StructureTree
{
    /// <summary>Vrai si la structure est l'ancêtre donné ou se trouve à l'intérieur.</summary>
    public static bool IsWithin(Structure structure, string ancestorId, IReadOnlyDictionary<string, Structure> all)
    {
        Structure? current = structure;
        while (current is not null)
        {
            if (current.Id == ancestorId)
                return true;
            current = current.ParentId is { } parentId ? all.GetValueOrDefault(parentId) : null;
        }

        return false;
    }
}
