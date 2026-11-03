using Demos.Application;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

/// <summary>La vue de l'interface ne fait que lire : elle ne crée rien et ne donne que des copies.</summary>
public class GameViewTests
{
    [Fact]
    public void Lire_une_commune_sans_marche_ne_cree_ni_marche_ni_caisse()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.World.AddStructure(new Structure("commune-vide", "Commune vide", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        var view = new GameView(simulation);

        var commune = view.Communes().Single(c => c.Id == "commune-vide");

        Assert.False(simulation.World.Markets.ContainsKey("commune-vide"));
        Assert.Equal(0, commune.Fund);
        Assert.Empty(commune.Stock);
        Assert.Empty(commune.StateStock);
    }

    [Fact]
    public void La_vue_est_une_copie_que_la_suite_de_la_partie_ne_change_pas()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);
        var before = view.Communes().Single(c => c.Id == PrototypeScenario.FirstCommuneId);
        var wealthBefore = before.Classes[0].Wealth;
        var stockBefore = before.Stock.ToDictionary();

        for (var day = 0; day < 10; day++)
            simulation.AdvanceDay();

        Assert.Equal(wealthBefore, before.Classes[0].Wealth);
        Assert.Equal(stockBefore, before.Stock);
        Assert.NotEqual(before, view.Communes().Single(c => c.Id == PrototypeScenario.FirstCommuneId));
    }

    [Fact]
    public void Les_reglages_de_l_administration_passent_par_la_simulation()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());

        simulation.SetStateTaxRate(0.01);
        simulation.SetCommuneTaxRate(PrototypeScenario.FirstCommuneId, 0.02);
        simulation.SetPrice("pierre", 3);

        Assert.Equal(0.01, simulation.World.StateTaxRate);
        Assert.Equal(0.02, simulation.World.CommuneTaxRateOf(PrototypeScenario.FirstCommuneId));
        Assert.Equal(3, simulation.World.PriceOf("pierre"));
    }
}
