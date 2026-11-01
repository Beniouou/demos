using Demos.Domain.Population;

namespace Demos.Domain.Politics;

/// <summary>Pôle qu'une loi favorise sur un axe.</summary>
public readonly record struct Orientation(IdeologicalAxis Axis, Pole Pole)
{
    /// <summary>Tous les pôles, axe par axe : EG, SE, PO, EC, PR, LI.</summary>
    public static IEnumerable<Orientation> All =>
        Enum.GetValues<IdeologicalAxis>().SelectMany(axis => new[] { new Orientation(axis, Pole.A), new Orientation(axis, Pole.B) });

    public string Code => (Axis, Pole) switch
    {
        (IdeologicalAxis.Hierarchy, Pole.A) => "EG",
        (IdeologicalAxis.Hierarchy, Pole.B) => "SE",
        (IdeologicalAxis.Production, Pole.A) => "PO",
        (IdeologicalAxis.Production, Pole.B) => "EC",
        (IdeologicalAxis.Property, Pole.A) => "PR",
        (IdeologicalAxis.Property, Pole.B) => "LI",
        _ => $"{Axis}:{Pole}",
    };
}

/// <summary>Effet concret d'une loi sur les structures qu'elle couvre.</summary>
public enum LawRule
{
    /// <summary>Le travail est payé avec les produits du bâtiment au lieu d'un salaire.</summary>
    PaymentInKind,
}

/// <summary>
/// Loi créée dans une structure. Les classes au pôle de son orientation l'acceptent
/// et produisent de la légitimité ; les autres la rejettent. Ses règles s'appliquent
/// à la structure et à tout ce qu'elle contient.
/// </summary>
public sealed record Law(
    string Id,
    string Name,
    string StructureId,
    IReadOnlyList<Orientation> Orientations,
    IReadOnlyList<LawRule>? Rules = null)
{
    public bool Has(LawRule rule) => Rules?.Contains(rule) == true;
}
