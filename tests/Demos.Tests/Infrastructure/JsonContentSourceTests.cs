using Demos.Domain.Economy;
using Demos.Infrastructure;

namespace Demos.Tests.Infrastructure;

public class JsonContentSourceTests
{
    [Fact]
    public void Lit_les_produits_depuis_le_json()
    {
        const string json = """
            [
              { "id": "pain", "name": "Pain", "need": "Food", "basePrice": 1 },
              { "id": "pierre", "name": "Pierre", "basePrice": 3 }
            ]
            """;
        var source = new JsonContentSource(_ => json);

        var products = source.LoadProducts();

        Assert.Equal(
            [new ProductDefinition("pain", "Pain", NeedCategory.Food, 1), new ProductDefinition("pierre", "Pierre", null, 3)],
            products);
    }

    [Fact]
    public void Un_besoin_nouveau_se_lit_comme_les_autres_y_compris_dans_les_reglages()
    {
        var files = new Dictionary<string, string>
        {
            ["besoins.json"] = """[ { "id": "Food", "name": "Se nourrir" }, { "id": "Culture", "name": "Se cultiver" } ]""",
            ["products.json"] = """[ { "id": "livre", "name": "Livre", "need": "Culture", "basePrice": 2 } ]""",
            ["equilibrage.json"] = """{ "needsByRank": { "Workers": { "Food": 1, "Culture": 0.1 } } }""",
        };
        var source = new JsonContentSource(file => files[file]);

        Assert.Equal(["Food", "Culture"], source.LoadNeeds().Select(n => n.Id));
        Assert.Equal(new NeedCategory("Culture"), source.LoadProducts().Single().Need);
        Assert.Equal(0.1, source.LoadBalance().NeedsByRank[Demos.Domain.Population.Rank.Workers][new NeedCategory("Culture")]);
    }
}
