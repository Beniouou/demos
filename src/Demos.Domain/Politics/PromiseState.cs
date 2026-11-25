using Demos.Domain.Economy;
using Demos.Domain.Population;

namespace Demos.Domain.Politics;

/// <summary>
/// Une blessure de la jauge de la promesse (décidé le 25 novembre 2026) : une part qui guérit avec le temps, une part qui
/// reste pour toute la partie. Elle vient d'un engagement trahi, ou d'une cicatrice de refondation.
/// </summary>
/// <param name="EngagementId">L'engagement trahi, ou null pour une cicatrice de refondation.</param>
/// <param name="Cause">Ce qui l'a causée, en clair (« vote de la loi … », « vente de … »).</param>
public sealed class PromiseWound(string? engagementId, string cause, DateOnly date, double healing, double permanent)
{
    public string? EngagementId { get; } = engagementId;
    public string Cause { get; } = cause;
    public DateOnly Date { get; } = date;
    public double Healing { get; private set; } = healing;
    public double Permanent { get; } = permanent;
    public double Depth => Healing + Permanent;

    /// <summary>Guérit d'une part, sans descendre sous zéro.</summary>
    public void Heal(double amount) => Healing = Math.Max(0, Healing - amount);
}

/// <summary>
/// La Promesse de la partie (mode avec promesse) : la combinaison de pôles, l'engagement choisi sur chacun de ses axes,
/// les blessures et le compte des mois passés sous le seuil de rupture. La jauge vaut l'alignement de l'axe le plus faible
/// (la part des habitants au pôle de La Promesse) moins les blessures et la part de la population qui vit dans une
/// commune à charte contraire (× 0,10) ; sous 0,3 pendant 12 mois, le pacte se déchire (propositions, réglages).
/// </summary>
public sealed class PromiseState
{
    private readonly List<PromiseWound> _wounds = [];

    public PromiseState(PromiseDefinition promise, IReadOnlyList<EngagementDefinition> engagements, double scar = 0, DateOnly founded = default)
    {
        Promise = promise;
        Engagements = engagements;
        if (scar > 0)
            _wounds.Add(new PromiseWound(null, "refondation : ce peuple a déjà renié une promesse", founded, 0, scar));
    }

    public PromiseDefinition Promise { get; }

    /// <summary>Les engagements choisis, un par axe de La Promesse.</summary>
    public IReadOnlyList<EngagementDefinition> Engagements { get; }

    public IReadOnlyList<PromiseWound> Wounds => _wounds;

    /// <summary>Les mois de suite passés sous le seuil de rupture.</summary>
    public int MonthsBelowThreshold { get; private set; }

    /// <summary>Vrai une fois le pacte déchiré : la jauge ne compte plus, le joueur doit choisir la suite.</summary>
    public bool Torn { get; private set; }

    /// <summary>
    /// Ce qui ne va pas dans ce choix de Promesse et d'engagements, ou null : un engagement par axe de La Promesse,
    /// au pôle de La Promesse sur cet axe, et retenu dans le catalogue.
    /// </summary>
    public static string? ProblemWith(PromiseDefinition promise, IReadOnlyList<EngagementDefinition> engagements)
    {
        var poles = promise.Orientations;
        if (engagements.Count != poles.Count)
            return $"il faut un engagement par axe de La Promesse ({poles.Count})";
        foreach (var pole in poles)
        {
            var onAxis = engagements.Where(e => e.Orientation.Axis == pole.Axis).ToList();
            if (onAxis.Count != 1)
                return "il faut un seul engagement par axe";
            if (onAxis[0].Orientation != pole)
                return $"l'engagement « {onAxis[0].Text} » ne défend pas le pôle {pole.Code} de La Promesse";
            if (!onAxis[0].Kept)
                return $"l'engagement « {onAxis[0].Text} » est écarté du catalogue";
        }

        return null;
    }

    /// <summary>L'alignement d'un axe de La Promesse : la part des habitants au pôle visé, chacun comptant pour un.</summary>
    public static double AlignmentOf(Orientation pole, IEnumerable<SocialClass> classes)
    {
        var people = classes.Where(c => c.Population > 0).ToList();
        var total = people.Sum(c => (double)c.Population);
        return total <= 0 ? 1 : people.Where(c => c.Ideology[pole.Axis] == pole.Pole).Sum(c => (double)c.Population) / total;
    }

    /// <summary>L'alignement de chaque axe de La Promesse.</summary>
    public IReadOnlyDictionary<Orientation, double> Alignments(IEnumerable<SocialClass> classes)
    {
        var list = classes.ToList();
        return Promise.Orientations.ToDictionary(p => p, p => AlignmentOf(p, list));
    }

    /// <summary>La somme des blessures.</summary>
    public double WoundDepth => _wounds.Sum(w => w.Depth);

    /// <summary>
    /// La jauge, de 0 à 1 : l'alignement de l'axe le plus faible, moins les blessures, moins la part de la population dans
    /// des communes à charte contraire × la pénalité des chartes.
    /// </summary>
    public double Gauge(IEnumerable<SocialClass> classes, double contraryCharterShare)
    {
        var weakest = Alignments(classes).Values.DefaultIfEmpty(1).Min();
        return Math.Clamp(weakest - WoundDepth - contraryCharterShare * Balance.Current.PromiseCharterPenalty, 0, 1);
    }

    /// <summary>L'encre d'un article, de 0 à 1 : il pâlit avec les blessures de son engagement (une blessure pleine en ôte un quart).</summary>
    public double InkOf(string engagementId) =>
        Math.Clamp(1 - 2.5 * _wounds.Where(w => w.EngagementId == engagementId).Sum(w => w.Depth), 0, 1);

    /// <summary>Une trahison d'engagement : une blessure, dont une part guérira.</summary>
    public PromiseWound Betray(EngagementDefinition engagement, string cause, DateOnly date)
    {
        var settings = Balance.Current;
        var wound = new PromiseWound(engagement.Id, cause, date,
            Math.Max(0, settings.PromiseBetrayalWound - settings.PromiseBetrayalPermanent), settings.PromiseBetrayalPermanent);
        _wounds.Add(wound);
        return wound;
    }

    /// <summary>
    /// Le bilan du mois : les blessures guérissent, et le compte des mois sous le seuil avance (ou repart de zéro). Renvoie
    /// vrai si le pacte vient de se déchirer.
    /// </summary>
    public bool RunMonthly(double gauge)
    {
        if (Torn)
            return false;

        var settings = Balance.Current;
        foreach (var wound in _wounds)
            wound.Heal(settings.PromiseHealingPerMonth);

        MonthsBelowThreshold = gauge < settings.PromiseRuptureThreshold ? MonthsBelowThreshold + 1 : 0;
        if (MonthsBelowThreshold < settings.PromiseRuptureMonths)
            return false;

        Torn = true;
        return true;
    }
}
