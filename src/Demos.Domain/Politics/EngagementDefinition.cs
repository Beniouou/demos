namespace Demos.Domain.Politics;

/// <summary>Sorte d'engagement de La Promesse (décidé le 23 novembre 2026).</summary>
public enum EngagementKind
{
    /// <summary>Rompu par toute une famille d'actes (toute loi qui favorise le pôle opposé…) : large et exigeant.</summary>
    Principle,

    /// <summary>Rompu par un acte précis : plus étroit, plus facile à tenir.</summary>
    Concrete,
}

/// <summary>
/// Un engagement de La Promesse : il porte sur un acte du joueur, jamais sur l'état de la société, et l'acte n'est jamais
/// bloqué (le rompre est une trahison). Le joueur en choisit un par axe de sa Promesse. Catalogue dans
/// game/data/engagements.json, feuille Engagements du tableur ; sans effet sur la partie pour l'instant.
/// </summary>
/// <param name="Pole">Le code du pôle que l'engagement défend (« EG », « CO »…).</param>
/// <param name="Breach">L'acte qui rompt l'engagement, en clair.</param>
/// <param name="Difficulty">De 1 (facile à tenir) à 3 (exigeant).</param>
/// <param name="Kept">Faux si l'engagement est écarté du catalogue (gardé pour mémoire).</param>
/// <param name="Note">Ce qui reste à créer ou à vérifier dans le jeu pour pouvoir le juger.</param>
/// <param name="Rules">Les actes qui rompent l'engagement, lus par la simulation (un seul suffit).</param>
public sealed record EngagementDefinition(
    string Id,
    string Pole,
    EngagementKind Kind,
    string Text,
    string Breach,
    int Difficulty,
    bool Kept = true,
    string? Note = null,
    IReadOnlyList<EngagementRule>? Rules = null)
{
    public Orientation Orientation => Orientation.FromCode(Pole);

    /// <summary>Vrai si ce changement de loi (une brique qui bouge) rompt l'engagement.</summary>
    public bool IsBrokenBy(BrickMovement movement, LawChangeKind kind, string domain) =>
        (Rules ?? []).Any(r => r.MatchesVote(movement, kind, domain, Orientation));

    /// <summary>Vrai si cet acte hors des lois (vendre, nationaliser, fonder…) rompt l'engagement.</summary>
    public bool IsBrokenBy(EngagementAct act, string? need = null, string? typeId = null) =>
        (Rules ?? []).Any(r => r.MatchesAct(act, need, typeId));
}

/// <summary>Les actes du joueur qu'un engagement peut viser.</summary>
public enum EngagementAct
{
    /// <summary>Une loi qui entre en vigueur (votée, changée ou abrogée).</summary>
    LawVote,
    Sale,
    Nationalization,
    Expropriation,
    Founding,
    /// <summary>Poser un bâtiment d'extraction au-delà du ressort de la commune.</summary>
    FarExtraction,
    /// <summary>Poser un bâtiment d'extraction sur une forêt (pas encore repéré par le jeu).</summary>
    ForestExtraction,
}

/// <summary>Le sens d'un curseur de loi.</summary>
public enum BrickDirection
{
    Raise,
    Lower,
}

/// <summary>
/// Un acte qui rompt un engagement. Pour une loi : toute brique qui favorise le pôle opposé (<paramref name="TowardOpposite"/>,
/// au besoin dans un seul domaine ou seulement pour un changement ou une abrogation), ou une brique précise dans un sens,
/// pour un rang. Pour un autre acte : l'acte, au besoin pour un besoin (« Food ») ou des types de bâtiments.
/// </summary>
public sealed record EngagementRule(
    EngagementAct Act,
    bool TowardOpposite = false,
    bool OnlyChanges = false,
    string? Domain = null,
    string? Brick = null,
    BrickDirection? Direction = null,
    string? Subject = null,
    string? Need = null,
    IReadOnlyList<string>? TypeIds = null)
{
    public bool MatchesVote(BrickMovement movement, LawChangeKind kind, string domain, Orientation pole)
    {
        if (Act != EngagementAct.LawVote || movement.Intensity <= 0)
            return false;
        if (OnlyChanges && kind == LawChangeKind.Enact)
            return false;
        if (Domain is not null && Domain != domain)
            return false;
        if (TowardOpposite)
            return movement.Poles.Contains(pole.Opposite);

        return movement.Brick.Id == Brick
            && (Direction is null || (Direction == BrickDirection.Raise) == movement.IsRaise)
            && (Subject is null || movement.Subject.Kind == SubjectKind.Rank && movement.Subject.Values.Contains(Subject));
    }

    public bool MatchesAct(EngagementAct act, string? need, string? typeId) =>
        Act == act
        && (Need is null || Need == need)
        && (TypeIds is null || typeId is not null && TypeIds.Contains(typeId));
}
