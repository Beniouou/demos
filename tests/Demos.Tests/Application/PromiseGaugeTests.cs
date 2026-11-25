using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Application;

/// <summary>La Promesse dans la partie (décidé le 25 novembre 2026) : jauge, trahisons, rupture, refondation.</summary>
[Collection(GlobalBalanceCollection.Name)]
public class PromiseGaugeTests
{
    private static readonly IReadOnlyList<EngagementDefinition> Catalogue = GameData.Content().LoadEngagements();
    private static readonly IReadOnlyList<PromiseDefinition> Promises = GameData.Content().LoadPromises();

    private static PromiseDefinition PromiseOf(string id) => Promises.Single(p => p.Id == id);

    private static EngagementDefinition Engagement(string id) => Catalogue.Single(e => e.Id == id);

    [Fact]
    public void On_choisit_un_engagement_par_axe_de_La_Promesse_et_a_son_pole()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var tableCommune = PromiseOf("eg-co");

        Assert.NotNull(simulation.ChoosePromise(tableCommune, [Engagement("eg-logement")]));
        Assert.NotNull(simulation.ChoosePromise(tableCommune, [Engagement("eg-logement"), Engagement("li-expropriation")]));
        Assert.Null(simulation.ChoosePromise(tableCommune, [Engagement("eg-logement"), Engagement("co-vente")]));
        Assert.NotNull(simulation.World.PromiseState);
    }

    [Fact]
    public void La_jauge_suit_l_axe_le_plus_faible_part_des_habitants()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.ChoosePromise(PromiseOf("eg-co"), [Engagement("eg-logement"), Engagement("co-vente")]);
        var classes = simulation.World.Classes;
        var egalitarian = PromiseState.AlignmentOf(Orientation.FromCode("EG"), classes);
        var collectivist = PromiseState.AlignmentOf(Orientation.FromCode("CO"), classes);

        // Au départ : 4 500 ouvriers communistes (EG, CO), 450 professions libertaires (EG, LI), 50 cadres capitalistes (SE, LI).
        Assert.Equal(0.99, egalitarian, precision: 6);
        Assert.Equal(0.9, collectivist, precision: 6);
        Assert.Equal(0.9, simulation.PromiseGauge!.Value, precision: 6);
    }

    [Fact]
    public void Vendre_au_prive_trahit_ce_qui_fait_vivre_appartiendra_a_tous()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.ChoosePromise(PromiseOf("co"), [Engagement("co-vente")]);
        var before = simulation.PromiseGauge!.Value;
        simulation.World.Legitimacy.Add(Simulation.SalePole, 1_000);
        var building = simulation.World.Structures.Values.First(s => s.Kind == StructureKind.Building && s.Ownership == Ownership.Public
            && simulation.World.Classes.Any(c => c.CommuneId == simulation.World.CommuneOf(s) && c.Rank == Rank.Workers));
        var buyer = simulation.World.Classes.First(c => c.Rank == Rank.Workers && c.CommuneId == simulation.World.CommuneOf(building));
        buyer.Wealth += 1_000_000;

        Assert.Equal(SaleOutcome.Sold, simulation.TrySellBuilding(building.Id, buyer.Id, 0));

        var wound = Assert.Single(simulation.World.PromiseState!.Wounds);
        Assert.Equal("co-vente", wound.EngagementId);
        Assert.Equal(before - Balance.Current.PromiseBetrayalWound, simulation.PromiseGauge!.Value, precision: 6);
        // Les classes collectivistes s'en méfient.
        Assert.True(simulation.World.Classes.Where(c => c.Ideology[IdeologicalAxis.Property] == Pole.A).All(c => c.Distrust > 0));
    }

    [Fact]
    public void Une_loi_qui_favorise_le_pole_oppose_trahit_l_engagement_de_principe_a_son_entree_en_vigueur()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(), StartingLaws.None);
        simulation.ChoosePromise(PromiseOf("ec"), [Engagement("ec-aucune-loi-contraire")]);
        simulation.World.Legitimacy.Add(Simulation.FoundingPole, 1_000);

        // La loi d'extension favorise PO : votée, elle trahit « Aucune loi ne sacrifiera la terre » le 1er du mois.
        Assert.Equal(LawVoteOutcome.Voted, simulation.TryVote(simulation.DraftFromTemplate("loi-extension")!));
        Assert.Empty(simulation.World.PromiseState!.Wounds);
        for (var day = 0; day < 32; day++)
            simulation.AdvanceDay();

        Assert.Equal("ec-aucune-loi-contraire", Assert.Single(simulation.World.PromiseState.Wounds).EngagementId);
    }

    [Fact]
    public void Une_trahison_guerit_mais_en_partie_seulement()
    {
        var state = new PromiseState(PromiseOf("co"), [Engagement("co-vente")]);
        state.Betray(Engagement("co-vente"), "vente", default);

        for (var month = 0; month < 20; month++)
            state.RunMonthly(gauge: 1);

        Assert.Equal(Balance.Current.PromiseBetrayalPermanent, state.WoundDepth, precision: 9);
        Assert.True(state.InkOf("co-vente") < 1);
    }

    [Fact]
    public void Sous_le_seuil_pendant_douze_mois_le_pacte_se_dechire()
    {
        var state = new PromiseState(PromiseOf("co"), [Engagement("co-vente")]);
        for (var month = 1; month < Balance.Current.PromiseRuptureMonths; month++)
            Assert.False(state.RunMonthly(gauge: 0.2));

        // Un mois au-dessus du seuil remet le compte à zéro…
        state.RunMonthly(gauge: 0.5);
        Assert.Equal(0, state.MonthsBelowThreshold);

        // … puis douze mois de suite en dessous déchirent le pacte.
        var torn = false;
        for (var month = 0; month < Balance.Current.PromiseRuptureMonths; month++)
            torn |= state.RunMonthly(gauge: 0.2);
        Assert.True(torn);
        Assert.True(state.Torn);
    }

    [Fact]
    public void Refonder_apres_un_pacte_dechire_coute_une_crise_et_une_cicatrice()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.ChoosePromise(PromiseOf("co"), [Engagement("co-vente")]);
        Assert.NotNull(simulation.Refound(PromiseOf("eg"), [Engagement("eg-logement")]));

        for (var month = 0; month < Balance.Current.PromiseRuptureMonths; month++)
            simulation.World.PromiseState!.RunMonthly(gauge: 0);
        Assert.Null(simulation.World.Promise);

        Assert.Null(simulation.Refound(PromiseOf("eg"), [Engagement("eg-logement")]));
        var state = simulation.World.PromiseState!;
        Assert.False(state.Torn);
        Assert.Equal(Balance.Current.RefoundingScar, state.WoundDepth, precision: 9);
        Assert.All(simulation.World.Classes, c => Assert.True(c.Distrust >= Balance.Current.RefoundingAdhesionLoss - 1e-9));
    }

    [Fact]
    public void Une_charte_de_commune_contraire_blesse_un_peu_la_jauge()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.ChoosePromise(PromiseOf("co"), [Engagement("co-vente")]);
        var commune = PrototypeScenario.FirstCommuneId;
        var before = simulation.PromiseGauge!.Value;

        simulation.World.SetCharter(commune, Orientation.FromCode("LI"));

        // Toute la population vit dans cette commune : −0,10.
        Assert.Equal(1, simulation.ContraryCharterShare(), precision: 6);
        Assert.Equal(before - Balance.Current.PromiseCharterPenalty, simulation.PromiseGauge!.Value, precision: 6);
    }

    [Fact]
    public void Sans_Promesse_rien_ne_change()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());

        Assert.Null(simulation.PromiseGauge);
        Assert.Null(new GameView(simulation).Promise());
    }
}
