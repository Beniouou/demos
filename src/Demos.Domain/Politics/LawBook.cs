using Demos.Domain.Economy;
using Demos.Domain.Population;

namespace Demos.Domain.Politics;

/// <summary>Identifiants des briques que le code utilise directement.</summary>
public static class Bricks
{
    public const string PaymentInKind = "paiement-en-nature";
    public const string PrivateCompanies = "entreprises-privees";
}

/// <summary>
/// Mouvement d'un curseur, de From à To, autour de sa valeur de référence (la valeur héritée des niveaux supérieurs, ou
/// celle par défaut). Une loi en vigueur est le mouvement de la référence à sa valeur ; changer un curseur, le mouvement
/// de l'ancienne valeur à la nouvelle ; abroger, le retour à la référence.
/// </summary>
public readonly record struct BrickMovement(BrickDefinition Brick, LawSubject Subject, double From, double To, double Reference)
{
    public bool IsRaise => To > From;

    /// <summary>
    /// Écart de 0 à 1 : la part du chemin parcourue entre la référence et la borne, de chaque côté de la référence
    /// (de 2 à 2,5 avec une borne à 4 : 0,25).
    /// </summary>
    public double Intensity
    {
        get
        {
            var low = Math.Min(From, To);
            var high = Math.Max(From, To);
            var above = Math.Max(0, high - Math.Max(low, Reference));
            var below = Math.Max(0, Math.Min(high, Reference) - low);
            return Share(above, Brick.Max - Reference) + Share(below, Reference - Brick.Min);
        }
    }

    /// <summary>Les pôles du mouvement : ceux d'une hausse ou ceux d'une baisse.</summary>
    public IReadOnlyList<Orientation> Poles => Brick.PolesOf(Subject, IsRaise);

    private static double Share(double moved, double range) => moved <= 0 ? 0 : range <= 0 ? 1 : Math.Min(1, moved / range);
}

