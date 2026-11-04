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

    /// <summary>Le pôle d'un code (« EG », « LI »…).</summary>
    public static Orientation FromCode(string code)
    {
        var wanted = code.Trim().ToUpperInvariant();
        foreach (var pole in All)
            if (pole.Code == wanted)
                return pole;
        throw new ArgumentException($"Pôle inconnu : « {code} ».", nameof(code));
    }

    /// <summary>Le pôle opposé, sur le même axe.</summary>
    public Orientation Opposite => this with { Pole = Pole == Pole.A ? Pole.B : Pole.A };
}

/// <summary>Famille d'une brique de loi.</summary>
public enum BrickFamily
{
    /// <summary>Fixe une valeur (taux, plafond) : entre deux niveaux, une seule loi s'applique.</summary>
    Parameter,

    /// <summary>Ajuste une valeur qui existe déjà, en part (−0,2 = −20 %) : les lois de chaque niveau s'additionnent.</summary>
    Modifier,

    /// <summary>Autorise (1) ou interdit (0) quelque chose : entre deux niveaux, une seule loi s'applique.</summary>
    Permission,
}

/// <summary>Type de sujet d'une brique : à qui ou à quoi elle s'applique dans son niveau.</summary>
public enum SubjectKind
{
    /// <summary>Tout le monde, ou tout ce que contient le niveau.</summary>
    Everyone,

    /// <summary>Un rang (Workers, Intermediate, Executives).</summary>
    Rank,

    /// <summary>Les bâtiments d'un statut (Public, Private, Cooperative) et ceux qui y travaillent.</summary>
    Ownership,

    /// <summary>Les bâtiments de certains types (« carriere,mine ») et ceux qui y travaillent.</summary>
    BuildingType,

    /// <summary>Les bâtiments de certaines catégories du catalogue (« Extraction,Production ») et ceux qui y travaillent.</summary>
    Category,

    /// <summary>Les bâtiments dont le produit couvre un besoin (« Food ») et ceux qui y travaillent.</summary>
    Need,
}

/// <summary>Sujet d'une brique. La valeur dépend du type : un rang, un statut, des types ou des catégories séparés par des virgules.</summary>
public sealed record LawSubject(SubjectKind Kind, string? Value = null)
{
    public static readonly LawSubject Everyone = new(SubjectKind.Everyone);

    /// <summary>Les valeurs, une par élément de la liste.</summary>
    public IReadOnlyList<string> Values =>
        Value is null ? [] : Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Vrai si ce sujet couvre tout ce que couvre l'autre (le sujet « tout le monde » couvre tout).</summary>
    public bool Covers(LawSubject other) =>
        Kind == SubjectKind.Everyone || (Kind == other.Kind && other.Values.All(Values.Contains));
}

/// <summary>Une brique réglée dans une loi : son sujet, la valeur de son curseur et sa case « verrouillée ».</summary>
public sealed record BrickSetting(string Brick, LawSubject Subject, double Value, bool Locked = false);

/// <summary>
/// Loi à briques, créée dans une structure (son niveau) et dans un seul domaine : une à trois briques de ce domaine,
/// chacune avec son sujet et son curseur. Elle s'applique à la structure et à tout ce qu'elle contient. Les classes
/// jugent chaque brique sur ses pôles (selon le sens du curseur) : celles qui l'acceptent produisent de la légitimité.
/// </summary>
public sealed record Law(
    string Id,
    string Name,
    string Domain,
    string StructureId,
    IReadOnlyList<BrickSetting> Bricks,
    string? TemplateId = null);
