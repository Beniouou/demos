namespace Demos.Domain.Map;

/// <summary>
/// Une cellule de la carte topographique : relief, biome, cours d'eau et gisement naturel.
/// </summary>
public sealed record TerrainCell(
    MapCoordinates Coordinates,
    TerrainType Terrain,
    double Elevation,
    bool HasRiver = false,
    string? NaturalResource = null)
{
    public int X => Coordinates.X;
    public int Y => Coordinates.Y;

    /// <summary>
    /// Coût de traversée de la cellule.
    /// Un cours d'eau navigable / berge réduit le coût, tandis que le relief l'augmente.
    /// </summary>
    public double MovementFriction =>
        Terrain switch
        {
            TerrainType.Water => double.PositiveInfinity,
            TerrainType.Plains => HasRiver ? 0.7 : 1.0,
            TerrainType.Forest => HasRiver ? 1.1 : 1.5,
            TerrainType.Hills => 2.2,
            TerrainType.Mountains => 5.5,
            _ => 1.0,
        };
}
