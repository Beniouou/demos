using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Application;

/// <summary>Ce que l'interface peut lire de l'extension, de la bureaucratie et des préfectures.</summary>
public class ExtensionViewTests
{
    private static readonly Orientation Liberal = new(IdeologicalAxis.Property, Pole.B);

    private static Simulation WithPrefecture()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.World.AddStructure(new Structure("commune-2", "Commune 2", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        simulation.GiveFoundingMaterials("commune-1");
        simulation.World.Legitimacy.Add(Liberal, 100);
        simulation.TryFoundPrefecture("Nord", PrototypeScenario.StateId, ["commune-1", "commune-2"], "commune-1", PaymentMode.StockFirst, Liberal);
        return simulation;
    }

    [Fact]
    public void La_commune_de_depart_est_efficace_a_100_pourcent()
    {
        var view = new GameView(PrototypeScenario.Create(GameData.Content()));

        var commune = view.EntityLoads().Single(l => l.Id == "commune-1");

        Assert.Equal(EntityLevel.Commune, commune.Level);
        Assert.InRange(commune.Charge, 0.6, 0.8);
        Assert.Equal(1, commune.Efficiency);
        Assert.Equal(1, commune.StateEfficiencyHere);
        Assert.Equal(EntityLevel.State, view.EntityLoads()[0].Level);
    }

    [Fact]
    public void Une_prefecture_apparait_dans_l_arbre_et_dans_sa_vue()
    {
        var simulation = WithPrefecture();
        var view = new GameView(simulation);

        var state = Assert.Single(view.StructureTree());
        var node = Assert.Single(state.Children, c => c.Kind == StructureNodeKind.Prefecture);
        Assert.Contains(node.Children, c => c.Id == "commune-1" && c.Kind == StructureNodeKind.Commune);

        var prefecture = Assert.Single(view.Prefectures());
        Assert.Equal("commune-1", prefecture.SeatId);
        Assert.Equal(["commune-1", "commune-2"], prefecture.MemberIds.Order().ToList());
        Assert.Null(prefecture.BuildingId);
        Assert.Equal(30, prefecture.ConstructionDaysLeft);
        Assert.Equal(30, prefecture.Jobs);

        var commune = view.EntityLoads().Single(l => l.Id == "commune-2");
        Assert.Equal(1, commune.PrefecturesFromState);
        Assert.Equal(0.9, commune.StateEfficiencyHere, 6);
    }

    [Fact]
    public void Le_cout_d_une_prefecture_vient_du_catalogue_et_des_reglages()
    {
        var view = new GameView(PrototypeScenario.Create(GameData.Content()));

        var cost = view.PrefectureCost();

        Assert.Equal("materiaux-de-construction", cost.MaterialProductId);
        Assert.Equal(200, cost.MaterialQuantity);
        Assert.Equal(30, cost.ConstructionDays);
        Assert.Equal(30, cost.FoundingLegitimacy);
        Assert.Equal(5, cost.AttachLegitimacy);
        Assert.Equal(20, cost.BaseJobs);
        Assert.Equal(5, cost.JobsPerEntity);
    }

    [Fact]
    public void L_interface_peut_savoir_ou_rattacher_une_entite()
    {
        var simulation = WithPrefecture();
        var view = new GameView(simulation);
        var prefectureId = view.Prefectures()[0].Id;

        Assert.True(view.CanAttach("commune-2", PrototypeScenario.StateId));
        Assert.False(view.CanAttach("commune-1", PrototypeScenario.StateId));
        Assert.False(view.CanAttach(prefectureId, "commune-2"));
    }
}
