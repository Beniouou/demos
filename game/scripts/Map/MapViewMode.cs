namespace Demos.Game.Map;

/// <summary>
/// Les différentes vues thématiques superposables de la carte, reprises du menu Carte.
/// </summary>
public enum MapViewMode
{
    Geography,          // Relief, climat, cours d'eau et courbes de niveau
    Geopolitics,        // Frontières, souveraineté, communes et préfectures
    Production,         // Sites d'extraction et unités de production actives
    Transit,            // Axes de transport et voies navigables
    NaturalResources,   // Gisements et potentiel naturel des sols
}
