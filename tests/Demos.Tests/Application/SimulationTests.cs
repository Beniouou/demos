using Demos.Application;
using Demos.Infrastructure;

namespace Demos.Tests.Application;

public class SimulationTests
{
    private static Simulation CreatePrototype() =>
        PrototypeScenario.Create(new JsonContentSource(_ => "[]"));

    [Fact]
    public void Le_cycle_mensuel_se_declenche_au_changement_de_mois()
    {
        var simulation = CreatePrototype();
        var months = 0;
        simulation.MonthPassed += () => months++;

        // Du 1er janvier au 1er mars 1900 : 59 jours, deux changements de mois.
        for (var day = 0; day < 59; day++)
            simulation.AdvanceDay();

        Assert.Equal(2, months);
    }

    [Fact]
    public void Une_classe_mal_servie_et_eloignee_du_regime_perd_de_l_approbation()
    {
        var simulation = CreatePrototype();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");
        workers.NeedsSatisfaction = 0.2;

        for (var day = 0; day < 365; day++)
            simulation.AdvanceDay();

        Assert.True(workers.Approval < 50);
    }
}
