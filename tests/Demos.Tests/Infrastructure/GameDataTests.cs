namespace Demos.Tests.Infrastructure;

public class GameDataTests
{
    [Fact]
    public void Le_catalogue_des_batiments_se_charge_et_ses_productions_existent()
    {
        var content = GameData.Content();
        var products = content.LoadProducts().Select(p => p.Id).ToHashSet();

        var types = content.LoadBuildingTypes();

        Assert.Contains(types, t => t.Id == "camp-de-base" && t.IsFunctional);
        Assert.Contains(types, t => t.Id == "centrale-nucleaire" && !t.IsFunctional);
        Assert.All(types.Where(t => t.Production is not null), t => Assert.Contains(t.Production!.Value.ProductId, products));
        Assert.Equal(types.Count, types.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void Chaque_produit_a_une_famille()
    {
        var products = GameData.Content().LoadProducts();

        Assert.All(products, p => Assert.NotNull(p.Family));
        Assert.Equal(Demos.Domain.Economy.ProductFamily.RawMaterial, products.Single(p => p.Id == "ble").Family);
        Assert.Equal(Demos.Domain.Economy.ProductFamily.Equipment, products.Single(p => p.Id == "charrette").Family);
    }
}
