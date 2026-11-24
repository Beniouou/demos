using Demos.Infrastructure;

namespace Demos.Tests.Infrastructure;

/// <summary>Plusieurs cartes (décidé le 24 novembre 2026) : un dossier par carte, une carte par défaut.</summary>
public class MapFilesTests
{
    private const string MapJson = """{ "name": "%NOM%", "width": 1000, "height": 640, "contours": [], "spotElevations": [], "river": { "name": "", "points": [] }, "forests": [], "waterBodies": [], "resources": [], "defaultCommune": { "id": "commune-1", "name": "Première commune", "position": { "x": 1, "y": 2 } } }""";

    private static string Map(string name) => MapJson.Replace("%NOM%", name);

    [Fact]
    public void La_carte_par_defaut_est_celle_du_fichier_sinon_la_carte_d_origine()
    {
        Assert.Equal("essai-1", MapFiles.DefaultName(_ => " essai-1\n"));
        Assert.Equal(MapFiles.OriginalMap, MapFiles.DefaultName(_ => ""));
        Assert.Equal(MapFiles.OriginalMap, MapFiles.DefaultName(file => throw new FileNotFoundException(file)));
    }

    [Theory]
    [InlineData("Terre des Exilés", "terre-des-exiles")]
    [InlineData("  Essai 1 (copie) ", "essai-1-copie")]
    [InlineData("Île d'Hiver", "ile-d-hiver")]
    [InlineData("???", "carte")]
    public void Le_dossier_d_une_carte_est_tire_de_son_nom(string name, string folder) =>
        Assert.Equal(folder, MapFiles.FolderNameOf(name));

    [Fact]
    public void Le_contenu_lit_la_carte_nommee_ou_la_carte_par_defaut()
    {
        var files = new Dictionary<string, string>
        {
            ["cartes/carte-par-defaut.txt"] = "origine",
            ["cartes/origine/carte.json"] = Map("Origine"),
            ["cartes/essai-1/carte.json"] = Map("Essai 1"),
        };

        Assert.Equal("Origine", new JsonContentSource(file => files[file]).LoadMap()!.Name);
        Assert.Equal("Essai 1", new JsonContentSource(file => files[file], mapName: "essai-1").LoadMap()!.Name);
        Assert.Equal("essai-1", new JsonContentSource(file => files[file], mapName: "essai-1").MapName);
    }

    [Fact]
    public void Une_carte_rangee_a_l_ancienne_se_lit_encore()
    {
        var files = new Dictionary<string, string> { ["carte.json"] = Map("Ancienne") };

        Assert.Equal("Ancienne", new JsonContentSource(file => files[file]).LoadMap()!.Name);
        Assert.Null(new JsonContentSource(_ => "").LoadMap());
    }

    [Fact]
    public void Chaque_carte_livree_a_sa_carte_et_son_relief()
    {
        var folder = Path.Combine(GameData.Folder, MapFiles.Folder);
        var maps = Directory.GetDirectories(folder).Select(Path.GetFileName).ToList();

        Assert.Contains(MapFiles.OriginalMap, maps);
        foreach (var map in maps)
        {
            var content = new JsonContentSource(file => File.ReadAllText(Path.Combine(GameData.Folder, file)),
                file => File.ReadAllBytes(Path.Combine(GameData.Folder, file)), map);
            Assert.True(content.LoadMap() is not null, $"La carte « {map} » doit se lire.");
            Assert.True(content.LoadRelief() is not null, $"La carte « {map} » doit avoir son relief (--relief {map}).");
        }
    }
}
