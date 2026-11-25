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
            state.Torn);
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
    bool Torn);

/// <summary>Un axe de La Promesse et la part des habitants qui est à son pôle.</summary>
public sealed record PromiseAxisView(Orientation Pole, double Alignment);

/// <param name="Ink">L'encre de l'article dans le texte signé, de 0 (effacé) à 1 (net) : il pâlit avec ses trahisons.</param>
public sealed record PromiseEngagementView(string Id, Orientation Pole, EngagementKind Kind, string Text, string Breach, double Ink);

/// <param name="EngagementId">L'engagement trahi, ou null pour une cicatrice de refondation.</param>
public sealed record PromiseWoundView(string? EngagementId, string Cause, DateOnly Date, double Healing, double Permanent);
