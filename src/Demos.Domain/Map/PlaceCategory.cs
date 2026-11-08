using System;
using System.Collections.Generic;
using System.Linq;

namespace Demos.Domain.Map;

/// <summary>
/// Définition d'une catégorie de lieu nommé (toponyme) sur la carte.
/// </summary>
public sealed record PlaceCategory(
    string Id,
    string Icon,
    string DisplayName,
    string DefaultName,
    bool DefaultShowAltitude);

public static class PlaceCategories
{
    public static readonly IReadOnlyList<PlaceCategory> All =
    [
        new("Peak", "▲", "Sommet / Pic", "Mont Aiguille", true),
        new("Hill", "◬", "Colline / Col", "Col de la Brume", true),
        new("Coast", "≈", "Cap / Baie / Plage", "Cap des Tempêtes", false),
        new("Forest", "♣", "Bois / Bosquet", "Bois des Chênes", false),
        new("Historic", "⚑", "Ruines / Site remarquable", "Ancien Fort", false),
        new("Locality", "◆", "Lieu-dit / Hameau", "Les Hautes-Terres", false),
        new("WaterSpot", "💧", "Source / Gué", "Source Claire", true)
    ];

    public static PlaceCategory Get(string? id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
