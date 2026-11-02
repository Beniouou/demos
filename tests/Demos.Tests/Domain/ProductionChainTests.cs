using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class ProductionChainTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("ble", "Blé", null, 0.5),
        new("farine", "Farine", null, 1),
        new("pain", "Pain", NeedCategory.Food, 1),
        new("viande", "Viande", NeedCategory.Food, 3),
    ];

    private static readonly Production Moulin = new("farine", 10, [new RecipeInput("ble", 1)]);

    private static (Dictionary<string, Structure> Structures, SocialClass Workers) Town(Ownership ownership = Ownership.Cooperative)
    {
        var structures = new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
            new("moulin", "Moulin", StructureKind.Building, "commune", ownership,
                ownership == Ownership.Public ? null : "ouvriers", Moulin, Jobs: 10, DailyWage: 2),
        }.ToDictionary(s => s.Id);
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 10);
        workers.SetResidence("commune", 1);
        return (structures, workers);
    }

    private static void Run(Dictionary<string, Structure> structures, SocialClass workers, Market market, Treasury fund, Treasury? treasury = null) =>
        WorkDay.Run(structures, Employment.Assign(structures, [workers]), [workers], [], treasury ?? new Treasury(), _ => market,
            companyFundOf: _ => fund);

    [Fact]
    public void Un_moulin_achete_son_ble_et_produit_sa_farine()
    {
        var (structures, workers) = Town();
        var market = new Market(Products);
        market.Offer("ble", 1_000, "ferme");
        var fund = new Treasury();
        fund.Deposit(1_000);

        Run(structures, workers, market, fund);

        // 10 travailleurs × 10 = 100 farines, pour 100 blés achetés à 0,5 ; puis 20 de salaires.
        Assert.Equal(900, market.QuantityOf("ble"), precision: 6);
        Assert.Equal(50, market.CollectEarnings()["ferme"], precision: 6);
        Assert.Equal(100, market.QuantityOf("farine", Market.CompanySellerId("moulin")), precision: 6);
        Assert.Equal(1_000 - 50 - 20, fund.Balance, precision: 6);
    }

    [Fact]
    public void Sans_assez_de_matiere_la_production_baisse_en_proportion()
    {
        var (structures, workers) = Town();
        var market = new Market(Products);
        market.Offer("ble", 30, "ferme");
        var fund = new Treasury();
        fund.Deposit(1_000);

        Run(structures, workers, market, fund);

        Assert.Equal(0, market.QuantityOf("ble"), precision: 6);
        Assert.Equal(30, market.QuantityOf("farine"), precision: 6);
    }

    [Fact]
    public void Sans_assez_d_argent_pour_ses_matieres_la_production_baisse_aussi()
    {
        var (structures, workers) = Town();
        var market = new Market(Products);
        market.Offer("ble", 1_000, "ferme");
        var fund = new Treasury();
        fund.Deposit(10);

        Run(structures, workers, market, fund);

        // 10 d'argent : 20 blés à 0,5, donc 20 farines.
        Assert.Equal(20, market.QuantityOf("farine"), precision: 6);
        Assert.Equal(0, fund.Balance, precision: 6);
    }

    [Fact]
    public void Un_batiment_public_reprend_d_abord_ce_que_l_Etat_a_produit()
    {
        var (structures, workers) = Town(Ownership.Public);
        var market = new Market(Products);
        market.Offer("ble", 60, Market.StateSellerId("commune"));
        market.Offer("ble", 100, "ferme");
        var treasury = new Treasury();
        treasury.Deposit(1_000);

        Run(structures, workers, market, new Treasury(), treasury);

        // 60 blés de l'État, gratuits, puis 40 achetés à 0,5 ; et 20 de salaires.
        Assert.Equal(0, market.QuantityOf("ble", Market.StateSellerId("commune")), precision: 6);
        Assert.Equal(60, market.QuantityOf("ble", "ferme"), precision: 6);
        Assert.Equal(1_000 - 20 - 20, treasury.Balance, precision: 6);
    }

    [Fact]
    public void Le_prix_compte_les_matieres_en_remontant_la_chaine()
    {
        BuildingType[] catalogue =
        [
            new("ferme", "Ferme", "Extraction", 1, "", Jobs: 100, DailyWage: 2, Recipes: [new Production("ble", 8)]),
            new("atelier", "Atelier", "Production", 1, "", Jobs: 100, DailyWage: 2,
                Recipes: [new Production("pain", 25, [new RecipeInput("farine", 1)]), new Production("farine", 25, [new RecipeInput("ble", 1)])]),
        ];

        var prices = Pricing.WithReferencePrices(Products, catalogue).ToDictionary(p => p.Id, p => p.BasePrice);

        // Blé : 1,3 × 2,48 / 8 = 0,403. Farine : 1,3 × (2,48 / 25 + 0,403). Pain : 1,3 × (2,48 / 25 + farine).
        var ble = 1.3 * 2.48 / 8;
        var farine = 1.3 * (2.48 / 25 + ble);
        Assert.Equal(ble, prices["ble"], precision: 6);
        Assert.Equal(farine, prices["farine"], precision: 6);
        Assert.Equal(1.3 * (2.48 / 25 + farine), prices["pain"], precision: 6);
        // La viande n'a pas de bâtiment de référence : elle garde son prix.
        Assert.Equal(3, prices["viande"]);
    }

    [Fact]
    public void Pour_se_nourrir_on_achete_d_abord_le_moins_cher()
    {
        var market = new Market(Products);
        market.Offer("pain", 30, "boulangerie");
        market.Offer("viande", 100, "boucherie");
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 100) { Wealth = 10_000 };
        workers.SetResidence("commune", 1);

        Consumption.RunDaily([workers], Products, market);

        // 100 repas : les 30 pains (à 1), puis 70 viandes (à 3).
        Assert.Equal(0, market.QuantityOf("pain"), precision: 6);
        Assert.Equal(30, market.QuantityOf("viande"), precision: 6);
        Assert.Equal(10_000 - 30 - 210, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Un_atelier_se_construit_avec_la_recette_demandee()
    {
        var atelier = new BuildingType("atelier", "Atelier", "Production", 1, "", Jobs: 100, DailyWage: 2,
            Recipes: [new Production("pain", 25, [new RecipeInput("farine", 1)]), new Production("farine", 25, [new RecipeInput("ble", 1)])]);

        Assert.Equal("pain", atelier.Build("a", "commune").Production!.Value.ProductId);
        Assert.Equal("farine ← ble", atelier.BuildFor("farine", "b", "commune").Production!.Value.Label);
    }
}
