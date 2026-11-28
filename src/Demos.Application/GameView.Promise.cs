using Demos.Domain.Shared;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Application;

/// <summary>La Promesse en lecture (décidé le 25 novembre 2026) : sa jauge, ses axes, ses engagements et leurs blessures.</summary>
public sealed partial class GameView
{
    /// <summary>La Promesse de la partie et l'état de sa jauge ; null en bac à sable.</summary>
    public PromiseView? Promise()
    {
        if (World.PromiseState is not { } state)
            return null;

        var settings = Balance.Current;
        return new PromiseView(
            state.Promise.Name,
            simulation.PromiseGauge ?? 1,
            state.Alignments(World.Classes).Select(a => new PromiseAxisView(a.Key, a.Value)).ToList(),
            state.Engagements.Select(e => new PromiseEngagementView(e.Id, e.Orientation, e.Kind, e.Text, e.Breach, state.InkOf(e.Id))).ToList(),
            state.Wounds.Select(w => new PromiseWoundView(w.EngagementId, w.Cause, w.Date, w.Healing, w.Permanent)).ToList(),
            simulation.ContraryCharterShare(),
            state.MonthsBelowThreshold,
            settings.PromiseRuptureThreshold,
            settings.PromiseRuptureMonths,
            state.Torn,
            state.PeopleName,
            state.Oath,
            state.FoundingLaws,
            state.Signature);
    }

    /// <summary>Les noms des bâtiments d'une commune (fenêtre de l'arrivée en exil).</summary>
    public IReadOnlyList<string> BuildingsOf(string communeId) => World.Structures.Values
        .Where(s => s.Kind == StructureKind.Building && World.CommuneOf(s) == communeId)
        .Select(World.DisplayNameOf).ToList();

    /// <summary>Vrai si une loi d'extension couvre cette commune (fonder, extraire au-delà de 8 km).</summary>
    public bool AllowsExtension(string communeId) => World.LawBook.AllowsExtension(communeId);

    /// <summary>Avant un vote : ce que ce brouillon trahirait de La Promesse (vide si rien, ou en bac à sable).</summary>
    public IReadOnlyList<PromiseBreachView> PromiseBreachesOfDraft(string draftId) => BreachViews(simulation.PromiseBreachesOfDraft(draftId));

    /// <summary>Avant de vendre un bâtiment.</summary>
    public IReadOnlyList<PromiseBreachView> PromiseBreachesOfSale(string buildingId) => BreachViews(simulation.PromiseBreachesOfSale(buildingId));

    /// <summary>Avant de nationaliser un bâtiment (rachat ou expropriation).</summary>
    public IReadOnlyList<PromiseBreachView> PromiseBreachesOfNationalization(NationalizationMode mode) =>
        BreachViews(simulation.PromiseBreachesOfNationalization(mode));

    /// <summary>Avant de fonder une commune.</summary>
    public IReadOnlyList<PromiseBreachView> PromiseBreachesOfFounding() => BreachViews(simulation.PromiseBreachesOfFounding());

    /// <summary>Avant de poser un bâtiment à cet endroit.</summary>
    public IReadOnlyList<PromiseBreachView> PromiseBreachesOfConstruction(string typeId, string? productId, string communeId, Demos.Domain.Map.Point2D? position) =>
        BreachViews(simulation.PromiseBreachesOfConstruction(typeId, productId, communeId, position));

    /// <summary>Ce qu'une trahison coûterait (décidé le 25 novembre 2026, réglages de la feuille Passages).</summary>
    private IReadOnlyList<PromiseBreachView> BreachViews(IReadOnlyList<EngagementDefinition> broken)
    {
        if (World.PromiseState is not { } state || broken.Count == 0)
            return [];

        var settings = Balance.Current;
        var permanent = Math.Min(settings.PromiseBetrayalPermanent, settings.PromiseBetrayalWound);
        var healing = settings.PromiseBetrayalWound - permanent;
        return broken.Select(e =>
        {
            var pole = e.Orientation;
            var hit = World.Classes.Count(c => c.Population > 0 && c.Ideology[pole.Axis] == pole.Pole);
            return new PromiseBreachView(
                e.Id, state.Engagements.ToList().IndexOf(e) + 1, e.Text, pole,
                settings.PromiseBetrayalWound, permanent,
                settings.PromiseHealingPerMonth > 0 ? (int)Math.Ceiling(healing / settings.PromiseHealingPerMonth) : 0,
                settings.BetrayalAdhesionLoss, hit);
        }).ToList();
    }
}

/// <param name="Gauge">La jauge de la promesse, de 0 à 1.</param>
/// <param name="ContraryCharterShare">La part de la population dans une commune à charte contraire.</param>
/// <param name="MonthsBelowThreshold">Les mois de suite passés sous le seuil de rupture.</param>
public sealed record PromiseView(
    string Name,
    double Gauge,
    IReadOnlyList<PromiseAxisView> Axes,
    IReadOnlyList<PromiseEngagementView> Engagements,
    IReadOnlyList<PromiseWoundView> Wounds,
    double ContraryCharterShare,
    int MonthsBelowThreshold,
    double RuptureThreshold,
    int RuptureMonths,
    bool Torn,
    string PeopleName,
    PromiseOath Oath,
    IReadOnlyList<string> FoundingLaws,
    IReadOnlyList<IReadOnlyList<Demos.Domain.Map.Point2D>> Signature);

/// <summary>
/// Un engagement qu'un acte romprait, et ce que la trahison coûterait : la blessure de la jauge (dont une part pour
/// toujours, le reste guéri en <paramref name="HealingMonths"/> mois), la méfiance des classes attachées au pôle.
/// </summary>
/// <param name="Article">Le numéro de l'article dans le texte signé (1 à 3).</param>
/// <param name="ClassesHit">Le nombre de classes attachées au pôle trahi, qui perdent de l'adhésion.</param>
public sealed record PromiseBreachView(
    string EngagementId,
    int Article,
    string Text,
    Orientation Pole,
    double Wound,
    double Permanent,
    int HealingMonths,
    double AdhesionLoss,
    int ClassesHit);

/// <summary>Un axe de La Promesse et la part des habitants qui est à son pôle.</summary>
public sealed record PromiseAxisView(Orientation Pole, double Alignment);

/// <param name="Ink">L'encre de l'article dans le texte signé, de 0 (effacé) à 1 (net) : il pâlit avec ses trahisons.</param>
public sealed record PromiseEngagementView(string Id, Orientation Pole, EngagementKind Kind, string Text, string Breach, double Ink);

/// <param name="EngagementId">L'engagement trahi, ou null pour une cicatrice de refondation.</param>
public sealed record PromiseWoundView(string? EngagementId, string Cause, DateOnly Date, double Healing, double Permanent);
