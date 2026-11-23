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

    [Fact]
    public void Chaque_brique_de_loi_ne_touche_qu_un_seul_axe()
    {
        // Révision des axes (23 novembre 2026) : la redistribution relève de la Hiérarchie, la Propriété de qui possède.
        var bricks = GameData.Content().LoadLawCatalogue().Bricks;

        Assert.NotEmpty(bricks);
        Assert.All(bricks, brick => Assert.Single(
            brick.Poles.SelectMany(p => p.Raise.Concat(p.Lower)).Select(o => o.Axis).Distinct()));
    }

    [Fact]
    public void Chaque_combinaison_de_poles_a_une_promesse_nommee()
    {
        var promises = GameData.Content().LoadPromises();
        var combinations = Demos.Domain.Politics.PromiseDefinition.AllCombinations()
            .Select(Demos.Domain.Politics.PromiseDefinition.IdOf).ToList();

        Assert.Equal(26, combinations.Count);
        Assert.Equal(combinations.OrderBy(c => c), promises.Select(p => p.Id).OrderBy(i => i));
        Assert.All(promises, p => Assert.Equal(p.Id, Demos.Domain.Politics.PromiseDefinition.IdOf(p.Orientations)));
        Assert.All(promises, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
        Assert.Equal(promises.Count, promises.Select(p => p.Name).Distinct().Count());
        Assert.Equal("La Commune Libre", promises.Single(p => p.Id == "eg-ec-co").Name);
    }
}
