using Demos.Domain.Economy;
using Demos.Infrastructure;

namespace Demos.Tests.Infrastructure;

public class JsonContentSourceTests
{
    [Fact]
    public void Lit_les_produits_depuis_le_json()
    {
        const string json = """[{ "id": "pain", "name": "Pain", "need": "Food" }]""";
        var source = new JsonContentSource(_ => json);

        var products = source.LoadProducts();

        Assert.Equal(new ProductDefinition("pain", "Pain", NeedCategory.Food), Assert.Single(products));
    }
}
