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
}
