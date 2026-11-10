using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Domain.Map;

/// <summary>
/// Calcule la charge de distance administrative entre communes, préfectures et État à partir de leurs coordonnées réelles.
/// Une commune gère son propre territoire en direct (charge distance = 0).
/// Pour une préfecture ou l'État, la charge est la somme des distances entre son chef-lieu (<see cref="Structure.SeatId"/> :
/// la capitale pour l'État) et les entités qu'elle contient, divisée par le réglage
/// <see cref="BalanceSettings.ExtensionDistancePerLoad"/> (décidé le 10 novembre 2026 : 2 000 lieues par point).
/// </summary>
public static class AdministrativeDistance
{
    /// <summary>Calcule la charge de distance administrative d'une entité politique.</summary>
    /// <param name="entityId">Identifiant de l'entité (État, préfecture ou commune).</param>
    /// <param name="structures">Ensemble des structures de la partie.</param>
    /// <param name="positions">Coordonnées cartographiques de chaque commune.</param>
    public static double LoadOf(
        string entityId,
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, Point2D> positions)
    {
        if (!structures.TryGetValue(entityId, out var entity) || entity.Kind != StructureKind.PoliticalEntity)
            return 0;

        // Une commune administre directement son territoire local : pas de distance administrative.
        if (entity.IsCommune)
            return 0;

        // Le chef-lieu de l'entité : celui d'une préfecture, ou la capitale pour l'État.
        var perLoad = Balance.Current.ExtensionDistancePerLoad;
        if (entity.SeatId is not { } seatCommuneId || !positions.TryGetValue(seatCommuneId, out var seatPos) || perLoad <= 0)
            return 0;

        var children = structures.Values
            .Where(s => s.ParentId == entity.Id && s.Kind == StructureKind.PoliticalEntity);

        var totalDistance = 0.0;
        foreach (var child in children)
        {
            var childCommuneId = child.IsCommune ? child.Id : child.SeatId;
            if (childCommuneId is not null && positions.TryGetValue(childCommuneId, out var childPos))
                totalDistance += seatPos.DistanceTo(childPos);
        }

        return totalDistance / perLoad;
    }
}
