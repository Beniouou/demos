using Demos.Domain.Economy;

namespace Demos.Domain.Politics;

public enum StructureKind
{
    PoliticalEntity,
    Organization,
    Building,
}

/// <summary>
/// Public : étatique ou public, la production va dans le stock de l'État.
/// Privé et coopératif (droit privé, autogestionnaire, scop) : la production est vendue sur le marché
/// au profit de la classe propriétaire.
/// </summary>
public enum Ownership
{
    Public,
    Private,
    Cooperative,
}

/// <summary>Ce qu'une structure produit chaque jour. Les matières consommées viendront plus tard.</summary>
public readonly record struct Production(string ProductId, double QuantityPerDay);

/// <summary>
/// Entité politique, organisme ou bâtiment. Les structures s'emboîtent :
/// l'État contient des communes, une commune des organismes et des bâtiments…
/// Une loi créée dans une structure s'applique à tout ce qu'elle contient.
/// </summary>
/// <param name="ParentId">Structure qui la contient ; null pour l'État, qui couvre toute la nation.</param>
/// <param name="OwnerClassId">Classe propriétaire d'une structure privée ou coopérative.</param>
public sealed record Structure(
    string Id,
    string Name,
    StructureKind Kind,
    string? ParentId,
    Ownership Ownership = Ownership.Public,
    string? OwnerClassId = null,
    Production? Production = null)
{
    public bool IsNational => ParentId is null;
}
