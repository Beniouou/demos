using ClosedXML.Excel;
using Demos.Infrastructure;

namespace Demos.Tests.Tools;

/// <summary>Le tableur d'équilibrage, généré et appliqué sur une copie des données du jeu.</summary>
[Collection(GlobalBalanceCollection.Name)]
public sealed class WorkbookTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "demos-tableur-" + Guid.NewGuid().ToString("N"));
    private string Workbook => Path.Combine(_folder, "equilibrage.xlsx");

    public WorkbookTests()
    {
        Directory.CreateDirectory(_folder);
        foreach (var file in Directory.GetFiles(GameData.Folder, "*.json"))
            File.Copy(file, Path.Combine(_folder, Path.GetFileName(file)));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private JsonContentSource Saved() => new(file => File.ReadAllText(Path.Combine(_folder, file)));

    /// <summary>Une recette de trois matières pour l'atelier : des meubles en bois, en fer et en cuir.</summary>
    private void AddThreeMaterialRecipe()
    {
        var creator = new ContentCreator(_folder);
        Assert.Empty(creator.AddProduct(new ProductDraft("meuble-luxe", "Meuble de luxe", null, 1, true, null)));
        Assert.Empty(creator.AddRecipe(new RecipeDraft("atelier", "meuble-luxe", 0.5, [("planche", 2), ("lingot-fer", 0.5), ("cuir", 1)])));
        Assert.Empty(creator.Save());
    }

    [Fact]
    public void Une_recette_de_trois_matieres_garde_l_ecart_a_zero_et_se_regle_dans_le_tableur()
    {
        AddThreeMaterialRecipe();
        WorkbookGenerator.Generate(Saved(), Workbook);

        using (var workbook = new XLWorkbook(Workbook))
        {
            var products = workbook.Worksheet("Produits");
            var gap = Layout.ProductPrice + 2;
            Assert.Equal("Écart", products.Cell(1, gap).GetString());
            for (var row = 2; !products.Cell(row, 1).Value.IsBlank; row++)
                Assert.True(Math.Abs(products.Cell(row, gap).GetDouble()) < 1e-6,
                    $"Écart de {products.Cell(row, 1).GetString()} : {products.Cell(row, gap).GetDouble()}");

            // La troisième matière se règle dans la feuille Recettes.
            var recipes = workbook.Worksheet("Recettes");
            var row3 = recipes.RowsUsed().First(r => r.Cell(7).GetString() == "meuble-luxe");
            Assert.Equal("cuir", row3.Cell(Layout.RecipeMaterial(2)).GetString());
            row3.Cell(Layout.RecipeQuantity(2)).Value = 2;
            row3.Cell(Layout.RecipeMaterial(3)).Value = "bois";
            row3.Cell(Layout.RecipeQuantity(3)).Value = 0.25;
            workbook.Save();
        }

        Assert.Equal(0, BalanceApplier.Apply(Workbook, _folder, Saved()));
        var recipe = Saved().LoadBuildingTypes().Single(t => t.Id == "atelier").AvailableRecipes.Single(r => r.ProductId == "meuble-luxe");
        Assert.Equal([("planche", 2.0), ("lingot-fer", 0.5), ("cuir", 2.0), ("bois", 0.25)],
            recipe.Materials.Select(m => (m.ProductId, m.Quantity)));
    }

    [Fact]
    public void Un_tableur_genere_puis_applique_sans_changement_ne_change_rien()
    {
        WorkbookGenerator.Generate(Saved(), Workbook);
        var before = Directory.GetFiles(_folder, "*.json").ToDictionary(f => f, File.ReadAllText);

        Assert.Equal(0, BalanceApplier.Apply(Workbook, _folder, Saved()));
        foreach (var (file, text) in before)
            Assert.Equal(text, File.ReadAllText(file));
    }

    [Fact]
    public void Une_recette_de_plus_de_cinq_matieres_est_refusee()
    {
        var creator = new ContentCreator(_folder);
        var materials = new[] { "planche", "lingot-fer", "cuir", "bois", "pierre", "tissus" }.Select(m => (m, 1.0)).ToList();
        Assert.NotEmpty(creator.AddRecipe(new RecipeDraft("atelier", "meubles", 1, materials)));
    }
}
