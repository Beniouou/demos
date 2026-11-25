using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Application;

/// <summary>Ce qu'a donné le vote du budget.</summary>
public enum BudgetVoteOutcome
{
    Voted,

    /// <summary>Un budget est déjà voté et attend le 1er du mois.</summary>
    AlreadyVoted,

    /// <summary>Le brouillon est le budget en vigueur : il n'y a rien à voter.</summary>
    Unchanged,

    NotEnoughLegitimacy,
}

/// <summary>
/// Le budget de l'État (décidé le 25 novembre 2026) : l'enveloppe annuelle de chaque poste, votée comme une loi (en
/// vigueur le 1er du mois suivant), et les dotations versées chaque mois aux communes.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Un budget voté vient d'entrer en vigueur (le 1er du mois).</summary>
    public event Action<BudgetPlan>? BudgetEnacted;

    /// <summary>Le coût d'un budget s'il était voté maintenant, par pôle, par rapport au budget en vigueur.</summary>
    public IReadOnlyDictionary<Orientation, double> BudgetCostOf(BudgetPlan plan) =>
        StateBudget.CostOf(World.Budget.Current, plan, Balance.Current, World.Communes.Sum(c => World.Residents(c.Id)));

    /// <summary>Action du joueur : change l'enveloppe d'un poste dans le brouillon du prochain budget.</summary>
    public void SetBudgetDraft(BudgetItem item, double amount) => World.Budget.Draft = World.Budget.Draft.With(item, amount);

    /// <summary>Action du joueur : le brouillon repart du budget en vigueur.</summary>
    public void ResetBudgetDraft() => World.Budget.Draft = World.Budget.Current;

    /// <summary>
    /// Action du joueur : vote le brouillon. La légitimité est payée tout de suite (à crédit si besoin, comme une loi) ;
    /// le budget entre en vigueur le 1er du mois suivant.
    /// </summary>
    public BudgetVoteOutcome TryVoteBudget()
    {
        var budget = World.Budget;
        if (budget.Voted is not null)
            return BudgetVoteOutcome.AlreadyVoted;
        if (budget.Draft.SameAs(budget.Current))
            return BudgetVoteOutcome.Unchanged;

        var cost = BudgetCostOf(budget.Draft);
        var maxDebt = Balance.Current.LegitimacyMaxDebt;
        if (cost.Any(c => !World.Legitimacy.CanSpendOnCredit(c.Key, c.Value, maxDebt)))
            return BudgetVoteOutcome.NotEnoughLegitimacy;

        foreach (var (pole, amount) in cost)
            World.Legitimacy.TrySpendOnCredit(pole, amount, maxDebt, LegitimacySpendingKind.BudgetVote, "");
        budget.MarkVoted(budget.Draft, cost);
        return BudgetVoteOutcome.Voted;
    }

    /// <summary>Action du joueur : annule le vote du budget avant le 1er du mois ; la légitimité est rendue.</summary>
    public bool CancelBudgetVote()
    {
        if (World.Budget.Unvote() is not { } voted)
            return false;

        foreach (var (pole, amount) in voted.Paid)
            World.Legitimacy.Add(pole, amount, LegitimacySpendingKind.BudgetVoteCancelled, "");
        return true;
    }

    /// <summary>Le 1er du mois : au 1er janvier, les enveloppes repartent entières ; puis le budget voté entre en vigueur.</summary>
    private void RunBudgetMonthly()
    {
        var budget = World.Budget;
        if (Clock.Today is { Month: 1, Day: 1 })
            budget.StartYear(Clock.Today);
        if (budget.EnactVoted() is { } enacted)
        {
            budget.Draft = enacted;
            BudgetEnacted?.Invoke(enacted);
        }
    }

    /// <summary>
    /// Les dotations du mois (décidé le 25 novembre 2026) : chaque commune reçoit un douzième du montant annuel par habitant
    /// × ses habitants du moment, une fois par mois (le 1er, ou le premier jour joué) ; une commune qui grandit reçoit donc
    /// plus. Si le trésor ne suffit pas, chacune reçoit la même part de ce qu'il a. Le trésor ne prend plus le relais d'une
    /// caisse communale vide.
    /// </summary>
    private void PayGrantsIfDue()
    {
        var budget = World.Budget;
        budget.StartCountingOn(Clock.Today);
        var month = Clock.Today.Year * 12 + Clock.Today.Month;
        if (budget.GrantsPaidFor == month)
            return;
        budget.GrantsPaidFor = month;

        var communes = World.Communes.ToList();
        if (communes.Count == 0)
            return;

        var perResident = budget.Current[BudgetItem.Grants] / 12;
        var due = communes.ToDictionary(c => c.Id, c => perResident * World.Residents(c.Id));
        var wanted = due.Values.Sum();
        var envelope = World.EnvelopeOf(BudgetItem.Grants);
        var paid = Math.Min(wanted, envelope.Balance);
        if (paid <= 0 || !envelope.TrySpend(paid))
            return;

        foreach (var (communeId, amount) in due)
            World.CommuneFundOf(communeId).Deposit(amount * paid / wanted);
    }
}
