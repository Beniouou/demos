using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

public class PrivatizationTests
{
    /// <summary>
    /// Partie de départ après 5 jours, avec un camp forestier d'État rendu rentable (5 bois par travailleur au lieu
    /// de 0,2) et assez de légitimité LI pour vendre.
    /// </summary>
    private static Simulation PrototypeWithProfitableForestCamp()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var camp = simulation.World.Structures["camp-forestier"];
        simulation.UpdateBuilding(camp with { Production = camp.Production!.Value with { QuantityPerWorker = 5 } });
        for (var day = 0; day < 5; day++)
            simulation.AdvanceDay();
        simulation.World.Legitimacy.Add(Simulation.SalePole, 100);
        return simulation;
    }

    [Fact]
    public void Vendre_un_batiment_public_le_donne_a_la_classe_et_l_argent_va_au_tresor()
    {
        var simulation = PrototypeWithProfitableForestCamp();
        var world = simulation.World;
        var treasuryBefore = world.Treasury.Balance;
        var cadresBefore = world.ClassesById["cadres"].Wealth;
        var legitimacyBefore = world.Legitimacy[Simulation.SalePole];

        var outcome = simulation.TrySellBuilding("camp-forestier", "cadres", 10_000);

        Assert.Equal(SaleOutcome.Sold, outcome);
        Assert.Equal(Ownership.Private, world.Structures["camp-forestier"].Ownership);
        Assert.Equal("cadres", world.Structures["camp-forestier"].OwnerClassId);
        Assert.Equal(treasuryBefore + 10_000, world.Treasury.Balance, precision: 6);
        Assert.Equal(cadresBefore - 10_000, world.ClassesById["cadres"].Wealth, precision: 6);
        Assert.Equal(legitimacyBefore - Simulation.SaleLegitimacyCost, world.Legitimacy[Simulation.SalePole], precision: 6);

        // La vente reste dans la commune comme une loi libérale.
        var law = Assert.Single(world.Laws, l => l.Id == "privatisation-camp-forestier");
        Assert.Equal("commune-1", law.StructureId);
        Assert.Equal([Simulation.SalePole], law.Orientations);
    }

    [Fact]
    public void Apres_la_vente_le_bois_est_en_vente_au_nom_du_proprietaire()
    {
        var simulation = PrototypeWithProfitableForestCamp();
        simulation.TrySellBuilding("camp-forestier", "cadres", 10_000);
        var stockBefore = simulation.World.StateQuantityOf("commune-1", "bois");

        simulation.AdvanceDay();

        Assert.True(simulation.World.StateQuantityOf("commune-1", "bois") <= stockBefore);
        Assert.True(simulation.World.MarketOf("commune-1").QuantityOf("bois", Market.CompanySellerId("camp-forestier")) > 0);
    }

    [Fact]
    public void Des_ouvriers_rachetent_en_cooperative()
    {
        var simulation = PrototypeWithProfitableForestCamp();

        Assert.Equal(SaleOutcome.Sold, simulation.TrySellBuilding("camp-forestier", "ouvriers", 1_000));
        Assert.Equal(Ownership.Cooperative, simulation.World.Structures["camp-forestier"].Ownership);
    }

    [Fact]
    public void Une_classe_refuse_un_prix_superieur_a_un_an_de_benefice()
    {
        var simulation = PrototypeWithProfitableForestCamp();
        var camp = simulation.World.Structures["camp-forestier"];
        var maximum = Privatization.MaximumPriceOf(simulation.DailyProfitOf(camp));
        simulation.World.ClassesById["cadres"].Wealth = maximum * 10;

        Assert.Equal(SaleOutcome.PriceAboveProfit, simulation.TrySellBuilding("camp-forestier", "cadres", maximum + 1));
        Assert.Equal(Ownership.Public, simulation.World.Structures["camp-forestier"].Ownership);
    }

    [Fact]
    public void Un_batiment_qui_perd_de_l_argent_ne_trouve_pas_preneur()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        for (var day = 0; day < 5; day++)
            simulation.AdvanceDay();
        simulation.World.Legitimacy.Add(Simulation.SalePole, 100);

        // L'école d'État est gratuite : elle ne rapporte rien et paie ses salaires.
        Assert.True(simulation.DailyProfitOf(simulation.World.Structures["ecole"]) < 0);
        Assert.Equal(SaleOutcome.PriceAboveProfit, simulation.TrySellBuilding("ecole", "cadres", 0));
    }

    [Fact]
    public void Sans_assez_d_argent_ou_de_legitimite_rien_n_est_vendu()
    {
        var simulation = PrototypeWithProfitableForestCamp();
        var cadres = simulation.World.ClassesById["cadres"];

        Assert.Equal(SaleOutcome.BuyerTooPoor, simulation.TrySellBuilding("camp-forestier", "cadres", cadres.Wealth + 1));

        simulation.World.Legitimacy.TrySpend(Simulation.SalePole, simulation.World.Legitimacy[Simulation.SalePole]);
        Assert.Equal(SaleOutcome.NotEnoughLegitimacy, simulation.TrySellBuilding("camp-forestier", "cadres", 1_000));
        Assert.Equal(Ownership.Public, simulation.World.Structures["camp-forestier"].Ownership);
    }
}
