using Demos.Application;
using Demos.Infrastructure;

namespace Demos.Tests.Application;

public class SimulationTests
{
    private static Simulation CreatePrototype() =>
        PrototypeScenario.Create(new JsonContentSource(_ => "[]"));

    [Fact]
    public void Une_annee_compte_quatre_saisons_et_un_bilan()
    {
        var simulation = CreatePrototype();
        var seasons = 0;
        var years = 0;
        simulation.SeasonPassed += () => seasons++;
        simulation.YearPassed += () => years++;

        // Du 1er janvier 1900 au 1er janvier 1901 : 1er mars, 1er juin, 1er septembre, 1er décembre.
        for (var day = 0; day < 365; day++)
            simulation.AdvanceDay();

        Assert.Equal(4, seasons);
        Assert.Equal(1, years);
    }

    [Fact]
    public void La_population_ne_change_qu_au_changement_de_saison()
    {
        var simulation = CreatePrototype();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");
        var start = workers.Population;

        // Du 1er janvier au 28 février : toujours l'hiver.
        for (var day = 0; day < 58; day++)
            simulation.AdvanceDay();
        Assert.Equal(start, workers.Population);

        simulation.AdvanceDay();
        Assert.NotEqual(start, workers.Population);
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
