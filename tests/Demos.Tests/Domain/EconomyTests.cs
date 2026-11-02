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
        new("alcool", "Alcool", NeedCategory.Leisure, 2),
        new("meubles", "Meubles", NeedCategory.Furniture, 20),
        new("administration", "Administration", NeedCategory.Administration, 1, Stockable: false),
        new("securite", "Sécurité", NeedCategory.Security, 1, Stockable: false),
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
        Market market, params Law[] laws)
    {
        var workers = Employment.Assign(structures, classes);
        WorkDay.Run(structures, workers, classes, laws, treasury, _ => market);
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
    public void La_production_publique_arrive_dans_le_stockage_de_la_commune_au_nom_de_l_Etat()
    {
        var market = new Market(Products);
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune",
            Production: new Production("bois", 0.5), Jobs: 100));

        RunWorkDay(structures, [Workers(40)], new Treasury(), market);

        // 40 travailleurs × 0,5 : 20 bois, en vente au nom de l'État de la commune.
        Assert.Equal(20, market.QuantityOf("bois", Market.StateSellerId("commune")), precision: 6);
    }

    [Fact]
    public void Une_production_privee_est_mise_en_vente_et_payee_a_l_achat()
    {
        var market = new Market(Products);
        var structures = Town(new Structure("carriere", "Carrière", StructureKind.Building, "commune",
            Ownership.Private, "cadres", new Production("pierre", 1), Jobs: 60));

        RunWorkDay(structures, [Workers(60)], new Treasury(), market);

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

        RunWorkDay(structures, [workers, intermediates], treasury, new Market(Products));

        // 10 ouvriers × 2 + 1 profession intermédiaire × 4 = 24.
        Assert.Equal(76, treasury.Balance, precision: 6);
        Assert.Equal(20, workers.Wealth, precision: 6);
        Assert.Equal(4, intermediates.Wealth, precision: 6);
    }

    [Fact]
    public void La_caisse_de_la_commune_paie_d_abord_puis_le_tresor_prend_le_relais()
    {
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune", Jobs: 10, DailyWage: 2));
        SocialClass[] classes = [Workers(10)];
        var fund = new Treasury();
        fund.Deposit(15);
        var treasury = new Treasury();
        treasury.Deposit(100);

        WorkDay.Run(structures, Employment.Assign(structures, classes), classes, [], treasury, _ => new Market(Products), _ => fund);

        // 20 de salaires : 15 de la caisse, 5 du trésor.
        Assert.Equal(0, fund.Balance, precision: 6);
        Assert.Equal(95, treasury.Balance, precision: 6);
        Assert.Equal(20, classes[0].Wealth, precision: 6);
    }

    [Fact]
    public void Sans_argent_le_salaire_est_verse_en_partie()
    {
        var structures = Town(new Structure("scierie", "Scierie", StructureKind.Building, "commune", Jobs: 10, DailyWage: 2));
        var workers = Workers(10);
        var treasury = new Treasury();
        treasury.Deposit(5);

        RunWorkDay(structures, [workers], treasury, new Market(Products));

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

        RunWorkDay(structures, [workers], new Treasury(), market, law);

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
        market.Offer("alcool", 5, "vendeur");
        market.Offer("meubles", 1, "vendeur");
        market.Offer("administration", 1, "mairie");
        market.Offer("securite", 1, "caserne");
        var workers = Workers(100, wealth: 1_000);
        workers.HousingCoverage = 1;

        Consumption.RunDaily([workers], Products, market);

        // 100 pains (100 × 1), 1 vêtement (100 × 0,01), 0,5 meuble (100 × 0,005), 5 alcools (100 × 0,05), 1 service
        // administratif et 1 de sécurité (100 × 0,01) : 100 + 8 + 10 + 10 + 2 dépensés, tout est couvert.
        Assert.Equal(1, workers.NeedsSatisfaction, precision: 6);
        Assert.Equal(870, workers.Wealth, precision: 6);
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
    public void L_impot_d_Etat_va_au_tresor_et_l_impot_communal_a_la_caisse_de_la_commune()
    {
        var (treasury, fund, workers) = CollectTaxes();

        // 0,2 % de 10 000 pour l'État, 0,1 % pour la commune.
        Assert.Equal(20, treasury.Balance, precision: 6);
        Assert.Equal(10, fund.Balance, precision: 6);
        Assert.Equal(9_970, workers.Wealth, precision: 6);
    }

    [Fact]
    public void Une_loi_peut_augmenter_l_impot_communal()
    {
        var law = new Law("taxe", "Taxe communale", "commune", [], TaxAdjustments: [new(TaxLevel.Commune, TaxAdjustmentKind.Surcharge, 0.002)]);

        var (treasury, fund, _) = CollectTaxes(law);

        Assert.Equal(20, treasury.Balance, precision: 6);
        Assert.Equal(30, fund.Balance, precision: 6);
    }

    [Fact]
    public void Une_loi_d_Etat_peut_plafonner_l_impot_d_Etat_meme_avec_un_supplement()
    {
        var surcharge = new Law("hausse", "Hausse", "commune", [], TaxAdjustments: [new(TaxLevel.State, TaxAdjustmentKind.Surcharge, 0.005)]);
        var maximum = new Law("plafond", "Plafond", "etat", [], TaxAdjustments: [new(TaxLevel.State, TaxAdjustmentKind.Maximum, 0.001)]);

        var (treasury, fund, _) = CollectTaxes(surcharge, maximum);

        Assert.Equal(10, treasury.Balance, precision: 6);
        Assert.Equal(10, fund.Balance, precision: 6);
    }

    [Fact]
    public void Une_loi_d_une_autre_commune_ne_change_pas_l_impot()
    {
        var structures = new Dictionary<string, Structure>(Town())
        {
            ["commune-2"] = new("commune-2", "Commune 2", StructureKind.PoliticalEntity, "etat"),
        };
        var law = new Law("taxe", "Taxe", "commune-2", [], TaxAdjustments: [new(TaxLevel.Commune, TaxAdjustmentKind.Surcharge, 0.01)]);

        var (_, fund, _) = CollectTaxes(structures, law);

        Assert.Equal(10, fund.Balance, precision: 6);
    }

    private static (Treasury Treasury, Treasury Fund, SocialClass Workers) CollectTaxes(params Law[] laws) => CollectTaxes(Town(), laws);

    private static (Treasury Treasury, Treasury Fund, SocialClass Workers) CollectTaxes(Dictionary<string, Structure> structures, params Law[] laws)
    {
        var treasury = new Treasury();
        var fund = new Treasury();
        var workers = Workers(100, wealth: 10_000);

        Taxation.CollectDaily([workers], 0.002, _ => 0.001, treasury, _ => fund, laws, structures);

        return (treasury, fund, workers);
    }

    [Fact]
    public void Construire_avec_le_stock_ne_coute_pas_d_argent()
    {
        var (treasury, market) = Setup(stockPierre: 100, stockBois: 50, money: 0, marketPierre: 0, marketBois: 0);

        Assert.True(Construction.TryPay(Cost, PaymentMode.StockFirst, State, treasury, market));
        Assert.Equal(0, market.QuantityOf("pierre", State));
        Assert.Equal(0, market.QuantityOf("bois", State));
    }

    [Fact]
    public void Un_stock_insuffisant_est_complete_par_un_achat_sur_le_marche()
    {
        var (treasury, market) = Setup(stockPierre: 40, stockBois: 50, money: 1_000, marketPierre: 100, marketBois: 0);

        Assert.True(Construction.TryPay(Cost, PaymentMode.StockFirst, State, treasury, market));

        // 60 pierres achetées à 3 : 180 dépensés, et payés au vendeur.
        Assert.Equal(820, treasury.Balance, precision: 6);
        Assert.Equal(40, market.QuantityOfOthers("pierre", State), precision: 6);
        Assert.Equal(0, market.QuantityOf("pierre", State), precision: 6);
        Assert.Equal(180, market.CollectEarnings()["vendeur"], precision: 6);
    }

    [Fact]
    public void Payer_en_argent_laisse_le_stock_intact()
    {
        var (treasury, market) = Setup(stockPierre: 100, stockBois: 50, money: 1_000, marketPierre: 100, marketBois: 50);

        Assert.True(Construction.TryPay(Cost, PaymentMode.MoneyOnly, State, treasury, market));

        // 100 × 3 + 50 × 2 = 400.
        Assert.Equal(600, treasury.Balance, precision: 6);
        Assert.Equal(100, market.QuantityOf("pierre", State));
        Assert.Equal(0, market.QuantityOfOthers("pierre", State));
    }

    [Fact]
    public void Si_le_tresor_ne_suffit_pas_rien_n_est_depense()
    {
        var (treasury, market) = Setup(stockPierre: 40, stockBois: 50, money: 10, marketPierre: 100, marketBois: 0);

        Assert.False(Construction.TryPay(Cost, PaymentMode.StockFirst, State, treasury, market));
        Assert.Equal(10, treasury.Balance);
        Assert.Equal(40, market.QuantityOf("pierre", State));
        Assert.Equal(50, market.QuantityOf("bois", State));
        Assert.Equal(100, market.QuantityOfOthers("pierre", State));
    }

    private static readonly string State = Market.StateSellerId("commune");

    /// <summary>Le stockage de la commune : des produits de l'État et des produits d'un autre vendeur.</summary>
    private static (Treasury Treasury, Market Market) Setup(
        double stockPierre, double stockBois, double money, double marketPierre, double marketBois)
    {

        var treasury = new Treasury();
        treasury.Deposit(money);

        var market = new Market(Products);
        market.Offer("pierre", stockPierre, State);
        market.Offer("bois", stockBois, State);
        market.Offer("pierre", marketPierre, "vendeur");
        market.Offer("bois", marketBois, "vendeur");

        return (treasury, market);
    }
}
