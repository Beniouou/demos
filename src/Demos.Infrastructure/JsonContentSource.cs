using System.Text.Json;
using System.Text.Json.Serialization;
using Demos.Application;
using Demos.Application.Ports;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Infrastructure;

/// <summary>
/// Lit le contenu du jeu dans des fichiers JSON.
/// <paramref name="readText"/> reçoit un nom de fichier (« products.json ») et renvoie son texte :
/// Godot passe sa propre lecture de res://data/, les tests passent File.ReadAllText.
/// </summary>
public sealed class JsonContentSource(Func<string, string> readText) : IContentSource
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

    private List<T> Load<T>(string fileName) =>
        JsonSerializer.Deserialize<List<T>>(readText(fileName), Options) ?? [];
}
