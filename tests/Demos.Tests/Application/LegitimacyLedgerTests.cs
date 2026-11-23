using Demos.Application;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

/// <summary>Le relevé de la légitimité (décidé le 23 novembre 2026) : gains par groupe et par loi, dépenses avec leur motif, 30 jours.</summary>
public class LegitimacyLedgerTests
{
    private static readonly Orientation EG = Orientation.FromCode("EG");
    private static readonly Orientation CO = Orientation.FromCode("CO");

    [Fact]
    public void Le_releve_ne_garde_que_les_30_derniers_jours()
    {
        var ledger = new LegitimacyLedger();
        var start = new DateOnly(1900, 1, 1);
        for (var day = 0; day < 40; day++)
        {
            ledger.BeginDay(start.AddDays(day));
            ledger.RecordGain(new LegitimacyGainKey(EG, "loi", "groupe"), 1);
            ledger.RecordSpending(new LegitimacySpending(start.AddDays(day), EG, 2, LegitimacySpendingKind.Attach, "x"));
        }

        Assert.Equal(LegitimacyLedger.Days, ledger.GainDays.Count);
        Assert.Equal(start.AddDays(10), ledger.GainDays[0].Date);
        Assert.Equal(start.AddDays(10), ledger.Spendings[0].Date);
    }

    [Fact]
    public void Le_gain_du_releve_est_ce_que_le_stock_a_gagne()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);
        var before = Orientation.All.ToDictionary(p => p, p => simulation.World.Legitimacy[p]);

        for (var day = 0; day < 5; day++)
            simulation.AdvanceDay();

        var ledger = view.LegitimacyLedger();
        Assert.Equal(5, ledger.Days);
        Assert.NotEmpty(ledger.Gains);
        foreach (var pole in Orientation.All)
            Assert.Equal(simulation.World.Legitimacy[pole] - before[pole], ledger.Gains.Where(g => g.Pole == pole).Sum(g => g.Total), precision: 6);
        Assert.All(ledger.Gains, g => Assert.NotNull(g.CommuneId));
    }

    [Fact]
    public void Un_vote_puis_son_annulation_sont_notes_avec_le_nom_de_la_loi()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(), StartingLaws.None);
        var view = new GameView(simulation);
        simulation.World.Legitimacy.Add(EG, 100);
        simulation.World.Legitimacy.Add(CO, 100);
        var draftId = simulation.DraftFromTemplate("salaire-minimum-ouvrier")!;
        var cost = simulation.CostOf(draftId);
        var name = simulation.World.LawCommission.DraftOf(draftId)!.Law.Name;

        Assert.Equal(LawVoteOutcome.Voted, simulation.TryVote(draftId));
        Assert.True(simulation.CancelVote(draftId));

        var spendings = view.LegitimacyLedger().Spendings;
        var vote = spendings.Single(s => s.Kind == LegitimacySpendingKind.LawVote && s.Pole == EG);
        var refund = spendings.Single(s => s.Kind == LegitimacySpendingKind.VoteCancelled && s.Pole == EG);
        Assert.Equal(cost[EG], vote.Amount, precision: 6);
        Assert.Equal(-cost[EG], refund.Amount, precision: 6);
        Assert.Equal(name, vote.Subject);
        Assert.Equal(view.Today, vote.Date);
        // La plus récente d'abord.
        Assert.Equal(LegitimacySpendingKind.VoteCancelled, spendings[0].Kind);
    }
}
