using Demos.Domain.Economy;
using Demos.Domain.Map;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

public class RoadsTests
{
    private static readonly BalanceSettings Settings = new();

    // L'État, une préfecture avec deux communes, une commune hors préfecture, et un bâtiment de la commune A.
    private static readonly Dictionary<string, Structure> Structures = new[]
    {
        new Structure("etat", "État", StructureKind.PoliticalEntity, null),
        new Structure("pref", "Préfecture", StructureKind.PoliticalEntity, "etat", EntityType: EntityLevel.Prefecture),
        new Structure("a", "A", StructureKind.PoliticalEntity, "pref"),
        new Structure("b", "B", StructureKind.PoliticalEntity, "pref"),
        new Structure("c", "C", StructureKind.PoliticalEntity, "etat"),
        new Structure("mine", "Mine", StructureKind.Building, "a"),
    }.ToDictionary(s => s.Id);

    private static readonly Dictionary<string, Point2D> Positions = new()
    {
        ["a"] = new(0, 0),
        ["b"] = new(100, 0),
        ["c"] = new(0, 300),
    };

    private static Point2D? PositionOf(string id) => Positions.TryGetValue(id, out var p) ? p : null;

    [Theory]
    [InlineData("a", "b", "pref")]
    [InlineData("a", "c", "etat")]
    [InlineData("mine", "a", "a")]
    [InlineData("mine", "b", "pref")]
    public void La_liaison_est_payee_par_la_plus_petite_structure_qui_contient_les_deux_lieux(string from, string to, string payer)
    {
        Assert.Equal(payer, Roads.PayerOf(from, to, Structures));
        Assert.Equal(payer, Roads.PayerOf(to, from, Structures));
    }

    [Theory]
    [InlineData(0, RoadLevel.None)]
    [InlineData(1000, RoadLevel.Trail)]
    [InlineData(4000, RoadLevel.Path)]
    [InlineData(9000, RoadLevel.Road)]
    public void Le_trafic_d_un_mois_decide_du_niveau(double traffic, RoadLevel level) =>
        Assert.Equal(level, Roads.LevelFor(traffic, Settings));

    [Fact]
    public void Un_chemin_retire_un_quart_de_la_perte_et_une_route_la_moitie()
    {
        Assert.Equal(1, Roads.LossFactor(RoadLevel.Trail, Settings));
        Assert.Equal(0.75, Roads.LossFactor(RoadLevel.Path, Settings));
        Assert.Equal(0.5, Roads.LossFactor(RoadLevel.Road, Settings));
    }

    [Fact]
    public void Un_fort_trafic_ouvre_un_sentier_puis_monte_d_un_niveau_par_mois_et_la_structure_paie()
    {
        var network = new RoadNetwork();
        var funds = new Dictionary<string, Treasury> { ["pref"] = new() };
        funds["pref"].Deposit(100_000);
        var planner = new RoadPlanner(null, Settings);
        var traffic = new Dictionary<(string, string), double> { [RoadLink.Key("a", "b")] = 20_000 };

        var first = Roads.RunMonthly(network, traffic, PositionOf, planner, (x, y) => Roads.PayerOf(x, y, Structures), id => funds[id], Settings);

        var trail = Assert.Single(first);
        Assert.Equal(RoadLevel.Trail, trail.Link.Level);
        Assert.Equal("pref", trail.Link.PayerId);
        Assert.Equal(100, trail.Link.Length, 6);
        Assert.Equal(Roads.BuildCost(RoadLevel.Trail, 100, 0, Settings), trail.Cost, 6);
        Assert.Equal(100_000 - trail.Cost, funds["pref"].Balance, 6);

        Roads.RunMonthly(network, traffic, PositionOf, planner, (x, y) => Roads.PayerOf(x, y, Structures), id => funds[id], Settings);
        Assert.Equal(RoadLevel.Path, network.LevelBetween("b", "a"));
        Roads.RunMonthly(network, traffic, PositionOf, planner, (x, y) => Roads.PayerOf(x, y, Structures), id => funds[id], Settings);
        Assert.Equal(RoadLevel.Road, network.LevelBetween("a", "b"));
    }

