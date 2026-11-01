using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class EconomyTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("pierre", "Pierre", null, 3),
        new("bois", "Bois", null, 2),
    ];

    private static readonly MaterialCost[] Cost = [new("pierre", 100), new("bois", 50)];

    [Fact]
    public void Une_structure_publique_produit_pour_le_stock_de_l_Etat()
    {
        var stock = new Inventory();
        var market = new Market(Products);
        Structure[] structures = [new("carriere", "Carrière", StructureKind.Building, "etat", Production: new Production("pierre", 60))];

        DailyProduction.Run(structures, new Dictionary<string, SocialClass>(), stock, market);

        Assert.Equal(60, stock.QuantityOf("pierre"));
        Assert.Equal(0, market.Stock.QuantityOf("pierre"));
    }

    [Fact]
    public void Une_structure_privee_vend_sur_le_marche_au_profit_de_sa_classe()
    {
        var stock = new Inventory();
        var market = new Market(Products);
        var owners = new SocialClass("dirigeants", "Dirigeants", Rank.Leaders, new Ideology(Pole.B, Pole.A, Pole.B), 100);
        Structure[] structures =
        [
            new("carriere", "Carrière", StructureKind.Building, "etat", Ownership.Private, "dirigeants", new Production("pierre", 60)),
        ];

        DailyProduction.Run(structures, new Dictionary<string, SocialClass> { ["dirigeants"] = owners }, stock, market);

        Assert.Equal(0, stock.QuantityOf("pierre"));
        Assert.Equal(60, market.Stock.QuantityOf("pierre"));
        Assert.Equal(180, owners.Wealth);
    }

    [Fact]
    public void Les_impots_passent_chaque_jour_de_la_richesse_des_classes_au_tresor()
    {
        var treasury = new Treasury();
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 100) { Wealth = 10_000 };

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

        // 60 pierres achetées à 3 : 180 dépensés.
        Assert.Equal(820, treasury.Balance, precision: 6);
        Assert.Equal(40, market.Stock.QuantityOf("pierre"));
        Assert.Equal(0, stock.QuantityOf("pierre"));
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
        Assert.Equal(100, market.Stock.QuantityOf("pierre"));
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
        market.Stock.Add("pierre", marketPierre);
        market.Stock.Add("bois", marketBois);

        return (stock, treasury, market);
    }
}
