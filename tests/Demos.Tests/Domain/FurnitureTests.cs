using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class FurnitureTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("pain", "Pain", NeedCategory.Food, 1),
        new("vetements", "Vêtements", NeedCategory.Clothing, 8),
        new("meubles", "Meubles", NeedCategory.Furniture, 20),
        new("alcool", "Alcool", NeedCategory.Leisure, 2),
    ];

    private static SocialClass Resident(Rank rank, long population)
    {
        var socialClass = new SocialClass($"classe-{rank}", "classe", rank, new Ideology(Pole.A, Pole.A, Pole.A), population) { Wealth = 1_000_000 };
        socialClass.SetResidence("commune", 1);
        socialClass.HousingCoverage = 1;
        socialClass.HousingPlaceShortage = 0;
        return socialClass;
    }

    private static Market MarketWith(double meubles)
    {
        var market = new Market(Products);
        market.Offer("pain", 10_000, "ferme");
        market.Offer("vetements", 1_000, "atelier");
        market.Offer("meubles", meubles, "menuiserie");
        market.Offer("alcool", 1_000, "distillerie");
        return market;
    }

    [Fact]
    public void Les_professions_intermediaires_et_les_cadres_achetent_des_meubles_mais_pas_les_ouvriers()
    {
        var market = MarketWith(100);
        var workers = Resident(Rank.Workers, 1_000);
        var intermediates = Resident(Rank.Intermediate, 500);
        var executives = Resident(Rank.Executives, 100);

        Consumption.RunDaily([workers, intermediates, executives], Products, market);

        // 500 × 0,02 + 100 × 0,1 = 20 meubles achetés, à 20 chacun.
        Assert.Equal(80, market.QuantityOf("meubles"), precision: 6);
        Assert.Equal(400, market.CollectEarnings()["menuiserie"], precision: 6);
        Assert.Equal(1, workers.NeedsSatisfaction, precision: 6);
        Assert.Equal(1, intermediates.NeedsSatisfaction, precision: 6);
        Assert.Equal(1, executives.NeedsSatisfaction, precision: 6);
    }

    [Fact]
    public void Sans_meubles_les_rangs_superieurs_ne_couvrent_que_quatre_besoins_sur_cinq()
    {
        var market = MarketWith(0);
        var workers = Resident(Rank.Workers, 1_000);
        var executives = Resident(Rank.Executives, 100);

        Consumption.RunDaily([workers, executives], Products, market);

        Assert.Equal(1, workers.NeedsSatisfaction, precision: 6);
        // Se nourrir, s'habiller, se loger, se distraire, se meubler : il manque les meubles.
        Assert.Equal(0.8, executives.NeedsSatisfaction, precision: 6);
        Assert.Equal(0.2, executives.SeasonSupplyShortage, precision: 6);
    }

    [Fact]
    public void Les_echanges_envoient_des_meubles_la_ou_vivent_les_cadres()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("meubles", 100, "menuiserie");
        var workers = Resident(Rank.Workers, 1_000);
        var executives = new SocialClass("cadres", "Cadres", Rank.Executives, new Ideology(Pole.A, Pole.A, Pole.A), 100);
        executives.SetResidence("b", 1);

        Trade.RunDaily(["a", "b"], [workers, executives], Products, c => markets[c], (_, _) => 10_000);

        // Les ouvriers de a n'ont pas besoin de meubles ; les 100 cadres de b en veulent 10.
        Assert.Equal(10, markets["b"].QuantityOf("meubles"), precision: 6);
    }
}
