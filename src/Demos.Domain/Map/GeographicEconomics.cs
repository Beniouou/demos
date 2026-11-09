using System;
using System.Collections.Generic;

namespace Demos.Domain.Map;

/// <summary>
/// Calculs économiques et environnementaux basés sur les coordonnées cartographiques réelles des communes :
/// pertes de transport et dispersion atmosphérique de la pollution.
/// </summary>
public static class GeographicEconomics
{
    /// <summary>Distance de référence logistique pour la perte de transport (500 lieues = perte de référence).</summary>
    public const double DefaultTransportReferenceDistance = 500.0;

    /// <summary>Plancher de perte de transport inter-communes (5 %).</summary>
    public const double MinTransportLoss = 0.05;

    /// <summary>Plafond maximal de perte de transport inter-communes (60 %).</summary>
    public const double MaxTransportLoss = 0.60;

    /// <summary>Distance caractéristique de dispersion atmosphérique de la pollution (250 lieues = pollution divisée par 2).</summary>
    public const double DefaultPollutionDispersionDistance = 250.0;

    /// <summary>
    /// Calcule la part de perte de transport d'un produit selon la distance physique en lieues.
    /// Formule : BaseLoss * (0.5 + 0.5 * distance / distanceRef), bornée entre MinTransportLoss et MaxTransportLoss.
    /// </summary>
    public static double TransportLoss(
        double distance,
        double baseLoss = 0.20,
        double referenceDistance = DefaultTransportReferenceDistance)
    {
        if (distance <= 0)
            return 0;

        if (referenceDistance <= 0)
            return baseLoss;

        var loss = baseLoss * (0.5 + 0.5 * (distance / referenceDistance));
        return Math.Clamp(loss, MinTransportLoss, MaxTransportLoss);
    }

    /// <summary>
    /// Calcule la part de perte de transport d'un produit entre deux communes distinctes.
    /// Formule : BaseLoss * (0.5 + 0.5 * distance / distanceRef), bornée entre MinTransportLoss et MaxTransportLoss.
    /// </summary>
    public static double TransportLoss(
        string originCommuneId,
        string destinationCommuneId,
        IReadOnlyDictionary<string, Point2D> positions,
        double baseLoss = 0.20,
        double referenceDistance = DefaultTransportReferenceDistance)
    {
        if (originCommuneId == destinationCommuneId)
            return 0;

        if (!positions.TryGetValue(originCommuneId, out var p1) || !positions.TryGetValue(destinationCommuneId, out var p2))
            return baseLoss;

        return TransportLoss(p1.DistanceTo(p2), baseLoss, referenceDistance);
    }

    /// <summary>
    /// Calcule la part de pollution reçue d'une commune émettrice selon la loi de dispersion atmosphérique en 1 / (1 + (d/d_disp)^2).
    /// </summary>
    public static double DistantPollutionShare(
        string sourceCommuneId,
        string targetCommuneId,
        IReadOnlyDictionary<string, Point2D> positions,
        double baseShare = 0.10,
        double dispersionDistance = DefaultPollutionDispersionDistance)
    {
        if (sourceCommuneId == targetCommuneId)
            return 0;

        if (!positions.TryGetValue(sourceCommuneId, out var p1) || !positions.TryGetValue(targetCommuneId, out var p2))
            return baseShare;

        if (dispersionDistance <= 0)
            return baseShare;

        var dist = p1.DistanceTo(p2);
        var ratio = dist / dispersionDistance;
        return baseShare / (1.0 + ratio * ratio);
    }
}
