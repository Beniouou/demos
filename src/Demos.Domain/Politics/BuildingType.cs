namespace Demos.Domain.Politics;

/// <summary>
/// Type de bâtiment du catalogue, avec ses paramètres par défaut. Le niveau indique la version améliorée
/// d'un même usage (une ferme devient une entreprise agricole).
/// </summary>
public sealed record BuildingType(
    string Id,
    string Name,
    string Category,
    int Level,
    string Description,
    int Jobs = 0,
    double DailyWage = 0,
    Production? Production = null,
    int HousingPlaces = 0,
    double DailyRent = 0)
{
    /// <summary>Un bâtiment est fonctionnel quand il a un effet dans la simulation : des emplois ou des places.</summary>
    public bool IsFunctional => Jobs > 0 || HousingPlaces > 0;

    /// <summary>Crée un bâtiment de ce type dans une structure, avec les paramètres par défaut.</summary>
    public Structure Build(string id, string parentId, Ownership ownership = Ownership.Public, string? ownerClassId = null, string? name = null) =>
        new(id, name ?? Name, StructureKind.Building, parentId, ownership, ownerClassId,
            Production, Jobs, DailyWage, HousingPlaces, DailyRent, TypeId: Id);
}
