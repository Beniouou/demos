using System.Collections.Generic;

namespace Demos.Domain.Map;

/// <summary>
/// État d'influence d'une cellule de la carte.
/// </summary>
public sealed record InfluenceCell(
    MapCoordinates Coordinates,
    string? DominantCommuneId,
    double Strength,
    string? PrefectureId = null)
{
    /// <summary>
    /// Vrai si la cellule est sous l'influence réelle d'une commune (zone d'aménagement et de souveraineté).
    /// Faux si elle est encore en terre sauvage / inexplorée (Terra Incognita).
    /// </summary>
    public bool IsControlled => Strength > 0.001 && DominantCommuneId is not null;
}

/// <summary>
/// Champ global d'influence sur toute la carte.
/// </summary>
public sealed class InfluenceField
{
    private readonly InfluenceCell[,] _cells;

    public TopographicMap Map { get; }
    public int Width => Map.Width;
    public int Height => Map.Height;

    public InfluenceField(TopographicMap map, InfluenceCell[,] cells)
    {
        Map = map;
        _cells = cells;
    }

    public InfluenceCell this[int x, int y] =>
        Map.IsInBounds(x, y) ? _cells[x, y] : new InfluenceCell(new(x, y), null, 0);

    public InfluenceCell this[MapCoordinates c] => this[c.X, c.Y];

    public IEnumerable<InfluenceCell> ControlledCells()
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                if (_cells[x, y].IsControlled)
                    yield return _cells[x, y];
            }
        }
    }

    public IEnumerable<InfluenceCell> CellsOfCommune(string communeId)
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                if (_cells[x, y].DominantCommuneId == communeId && _cells[x, y].IsControlled)
                    yield return _cells[x, y];
            }
        }
    }
}
