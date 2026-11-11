using System;
using System.Collections.Generic;

namespace Demos.Game.Map;

/// <summary>
/// Les filtres d'information de la carte, qu'on coche et décoche et qu'on peut superposer. Sans filtre, la carte montre
/// la vue de base, Monde : le terrain, l'eau, les forêts, les bâtiments et le nom des communes, avec le moins
/// d'écritures possible.
/// </summary>
[Flags]
public enum MapFilters
{
    None = 0,
    /// <summary>Géographie physique : relief (courbes de niveau et leurs cotes), points cotés, nom des cours d'eau.</summary>
    Physical = 1,
    /// <summary>Biomes : une couleur nette par type de biome.</summary>
    Biomes = 2,
    /// <summary>Ressources minières et sols : les gisements.</summary>
    Deposits = 4,
    /// <summary>Politique : frontières, préfectures, liaisons, capitale, habitants des communes.</summary>
    Politics = 8,
    /// <summary>Production et transport : liens des bâtiments, voies navigables, axes d'échange.</summary>
    ProductionTransport = 16,
}

/// <summary>Le nom et la description de chaque filtre, dans l'ordre du menu Carte.</summary>
public static class MapFilterCatalog
{
    public static readonly IReadOnlyList<(MapFilters Filter, string Name, string Description)> All =
    [
        (MapFilters.Physical, "Géographie physique", "Relief, courbes de niveau et leurs cotes, points cotés, nom des cours d'eau."),
        (MapFilters.Biomes, "Biomes", "Une couleur nette par type de biome : forêt, terres fertiles, désert, falaise, plage, glacier."),
        (MapFilters.Deposits, "Ressources minières et sols", "Les gisements de pierre, de charbon et de fer, et les terres fertiles."),
        (MapFilters.Politics, "Politique", "Frontières des préfectures, chefs-lieux, liaisons administratives, capitale et habitants."),
        (MapFilters.ProductionTransport, "Production et transport", "Bâtiments d'extraction reliés à leur commune, voies navigables et axes d'échange."),
    ];
}
