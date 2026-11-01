using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

public class NationalizationTests
{
    /// <summary>Partie de départ après 5 jours, avec assez de légitimité PR et d'argent au trésor.</summary>
    private static Simulation Prototype()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        for (var day = 0; day < 5; day++)
            simulation.AdvanceDay();
        simulation.World.Legitimacy.Add(Simulation.NationalizationPole, 100);
        simulation.World.Treasury.Deposit(10_000_000);
        return simulation;
    }

    /// <summary>L'atelier des cadres n'a versé que 10 % de ses salaires sur 30 jours.</summary>
    private static void MakeStruggling(Simulation simulation, string buildingId)
    {
        for (var day = 0; day < WageLedger.Days; day++)
            simulation.World.Wages.Record(buildingId, 100, 10);
    }

    [Fact]
    public void Une_entreprise_qui_verse_moins_de_80_pourcent_des_salaires_est_en_difficulte()
    {
        var ledger = new WageLedger();
        for (var day = 0; day < 40; day++)
            ledger.Record("atelier", 100, day < 20 ? 100 : 70);

        // Seuls les 30 derniers jours comptent : 10 jours à 100 %, 20 jours à 70 %, soit 80 % tout juste.
        Assert.Equal(0.8, ledger.PaidShare("atelier")!.Value, precision: 6);
        Assert.False(ledger.IsStruggling("atelier"));

        ledger.Record("atelier", 100, 0);
        Assert.True(ledger.IsStruggling("atelier"));
    }

    [Fact]
    public void Une_entreprise_en_difficulte_se_rachete_presque_pour_rien()
    {
        var simulation = Prototype();
        MakeStruggling(simulation, "atelier");
        var atelier = simulation.World.Structures["atelier"];
        var expected = Math.Max(Nationalization.MinimumPrice, simulation.DailyProfitOf(atelier) * Nationalization.StrugglingProfitDays);
        var cadresBefore = simulation.World.ClassesById["cadres"].Wealth;
        var legitimacyBefore = simulation.World.Legitimacy[Simulation.NationalizationPole];

        Assert.Equal(NationalizationOutcome.Done, simulation.TryNationalize("atelier", NationalizationMode.Buyout));

        Assert.Equal(Ownership.Public, simulation.World.Structures["atelier"].Ownership);
        Assert.Null(simulation.World.Structures["atelier"].OwnerClassId);
        Assert.Equal(cadresBefore + expected, simulation.World.ClassesById["cadres"].Wealth, precision: 6);
        Assert.Equal(legitimacyBefore - Nationalization.StrugglingLegitimacyCost, simulation.World.Legitimacy[Simulation.NationalizationPole], precision: 6);
        var law = Assert.Single(simulation.World.Laws, l => l.Id == "nationalisation-atelier");
        Assert.Equal([Simulation.NationalizationPole], law.Orientations);
    }

    [Fact]
    public void Une_entreprise_qui_marche_coute_plus_cher_en_argent_et_en_legitimite()
    {
        var simulation = Prototype();
        var atelier = simulation.World.Structures["atelier"];
        var thrivingPrice = simulation.NationalizationPriceOf(atelier, NationalizationMode.Buyout);
        var thrivingCost = simulation.NationalizationLegitimacyCostOf(atelier, NationalizationMode.Buyout);

        MakeStruggling(simulation, "atelier");

        Assert.True(thrivingPrice >= simulation.NationalizationPriceOf(atelier, NationalizationMode.Buyout));
        Assert.Equal(Nationalization.ThrivingLegitimacyCost, thrivingCost);
        Assert.Equal(Nationalization.StrugglingLegitimacyCost, simulation.NationalizationLegitimacyCostOf(atelier, NationalizationMode.Buyout));
    }

    [Fact]
    public void L_expropriation_ne_coute_rien_en_argent_mais_beaucoup_en_legitimite()
    {
        var simulation = Prototype();
        var treasuryBefore = simulation.World.Treasury.Balance;
        var cadresBefore = simulation.World.ClassesById["cadres"].Wealth;
        var legitimacyBefore = simulation.World.Legitimacy[Simulation.NationalizationPole];

        Assert.Equal(NationalizationOutcome.Done, simulation.TryNationalize("atelier", NationalizationMode.Expropriation));

        Assert.Equal(treasuryBefore, simulation.World.Treasury.Balance, precision: 6);
        Assert.Equal(cadresBefore, simulation.World.ClassesById["cadres"].Wealth, precision: 6);
        Assert.Equal(legitimacyBefore - Nationalization.ExpropriationLegitimacyCost, simulation.World.Legitimacy[Simulation.NationalizationPole], precision: 6);
    }

    [Fact]
    public void Les_habitants_achetent_le_pain_d_une_ferme_nationalisee()
    {
        var simulation = Prototype();
        Assert.Equal(NationalizationOutcome.Done, simulation.TryNationalize("ferme", NationalizationMode.Expropriation));
        var farm = simulation.World.Structures["ferme"];

        simulation.AdvanceDay();

        // Le pain du jour est au nom de l'État ; les habitants en ont acheté une partie.
        var produced = simulation.World.Workers["ferme"] * farm.Production!.Value.QuantityPerWorker;
        Assert.True(simulation.World.StateQuantityOf("commune-1", "pain") < produced);
        Assert.True(simulation.World.ClassesById["ouvriers"].NeedsSatisfaction > 0.5);
    }

    [Fact]
    public void Nationaliser_un_batiment_vendu_retire_la_loi_de_privatisation()
    {
        var simulation = Prototype();
        simulation.World.Legitimacy.Add(Simulation.SalePole, 100);
        Assert.Equal(SaleOutcome.Sold, simulation.TrySellBuilding("logements-etat", "cadres", 1_000));

        Assert.Equal(NationalizationOutcome.Done, simulation.TryNationalize("logements-etat", NationalizationMode.Expropriation));

        Assert.DoesNotContain(simulation.World.Laws, l => l.Id == "privatisation-logements-etat");
        Assert.Contains(simulation.World.Laws, l => l.Id == "nationalisation-logements-etat");
    }

    [Fact]
    public void Sans_legitimite_ou_sans_argent_rien_n_est_nationalise()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.AdvanceDay();
        simulation.World.Legitimacy.TrySpend(Simulation.NationalizationPole, simulation.World.Legitimacy[Simulation.NationalizationPole]);

        Assert.Equal(NationalizationOutcome.NotEnoughLegitimacy, simulation.TryNationalize("atelier", NationalizationMode.Expropriation));

        simulation.World.Legitimacy.Add(Simulation.NationalizationPole, 100);
        simulation.World.Treasury.TrySpend(simulation.World.Treasury.Balance);
        Assert.Equal(NationalizationOutcome.TreasuryTooPoor, simulation.TryNationalize("atelier", NationalizationMode.Buyout));
        Assert.Equal(Ownership.Private, simulation.World.Structures["atelier"].Ownership);
        Assert.Equal(NationalizationOutcome.NotPrivate, simulation.TryNationalize("logements-etat", NationalizationMode.Buyout));
    }
}
