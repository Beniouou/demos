using System;
using System.Collections.Generic;
using Demos.Domain.Economy;

namespace Demos.Domain.Map;

/// <summary>
/// Calculs économiques et environnementaux basés sur les coordonnées cartographiques réelles des communes :
/// pertes de transport et dispersion atmosphérique de la pollution (décidé le 10 novembre 2026). Les chiffres sont des
/// réglages (<see cref="BalanceSettings"/>, equilibrage.json, feuille Constantes du tableur).
/// </summary>
public static class GeographicEconomics
{
    /// <summary>
    /// Part de perte de transport d'un produit selon la distance en lieues :
    /// TransportLoss × (0,5 + 0,5 × distance ÷ distance de référence), bornée entre la perte minimale et maximale.
    /// </summary>
    public static double TransportLoss(double distance)
    {
        var settings = Balance.Current;
        if (distance <= 0)
            return 0;
        if (settings.TransportReferenceDistance <= 0)
            return settings.TransportLoss;

        var loss = settings.TransportLoss * (0.5 + 0.5 * (distance / settings.TransportReferenceDistance));
        return Math.Clamp(loss, settings.TransportLossMin, settings.TransportLossMax);
    }

    /// <summary>
    /// Part de perte de transport entre deux communes : 0 dans la même commune, la perte de base si l'une n'a pas de
    /// position sur la carte, sinon selon leur distance.
    /// </summary>
    public static double TransportLoss(string originCommuneId, string destinationCommuneId, IReadOnlyDictionary<string, Point2D> positions)
    {
        if (originCommuneId == destinationCommuneId)
            return 0;

        if (!positions.TryGetValue(originCommuneId, out var p1) || !positions.TryGetValue(destinationCommuneId, out var p2))
            return Balance.Current.TransportLoss;

        return TransportLoss(p1.DistanceTo(p2));
    }

    /// <summary>
    /// Part de la pollution d'une commune émettrice qu'une autre reçoit : DistantPollutionShare ÷ (1 + (d ÷ distance de
    /// dispersion)²) ; la part de base si l'une n'a pas de position sur la carte.
    /// </summary>
    public static double DistantPollutionShare(string sourceCommuneId, string targetCommuneId, IReadOnlyDictionary<string, Point2D> positions)
    {
        var settings = Balance.Current;
        if (sourceCommuneId == targetCommuneId)
            return 0;

        if (!positions.TryGetValue(sourceCommuneId, out var p1) || !positions.TryGetValue(targetCommuneId, out var p2)
            || settings.PollutionDispersionDistance <= 0)
            return settings.DistantPollutionShare;

        var ratio = p1.DistanceTo(p2) / settings.PollutionDispersionDistance;
        return settings.DistantPollutionShare / (1.0 + ratio * ratio);
    }
}
