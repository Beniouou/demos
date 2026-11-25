using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Application;

/// <summary>Un poste du budget : son enveloppe, ce qu'il a dépensé cette année, et le brouillon du prochain budget.</summary>
/// <param name="PerResident">Vrai pour les dotations : <paramref name="Voted"/> et <paramref name="Draft"/> sont des montants par habitant et par an.</param>
/// <param name="VotedTotal">L'enveloppe de l'année en argent (pour les dotations : le montant par habitant × les habitants du moment).</param>
/// <param name="Projected">La dépense de l'année au rythme actuel (pour les dotations, avec les habitants du moment).</param>
/// <param name="Cost">Ce que coûte, dans son pôle, le changement de ce poste dans le brouillon (0 s'il baisse ou ne bouge pas).</param>
public sealed record BudgetLineView(
    BudgetItem Item, Orientation Pole, bool PerResident, double Voted, double VotedTotal, double Spent, double Available,
    double Projected, double Draft, double Cost);

/// <summary>La caisse d'une commune, ses habitants et ce qu'elle reçoit chaque mois en dotation.</summary>
public sealed record CommuneFundView(string CommuneId, string Name, double Residents, double Fund, double MonthlyGrant);

/// <summary>Le budget de l'État, en lecture (décidé le 25 novembre 2026).</summary>
/// <param name="CountedSince">Le jour depuis lequel les dépenses de l'année sont comptées.</param>
/// <param name="Pending">Le budget voté qui attend le 1er du mois, poste par poste (null s'il n'y en a pas).</param>
public sealed record BudgetView(
    double Treasury,
    int Year,
    DateOnly CountedSince,
    double Residents,
    IReadOnlyList<BudgetLineView> Lines,
    IReadOnlyDictionary<Orientation, double> DraftCost,
    bool DraftChanged,
    IReadOnlyDictionary<BudgetItem, double>? Pending,
    IReadOnlyDictionary<Orientation, double> PendingPaid,
    IReadOnlyList<CommuneFundView> Communes);

public sealed partial class GameView
{
    public BudgetView Budget()
    {
        var budget = World.Budget;
        var today = Today;
        var since = budget.CountedSince ?? today;
        var counted = Math.Max(1, today.DayNumber - since.DayNumber + 1);
        var remaining = new DateOnly(today.Year, 12, 31).DayNumber - today.DayNumber;
        var communes = World.Communes.ToList();
        var residents = communes.ToDictionary(c => c.Id, c => World.Residents(c.Id));
        var population = residents.Values.Sum();
        var settings = Balance.Current;

        var lines = BudgetItems.All.Select(item =>
        {
            var perResident = BudgetItems.IsPerResident(item);
            var spent = budget.SpentOn(item);
            var votedTotal = perResident ? budget.Current[item] * population : budget.Current[item];
            var projected = perResident
                ? spent + budget.Current[item] / 12 * population * (12 - today.Month)
                : spent + spent / counted * remaining;
            var increase = (budget.Draft[item] - budget.Current[item]) * (perResident ? population : 1);
            var lineCost = increase < 0.5 ? 0 : settings.BudgetVoteBaseCost + increase / 1000 * settings.BudgetVoteCostPerThousand;
            return new BudgetLineView(item, BudgetItems.PoleOf(item), perResident, budget.Current[item], votedTotal, spent,
                Math.Max(0, votedTotal - spent), projected, budget.Draft[item], lineCost);
        }).ToList();

        var monthly = budget.Current[BudgetItem.Grants] / 12;
        var funds = communes.Select(c => new CommuneFundView(c.Id, c.Name, residents[c.Id], World.CommuneFundBalanceOf(c.Id),
            monthly * residents[c.Id])).ToList();

        return new BudgetView(
            World.Treasury.Balance,
            today.Year,
            since,
            population,
            lines,
            simulation.BudgetCostOf(budget.Draft),
            !budget.Draft.SameAs(budget.Current),
            budget.Voted is { } voted ? BudgetItems.All.ToDictionary(i => i, i => voted.Plan[i]) : null,
            budget.Voted?.Paid ?? new Dictionary<Orientation, double>(),
            funds);
    }
}
