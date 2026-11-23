using Demos.Domain.Politics;

namespace Demos.Application;

/// <summary>Le relevé de la légitimité, en lecture (décidé le 23 novembre 2026).</summary>
public sealed partial class GameView
{
    private (int Days, DateOnly Last, int Gains, int Spendings) _ledgerKey;
    private LegitimacyLedgerView? _ledgerView;

    /// <summary>
    /// Ce que chaque groupe a produit sur les jours gardés (au plus 30), loi par loi et pôle par pôle, et les dépenses de
    /// ces jours, la plus récente d'abord. Recalculé seulement quand le relevé a changé.
    /// </summary>
    public LegitimacyLedgerView LegitimacyLedger()
    {
        var ledger = World.Legitimacy.Ledger;
        var days = ledger.GainDays;
        var key = (days.Count, days.Count > 0 ? days[^1].Date : default, days.Count > 0 ? days[^1].Gains.Count : 0, ledger.Spendings.Count);
        if (_ledgerView is not null && key == _ledgerKey)
            return _ledgerView;

        var last = days.Count > 0 ? days[^1] : null;
        var gains = days.SelectMany(d => d.Gains).GroupBy(g => g.Key).Select(group =>
        {
            var socialClass = World.ClassesById.GetValueOrDefault(group.Key.ClassId);
            return new LegitimacyGainView(
                group.Key.Pole,
                group.Key.LawId,
                World.Laws.FirstOrDefault(l => l.Id == group.Key.LawId)?.Name ?? group.Key.LawId,
                group.Key.ClassId,
                socialClass?.Name ?? group.Key.ClassId,
                socialClass?.CommuneId,
                last?.Gains.GetValueOrDefault(group.Key) ?? 0,
                group.Sum(g => g.Value));
        }).ToList();

        var spendings = ledger.Spendings.Reverse().Select(s => new LegitimacySpendingView(s.Date, s.Pole, s.Amount, s.Kind,
            s.Kind is LegitimacySpendingKind.LawVote or LegitimacySpendingKind.VoteCancelled ? s.Subject : NameOf(s.Subject))).ToList();

        _ledgerKey = key;
        _ledgerView = new LegitimacyLedgerView(days.Count, gains, spendings);
        return _ledgerView;
    }

    /// <summary>
    /// L'efficacité d'une loi en vigueur dans une commune (de 0 à 1) : la part de ses effets, et de la légitimité qu'elle
    /// produit, qui arrive jusque-là (extension et bureaucratie). 1 pour une loi inconnue.
    /// </summary>
    public double LawEfficiencyIn(string lawId, string communeId)
    {
        var book = World.LawBook;
        if (World.Laws.FirstOrDefault(l => l.Id == lawId) is not { } law
            || !book.Structures.TryGetValue(law.StructureId, out var level)
            || !book.Structures.TryGetValue(communeId, out var commune))
            return 1;
        return book.EfficiencyFor(level.Id, commune);
    }
}

/// <summary>
/// Le relevé de la légitimité : le nombre de jours joués gardés (au plus 30), les gains et les dépenses de ces jours.
/// </summary>
public sealed record LegitimacyLedgerView(int Days, IReadOnlyList<LegitimacyGainView> Gains, IReadOnlyList<LegitimacySpendingView> Spendings);

/// <summary>
/// Ce qu'un groupe a produit dans un pôle en acceptant une loi : au dernier jour joué, et en tout sur les jours gardés.
/// </summary>
public sealed record LegitimacyGainView(
    Orientation Pole,
    string LawId,
    string LawName,
    string ClassId,
    string ClassName,
    string? CommuneId,
    double LastDay,
    double Total);

/// <summary>Une dépense de légitimité (négative quand elle est rendue) et le nom de ce qu'elle visait.</summary>
public sealed record LegitimacySpendingView(DateOnly Date, Orientation Pole, double Amount, LegitimacySpendingKind Kind, string Subject);
