using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Infrastructure;

namespace Demos.Tests.Application;

/// <summary>Fonder une préfecture, y rattacher des entités, et ce que coûte son bâtiment.</summary>
public class PrefectureTests
{
    private static readonly Orientation Liberal = new(IdeologicalAxis.Property, Pole.B);

    /// <summary>La partie de départ, avec deux communes de plus et de quoi payer chantier et légitimité.</summary>
    private static Simulation Prototype()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        foreach (var id in new[] { "commune-2", "commune-3" })
            simulation.World.AddStructure(new Structure(id, id, StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        simulation.GiveFoundingMaterials("commune-1");
        simulation.World.Legitimacy.Add(Liberal, 100);
        return simulation;
    }

    private static double ReserveOf(Simulation simulation) =>
        simulation.World.ConstructionReserveOf("commune-1").QuantityOf("materiaux-de-construction");

    private static Structure? Found(Simulation simulation, params string[] members) =>
        simulation.TryFoundPrefecture("Préfecture du nord", PrototypeScenario.StateId, members, "commune-1", PaymentMode.StockFirst, Liberal);

    private static void FinishSites(Simulation simulation)
    {
        while (simulation.World.ConstructionSites.Count > 0)
            simulation.AdvanceDay();
    }

    [Fact]
    public void Fonder_une_prefecture_regroupe_les_communes_et_lance_son_chantier()
    {
        var simulation = Prototype();
        var reserveBefore = ReserveOf(simulation);

        var prefecture = Found(simulation, "commune-1", "commune-2");

        Assert.NotNull(prefecture);
        Assert.True(prefecture.IsPrefecture);
        Assert.Equal("commune-1", prefecture.SeatId);
        Assert.Equal(prefecture.Id, simulation.World.Structures["commune-1"].ParentId);
        Assert.Equal(prefecture.Id, simulation.World.Structures["commune-2"].ParentId);
        Assert.Equal(PrototypeScenario.StateId, simulation.World.Structures["commune-3"].ParentId);
        Assert.Equal(70, simulation.World.Legitimacy[Liberal], 6);
        Assert.Equal(reserveBefore - 200, ReserveOf(simulation), 6);

        // 20 emplois du type + 5 par entité contenue.
        var site = Assert.Single(simulation.World.ConstructionSites);
        Assert.Equal(30, site.Building.Jobs);
        Assert.Equal(30, site.DaysLeft);
        Assert.Equal(3, simulation.World.Communes.Count());
    }

    [Fact]
    public void Le_batiment_de_la_prefecture_se_trouve_dans_son_chef_lieu()
    {
        var simulation = Prototype();
        var prefecture = Found(simulation, "commune-1", "commune-2")!;

        FinishSites(simulation);

        var building = simulation.World.Structures.Values.Single(s => s.TypeId == Simulation.PrefectureTypeId);
        Assert.Equal(prefecture.Id, building.ParentId);
        Assert.Equal("commune-1", simulation.World.CommuneOf(building));
        Assert.Equal(prefecture.Id, Extension.ManagerOf(building, simulation.World.Structures));
    }

    [Fact]
    public void Une_prefecture_impossible_ne_coute_rien()
    {
        var simulation = Prototype();
        var reserveBefore = ReserveOf(simulation);

        // Une seule commune, un chef-lieu hors de la préfecture, pas assez de légitimité.
        Assert.Null(Found(simulation, "commune-2"));
        Assert.Null(simulation.TryFoundPrefecture("P", PrototypeScenario.StateId, ["commune-2", "commune-3"], "commune-1",
            PaymentMode.StockFirst, Liberal));
        simulation.World.Legitimacy.TrySpend(Liberal, 80);
        Assert.Null(Found(simulation, "commune-1", "commune-2"));

        Assert.Equal(20, simulation.World.Legitimacy[Liberal], 6);
        Assert.Equal(reserveBefore, ReserveOf(simulation), 6);
        Assert.Empty(simulation.World.ConstructionSites);
        Assert.DoesNotContain(simulation.World.Structures.Values, s => s.IsPrefecture);
    }

    [Fact]
    public void Rattacher_une_commune_coute_de_la_legitimite_et_ajoute_des_emplois()
    {
        var simulation = Prototype();
        var prefecture = Found(simulation, "commune-1", "commune-2")!;
        FinishSites(simulation);
        var legitimacyBefore = simulation.World.Legitimacy[Liberal];

        Assert.True(simulation.TryAttach("commune-3", prefecture.Id, Liberal));

        Assert.Equal(prefecture.Id, simulation.World.Structures["commune-3"].ParentId);
        Assert.Equal(legitimacyBefore - 5, simulation.World.Legitimacy[Liberal], 6);
        var building = simulation.World.Structures.Values.Single(s => s.TypeId == Simulation.PrefectureTypeId);
        Assert.Equal(35, building.JobCapacity);
    }

    [Fact]
    public void Un_rattachement_qui_ferait_une_boucle_ou_retirerait_le_chef_lieu_est_refuse()
    {
        var simulation = Prototype();
        var prefecture = Found(simulation, "commune-1", "commune-2")!;
        var legitimacyBefore = simulation.World.Legitimacy[Liberal];

        // Une commune ne peut pas contenir d'entité, une préfecture ne peut pas entrer en elle-même,
        // et le chef-lieu ne quitte pas sa préfecture.
        Assert.False(simulation.TryAttach("commune-3", "commune-2", Liberal));
        Assert.False(simulation.TryAttach(prefecture.Id, prefecture.Id, Liberal));
        Assert.False(simulation.TryAttach("commune-1", PrototypeScenario.StateId, Liberal));
        Assert.False(simulation.TryAttach("commune-2", prefecture.Id, Liberal));

        Assert.Equal(legitimacyBefore, simulation.World.Legitimacy[Liberal], 6);
        Assert.True(simulation.TryAttach("commune-2", PrototypeScenario.StateId, Liberal));
    }

    [Fact]
    public void Des_prefectures_s_emboitent_dans_une_prefecture()
    {
        var simulation = Prototype();
        simulation.World.AddStructure(new Structure("commune-4", "commune-4", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        simulation.GiveFoundingMaterials("commune-3");
        var north = Found(simulation, "commune-1", "commune-2")!;
        var south = simulation.TryFoundPrefecture("Sud", PrototypeScenario.StateId, ["commune-3", "commune-4"], "commune-3",
            PaymentMode.StockFirst, Liberal)!;

        var big = simulation.TryFoundPrefecture("Grande", PrototypeScenario.StateId, [north.Id, south.Id], "commune-1",
            PaymentMode.StockFirst, Liberal);

        Assert.NotNull(big);
        Assert.Equal(big.Id, simulation.World.Structures[north.Id].ParentId);
        Assert.Equal(2, simulation.World.Extension.LevelsBetween(PrototypeScenario.StateId, simulation.World.Structures["commune-4"]));
    }

    [Fact]
    public void Le_batiment_de_la_prefecture_ne_se_construit_pas_a_part()
    {
        var simulation = Prototype();
        var type = simulation.World.BuildingTypes[Simulation.PrefectureTypeId];

        Assert.False(simulation.TryBuild(type.Build("prefecture-a-part", "commune-1"), PaymentMode.StockFirst));
    }

    [Fact]
    public void Le_tresor_paie_les_salaires_d_un_batiment_de_prefecture()
    {
        Structure[] structures =
        [
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("pref", "Préfecture", StructureKind.PoliticalEntity, "etat", EntityType: EntityLevel.Prefecture, SeatId: "a"),
            new("a", "A", StructureKind.PoliticalEntity, "pref"),
            new("b", "B", StructureKind.PoliticalEntity, "pref"),
            new("siege", "Siège", StructureKind.Building, "pref", Jobs: 10, DailyWage: 2),
        ];
        var byId = structures.ToDictionary(s => s.Id);
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 10);
        workers.SetResidence("a", 1);
        SocialClass[] classes = [workers];
        var treasury = new Treasury();
        treasury.Deposit(100);
        var fund = new Treasury();
        fund.Deposit(100);

        WorkDay.Run(byId, Employment.Assign(byId, classes), classes, treasury, _ => new Market([]), _ => fund);

        Assert.Equal(100, fund.Balance, 6);
        Assert.Equal(80, treasury.Balance, 6);
    }
}
