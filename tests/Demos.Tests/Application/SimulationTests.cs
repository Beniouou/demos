using Demos.Application;
using Demos.Domain.Politics;
using Demos.Domain.Population;
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
    public void Les_classes_produisent_chaque_jour_la_legitimite_des_lois_qu_elles_acceptent()
    {
        var simulation = CreatePrototype();
        var libertarian = new Orientation(IdeologicalAxis.Property, Pole.B);

        simulation.AdvanceDay();
        var afterOneDay = simulation.World.Legitimacy[libertarian];
        simulation.AdvanceDay();

        // Cadres et dirigeants (LI) acceptent la liberté d'entreprendre, loi de l'État : 170 000 × 0,0001 par jour.
        Assert.Equal(17, afterOneDay, precision: 6);
        Assert.Equal(34, simulation.World.Legitimacy[libertarian], precision: 6);
    }

    [Fact]
    public void Les_ouvriers_communistes_rejettent_la_liberte_d_entreprendre()
    {
        var simulation = CreatePrototype();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");

        simulation.AdvanceDay();

        Assert.True(workers.Adhesion < 1);
    }
}
