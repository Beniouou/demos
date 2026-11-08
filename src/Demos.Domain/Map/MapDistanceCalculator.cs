using System;
using System.Collections.Generic;
using System.Linq;

namespace Demos.Domain.Map;

/// <summary>
/// Calcule la charge de distance administrative pour l'extension politique (bureaucratie)
/// et pour les pertes de transport logistiques.
/// </summary>
public sealed class MapDistanceCalculator
{
    private readonly TopographicMap _map;
    private readonly IReadOnlyDictionary<string, CommuneSite> _communes;

    public MapDistanceCalculator(TopographicMap map, IEnumerable<CommuneSite> communes)
    {
        _map = map;
        _communes = communes.ToDictionary(c => c.CommuneId);
    }

    /// <summary>
    /// Calcule le coût de trajet réel (avec friction du terrain) entre deux communes.
    /// </summary>
    public double TravelCostBetween(string communeAId, string communeBId)
    {
        if (communeAId == communeBId)
            return 0;

        if (!_communes.TryGetValue(communeAId, out var siteA) ||
            !_communes.TryGetValue(communeBId, out var siteB))
        {
            return double.PositiveInfinity;
        }

        return _map.ComputeTravelCost(siteA.Coordinates, siteB.Coordinates);
    }

    /// <summary>
    /// Calcule le terme de distance administrative pour une entité politique (proposition).
    /// Pour une préfecture, c'est la somme des distances pondérées entre son chef-lieu et les communes administrées.
    /// Une distance de référence (ex. 50 unités d'effort) correspond à une charge de 1,0.
    /// </summary>
    /// <param name="seatCommuneId">Chef-lieu de l'entité (ex: préfecture).</param>
    /// <param name="administeredCommuneIds">Communes administrées par cette entité.</param>
    /// <param name="distanceNorm">Facteur de normalisation de distance (proposition : 50.0).</param>
    public double ComputeAdministrativeDistanceLoad(
        string? seatCommuneId,
        IEnumerable<string> administeredCommuneIds,
        double distanceNorm = 50.0)
    {
        if (seatCommuneId is null || distanceNorm <= 0)
            return 0;

        var totalTravelCost = 0.0;
        foreach (var communeId in administeredCommuneIds)
        {
            if (communeId == seatCommuneId)
                continue;

            var cost = TravelCostBetween(seatCommuneId, communeId);
            if (!double.IsPositiveInfinity(cost))
            {
                totalTravelCost += cost;
            }
            else
            {
                // Inaccessible par voie terrestre : pénalité maximale.
                totalTravelCost += distanceNorm * 2;
            }
        }

        return totalTravelCost / distanceNorm;
    }
}
