using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Application;

/// <summary>Coûts fixes indexés et usure de la légitimité (décidé le 23 novembre 2026).</summary>
[Collection(GlobalBalanceCollection.Name)]
public class FixedCostTests
{
    private static readonly Orientation CO = Orientation.FromCode("CO");
    private static readonly Orientation LI = Orientation.FromCode("LI");

    [Fact]
    public void La_legitimite_s_use_mais_pas_une_dette()
    {
        var pool = new LegitimacyPool();
        pool.Add(CO, 100);
        pool.TrySpendOnCredit(LI, 40, maxDebt: 100);

        pool.Erode(0.1);

        Assert.Equal(90, pool[CO], precision: 9);
        Assert.Equal(-40, pool[LI], precision: 9);
        Assert.Contains(pool.Ledger.Spendings, s => s.Kind == LegitimacySpendingKind.Erosion && s.Pole == CO);
    }

    [Fact]
    public void En_un_an_la_legitimite_perd_dix_pour_cent_si_rien_ne_la_renouvelle()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(), StartingLaws.None);
        simulation.World.Legitimacy.Add(CO, 1_000);

        for (var day = 0; day < 365; day++)
            simulation.AdvanceDay();

        Assert.Equal(900, simulation.World.Legitimacy[CO], precision: 0);
    }

    [Fact]
    public void Fonder_coute_plus_cher_dans_une_nation_plus_peuplee()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var commune = PrototypeScenario.FirstCommuneId;
        var before = simulation.FoundingLegitimacyCostOf(commune);
        var population = simulation.NationPopulation;

        // Une nation deux fois plus peuplée (à partir de la population de référence) : le coût double.
        var newcomers = new SocialClass("nouveaux", "Nouveaux", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), (long)population);
        newcomers.SetResidence(commune, 1);
        simulation.World.AddClass(newcomers);

        Assert.True(population >= Balance.Current.LegitimacyCostReferencePopulation);
        Assert.Equal(before * 2, simulation.FoundingLegitimacyCostOf(commune), precision: 6);
        Assert.Equal(Balance.Current.AttachLegitimacyCost * simulation.NationCostScale, simulation.AttachLegitimacyCost, precision: 9);
    }

    [Fact]
    public void Vendre_ou_nationaliser_coute_selon_les_emplois_du_batiment()
    {
        var reference = Balance.Current.LegitimacyCostReferenceJobs;
        var big = new Structure("grand", "Grand", StructureKind.Building, "commune", Jobs: (int)(reference * 4));
        var tiny = new Structure("petit", "Petit", StructureKind.Building, "commune", Jobs: 1);

        Assert.Equal(Balance.Current.SaleLegitimacyCost * 4, Simulation.BuildingLegitimacyCost(Balance.Current.SaleLegitimacyCost, big), precision: 9);
        // Un tout petit bâtiment coûte au moins le coût minimal d'une brique.
        Assert.Equal(Balance.Current.MinimumBrickCost, Simulation.BuildingLegitimacyCost(Balance.Current.SaleLegitimacyCost, tiny), precision: 9);
    }

    [Fact]
    public void Une_prefecture_coute_selon_les_habitants_qu_elle_regroupe()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var commune = PrototypeScenario.FirstCommuneId;
        var inhabitants = simulation.World.Residents(commune);
        var expected = Balance.Current.PrefectureLegitimacyCost
            * Math.Max(1, inhabitants / Balance.Current.LegitimacyCostReferencePopulation);

        Assert.Equal(expected, simulation.PrefectureLegitimacyCostOf([commune]), precision: 6);
    }
}
