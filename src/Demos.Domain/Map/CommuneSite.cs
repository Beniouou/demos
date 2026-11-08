using System;

namespace Demos.Domain.Map;

/// <summary>
/// Emplacement géographique d'une commune sur la carte, avec son pouvoir d'influence émis.
/// </summary>
public sealed record CommuneSite(
    string CommuneId,
    string Name,
    MapCoordinates Coordinates,
    double Population = 0,
    bool HasTownHall = true,
    bool HasPrefectureSeat = false)
{
    /// <summary>
    /// Rayon d'action / potentiel d'influence brut émis par la commune (proposition).
    /// Dépend de la taille de sa population et de ses institutions administratives.
    /// </summary>
    public double InfluencePower
    {
        get
        {
            // Base pour un camp de base : 15 unités de distance/effort.
            var power = 15.0;

            // La population étend le rayonnement : racine carrée pour adoucir les très grandes métropoles.
            if (Population > 0)
                power += Math.Sqrt(Population) * 0.4;

            if (HasTownHall)
                power += 5.0;

            if (HasPrefectureSeat)
                power += 10.0;

            return power;
        }
    }
}
