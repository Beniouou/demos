using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class DailyProduction
{
    /// <summary>
    /// Chaque structure productive produit sa quantité du jour : dans le stock de l'État si elle est publique,
    /// sinon vendue sur le marché au profit de sa classe propriétaire.
    /// Les postes et les matières consommées ne sont pas encore pris en compte.
    /// </summary>
    public static void Run(
        IEnumerable<Structure> structures,
        IReadOnlyDictionary<string, SocialClass> classes,
        Inventory stateStock,
        Market market)
    {
        foreach (var structure in structures)
        {
            if (structure.Production is not { } production)
                continue;

            if (structure.Ownership == Ownership.Public)
            {
                stateStock.Add(production.ProductId, production.QuantityPerDay);
                continue;
            }

            var revenue = market.Sell(production.ProductId, production.QuantityPerDay);
            if (structure.OwnerClassId is { } ownerId && classes.TryGetValue(ownerId, out var owner))
                owner.Wealth += revenue;
        }
    }
}
