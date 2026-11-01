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
}
