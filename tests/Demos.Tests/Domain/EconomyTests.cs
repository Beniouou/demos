using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class EconomyTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("pain", "Pain", NeedCategory.Food, 1),
        new("vetements", "Vêtements", NeedCategory.Clothing, 8),
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

    private static SocialClass Resident(string id, Rank rank, long population, double wealth = 0)
    {
        var socialClass = new SocialClass(id, id, rank, new Ideology(Pole.A, Pole.A, Pole.A), population) { Wealth = wealth };
        socialClass.SetResidence("commune", 1);
        return socialClass;
    }

    private static SocialClass Workers(long population, double wealth = 0) => Resident("ouvriers", Rank.Workers, population, wealth);

    private static void RunWorkDay(Dictionary<string, Structure> structures, SocialClass[] classes, Treasury treasury,
        Inventory stock, Market market, params Law[] laws)
    {
        var workers = Employment.Assign(structures, classes);
        WorkDay.Run(structures, workers, classes, laws, treasury, stock, market);
    }

    [Fact]
    public void Chaque_rang_occupe_les_emplois_de_son_rang()
    {
        var structures = Town(new Structure("ferme", "Ferme", StructureKind.Building, "commune", Jobs: 100));
        var workers = Workers(60);
        var intermediates = Resident("intermediaires", Rank.Intermediate, 30);
        var executives = Resident("cadres", Rank.Executives, 5);

        var production = Employment.Assign(structures, [workers, intermediates, executives]);

        // 100 emplois de production, 10 d'encadrement, 1 de direction.
        Assert.Equal(60, production["ferme"], precision: 6);
        Assert.Equal(60, workers.MembersWorkingIn("ferme"), precision: 6);
        Assert.Equal(10, intermediates.MembersWorkingIn("ferme"), precision: 6);
        Assert.Equal(1, executives.MembersWorkingIn("ferme"), precision: 6);
    }

    [Fact]
    public void La_production_depend_du_nombre_de_travailleurs()
    {
        var stock = new Inventory();
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune",
            Production: new Production("bois", 0.5), Jobs: 100));

        RunWorkDay(structures, [Workers(40)], new Treasury(), stock, new Market(Products));

        Assert.Equal(20, stock.QuantityOf("bois"), precision: 6);
    }

    [Fact]
    public void Une_production_privee_est_mise_en_vente_et_payee_a_l_achat()
    {
        var market = new Market(Products);
        var structures = Town(new Structure("carriere", "Carrière", StructureKind.Building, "commune",
            Ownership.Private, "cadres", new Production("pierre", 1), Jobs: 60));

        RunWorkDay(structures, [Workers(60)], new Treasury(), new Inventory(), market);

        Assert.Equal(60, market.QuantityOf("pierre"), precision: 6);
        Assert.Empty(market.CollectEarnings());

        Assert.True(market.TryBuy("pierre", 10));
        Assert.Equal(30, market.CollectEarnings()["cadres"], precision: 6);
    }

    [Fact]
    public void Le_tresor_paie_les_salaires_d_un_batiment_public_et_les_professions_intermediaires_gagnent_le_double()
    {
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune", Jobs: 10, DailyWage: 2));
        var workers = Workers(10);
        var intermediates = Resident("intermediaires", Rank.Intermediate, 1);
        var treasury = new Treasury();
        treasury.Deposit(100);

        RunWorkDay(structures, [workers, intermediates], treasury, new Inventory(), new Market(Products));

        // 10 ouvriers × 2 + 1 profession intermédiaire × 4 = 24.
        Assert.Equal(76, treasury.Balance, precision: 6);
        Assert.Equal(20, workers.Wealth, precision: 6);
        Assert.Equal(4, intermediates.Wealth, precision: 6);
    }

    [Fact]
    public void Sans_argent_le_salaire_est_verse_en_partie()
    {
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune", Jobs: 10, DailyWage: 2));
        var workers = Workers(10);
        var treasury = new Treasury();
        treasury.Deposit(5);

        RunWorkDay(structures, [workers], treasury, new Inventory(), new Market(Products));

        Assert.Equal(0, treasury.Balance, precision: 6);
        Assert.Equal(5, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Une_loi_de_paiement_en_nature_paie_les_travailleurs_en_produits()
    {
        var market = new Market(Products);
        var structures = Town(new Structure("ferme", "Ferme", StructureKind.Building, "commune",
            Ownership.Cooperative, "ouvriers", new Production("pain", 3), Jobs: 10, DailyWage: 2));
        var workers = Workers(10, wealth: 100);
        var law = new Law("rations", "Rations", "commune", [], [LawRule.PaymentInKind]);

        RunWorkDay(structures, [workers], new Treasury(), new Inventory(), market, law);

        // 30 pains produits ; 20 de salaire au prix de 1 → 20 pains donnés, 10 mis en vente, aucun argent versé.
        Assert.Equal(20, workers.Goods.QuantityOf("pain"), precision: 6);
        Assert.Equal(10, market.QuantityOf("pain"), precision: 6);
        Assert.Equal(100, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Quand_il_manque_des_places_chaque_classe_est_logee_au_prorata_et_paie_son_loyer()
    {
        var structures = Town(new Structure("logements", "Logements", StructureKind.Building, "commune",
            HousingPlaces: 50, DailyRent: 1));
        var workers = Workers(100, wealth: 1_000);
        var treasury = new Treasury();

        Housing.RunDaily(structures, [workers], treasury);

        Assert.Equal(0.5, workers.HousingCoverage, precision: 6);
        Assert.Equal(950, workers.Wealth, precision: 6);
        Assert.Equal(50, treasury.Balance, precision: 6);
    }

    [Fact]
    public void Une_classe_qui_ne_peut_pas_payer_n_est_logee_qu_a_hauteur_de_ce_qu_elle_paie()
    {
        var structures = Town(new Structure("logements", "Logements", StructureKind.Building, "commune",
            HousingPlaces: 100, DailyRent: 1));
        var workers = Workers(100, wealth: 10);

        Housing.RunDaily(structures, [workers], new Treasury());

        Assert.Equal(0.1, workers.HousingCoverage, precision: 6);
        Assert.Equal(0, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Les_classes_achetent_leurs_besoins_de_base_et_en_tirent_leur_satisfaction()
    {
        var market = new Market(Products);
        market.Offer("pain", 100, "vendeur");
        market.Offer("vetements", 1, "vendeur");
        var workers = Workers(100, wealth: 1_000);
        workers.HousingCoverage = 1;

        Consumption.RunDaily([workers], Products, market);

        // 100 pains (100 × 1) et 1 vêtement (100 × 0,01) : 100 + 8 dépensés, tout est couvert.
        Assert.Equal(1, workers.NeedsSatisfaction, precision: 6);
        Assert.Equal(892, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Les_produits_recus_en_nature_sont_consommes_avant_d_acheter()
    {
        var market = new Market(Products);
        market.Offer("pain", 100, "vendeur");
        var workers = Workers(100, wealth: 1_000);
        workers.Goods.Add("pain", 100);

        Consumption.RunDaily([workers], Products, market);

        Assert.Equal(0, workers.Goods.QuantityOf("pain"), precision: 6);
        Assert.Equal(100, market.QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Quand_un_produit_manque_chaque_classe_est_servie_au_prorata()
    {
        var market = new Market(Products);
        market.Offer("pain", 100, "vendeur");
        var workers = Workers(100, wealth: 10_000);
        var intermediates = Resident("intermediaires", Rank.Intermediate, 100, wealth: 10_000);

        Consumption.RunDaily([workers, intermediates], Products, market);

        // Demande : 100 + 100 pains pour 100 en vente → la moitié de chaque demande est servie.
        Assert.Equal(9_950, workers.Wealth, precision: 6);
        Assert.Equal(9_950, intermediates.Wealth, precision: 6);
    }

    [Fact]
    public void Sans_argent_ni_logement_aucun_besoin_n_est_couvert()
    {
        var market = new Market(Products);
        market.Offer("pain", 100, "vendeur");
        var workers = Workers(100, wealth: 0);
        workers.HousingCoverage = 0;

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