    [Fact]
    public void Sans_l_argent_rien_n_est_construit()
    {
        var network = new RoadNetwork();
        var poor = new Treasury();
        var traffic = new Dictionary<(string, string), double> { [RoadLink.Key("a", "c")] = 5000 };

        var changes = Roads.RunMonthly(network, traffic, PositionOf, new RoadPlanner(null, Settings),
            (x, y) => Roads.PayerOf(x, y, Structures), _ => poor, Settings);

        Assert.Empty(changes);
        Assert.Equal(RoadLevel.None, network.LevelBetween("a", "c"));
    }

    [Fact]
    public void L_entretien_se_paie_chaque_annee_et_une_liaison_impayee_descend_d_un_niveau()
    {
        var network = new RoadNetwork();
        network.Set(new RoadLink("a", "b", RoadLevel.Road, [new(0, 0), new(100, 0)], 100, 20, "pref"));
        var fund = new Treasury();
        var upkeep = Roads.Upkeep(network.Between("a", "b")!, Settings);
        fund.Deposit(upkeep);

        Roads.RunYearly(network, _ => fund, Settings);
        Assert.Equal(RoadLevel.Road, network.LevelBetween("a", "b"));
        Assert.Equal(0, fund.Balance, 6);

        Roads.RunYearly(network, _ => fund, Settings);
        Assert.Equal(RoadLevel.Path, network.LevelBetween("a", "b"));
    }

    // Une carte d'altitude de 200 × 200 lieues (un pixel tous les 2 lieues), décrite par une fonction.
    private static ReliefGrid Relief(Func<double, double, float> altitude)
    {
        var values = new float[100 * 100];
        for (var y = 0; y < 100; y++)
            for (var x = 0; x < 100; x++)
                values[y * 100 + x] = altitude(x * 2 + 1, y * 2 + 1);
        return new ReliefGrid(100, 100, 2, values);
    }

    [Fact]
    public void Le_trace_contourne_une_falaise_par_son_col()
    {
        // Un mur de 2 000 m à x = 100, avec un passage au niveau du sol en bas de la carte (y > 160).
        var relief = Relief((x, y) => x is > 92 and < 108 && y < 160 ? 2000f : 0f);
        var plan = new RoadPlanner(relief, Settings).Plan(new(20, 40), new(180, 40), []);

        Assert.NotNull(plan);
        Assert.Contains(plan.Route, p => p.Y > 150);
        Assert.True(plan.Length > 160);
    }

    [Fact]
    public void La_mer_et_les_pentes_trop_fortes_sont_interdites()
    {
        // Un bras de mer qui coupe toute la carte.
        var sea = Relief((x, _) => x is > 90 and < 110 ? -20f : 10f);
        Assert.Null(new RoadPlanner(sea, Settings).Plan(new(20, 40), new(180, 40), []));
    }

    [Fact]
    public void Le_trace_prefere_une_voie_deja_tracee()
    {
        var flat = Relief((_, _) => 50f);
        var planner = new RoadPlanner(flat, Settings);
        var straight = planner.Plan(new(20, 120), new(180, 120), [])!;
        Assert.DoesNotContain(straight.Route, p => p.Y < 100);

        // Une voie en V, par le haut de la carte : moins chère à suivre (moitié prix) que la ligne droite.
        IReadOnlyList<Point2D> existing = [new(20, 120), new(100, 60), new(180, 120)];
        var branched = planner.Plan(new(20, 120), new(180, 120), [existing])!;
        Assert.Contains(branched.Route, p => p.Y < 80);
    }

    [Fact]
    public void Le_trace_compte_la_montee()
    {
        // Une pente douce : 10 m par lieue vers l'est.
        var slope = Relief((x, _) => (float)(x * 0.1));
        var plan = new RoadPlanner(slope, Settings).Plan(new(10, 100), new(190, 100), [])!;
        Assert.InRange(plan.Climb, 15, 20);
        Assert.True(Roads.CostLength(plan.Length, plan.Climb, Settings) > plan.Length);
    }
}
