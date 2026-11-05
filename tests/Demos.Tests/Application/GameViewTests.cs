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

    [Fact]
    public void Le_code_civil_liste_toutes_les_lois_en_vigueur_et_le_catalogue_ses_briques()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);

        Assert.Equal(simulation.World.Laws.Select(l => l.Id), view.Laws().Select(l => l.Id));
        Assert.Equal(simulation.World.LawCatalogue.Bricks.Count, view.LawBricks().Count);
    }

    [Fact]
    public void L_arbre_des_structures_part_de_l_Etat_et_porte_les_lois_de_chaque_niveau()
    {
        var view = new GameView(WithLawAtEachLevel());

        var state = Assert.Single(view.StructureTree());
        Assert.Equal(StructureNodeKind.State, state.Kind);
        Assert.Contains(state.Laws, l => l.Id == "loi-etat");

        var commune = Assert.Single(state.Children, c => c.Id == PrototypeScenario.FirstCommuneId);
        Assert.Equal(StructureNodeKind.Commune, commune.Kind);
        var quarry = Assert.Single(commune.Children, c => c.Id == "carriere");
        Assert.Equal(StructureNodeKind.Building, quarry.Kind);
        Assert.Equal("Extraction", quarry.Category);
        Assert.Contains(quarry.Laws, l => l.Id == "loi-carriere");
    }

    [Fact]
    public void Une_structure_subit_les_lois_de_tout_ce_qui_la_contient_de_l_Etat_a_elle_meme()
    {
        var view = new GameView(WithLawAtEachLevel());

        var laws = view.LawsApplyingTo("carriere").Select(l => l.Id).ToList();

        Assert.Equal(["loi-etat", "loi-commune", "loi-carriere"], laws);
        Assert.DoesNotContain("loi-carriere", view.LawsApplyingTo(PrototypeScenario.FirstCommuneId).Select(l => l.Id));
    }

    /// <summary>Une partie sans loi de départ, avec une loi à l'État, à la commune et à la carrière.</summary>
    private static Simulation WithLawAtEachLevel()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(), StartingLaws.None);
        Law Production(string id, string level, double value) =>
            new(id, id, "environnement", level, [new BrickSetting("production", LawSubject.Everyone, value)]);
        simulation.World.AddLaw(Production("loi-etat", PrototypeScenario.StateId, 0.1));
        simulation.World.AddLaw(Production("loi-commune", PrototypeScenario.FirstCommuneId, 0.1));
        simulation.World.AddLaw(Production("loi-carriere", "carriere", -0.2));
        return simulation;
    }
}
