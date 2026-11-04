using Demos.Domain.Economy;
using Demos.Domain.Population;

namespace Demos.Domain.Politics;

/// <summary>
/// Coût en légitimité d'un changement de loi : voter une loi, changer ses curseurs ou l'abroger. Chaque brique coûte
/// ce qu'il faudrait de jours (30, proposition) pour la rembourser si tous les membres qu'elle touche l'acceptaient :
/// membres pondérés × 0,001 × jours × écart × poids, au moins 1 point. Le coût se paie dans les pôles du mouvement, à
/// parts égales. Verrouiller une brique la rend plus chère, et une loi d'un domaine régalien votée sous l'État aussi.
/// Abroger, c'est ramener chaque curseur à sa référence : le mouvement se paie dans les pôles opposés.
/// </summary>
public static class LawCost
{
    /// <param name="before">La loi en vigueur, ou null pour une nouvelle loi.</param>
    /// <param name="after">La nouvelle version, ou null pour abroger.</param>
    public static Dictionary<Orientation, double> Of(LawBook book, IEnumerable<SocialClass> classes, Law? before, Law? after)
    {
        var cost = new Dictionary<Orientation, double>();
        var law = after ?? before;
        if (law is null || !book.Structures.TryGetValue(law.StructureId, out var level))
            return cost;

        var settings = Balance.Current;
        var regalian = book.Catalogue.DomainOf(law.Domain)?.Regalian == true && !level.IsNational;
        var population = classes.ToList();

        var keys = (before?.Bricks ?? []).Concat(after?.Bricks ?? []).Select(s => (s.Brick, s.Subject)).Distinct();
        foreach (var (brickId, subject) in keys)
        {
            if (book.Catalogue.BrickOf(brickId) is not { } brick)
                continue;

            var old = before?.Bricks.FirstOrDefault(s => s.Brick == brickId && s.Subject == subject);
            var next = after?.Bricks.FirstOrDefault(s => s.Brick == brickId && s.Subject == subject);
            var reference = book.ReferenceOf(law, (next ?? old)!);
            var movement = new BrickMovement(brick, subject, old?.Value ?? reference, next?.Value ?? reference, reference);
            var lockChanged = (next?.Locked ?? false) != (old?.Locked ?? false);
            if (movement.Intensity <= 0 && !lockChanged)
                continue;

            // Seule la case « verrouillée » change : la brique se paie dans les pôles de sa valeur actuelle.
            var poles = movement.Intensity > 0 ? movement.Poles : brick.PolesOf(subject, (next?.Value ?? reference) >= reference);
            if (poles.Count == 0)
                continue;

            var members = population.Sum(c => c.Population * book.WeightedShare(c, subject, level, settings.UntargetedMemberWeight));
            var amount = Math.Max(settings.MinimumBrickCost,
                members * LawEffects.LegitimacyPerMemberPerDay * settings.LawPaybackDays * movement.Intensity * brick.Weight);
            if (next?.Locked == true)
                amount *= settings.LockedBrickCostFactor;
            if (regalian)
                amount *= settings.RegalianCostFactor;

            foreach (var pole in poles)
                cost[pole] = cost.GetValueOrDefault(pole) + amount / poles.Count;
        }

        return cost;
    }
}
