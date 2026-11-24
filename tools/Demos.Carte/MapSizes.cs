using System;

namespace Demos.Carte;

/// <summary>
/// Les tailles d'une carte neuve (décidé le 24 novembre 2026, propositions validées) et la finesse des grilles de
/// l'éditeur selon la taille. Les distances sont en unités de carte (100 m).
/// </summary>
public static class MapSizes
{
    public static readonly (string Label, double Width, double Height)[] All =
    [
        ("Département (100 × 60 km)", 1000, 600),
        ("Petit pays (240 × 150 km)", 2400, 1500),
        ("Grand pays (800 × 500 km)", 8000, 5000),
        ("Petit continent (1 600 × 1 000 km)", 16000, 10000),
    ];

    /// <summary>La taille proposée d'abord : le petit pays.</summary>
    public const int DefaultIndex = 1;

    /// <summary>
    /// La grille du relief : des cases de 625 m au plus fin ; jusqu'à la carte d'origine (360 × 225 km), 360 × 230 cases
    /// au plus (comme avant) ; au-delà, des cases d'1 km au moins et 800 × 500 cases au plus (2 km pour le petit continent),
    /// pour que le pinceau et le tracé des fleuves restent fluides.
    /// </summary>
    public static (int Columns, int Rows) ReliefGrid(double width, double height) =>
        width <= 3600 && height <= 2250
            ? Grid(width, height, Math.Max(6.25, Math.Max(width / 360, height / 230)))
            : Grid(width, height, Math.Max(10, Math.Max(width / 800, height / 500)));

    /// <summary>La grille des biomes et des gisements : des cases d'1 km au plus fin, 800 × 500 cases au plus.</summary>
    public static (int Columns, int Rows) ZoneGrid(double width, double height) =>
        Grid(width, height, Math.Max(10, Math.Max(width / 800, height / 500)));

    private static (int Columns, int Rows) Grid(double width, double height, double cell) =>
        (Math.Max(16, (int)Math.Round(width / cell)), Math.Max(16, (int)Math.Round(height / cell)));
}
