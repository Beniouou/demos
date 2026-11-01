using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class Wages
{
    /// <summary>
    /// Chaque structure verse son salaire à ses travailleurs : le trésor paie les structures publiques,
    /// la classe propriétaire paie les autres. Si l'argent manque, les salaires sont versés en partie.
    /// </summary>
    public static void PayDaily(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> workers,
        IReadOnlyDictionary<string, SocialClass> classes,
        Treasury treasury)
    {
        foreach (var (structureId, count) in workers)
        {
            var structure = structures[structureId];
            var due = count * structure.DailyWage;
            if (due <= 0)
                continue;

            double paid;
            if (structure.Ownership == Ownership.Public)
            {
                paid = Math.Min(due, treasury.Balance);
                treasury.TrySpend(paid);
            }
            else if (structure.OwnerClassId is { } ownerId && classes.TryGetValue(ownerId, out var owner))
            {
                paid = Math.Min(due, Math.Max(0, owner.Wealth));
                owner.Wealth -= paid;
            }
            else
            {
                continue;
            }

            foreach (var socialClass in classes.Values)
                socialClass.Wealth += paid * socialClass.MembersWorkingIn(structureId) / count;
        }
    }
}
