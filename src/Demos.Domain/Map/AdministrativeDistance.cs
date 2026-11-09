using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Politics;

namespace Demos.Domain.Map;

/// <summary>
/// Calcule la charge de distance administrative entre communes, préfectures et État à partir de leurs coordonnées réelles.
/// Une commune gère son propre territoire en direct (charge distance = 0).
/// Pour une préfecture ou l'État, la charge dépend de l'éloignement géographique des entités administrées par rapport au chef-lieu.
/// </summary>
public static class AdministrativeDistance
{
    /// <summary>Distance de référence en lieues pour normaliser la charge (500 lieues = 1 point de charge).</summary>
    public const double DefaultReferenceDistance = 500.0;

    /// <summary>
    /// Calcule la charge de distance administrative d'une entité politique.
    /// </summary>
    /// <param name="entityId">Identifiant de l'entité (État, préfecture ou commune).</param>
    /// <param name="structures">Ensemble des structures de la partie.</param>
    /// <param name="positions">Coordonnées cartographiques de chaque commune.</param>
    /// <param name="capitalCommuneId">Chef-lieu ou capitale nationale de référence pour l'État.</param>
    /// <param name="referenceDistance">Distance de référence pour 1 point de charge (défaut : 500 lieues).</param>
    public static double LoadOf(
        string entityId,
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, Point2D> positions,
        string capitalCommuneId = "commune-1",
        double referenceDistance = DefaultReferenceDistance)
    {
        if (!structures.TryGetValue(entityId, out var entity) || entity.Kind != StructureKind.PoliticalEntity)
            return 0;

        // Une commune administre directement son territoire local : pas de distance administrative.
        if (entity.IsCommune)
            return 0;

        // Chef-lieu ou centre de l'entité
        var seatCommuneId = entity.IsPrefecture ? entity.SeatId : capitalCommuneId;
        if (seatCommuneId is null || !positions.TryGetValue(seatCommuneId, out var seatPos))
            return 0;

        if (referenceDistance <= 0)
            return 0;

        var children = structures.Values
            .Where(s => s.ParentId == entity.Id && s.Kind == StructureKind.PoliticalEntity);

        var totalDistance = 0.0;
        foreach (var child in children)
        {
            var childCommuneId = child.IsCommune ? child.Id : child.SeatId;
            if (childCommuneId is not null && positions.TryGetValue(childCommuneId, out var childPos))
            {
                totalDistance += seatPos.DistanceTo(childPos);
            }
        }

        return totalDistance / referenceDistance;
    }
}
