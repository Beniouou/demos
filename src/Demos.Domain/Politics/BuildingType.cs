namespace Demos.Domain.Politics;

/// <summary>
/// Type de bâtiment du catalogue, avec ses paramètres par défaut. Le niveau indique la version améliorée
/// d'un même usage (une ferme devient une entreprise agricole). Un bâtiment de production peut être polyvalent :
/// ses recettes sont les productions possibles (un atelier fait du pain, des vêtements ou des meubles…), et chaque
/// bâtiment construit en choisit une ; la première est celle par défaut. Le tier est la « liste » du bâtiment, son niveau
/// technique : 1 pour les bâtiments chauffés et alimentés en eau, 2 pour ceux qui auront besoin d'électricité, 3 pour
/// ceux qui auront besoin d'internet.
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
    double DailyRent = 0,
    int Posts = 0,
    int SchoolPlaces = 0,
    double DailyTuition = 0,
    Economy.EducationLevel SchoolLevel = Economy.EducationLevel.Basic,
    double PointsPerPupil = 1,
    double PollutionPerWorker = 0,
    int StorageCapacity = 0,
    Economy.StorageKind StorageKind = Economy.StorageKind.General,
    string? StorageProductId = null,
    IReadOnlyList<Production>? Recipes = null,
    int Tier = 1)
{
    /// <summary>Les productions possibles de ce type : ses recettes, ou sa seule production.</summary>
    public IReadOnlyList<Production> AvailableRecipes =>
        Recipes is { Count: > 0 } recipes ? recipes : Production is { } production ? [production] : [];

    /// <summary>La production d'un bâtiment neuf : la première recette.</summary>
    public Production? DefaultProduction => AvailableRecipes.Count > 0 ? AvailableRecipes[0] : null;

    /// <summary>Un bâtiment est fonctionnel quand il a un effet dans la simulation : des emplois, des places ou du stockage.</summary>
    public bool IsFunctional => Jobs > 0 || HousingPlaces > 0 || SchoolPlaces > 0 || StorageCapacity > 0;

    /// <summary>Crée un bâtiment de ce type dans une structure, avec les paramètres par défaut.</summary>
    public Structure Build(string id, string parentId, Ownership ownership = Ownership.Public, string? ownerClassId = null, string? name = null) =>
        new(id, name ?? Name, StructureKind.Building, parentId, ownership, ownerClassId,
            DefaultProduction, Jobs, DailyWage, HousingPlaces, DailyRent, TypeId: Id, Posts: Posts,
            SchoolPlaces: SchoolPlaces, DailyTuition: DailyTuition, SchoolLevel: SchoolLevel, PointsPerPupil: PointsPerPupil,
            PollutionPerWorker: PollutionPerWorker, StorageCapacity: StorageCapacity,
            StorageKind: StorageKind, StorageProductId: StorageProductId);

    /// <summary>Crée un bâtiment de ce type qui fabrique un produit précis, avec la première recette qui le fait.</summary>
    public Structure BuildFor(string productId, string id, string parentId, Ownership ownership = Ownership.Public, string? ownerClassId = null, string? name = null) =>
        Build(id, parentId, ownership, ownerClassId, name) with { Production = AvailableRecipes.First(r => r.ProductId == productId) };
}
