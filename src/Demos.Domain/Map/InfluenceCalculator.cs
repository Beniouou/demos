using System;
using System.Collections.Generic;

namespace Demos.Domain.Map;

/// <summary>
/// Calcule la propagation de l'influence géographique des communes à travers la topographie.
/// Le relief (montagnes) freine rapidement l'influence, tandis que les cours d'eau et plaines la favorisent.
/// </summary>
public static class InfluenceCalculator
{
    private sealed record Candidate(string CommuneId, double InitialPower, double RemainingPower);

    /// <summary>
    /// Calcule le champ d'influence complet sur la carte pour un ensemble de communes.
    /// </summary>
    public static InfluenceField Compute(
        TopographicMap map,
        IEnumerable<CommuneSite> communes,
        IReadOnlyDictionary<string, string>? communeToPrefecture = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(communes);

        var bestPerCell = new Candidate?[map.Width, map.Height];

        foreach (var commune in communes)
        {
            if (!map.IsInBounds(commune.Coordinates))
                continue;

            PropagateFromCommune(map, commune, bestPerCell);
        }

        var result = new InfluenceCell[map.Width, map.Height];
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                var candidate = bestPerCell[x, y];
                var coord = new MapCoordinates(x, y);

                if (candidate is not null && candidate.RemainingPower > 0)
                {
                    var strength = candidate.InitialPower > 0
                        ? Math.Clamp(candidate.RemainingPower / candidate.InitialPower, 0.0, 1.0)
                        : 0.0;

                    var prefectureId = candidate.CommuneId is not null && communeToPrefecture is not null
                        ? communeToPrefecture.GetValueOrDefault(candidate.CommuneId)
                        : null;

                    result[x, y] = new InfluenceCell(coord, candidate.CommuneId, strength, prefectureId);
                }
                else
                {
                    result[x, y] = new InfluenceCell(coord, null, 0);
                }
            }
        }

        return new InfluenceField(map, result);
    }

    private static void PropagateFromCommune(
        TopographicMap map,
        CommuneSite commune,
        Candidate?[,] bestPerCell)
    {
        var power = commune.InfluencePower;
        var start = commune.Coordinates;

        var minCosts = new Dictionary<MapCoordinates, double> { [start] = 0 };
        var queue = new PriorityQueue<MapCoordinates, double>();
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out var current, out var currentCost))
        {
            if (currentCost > minCosts[current])
                continue;

            var remaining = power - currentCost;
            if (remaining <= 0)
                continue;

            var existing = bestPerCell[current.X, current.Y];
            if (existing is null || remaining > existing.RemainingPower)
            {
                bestPerCell[current.X, current.Y] = new Candidate(commune.CommuneId, power, remaining);
            }

            foreach (var neighbor in map.NeighborsOf(current, includeDiagonals: true))
            {
                if (double.IsPositiveInfinity(neighbor.MovementFriction))
                    continue;

                var isDiagonal = neighbor.Coordinates.X != current.X && neighbor.Coordinates.Y != current.Y;
                var stepDistance = isDiagonal ? 1.414 : 1.0;
                var stepCost = stepDistance * neighbor.MovementFriction;
                var newCost = currentCost + stepCost;

                if (newCost < power && newCost < minCosts.GetValueOrDefault(neighbor.Coordinates, double.PositiveInfinity))
                {
                    minCosts[neighbor.Coordinates] = newCost;
                    queue.Enqueue(neighbor.Coordinates, newCost);
                }
            }
        }
    }
}