/// <summary>
/// Les lois en vigueur, lues avec le catalogue et les structures : la valeur d'une brique à un endroit, la valeur
/// héritée par une loi, et les membres qu'une brique touche.
/// Une loi s'applique dans sa structure et tout ce qu'elle contient. Entre deux niveaux, pour un paramètre ou une
/// autorisation, le plus bas l'emporte ; les modificateurs s'additionnent. Une brique verrouillée ne peut plus être
/// changée par les niveaux en dessous.
/// </summary>
public sealed class LawBook(
    LawCatalogue catalogue,
    IReadOnlyList<Law> laws,
    IReadOnlyDictionary<string, Structure> structures,
    Func<Structure, string?>? categoryOf = null,
    Func<Structure, string?>? needOf = null)
{
    public static LawBook Empty(IReadOnlyDictionary<string, Structure> structures) => new(LawCatalogue.Empty, [], structures);

    public LawCatalogue Catalogue { get; } = catalogue;
    public IReadOnlyList<Law> Laws { get; } = laws;
    public IReadOnlyDictionary<string, Structure> Structures { get; } = structures;

    /// <summary>Les structures qui contiennent celle-ci, de la plus large (l'État) à elle-même.</summary>
    public List<string> ChainOf(string structureId)
    {
        var chain = new List<string>();
        for (var current = Structures.GetValueOrDefault(structureId); current is not null;
             current = current.ParentId is { } parentId ? Structures.GetValueOrDefault(parentId) : null)
            chain.Insert(0, current.Id);
        return chain;
    }

    /// <summary>Valeur d'une brique pour un bâtiment, d'après les lois qui le couvrent et dont le sujet le vise.</summary>
    public double ValueFor(string brickId, Structure building)
    {
        if (Catalogue.BrickOf(brickId) is not { } brick)
            return 0;
        return Resolve(brick, ChainOf(building.Id), subject => Targets(subject, building), excluded: null);
    }

    /// <summary>
    /// Valeur de référence d'une brique d'une loi : celle que fixent les niveaux au-dessus de la loi, pour un sujet qui
    /// couvre le sien, ou la valeur par défaut. Pour un modificateur, 0 : les modificateurs s'additionnent.
    /// </summary>
    public double ReferenceOf(Law law, BrickSetting setting)
    {
        if (Catalogue.BrickOf(setting.Brick) is not { } brick)
            return 0;
        if (brick.Family == BrickFamily.Modifier)
            return 0;

        var above = ChainOf(law.StructureId);
        if (above.Count > 0)
            above.RemoveAt(above.Count - 1);
        return Resolve(brick, above, subject => subject.Covers(setting.Subject), excluded: law.Id);
    }

    /// <summary>Vrai si un niveau au-dessus de la loi a verrouillé cette brique pour un sujet qui recoupe le sien.</summary>
    public bool IsLockedAbove(Law law, BrickSetting setting)
    {
        var above = ChainOf(law.StructureId);
        if (above.Count > 0)
            above.RemoveAt(above.Count - 1);

        return above.Any(level => Laws.Any(other => other.Id != law.Id && other.StructureId == level && other.Bricks.Any(s =>
            s.Brick == setting.Brick && s.Locked && (s.Subject.Covers(setting.Subject) || setting.Subject.Covers(s.Subject)))));
    }

    /// <summary>Le mouvement d'une brique en vigueur : de sa référence à sa valeur.</summary>
    public BrickMovement? MovementOf(Law law, BrickSetting setting) =>
        Catalogue.BrickOf(setting.Brick) is { } brick
            ? new BrickMovement(brick, setting.Subject, ReferenceOf(law, setting), setting.Value, ReferenceOf(law, setting))
            : null;

    /// <summary>
    /// Part des membres d'une classe que la brique touche, pondérée : ceux que vise son sujet comptent pour 1, les autres
    /// membres du niveau pour <paramref name="untargetedWeight"/>.
    /// </summary>
    public double WeightedShare(SocialClass socialClass, LawSubject subject, Structure level, double untargetedWeight)
    {
        var inLevel = socialClass.ShareSubjectTo(level);
        if (inLevel <= 0)
            return 0;

        var targeted = Math.Min(inLevel, TargetedShare(socialClass, subject, level, inLevel));
        return targeted + untargetedWeight * (inLevel - targeted);
    }

    /// <summary>Vrai si le sujet vise ce bâtiment (son statut, son type, sa catégorie, le besoin que couvre son produit).</summary>
    public bool Targets(LawSubject subject, Structure building) => subject.Kind switch
    {
        SubjectKind.Everyone => true,
        SubjectKind.Ownership => subject.Values.Contains(building.Ownership.ToString()),
        SubjectKind.BuildingType => building.TypeId is { } typeId && subject.Values.Contains(typeId),
        SubjectKind.Category => categoryOf?.Invoke(building) is { } category && subject.Values.Contains(category),
        SubjectKind.Need => needOf?.Invoke(building) is { } need && subject.Values.Contains(need),
        _ => false,
    };

    private double TargetedShare(SocialClass socialClass, LawSubject subject, Structure level, double inLevel) => subject.Kind switch
    {
        SubjectKind.Everyone => inLevel,
        SubjectKind.Rank => subject.Values.Contains(socialClass.Rank.ToString()) ? inLevel : 0,
        _ => socialClass.Employment
            .Where(job => Structures.TryGetValue(job.Key, out var building)
                && building.Kind == StructureKind.Building
                && StructureTree.IsWithin(building, level.Id, Structures)
                && Targets(subject, building))
            .Sum(job => job.Value),
    };

    private double Resolve(BrickDefinition brick, IReadOnlyList<string> levels, Func<LawSubject, bool> applies, string? excluded)
    {
        var value = brick.Family == BrickFamily.Modifier ? 0 : brick.Default;
        foreach (var level in levels)
        {
            var settings = Laws
                .Where(law => law.Id != excluded && law.StructureId == level)
                .SelectMany(law => law.Bricks)
                .Where(s => s.Brick == brick.Id && applies(s.Subject))
                .ToList();
            if (settings.Count == 0)
                continue;

            value = brick.Family == BrickFamily.Modifier ? value + settings.Sum(s => s.Value) : settings[^1].Value;
            if (settings.Any(s => s.Locked))
                break;
        }

        return value;
    }
}
