using System.Text.Json;
using System.Text.Json.Serialization;
using Demos.Application;
using Demos.Application.Ports;
using Demos.Domain.Economy;
using Demos.Domain.Map;
using Demos.Domain.Politics;

namespace Demos.Infrastructure;

/// <summary>
/// Lit le contenu du jeu dans des fichiers JSON.
/// <paramref name="readText"/> reçoit un nom de fichier (« products.json ») et renvoie son texte :
/// Godot passe sa propre lecture de res://data/, les tests passent File.ReadAllText. <paramref name="readBytes"/> lit
/// les images (la carte d'altitude) ; sans lui, pas de relief. <paramref name="mapName"/> choisit la carte (un dossier de
/// <c>cartes/</c>, voir <see cref="MapFiles"/>) ; sans lui, la carte par défaut. Une carte rangée à l'ancienne
/// (<c>carte.json</c> et le relief à la racine des données) est lue si son dossier manque.
/// </summary>
public sealed class JsonContentSource(Func<string, string> readText, Func<string, byte[]>? readBytes = null, string? mapName = null)
    : IContentSource
{
    private string? _mapName = mapName;

    /// <summary>Le nom de la carte lue (son dossier dans <c>cartes/</c>).</summary>
    public string MapName => _mapName ??= MapFiles.DefaultName(SafeRead);

    private string SafeRead(string file)
    {
        try
        {
            return readText(file);
        }
        catch (KeyNotFoundException)
        {
            throw new FileNotFoundException(file);
        }
    }

    /// <summary>
    /// Le texte d'un fichier de la carte : dans son dossier, sinon (ancienne carte) à la racine des données ; le
    /// préfixe de chemin des autres fichiers de cette carte est rendu dans <paramref name="prefix"/>.
    /// </summary>
    private string ReadMapFile(string file, out string prefix)
    {
        var inFolder = MapFiles.PathOf(MapName, file);
        try
        {
            var text = SafeRead(inFolder);
            if (!string.IsNullOrWhiteSpace(text))
            {
                prefix = inFolder[..^file.Length];
                return text;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
        }

        prefix = "";
        return SafeRead(file);
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new NeedCategoryConverter(), new OrientationConverter() },
    };

    public IReadOnlyList<ProductDefinition> LoadProducts() => Load<ProductDefinition>("products.json");

    public IReadOnlyList<BuildingType> LoadBuildingTypes() => Load<BuildingType>("buildings.json");

    public IReadOnlyList<NeedDefinition> LoadNeeds() => Load<NeedDefinition>("besoins.json");

    /// <summary>Réglages d'équilibrage (equilibrage.json) ; ceux par défaut si le fichier manque ou est vide.</summary>
    public BalanceSettings LoadBalance()
    {
        string text;
        try
        {
            text = readText("equilibrage.json");
        }
        catch (FileNotFoundException)
        {
            return new BalanceSettings();
        }

        return string.IsNullOrWhiteSpace(text)
            ? new BalanceSettings()
            : JsonSerializer.Deserialize<BalanceSettings>(text, Options) ?? new BalanceSettings();
    }

    /// <summary>Catalogue des lois (lois.json) ; vide si le fichier manque.</summary>
    public LawCatalogue LoadLawCatalogue()
    {
        string text;
        try
        {
            text = readText("lois.json");
        }
        catch (FileNotFoundException)
        {
            return LawCatalogue.Empty;
        }

        return string.IsNullOrWhiteSpace(text)
            ? LawCatalogue.Empty
            : JsonSerializer.Deserialize<LawCatalogue>(text, Options) ?? LawCatalogue.Empty;
    }

    public ScenarioDefinition LoadScenario() =>
        JsonSerializer.Deserialize<ScenarioDefinition>(readText("partie-depart.json"), Options)
        ?? throw new InvalidDataException("partie-depart.json est vide.");

    /// <summary>La partie de départ d'exil (partie-exil.json) ; nulle si le fichier manque.</summary>
    public ScenarioDefinition? LoadExileScenario()
    {
        try
        {
            return JsonSerializer.Deserialize<ScenarioDefinition>(readText("partie-exil.json"), Options);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>La carte géographique vectorielle (carte.json) ; nulle si absente ou invalide.</summary>
    public VectorMapDefinition? LoadMap()
    {
        string text;
        try
        {
            text = ReadMapFile(MapFiles.MapFile, out _);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(text)
            ? null
            : JsonSerializer.Deserialize<VectorMapDefinition>(text, Options);
    }

    /// <summary>
    /// La carte d'altitude calculée par l'outil de carte (relief.json et relief-altitude.png) ; nulle si elle manque ou
    /// sans lecture des images.
    /// </summary>
    public ReliefGrid? LoadRelief()
    {
        if (readBytes is null)
            return null;
        try
        {
            using var info = JsonDocument.Parse(ReadMapFile(MapFiles.ReliefFile, out var prefix));
            var root = info.RootElement;
            var image = PngReader.Read(readBytes(prefix + root.GetProperty("altitude").GetString()!));
            var altitudes = new float[image.Width * image.Height];
            for (var y = 0; y < image.Height; y++)
                for (var x = 0; x < image.Width; x++)
                {
                    var (high, low, _) = image[x, y];
                    altitudes[y * image.Width + x] = (float)ReliefEncoding.Decode(high, low);
                }
            return new ReliefGrid(image.Width, image.Height, root.GetProperty("metresParPixel").GetDouble() / Demos.Domain.Map.MapScale.MetersPerUnit, altitudes);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or KeyNotFoundException or InvalidDataException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Les Promesses des exilés (promesses.json) ; vide si le fichier manque.</summary>
    public IReadOnlyList<PromiseDefinition> LoadPromises()
    {
        try
        {
            return Load<PromiseDefinition>("promesses.json");
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }

    /// <summary>Le catalogue des engagements de La Promesse (engagements.json) ; vide si le fichier manque.</summary>
    public IReadOnlyList<EngagementDefinition> LoadEngagements()
    {
        try
        {
            return Load<EngagementDefinition>("engagements.json");
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }

    private List<T> Load<T>(string fileName) =>
        JsonSerializer.Deserialize<List<T>>(readText(fileName), Options) ?? [];
}
