using Demos.Domain.Politics;

namespace Demos.Domain.Economy;

public static class DailyProduction
{
    /// <summary>
    /// Chaque structure produit selon son nombre de travailleurs : dans le stock de l'État si elle est publique,
    /// sinon mise en vente sur le marché pour sa classe propriétaire. Une ruine, sans travailleurs, ne produit rien.
    /// Les matières consommées ne sont pas encore prises en compte.
    /// </summary>
    public static void Run(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> workers,
        Inventory stateStock,
        Market market)
    {
        foreach (var (structureId, count) in workers)
        {
            var structure = structures[structureId];
            if (structure.Production is not { } production)
                continue;

            var quantity = production.QuantityPerWorker * count;
            if (structure.Ownership == Ownership.Public)
                stateStock.Add(production.ProductId, quantity);
            else
                market.Offer(production.ProductId, quantity, structure.OwnerClassId ?? "");
        }
    }
}
