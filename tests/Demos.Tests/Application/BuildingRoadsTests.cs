using Demos.Application;
using Demos.Domain.Map;

namespace Demos.Tests.Application;

/// <summary>
/// Routes des bâtiments (décidé le 12 novembre 2026) : un bâtiment posé sur la carte fait du trafic avec sa commune, qui
/// crée sentier, chemin et route payés par la commune, sans perte de transport.
/// </summary>
public class BuildingRoadsTests
{
    private static readonly string[] Extraction = ["camp-forestier", "ferme", "champ-de-lin", "carriere", "mine"];

    private static void AdvanceDays(Simulation simulation, int days)
    {
        for (var day = 0; day < days; day++)
            simulation.AdvanceDay();
    }

    [Fact]
    public void Ce_que_produit_un_batiment_pose_fait_une_route_vers_sa_commune_payee_par_elle()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());

        AdvanceDays(simulation, 62);

        var links = simulation.World.Roads.Links
            .Where(l => Extraction.Contains(l.A) || Extraction.Contains(l.B))
            .ToList();
        Assert.NotEmpty(links);
        Assert.All(links, link =>
        {
            Assert.Contains(PrototypeScenario.FirstCommuneId, new[] { link.A, link.B });
            Assert.Equal(PrototypeScenario.FirstCommuneId, link.PayerId);
        });
    }

    [Fact]
    public void Le_trafic_d_un_batiment_n_est_pas_un_echange_entre_communes()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        AdvanceDays(simulation, 5);

        Assert.Contains(simulation.World.RoadTraffic.ThisMonth.Keys, k => Extraction.Contains(k.Item1) || Extraction.Contains(k.Item2));
        Assert.DoesNotContain(new GameView(simulation).TradeFlows(), f => Extraction.Contains(f.A) || Extraction.Contains(f.B));
    }
}
