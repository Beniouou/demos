using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Game;

/// <summary>Les décisions du joueur sur le budget de l'État (Économie → Budget).</summary>
public sealed partial class PlayerDesk
{
    public void SetBudgetDraft(BudgetItem item, double amount) => Simulation.SetBudgetDraft(item, amount);

    public void ResetBudgetDraft() => Simulation.ResetBudgetDraft();

    public string VoteBudget()
    {
        var cost = Simulation.BudgetCostOf(World.Budget.Draft);
        return Simulation.TryVoteBudget() switch
        {
            BudgetVoteOutcome.Voted => $"Budget voté pour {Paid(cost)} : il entre en vigueur le 1er du mois prochain.",
            BudgetVoteOutcome.AlreadyVoted => "Un budget est déjà voté : il attend le 1er du mois. Annulez ce vote pour en voter un autre.",
            BudgetVoteOutcome.Unchanged => "Le brouillon est le budget en vigueur : il n'y a rien à voter.",
            _ => "Pas assez de légitimité, même à crédit, pour voter ce budget.",
        };
    }

    public string CancelBudgetVote()
    {
        var paid = World.Budget.Voted?.Paid ?? new Dictionary<Orientation, double>();
        return Simulation.CancelBudgetVote()
            ? $"Vote du budget annulé : {Paid(paid)} rendu."
            : "Aucun budget n'attend le 1er du mois.";
    }
}
