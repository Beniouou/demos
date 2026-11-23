using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Application;

/// <summary>
/// L'expansion (décidé le 23 novembre 2026) : fonder une commune se paie en légitimité PO, la charte se choisit à part,
/// une loi d'extension est nécessaire, et le défrichement change le coût.
/// </summary>
[Collection(GlobalBalanceCollection.Name)]
public class ExpansionTests
{
    private static readonly Orientation Liberal = new(IdeologicalAxis.Property, Pole.B);

    [Fact]
    public void Sans_loi_d_extension_on_ne_fonde_pas_de_commune()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(), StartingLaws.None);
        simulation.World.Legitimacy.Add(Simulation.FoundingPole, 100);

        Assert.False(simulation.AllowsExtension(PrototypeScenario.FirstCommuneId));
        Assert.Null(simulation.TryFoundCommune("Nouvelle commune", PrototypeScenario.FirstCommuneId, PaymentMode.StockFirst, Liberal));
        Assert.Equal(100, simulation.World.Legitimacy[Simulation.FoundingPole]);
    }

    [Fact]
    public void La_fondation_se_paie_en_productivisme_et_la_charte_se_choisit_a_part()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var legitimacy = simulation.World.Legitimacy;
        legitimacy.Add(Simulation.FoundingPole, 100);
        var liberalBefore = legitimacy[Liberal];
        var cost = simulation.FoundingLegitimacyCostOf(PrototypeScenario.FirstCommuneId);

        var commune = simulation.TryFoundCommune("Nouvelle commune", PrototypeScenario.FirstCommuneId, PaymentMode.StockFirst, Liberal);

        Assert.NotNull(commune);
        Assert.Equal(Balance.Current.BaseCampLegitimacyCost, cost, precision: 9);
        Assert.Equal(100 - cost, legitimacy[Simulation.FoundingPole], precision: 9);
        Assert.Equal(liberalBefore, legitimacy[Liberal], precision: 9);
        Assert.Equal(Liberal, simulation.World.CharterOf(commune.Id));
    }

    [Fact]
    public void Sans_assez_de_legitimite_PO_la_fondation_est_refusee()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.World.Legitimacy.Add(Liberal, 1_000);

        Assert.Null(simulation.TryFoundCommune("Nouvelle commune", PrototypeScenario.FirstCommuneId, PaymentMode.StockFirst, Liberal));
    }

    [Fact]
    public void Sans_legitimite_PO_on_vote_la_loi_d_extension_a_credit_et_le_peuple_rembourse()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(), StartingLaws.None);
        var legitimacy = simulation.World.Legitimacy;
        Assert.Equal(0, legitimacy[Simulation.FoundingPole]);

        var draftId = simulation.DraftFromTemplate("loi-extension")!;
        var cost = simulation.CostOf(draftId)[Simulation.FoundingPole];
        Assert.True(cost <= Balance.Current.LegitimacyMaxDebt, $"Coût {cost}");

        Assert.Equal(LawVoteOutcome.Voted, simulation.TryVote(draftId));
        Assert.Equal(-cost, legitimacy[Simulation.FoundingPole], precision: 6);

        // En dette, le pôle ne paie rien d'autre ; une fois la loi en vigueur, les classes positivistes la remboursent.
        Assert.False(legitimacy.TrySpend(Simulation.FoundingPole, 1));
        for (var day = 0; day < 365; day++)
            simulation.AdvanceDay();
        Assert.True(legitimacy[Simulation.FoundingPole] > 0, $"PO {legitimacy[Simulation.FoundingPole]}");
        Assert.True(simulation.AllowsExtension(PrototypeScenario.FirstCommuneId));
    }

    [Fact]
    public void Un_vote_qui_depasse_la_dette_permise_est_refuse()
    {
        var pool = new LegitimacyPool();

        Assert.True(pool.TrySpendOnCredit(Simulation.FoundingPole, 60, maxDebt: 100));
        Assert.False(pool.TrySpendOnCredit(Simulation.FoundingPole, 50, maxDebt: 100));
        Assert.Equal(-60, pool[Simulation.FoundingPole]);
        Assert.True(pool.TrySpendOnCredit(Simulation.FoundingPole, 40, maxDebt: 100));
        Assert.Equal(-100, pool[Simulation.FoundingPole]);
    }

    [Fact]
    public void Le_defrichement_change_le_cout_de_la_fondation()
    {
        var content = GameData.Content();
        var catalogue = content.LoadLawCatalogue();
        var structures = PrototypeScenario.Create(content).World.Structures;
        LawBook With(double value)
        {
            var law = catalogue.Templates.Single(t => t.Id == "terres-nouvelles").ToLaw("loi");
            law = law with { Bricks = [law.Bricks[0] with { Value = value }] };
            return new LawBook(catalogue, [law], structures);
        }

        // La loi des terres nouvelles (+0,5) : moitié moins cher ; un défrichement baissé (−0,5) : moitié plus cher.
        Assert.Equal(0.5, With(0.5).FoundingCostFactor(PrototypeScenario.FirstCommuneId), precision: 9);
        Assert.Equal(1.5, With(-0.5).FoundingCostFactor(PrototypeScenario.FirstCommuneId), precision: 9);
        Assert.Equal(1, new LawBook(catalogue, [], structures).FoundingCostFactor(PrototypeScenario.FirstCommuneId), precision: 9);
    }

    [Fact]
    public void La_loi_d_extension_autorise_l_expansion_et_favorise_le_productivisme()
    {
        var content = GameData.Content();
        var catalogue = content.LoadLawCatalogue();
        var structures = PrototypeScenario.Create(content).World.Structures;
        var law = catalogue.Templates.Single(t => t.Id == "loi-extension").ToLaw("loi");

        Assert.False(new LawBook(catalogue, [], structures).AllowsExtension(PrototypeScenario.FirstCommuneId));
        Assert.True(new LawBook(catalogue, [law], structures).AllowsExtension(PrototypeScenario.FirstCommuneId));
        Assert.Equal([Simulation.FoundingPole], catalogue.BrickOf(Bricks.Extension)!.PolesOf(new LawSubject(SubjectKind.Everyone), raise: true));
    }
}
