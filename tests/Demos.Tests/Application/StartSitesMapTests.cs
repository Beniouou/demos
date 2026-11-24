using Demos.Application;
using Demos.Domain.Map;

namespace Demos.Tests.Application;

/// <summary>La carte livrée (game/data/carte.json) : son départ riche doit porter le départ préréglé.</summary>
[Collection(GlobalBalanceCollection.Name)]
public class StartSitesMapTests
{
    [Fact]
    public void Le_depart_preregle_tient_au_depart_riche_de_la_carte()
    {
        var content = GameData.Content();
        var world = PrototypeScenario.Create(content).World;
        var map = content.LoadMap()!;
        var demands = world.Structures.Values
            .Select(s => (Structure: s, Kinds: ExtractionSites.ZonesFor(s.TypeId, s.Production?.ProductId)))
            .Where(s => s.Kinds.Count > 0)
            .Select(s => new ZoneDemand(world.DisplayNameOf(s.Structure), s.Kinds, s.Structure.Jobs))
            .ToList();

        Assert.NotEmpty(demands);
        Assert.Empty(StartSites.PresetProblems(demands, map.DefaultCommune.Position, [.. map.AllBiomes, .. map.AllResourceZones]));
    }
}
