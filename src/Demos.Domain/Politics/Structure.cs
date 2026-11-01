namespace Demos.Domain.Politics;

public enum StructureKind
{
    PoliticalEntity,
    Organization,
    Building,
}

/// <summary>
/// Entité politique, organisme ou bâtiment. Les structures s'emboîtent :
/// l'État contient des communes, une commune des organismes et des bâtiments…
/// Une loi créée dans une structure s'applique à tout ce qu'elle contient.
/// </summary>
/// <param name="ParentId">Structure qui la contient ; null pour l'État, qui couvre toute la nation.</param>
public sealed record Structure(string Id, string Name, StructureKind Kind, string? ParentId)
{
    public bool IsNational => ParentId is null;
}
