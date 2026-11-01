using Demos.Domain.Economy;

namespace Demos.Domain.Politics;

public enum StructureKind
{
    PoliticalEntity,
    Organization,
    Building,
}

/// <summary>
/// Public : étatique ou public, la production va dans le stock de l'État et les salaires sont payés par le trésor.
/// Privé et coopératif (droit privé, autogestionnaire, scop) : la production est vendue sur le marché
/// et les salaires sont payés par la classe propriétaire, qui touche les ventes.
/// </summary>
public enum Ownership
{
    Public,
    Private,
    Cooperative,
}

/// <summary>Ce qu'une structure produit chaque jour, par travailleur. Les matières consommées viendront plus tard.</summary>
public readonly record struct Production(string ProductId, double QuantityPerWorker);

/// <summary>
/// Entité politique, organisme ou bâtiment. Les structures s'emboîtent :
/// l'État contient des communes, une commune des organismes et des bâtiments…
/// Une loi créée dans une structure s'applique à tout ce qu'elle contient.
/// </summary>
/// <param name="ParentId">Structure qui la contient ; null pour l'État, qui couvre toute la nation.</param>
/// <param name="OwnerClassId">Classe propriétaire d'une structure privée ou coopérative.</param>
/// <param name="Jobs">Nombre de postes de production ; les postes d'encadrement et de direction en découlent.</param>
/// <param name="DailyWage">Salaire de base versé par poste et par jour.</param>
/// <param name="HousingPlaces">Places de logement offertes dans la commune.</param>
/// <param name="DailyRent">Loyer d'une place par jour, versé au propriétaire.</param>
/// <param name="TypeId">Type de bâtiment du catalogue dont il est issu, s'il y en a un.</param>
public sealed record Structure(
    string Id,
    string Name,
    StructureKind Kind,
    string? ParentId,
    Ownership Ownership = Ownership.Public,
    string? OwnerClassId = null,
    Production? Production = null,
    int Jobs = 0,
    double DailyWage = 0,
    int HousingPlaces = 0,
    double DailyRent = 0,
    string? TypeId = null)
{
    public bool IsNational => ParentId is null;
}
