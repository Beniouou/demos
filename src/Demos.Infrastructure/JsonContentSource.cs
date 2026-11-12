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
/// les images (la carte d'altitude) ; sans lui, pas de relief.
/// </summary>
public sealed class JsonContentSource(Func<string, string> readText, Func<string, byte[]>? readBytes = null) : IContentSource
{
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

    /// <summary>La carte géographique vectorielle (carte.json) ; nulle si absente ou invalide.</summary>
    public VectorMapDefinition? LoadMap()
    {
        string text;
        try
        {
            text = readText("carte.json");
        }
        catch (FileNotFoundException)
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
            using var info = JsonDocument.Parse(readText("relief.json"));
            var root = info.RootElement;
            var image = PngReader.Read(readBytes(root.GetProperty("altitude").GetString()!));
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

    private List<T> Load<T>(string fileName) =>
        JsonSerializer.Deserialize<List<T>>(readText(fileName), Options) ?? [];
}
