using System;
using System.Collections.Generic;

namespace Demos.Domain.Map;

/// <summary>
/// Carte géographique continue découpée en mailles topographiques.
/// Fournit l'accès aux cellules, aux voisins et au calcul de distance réelle avec relief.
/// </summary>
public sealed class TopographicMap
{
    private readonly TerrainCell[,] _cells;

    public int Width { get; }
    public int Height { get; }

    public TopographicMap(int width, int height, Func<int, int, TerrainCell> cellFactory)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(cellFactory);

        Width = width;
        Height = height;
        _cells = new TerrainCell[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                _cells[x, y] = cellFactory(x, y);
            }
        }
    }

    public TopographicMap(TerrainCell[,] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Width = cells.GetLength(0);
        Height = cells.GetLength(1);
        _cells = (TerrainCell[,])cells.Clone();
    }

    public bool IsInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
    public bool IsInBounds(MapCoordinates c) => IsInBounds(c.X, c.Y);

    public TerrainCell this[int x, int y] =>
        IsInBounds(x, y) ? _cells[x, y] : throw new IndexOutOfRangeException($"Coordonnées ({x},{y}) hors limites.");

    public TerrainCell this[MapCoordinates c] => this[c.X, c.Y];

    public IEnumerable<TerrainCell> NeighborsOf(MapCoordinates c, bool includeDiagonals = false)
    {
        var candidates = includeDiagonals ? c.AllNeighbors() : c.OrthogonalNeighbors();
        foreach (var coord in candidates)
        {
            if (IsInBounds(coord))
                yield return _cells[coord.X, coord.Y];
        }
    }

    /// <summary>
    /// Calcule le coût effectif minimum de déplacement entre deux coordonnées en tenant compte du relief.
    /// Renvoie l'infini si la destination est inaccessible.
    /// </summary>
    public double ComputeTravelCost(MapCoordinates start, MapCoordinates destination)
    {
        if (!IsInBounds(start) || !IsInBounds(destination))
            return double.PositiveInfinity;
        if (start == destination)
            return 0;

        var distances = new Dictionary<MapCoordinates, double> { [start] = 0 };
        var queue = new PriorityQueue<MapCoordinates, double>();
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out var current, out var currentCost))
        {
            if (current == destination)
                return currentCost;

            if (currentCost > distances[current])
                continue;

            foreach (var neighbor in NeighborsOf(current, includeDiagonals: true))
            {
                if (double.IsPositiveInfinity(neighbor.MovementFriction))
                    continue;

                var isDiagonal = neighbor.Coordinates.X != current.X && neighbor.Coordinates.Y != current.Y;
                var stepDistance = isDiagonal ? Math.Sqrt(2) : 1.0;
                var stepCost = stepDistance * neighbor.MovementFriction;
                var newCost = currentCost + stepCost;

                if (newCost < distances.GetValueOrDefault(neighbor.Coordinates, double.PositiveInfinity))
                {
                    distances[neighbor.Coordinates] = newCost;
                    queue.Enqueue(neighbor.Coordinates, newCost);
                }
            }
        }

        return distances.GetValueOrDefault(destination, double.PositiveInfinity);
    }
}
