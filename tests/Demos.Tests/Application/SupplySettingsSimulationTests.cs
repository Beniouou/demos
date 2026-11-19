using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

/// <summary>Les réglages d'achat et de vente dans une vraie partie.</summary>
public class SupplySettingsSimulationTests
{
    [Fact]
    public void Seuls_les_batiments_publics_ont_des_reglages()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var farm = simulation.World.Structures.Values.First(b => b.Ownership == Ownership.Cooperative && b.Production is not null);

        Assert.Contains("seuls les bâtiments publics", simulation.SetSupplySettings(farm.Id, new SupplySettings(OnlyFromLines: true)));
        Assert.False(simulation.World.HasSupplySettings);
    }

    [Fact]
    public void La_reserve_hors_vente_s_applique_au_marche_de_la_commune_puis_se_retire()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var camp = simulation.World.Structures.Values.Single(b => b.Ownership == Ownership.Public && b.Production?.ProductId == "bois");
        var state = Market.StateSellerId(PrototypeScenario.FirstCommuneId);

        Assert.Null(simulation.SetSupplySettings(camp.Id, new SupplySettings(ReserveDays: 99)));
        Assert.Equal(SupplySettings.MaxReserveDays, simulation.World.SupplySettingsOf(camp.Id).ReserveDays);
        simulation.AdvanceDay();
        var market = simulation.World.MarketOf(PrototypeScenario.FirstCommuneId);
        Assert.True(market.HeldOf("bois", state) > 0);

        Assert.Null(simulation.SetSupplySettings(camp.Id, SupplySettings.None));
        simulation.AdvanceDay();
        Assert.Equal(0, market.HeldOf("bois", state));
        Assert.False(simulation.World.HasSupplySettings);
    }
}
