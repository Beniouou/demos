using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class TradeTests
{
    private static readonly ProductDefinition[] Products = [new("pain", "Pain", NeedCategory.Food, 1)];

    private static SocialClass Residents(string communeId, long population)
    {
        var residents = new SocialClass($"ouvriers-{communeId}", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), population);
        residents.SetResidence(communeId, 1);
        return residents;
    }

    [Fact]
    public void Les_surplus_partent_vers_la_commune_qui_manque_avec_une_perte_de_20_pourcent()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 80)];

        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], _ => 10_000);

        // La commune b manque de 80 pains : il en part 100 de a, 80 arrivent. Le vendeur reste le même.
        Assert.Equal(80, markets["b"].QuantityOf("pain"), precision: 6);
        Assert.Equal(900, markets["a"].QuantityOf("pain"), precision: 6);
        Assert.True(markets["b"].TryBuy("pain", 80));
        Assert.Equal(80, markets["b"].CollectEarnings()["ferme"], precision: 6);
    }

    [Fact]
    public void Une_commune_ne_recoit_que_dans_la_limite_de_sa_place_libre()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 300)];

        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], c => c == "b" ? 50 : 10_000);

        Assert.Equal(50, markets["b"].QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Sans_surplus_rien_ne_part()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 100, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 80)];

        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], _ => 10_000);

        Assert.Equal(0, markets["b"].QuantityOf("pain"));
    }

    [Fact]
    public void Ce_qui_depasse_le_stockage_est_perdu_en_fin_de_journee()
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures)
        {
            ["entrepot"] = new("entrepot", "Entrepôt", StructureKind.Building, "commune", StorageCapacity: 300),
        };
        var market = new Market(Products);
        market.Offer("pain", 400, "ferme");
        var stock = new Inventory();
        stock.Add("bois", 200);

        Storage.ApplyDaily(["commune"], structures, _ => market, _ => stock);

        // 600 produits pour 300 places : chacun garde la moitié.
        Assert.Equal(200, market.QuantityOf("pain"), precision: 6);
        Assert.Equal(100, stock.QuantityOf("bois"), precision: 6);
    }
}
