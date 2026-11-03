using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Infrastructure;

namespace Demos.Tests.Tools;

/// <summary>L'éditeur de contenu, sur une copie des données du jeu.</summary>
public sealed class ContentCreatorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "demos-editeur-" + Guid.NewGuid().ToString("N"));

    public ContentCreatorTests()
    {
        Directory.CreateDirectory(_folder);
        foreach (var file in Directory.GetFiles(GameData.Folder, "*.json"))
            File.Copy(file, Path.Combine(_folder, Path.GetFileName(file)));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private JsonContentSource Saved() => new(file => File.ReadAllText(Path.Combine(_folder, file)));

    private static readonly Dictionary<Rank, double> Wages = new() { [Rank.Workers] = 2, [Rank.Intermediate] = 4, [Rank.Executives] = 8 };
    private static readonly Dictionary<Rank, double> Ratios = new() { [Rank.Intermediate] = 0.1, [Rank.Executives] = 0.01 };

    private static BuildingDraft Printing(RecipeDraft? recipe = null, StartingDraft? start = null) => new(
        "imprimerie", "Imprimerie", "Production", 1, "Imprime des livres", 1, Rank.Workers, 50, Ratios, Wages,
        0, 0, 0, 0, 0, StorageKind.General, null, 0.05, false, recipe, start);

    [Fact]
    public void Un_besoin_un_produit_et_un_batiment_crees_arrivent_dans_la_partie()
    {
        var creator = new ContentCreator(_folder);
        Assert.Empty(creator.AddNeed(new NeedDraft("Culture", "Se cultiver", null,
            new Dictionary<Rank, double> { [Rank.Workers] = 0.01, [Rank.Intermediate] = 0.05, [Rank.Executives] = 0.1 })));
        Assert.Empty(creator.AddProduct(new ProductDraft("livre", "Livre", "Culture", 5, true, null)));
        Assert.Empty(creator.AddBuilding(Printing(new RecipeDraft("", "livre", 1, [("planche", 0.2)]), new StartingDraft(Ownership.Public, null, 20, null))));
        Assert.Empty(creator.Save());

        var simulation = PrototypeScenario.Create(Saved());
        var printing = simulation.World.Structures["imprimerie"];
        Assert.Equal("livre", printing.Production?.ProductId);
        Assert.Equal(20, printing.Jobs);
        Assert.Equal(0.1, Saved().LoadBalance().NeedsByRank[Rank.Executives][new NeedCategory("Culture")]);
        simulation.AdvanceDay();
    }

    [Fact]
    public void Une_recette_ajoutee_a_un_batiment_a_production_unique_le_rend_polyvalent()
    {
        var creator = new ContentCreator(_folder);
        Assert.Empty(creator.AddRecipe(new RecipeDraft("camp-forestier", "charbon", 0.05, [("bois", 2)])));
        Assert.Empty(creator.Save());

        var type = Saved().LoadBuildingTypes().Single(t => t.Id == "camp-forestier");
        Assert.Equal(["bois", "charbon"], type.AvailableRecipes.Select(r => r.ProductId));
    }

    [Fact]
    public void Les_erreurs_sont_refusees_et_rien_n_est_ecrit()
    {
        var before = File.ReadAllText(Path.Combine(_folder, "buildings.json"));
        var creator = new ContentCreator(_folder);

        Assert.Contains(creator.AddProduct(new ProductDraft("pain", "Pain", null, 1, true, null)), e => e.Contains("existe déjà"));
        Assert.Contains(creator.AddProduct(new ProductDraft("brique", "Brique", "Housing", 1, true, null)), e => e.Contains("Besoin inconnu"));
        Assert.Contains(creator.AddRecipe(new RecipeDraft("atelier", "farine", 3, [("ble", 2)])), e => e.Contains("a déjà cette recette"));
        Assert.Contains(creator.AddRecipe(new RecipeDraft("logements", "pain", 1, [])), e => e.Contains("pas d'emplois"));
        Assert.Contains(creator.AddRecipe(new RecipeDraft("atelier", "pain", 1, [("pain", 1)])), e => e.Contains("son propre produit"));
        Assert.Contains(creator.AddBuilding(Printing(start: new StartingDraft(Ownership.Private, null, null, null))), e => e.Contains("classe propriétaire"));

        Assert.Empty(creator.Changes);
        Assert.Empty(creator.Save());
        Assert.Equal(before, File.ReadAllText(Path.Combine(_folder, "buildings.json")));
    }

    [Fact]
    public void Le_prix_d_un_nouveau_produit_est_de_secours_puis_calcule_des_qu_une_recette_le_fabrique()
    {
        var creator = new ContentCreator(_folder);
        Assert.Empty(creator.AddProduct(new ProductDraft("livre", "Livre", null, 1, true, null)));
        Assert.Contains(creator.PriceChanges(), c => c.StartsWith("Prix de Livre : 1 (de secours"));
        Assert.Empty(creator.Save());

        // Une recette plus tard : le prix devient celui des coûts de fabrication.
        var next = new ContentCreator(_folder);
        Assert.Empty(next.AddRecipe(new RecipeDraft("atelier", "livre", 1, [("planche", 0.2)])));
        var change = Assert.Single(next.PriceChanges(), c => c.StartsWith("Prix de Livre"));
        Assert.Contains("1 → ", change);
        Assert.EndsWith("(calculé)", change);
        Assert.True(ContentCreator.PricesOf(new JsonContentSource(file => File.ReadAllText(Path.Combine(_folder, file))))["livre"].Price == 1);
    }

    [Fact]
    public void L_identifiant_vient_du_nom_sans_accents()
    {
        Assert.Equal("imprimerie-d-etat", ContentCreator.IdFrom("Imprimerie d'État"));
        Assert.Equal("chateau-d-eau", ContentCreator.IdFrom("  Château d'eau "));
    }
}
