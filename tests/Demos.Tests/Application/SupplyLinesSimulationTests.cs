using Demos.Application;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

/// <summary>Les lignes d'approvisionnement dans une vraie partie : création, refus, livraison.</summary>
public class SupplyLinesSimulationTests
{
    private static (Simulation Simulation, string Camp, string Sawmill) Start()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var buildings = simulation.World.Structures.Values.Where(s => s.Kind == StructureKind.Building).ToList();
        var camp = buildings.Single(b => b.Ownership == Ownership.Public && b.Production?.ProductId == "bois");
        var sawmill = buildings.Single(b => b.Ownership == Ownership.Public && b.Production?.ProductId == "planche");
        return (simulation, camp.Id, sawmill.Id);
    }

    [Fact]
    public void Le_camp_de_bucheron_d_etat_livre_la_scierie_d_etat()
    {
        var (simulation, camp, sawmill) = Start();
        var (line, problem) = simulation.TryAddSupplyLine(camp, sawmill);

        Assert.Null(problem);
        for (var day = 0; day < 5; day++)
            simulation.AdvanceDay();

        Assert.True(simulation.World.SupplyLineDeliveries[line!.Id] > 0);
        Assert.True(simulation.World.ReserveContentsOf(sawmill).GetValueOrDefault("bois") > 0);
    }

    [Fact]
    public void Une_ligne_vers_un_batiment_qui_n_utilise_pas_le_produit_est_refusee()
    {
        var (simulation, camp, sawmill) = Start();

        var (line, problem) = simulation.TryAddSupplyLine(sawmill, camp);

        Assert.Null(line);
        Assert.Contains("n'utilise pas", problem);
        Assert.Empty(simulation.World.SupplyLines);
    }

    [Fact]
    public void La_part_d_une_ligne_reste_entre_5_et_100_pour_cent()
    {
        var (simulation, camp, sawmill) = Start();
        var (line, _) = simulation.TryAddSupplyLine(camp, sawmill, share: 3);

        Assert.Equal(1, line!.Share);
        simulation.SetSupplyLineShare(line.Id, 0);
        Assert.Equal(0.05, simulation.World.SupplyLines.Single().Share, 6);
        Assert.True(simulation.RemoveSupplyLine(line.Id));
        Assert.Empty(simulation.World.SupplyLines);
    }
}
