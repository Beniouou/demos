namespace Demos.Domain.Politics;

/// <summary>Domaine du droit. Un domaine régalien est normalement du rôle de l'État : voté plus bas, il coûte plus cher.</summary>
public sealed record LawDomain(string Id, string Name, bool Regalian = false);

/// <summary>
/// Pôles d'une brique selon le sens du curseur. Sans sujet, la règle vaut pour tous ; avec un sujet, elle ne vaut que
/// pour lui (un impôt en hausse sur les cadres est EG et PR, sur tout le monde seulement PR).
/// </summary>
public sealed record BrickPoles(IReadOnlyList<Orientation> Raise, IReadOnlyList<Orientation> Lower, LawSubject? Subject = null);

/// <summary>
/// Brique du catalogue : son domaine, sa famille, les sujets qu'elle accepte, son curseur (valeur par défaut, bornes),
/// son poids et ses pôles. Une autorisation ou interdiction a un curseur de 0 (interdit) à 1 (autorisé).
/// </summary>
public sealed record BrickDefinition(
    string Id,
    string Name,
    string Domain,
    BrickFamily Family,
    IReadOnlyList<SubjectKind> Subjects,
    double Default,
    double Min,
    double Max,
    IReadOnlyList<BrickPoles> Poles,
    double Weight = 1,
    string? Description = null)
{
    /// <summary>Les pôles d'un mouvement du curseur, pour un sujet : ceux de la règle de ce sujet, sinon ceux de la règle générale.</summary>
    public IReadOnlyList<Orientation> PolesOf(LawSubject subject, bool raise)
    {
        var rule = Poles.FirstOrDefault(p => p.Subject is { } s && s.Kind == subject.Kind && s.Values.SequenceEqual(subject.Values))
            ?? Poles.FirstOrDefault(p => p.Subject is null);
        return rule is null ? [] : raise ? rule.Raise : rule.Lower;
    }
}

/// <summary>Modèle de loi : une loi déjà remplie que le joueur copie dans la commission, avec un niveau par défaut modifiable.</summary>
public sealed record LawTemplate(
    string Id,
    string Name,
    string Domain,
    string Level,
    IReadOnlyList<BrickSetting> Bricks,
    string? Description = null)
{
    public Law ToLaw(string lawId, string? level = null) => new(lawId, Name, Domain, level ?? Level, Bricks, Id);
}

/// <summary>Le catalogue des lois (game/data/lois.json) : les domaines, les briques et les modèles.</summary>
public sealed record LawCatalogue(
    IReadOnlyList<LawDomain> Domains,
    IReadOnlyList<BrickDefinition> Bricks,
    IReadOnlyList<LawTemplate> Templates)
{
    public static readonly LawCatalogue Empty = new([], [], []);

    /// <summary>Nombre maximal de briques dans une loi (décidé).</summary>
    public const int MaxBricksPerLaw = 3;

    public BrickDefinition? BrickOf(string id) => Bricks.FirstOrDefault(b => b.Id == id);

    public LawDomain? DomainOf(string id) => Domains.FirstOrDefault(d => d.Id == id);

    public LawTemplate? TemplateOf(string id) => Templates.FirstOrDefault(t => t.Id == id);

    /// <summary>
    /// Ce qui empêche une loi d'exister, ou null si elle est valable : un seul domaine, une à trois briques de ce
    /// domaine, des sujets acceptés par chaque brique et des curseurs entre leurs bornes.
    /// </summary>
    public string? ProblemWith(Law law)
    {
        if (DomainOf(law.Domain) is null)
            return $"domaine inconnu « {law.Domain} »";
        if (law.Bricks.Count is 0 or > MaxBricksPerLaw)
            return $"une loi a de 1 à {MaxBricksPerLaw} briques";

        foreach (var setting in law.Bricks)
        {
            if (BrickOf(setting.Brick) is not { } brick)
                return $"brique inconnue « {setting.Brick} »";
            if (brick.Domain != law.Domain)
                return $"la brique « {brick.Name} » n'est pas du domaine de la loi";
            if (!brick.Subjects.Contains(setting.Subject.Kind))
                return $"la brique « {brick.Name} » n'accepte pas ce sujet";
            if (setting.Value < brick.Min || setting.Value > brick.Max)
                return $"le curseur de « {brick.Name} » sort de ses bornes";
        }

        return law.Bricks.GroupBy(b => (b.Brick, b.Subject)).Any(g => g.Count() > 1)
            ? "la même brique est réglée deux fois pour le même sujet"
            : null;
    }
}
