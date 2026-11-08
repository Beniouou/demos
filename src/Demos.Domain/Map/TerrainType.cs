namespace Demos.Domain.Map;

/// <summary>
/// Type de terrain géographique, avec son coût de friction naturel pour le déplacement et l'extension.
/// </summary>
public enum TerrainType
{
    Plains,     // Plaine : terrain dégagé, friction faible (1.0)
    Forest,     // Forêt : bois, friction modérée (1.5)
    Hills,      // Collines : pierre, friction moyenne (2.2)
    Mountains,  // Montagnes : roche, forte barrière naturelle (5.5)
    Water,      // Étendue d'eau infranchissable à pied (mer, lac profond)
}
