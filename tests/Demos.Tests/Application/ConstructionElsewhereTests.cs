using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

/// <summary>
/// Un chantier qui prend ses matériaux dans une autre commune (décidé le 20 novembre 2026), et les outils comptés dans
/// la demande des échanges.
/// </summary>
public class ConstructionElsewhereTests
{
    [Fact]
    public void Un_chantier_sans_materiaux_les_prend_a_la_commune_voisine_et_commence_a_leur_arrivee()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var world = simulation.World;
        var capital = PrototypeScenario.FirstCommuneId;
        // Une commune vide, sans réserve de chantier ; la capitale a des matériaux dans le stock de l'État.
        world.AddStructure(new Structure("commune-vide", "Commune vide", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        world.MarketOf(capital).Offer("materiaux-de-construction", 500, Market.StateSellerId(capital));
        var granary = world.BuildingTypes["grenier"];

        var outcome = simulation.Build(granary.Build("grenier-neuf", "commune-vide"), PaymentMode.StockFirst);

        Assert.True(outcome.Started, outcome.Problem);
        var plan = outcome.Plan!;
        Assert.Equal(capital, plan.OtherCommuneId);
        // Il en part plus que les 50 du coût, pour couvrir la perte en route.
        Assert.True(plan.SentFromOther > 50);
        Assert.Equal(50, plan.SentFromOther - plan.LostOnTheWay, 6);
        Assert.Equal(30 + plan.TravelDays, world.ConstructionSites.Single(s => s.Building.Id == "grenier-neuf").DaysLeft);
        Assert.Contains("pris à", new GameView(simulation).DescribePlan(plan));
    }

    [Fact]
    public void Sans_materiaux_nulle_part_le_chantier_est_refuse_et_dit_ce_qui_manque()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var world = simulation.World;
        world.AddStructure(new Structure("commune-vide", "Commune vide", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        // On vide la réserve de chantier de la capitale et son marché de matériaux.
        var capital = PrototypeScenario.FirstCommuneId;
        world.ConstructionReserveOf(capital).Keep("materiaux-de-construction", 0);
        world.MarketOf(capital).Keep("materiaux-de-construction", 0);

        var outcome = simulation.Build(world.BuildingTypes["grenier"].Build("grenier-neuf", "commune-vide"), PaymentMode.StockFirst);

        Assert.False(outcome.Started);
        Assert.Contains("il manque", outcome.Problem);
        Assert.Empty(world.ConstructionSites.Where(s => s.Building.Id == "grenier-neuf"));
    }

    [Fact]
    public void Une_taille_de_10_pourcent_reduit_les_emplois_et_le_cout()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var world = simulation.World;
        var reserveBefore = world.ConstructionReserveOf(PrototypeScenario.FirstCommuneId).QuantityOf("materiaux-de-construction");
        var workshop = world.BuildingTypes["atelier"];

        var outcome = simulation.Build(workshop.Build("atelier-neuf", PrototypeScenario.FirstCommuneId), PaymentMode.StockFirst, size: 0.1);

        Assert.True(outcome.Started, outcome.Problem);
        Assert.Equal(15, world.ConstructionSites.Single(s => s.Building.Id == "atelier-neuf").Building.Jobs);
        Assert.Equal(reserveBefore - 20, world.ConstructionReserveOf(PrototypeScenario.FirstCommuneId).QuantityOf("materiaux-de-construction"), 6);
    }

    [Fact]
    public void La_demande_des_batiments_compte_l_usure_et_le_manque_de_leurs_outils()
    {
        var structures = new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("a", "Commune A", StructureKind.PoliticalEntity, "etat"),
            new("camp", "Camp", StructureKind.Building, "a", Ownership.Public, null, new Production("bois", 1), Jobs: 100),
        }.ToDictionary(s => s.Id);

        var demand = Trade.BuildingDemand(structures, new Dictionary<string, double>(), _ => 0, Demos.Domain.Shared.Season.Spring,
            toolsOf: _ => 4, toolUsageDays: 100);

        // 10 outils visés, 4 possédés : 4 ÷ 100 d'usure, plus 1/30 des 6 qui manquent.
        Assert.Equal(0.04 + 6 * Balance.Current.PossessionCatchUp, demand["a"]["outils"], 6);
    }
}
