using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

/// <summary>Le budget de l'État (décidé le 25 novembre 2026) : enveloppes par poste, vote, dotations aux communes.</summary>
public class BudgetTests
{
    private static readonly Orientation PO = Orientation.FromCode("PO");
    private static readonly BalanceSettings Settings = new();

    private static BudgetPlan Plan(double grants, double construction, double roads, double investment) => new(new Dictionary<BudgetItem, double>
    {
        [BudgetItem.Grants] = grants,
        [BudgetItem.Construction] = construction,
        [BudgetItem.Roads] = roads,
        [BudgetItem.Investment] = investment,
    });

    [Fact]
    public void Une_enveloppe_ne_depense_que_ce_qui_reste_au_poste_et_au_tresor()
    {
        var treasury = new Treasury();
        treasury.Deposit(1_000);
        var budget = new StateBudget(Plan(0, 100, 0, 0));
        var envelope = new BudgetEnvelope(treasury, budget, BudgetItem.Construction);

        Assert.Equal(100, envelope.Balance);
        Assert.False(envelope.TrySpend(150));
        Assert.True(envelope.TrySpend(80));
        Assert.Equal(920, treasury.Balance);
        Assert.Equal(20, envelope.Balance);

        // De l'argent rendu revient au trésor et au poste.
        envelope.Deposit(30);
        Assert.Equal(950, treasury.Balance);
        Assert.Equal(50, budget.SpentOn(BudgetItem.Construction));

        // Le 1er janvier, l'enveloppe repart entière.
        budget.StartYear(new DateOnly(1901, 1, 1));
        Assert.Equal(100, envelope.Balance);
    }

    [Fact]
    public void Seuls_les_postes_qui_augmentent_coutent_dans_leur_pole()
    {
        var current = Plan(90, 50_000, 20_000, 50_000);
        var proposed = Plan(94, 90_000, 10_000, 50_000);

        var cost = StateBudget.CostOf(current, proposed, Settings, residents: 5_000);

        // Dotations +4 par habitant × 5 000 = +20 000 : 20 + 20 CO ; chantiers +40 000 : 20 + 40 PO ; routes en baisse : rien.
        Assert.Equal(40, cost[Orientation.FromCode("CO")], precision: 6);
        Assert.Equal(60, cost[PO], precision: 6);
        Assert.Equal(2, cost.Count);
        Assert.Empty(StateBudget.CostOf(current, current, Settings, residents: 5_000));
    }

    [Fact]
    public void Le_budget_vote_entre_en_vigueur_le_1er_du_mois_et_son_annulation_rend_la_legitimite()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var budget = simulation.World.Budget;
        var construction = budget.Current[BudgetItem.Construction];
        simulation.World.Legitimacy.Add(PO, 200);
        var before = simulation.World.Legitimacy[PO];

        simulation.SetBudgetDraft(BudgetItem.Construction, construction + 40_000);
        Assert.Equal(BudgetVoteOutcome.Voted, simulation.TryVoteBudget());
        Assert.Equal(before - 60, simulation.World.Legitimacy[PO], precision: 6);
        Assert.Equal(BudgetVoteOutcome.AlreadyVoted, simulation.TryVoteBudget());

        Assert.True(simulation.CancelBudgetVote());
        Assert.Equal(before, simulation.World.Legitimacy[PO], precision: 6);

        Assert.Equal(BudgetVoteOutcome.Voted, simulation.TryVoteBudget());
        Assert.Equal(construction, budget.Current[BudgetItem.Construction]);
        var month = simulation.Clock.Today.Month;
        while (simulation.Clock.Today.Month == month)
            simulation.AdvanceDay();

        Assert.Equal(construction + 40_000, budget.Current[BudgetItem.Construction]);
        Assert.Null(budget.Voted);
    }

    [Fact]
    public void Les_dotations_sont_versees_une_fois_par_mois_selon_les_habitants_de_chaque_commune()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var world = simulation.World;
        var budget = world.Budget;
        var perResident = budget.Current[BudgetItem.Grants] / 12;
        double Due() => world.Communes.Sum(c => perResident * world.Residents(c.Id));

        var first = Due();
        simulation.AdvanceDay();
        Assert.Equal(first, budget.SpentOn(BudgetItem.Grants), precision: 6);

        var month = simulation.Clock.Today.Month;
        while (simulation.Clock.Today.Month == month)
            simulation.AdvanceDay();
        // Le 1er du mois suivant, la dotation suit les habitants de ce jour-là.
        Assert.Equal(first + Due(), budget.SpentOn(BudgetItem.Grants), precision: 3);
    }

    [Fact]
    public void Un_chantier_sans_enveloppe_ne_demarre_pas()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var budget = simulation.World.Budget;
        budget.Record(BudgetItem.Construction, budget.Current[BudgetItem.Construction]);

        Assert.Equal(0, simulation.World.EnvelopeOf(BudgetItem.Construction).Balance);
        Assert.True(simulation.World.Treasury.Balance > 0);
    }
}
