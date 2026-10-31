using System.Text.Json;
using System.Text.Json.Serialization;
using Demos.Application.Ports;
using Demos.Domain.Economy;

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
        Converters = { new JsonStringEnumConverter() },
    };

    public IReadOnlyList<ProductDefinition> LoadProducts() =>
        JsonSerializer.Deserialize<List<ProductDefinition>>(readText("products.json"), Options) ?? [];
}
