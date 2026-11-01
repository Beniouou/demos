using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class EconomyTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("pain", "Pain", NeedCategory.Food, 1),
        new("meubles", "Meubles", NeedCategory.Furniture, 20),
        new("pierre", "Pierre", null, 3),
        new("bois", "Bois", null, 2),
    ];

    private static readonly MaterialCost[] Cost = [new("pierre", 100), new("bois", 50)];

    private static Dictionary<string, Structure> Town(params Structure[] buildings)
    {
        Structure[] political =
        [
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        ];
        return political.Concat(buildings).ToDictionary(s => s.Id);
    }

    private static SocialClass Workers(long population, double wealth = 0)
    {
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), population) { Wealth = wealth };
        workers.SetResidence("commune", 1);
        return workers;
    }

    [Fact]
    public void Les_postes_sont_pourvus_par_les_habitants_de_la_commune()
    {
        var structures = Town(new Structure("ferme", "Ferme", StructureKind.Building, "commune", Jobs: 100));
        var workers = Workers(60);

        var filled = Employment.Assign(structures, [workers]);

        Assert.Equal(60, filled["ferme"], precision: 6);
        Assert.Equal(60, workers.MembersWorkingIn("ferme"), precision: 6);
    }

    [Fact]
    public void Les_dirigeants_ne_prennent_pas_les_postes()
    {
        var structures = Town(new Structure("ferme", "Ferme", StructureKind.Building, "commune", Jobs: 100));
        var leaders = new SocialClass("dirigeants", "Dirigeants", Rank.Leaders, new Ideology(Pole.B, Pole.A, Pole.B), 50);
        leaders.SetResidence("commune", 1);

        var filled = Employment.Assign(structures, [leaders]);

        Assert.False(filled.ContainsKey("ferme"));
    }

    [Fact]
    public void La_production_depend_du_nombre_de_travailleurs()
    {
        var stock = new Inventory();
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune",
            Production: new Production("bois", 0.5), Jobs: 100));

        DailyProduction.Run(structures, new Dictionary<string, double> { ["scierie"] = 40 }, stock, new Market(Products));

        Assert.Equal(20, stock.QuantityOf("bois"), precision: 6);
    }

    [Fact]
    public void Une_production_privee_est_mise_en_vente_et_payee_a_l_achat()
    {
        var market = new Market(Products);
        var structures = Town(new Structure("carriere", "Carrière", StructureKind.Building, "commune",
            Ownership.Private, "dirigeants", new Production("pierre", 1), Jobs: 60));

        DailyProduction.Run(structures, new Dictionary<string, double> { ["carriere"] = 60 }, new Inventory(), market);

        Assert.Equal(60, market.QuantityOf("pierre"));
        Assert.Empty(market.CollectEarnings());

        Assert.True(market.TryBuy("pierre", 10));
        Assert.Equal(30, market.CollectEarnings()["dirigeants"], precision: 6);
    }

    [Fact]
    public void Le_tresor_paie_les_salaires_d_un_batiment_public()
    {
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune", Jobs: 10, DailyWage: 2));
        var workers = Workers(10);
        var filled = Employment.Assign(structures, [workers]);
        var treasury = new Treasury();
        treasury.Deposit(100);

        Wages.PayDaily(structures, filled, new Dictionary<string, SocialClass> { ["ouvriers"] = workers }, treasury);

        Assert.Equal(80, treasury.Balance, precision: 6);
        Assert.Equal(20, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Sans_argent_le_salaire_est_verse_en_partie()
    {
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune", Jobs: 10, DailyWage: 2));
        var workers = Workers(10);
        var filled = Employment.Assign(structures, [workers]);
        var treasury = new Treasury();
        treasury.Deposit(5);

        Wages.PayDaily(structures, filled, new Dictionary<string, SocialClass> { ["ouvriers"] = workers }, treasury);

        Assert.Equal(0, treasury.Balance, precision: 6);
        Assert.Equal(5, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Les_classes_achetent_leurs_besoins_et_en_tirent_leur_satisfaction()
    {
        var market = new Market(Products);
        market.Offer("pain", 100, "vendeur");
        market.Offer("meubles", 1, "vendeur");
        var workers = Workers(100, wealth: 1_000);

        Consumption.RunDaily([workers], Products, market);

        // 100 pains (100 × 1) et 1 meuble (100 × 0,01) : tout est couvert pour 100 + 20.
        Assert.Equal(1, workers.NeedsSatisfaction, precision: 6);
        Assert.Equal(880, workers.Wealth, precision: 6);
        Assert.Equal(0, market.QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Quand_un_produit_manque_chaque_classe_est_servie_au_prorata()
    {
        var market = new Market(Products);
        market.Offer("pain", 100, "vendeur");
        var workers = Workers(100, wealth: 10_000);
        var executives = new SocialClass("cadres", "Cadres", Rank.Executives, new Ideology(Pole.A, Pole.A, Pole.B), 100) { Wealth = 10_000 };

        Consumption.RunDaily([workers, executives], Products, market);

        // Demande : 100 + 150 pains pour 100 en vente → 40 % de chaque demande est servi.
        Assert.Equal(9_960, workers.Wealth, precision: 6);
        Assert.Equal(9_940, executives.Wealth, precision: 6);
    }

    [Fact]
    public void Une_classe_sans_argent_ne_couvre_pas_ses_besoins()
    {
        var market = new Market(Products);
        market.Offer("pain", 100, "vendeur");
        var workers = Workers(100, wealth: 0);

        Consumption.RunDaily([workers], Products, market);

        Assert.Equal(0, workers.NeedsSatisfaction);
        Assert.Equal(100, market.QuantityOf("pain"));
    }

    [Fact]
    public void Les_impots_passent_chaque_jour_de_la_richesse_des_classes_au_tresor()
    {
        var treasury = new Treasury();
        var workers = Workers(100, wealth: 10_000);

        Taxation.CollectDaily([workers], 0.001, treasury);

        Assert.Equal(10, treasury.Balance, precision: 6);
        Assert.Equal(9_990, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Construire_avec_le_stock_ne_coute_pas_d_argent()
    {
        var (stock, treasury, market) = Setup(stockPierre: 100, stockBois: 50, money: 0, marketPierre: 0, marketBois: 0);

        Assert.True(Construction.TryPay(Cost, PaymentMode.StockFirst, stock, treasury, market));
        Assert.Equal(0, stock.QuantityOf("pierre"));
        Assert.Equal(0, stock.QuantityOf("bois"));
    }

    [Fact]
    public void Un_stock_insuffisant_est_complete_par_un_achat_sur_le_marche()
    {
        var (stock, treasury, market) = Setup(stockPierre: 40, stockBois: 50, money: 1_000, marketPierre: 100, marketBois: 0);

        Assert.True(Construction.TryPay(Cost, PaymentMode.StockFirst, stock, treasury, market));

        // 60 pierres achetées à 3 : 180 dépensés, et payés au vendeur.
        Assert.Equal(820, treasury.Balance, precision: 6);
        Assert.Equal(40, market.QuantityOf("pierre"), precision: 6);
        Assert.Equal(180, market.CollectEarnings()["vendeur"], precision: 6);
    }

    [Fact]
    public void Payer_en_argent_laisse_le_stock_intact()
    {
        var (stock, treasury, market) = Setup(stockPierre: 100, stockBois: 50, money: 1_000, marketPierre: 100, marketBois: 50);

        Assert.True(Construction.TryPay(Cost, PaymentMode.MoneyOnly, stock, treasury, market));

        // 100 × 3 + 50 × 2 = 400.
        Assert.Equal(600, treasury.Balance, precision: 6);
        Assert.Equal(100, stock.QuantityOf("pierre"));
    }

    [Fact]
    public void Si_le_tresor_ne_suffit_pas_rien_n_est_depense()
    {
        var (stock, treasury, market) = Setup(stockPierre: 40, stockBois: 50, money: 10, marketPierre: 100, marketBois: 0);

        Assert.False(Construction.TryPay(Cost, PaymentMode.StockFirst, stock, treasury, market));
        Assert.Equal(10, treasury.Balance);
        Assert.Equal(40, stock.QuantityOf("pierre"));
        Assert.Equal(50, stock.QuantityOf("bois"));
        Assert.Equal(100, market.QuantityOf("pierre"));
    }

    private static (Inventory Stock, Treasury Treasury, Market Market) Setup(
        double stockPierre, double stockBois, double money, double marketPierre, double marketBois)
    {
        var stock = new Inventory();
        stock.Add("pierre", stockPierre);
        stock.Add("bois", stockBois);

        var treasury = new Treasury();
        treasury.Deposit(money);

        var market = new Market(Products);
        market.Offer("pierre", marketPierre, "vendeur");
        market.Offer("bois", marketBois, "vendeur");

        return (stock, treasury, market);
    }
}
