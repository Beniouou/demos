using Demos.Domain.Politics;

namespace Demos.Domain.Economy;

/// <summary>
/// Une commune ne garde que ce que ses bâtiments de stockage contiennent (camp de base, entrepôt, dépôt
/// logistique) : produits en vente sur son marché et stock de l'État confondus. En fin de journée, ce qui dépasse
/// est perdu, chaque produit dans la même proportion. Les produits déjà chez les habitants ne comptent pas.
/// </summary>
public static class Storage
{
    public static int CapacityOf(string communeId, IReadOnlyDictionary<string, Structure> structures) =>
        structures.Values
            .Where(s => s.StorageCapacity > 0 && Communes.CommuneOf(s, structures) == communeId)
            .Sum(s => s.StorageCapacity);

    public static double HeldIn(Market market, Inventory stateStock) =>
        market.Contents.Values.Sum() + stateStock.Contents.Values.Sum();

    public static double FreeSpace(string communeId, IReadOnlyDictionary<string, Structure> structures, Market market, Inventory stateStock) =>
        Math.Max(0, CapacityOf(communeId, structures) - HeldIn(market, stateStock));

    /// <summary>Fin de journée : ce qui dépasse la capacité de stockage de chaque commune est perdu.</summary>
    public static void ApplyDaily(
        IEnumerable<string> communeIds,
        IReadOnlyDictionary<string, Structure> structures,
        Func<string, Market> marketOf,
        Func<string, Inventory> stateStockOf)
    {
        foreach (var communeId in communeIds)
        {
            var market = marketOf(communeId);
            var stock = stateStockOf(communeId);
            var held = HeldIn(market, stock);
            var capacity = CapacityOf(communeId, structures);
            if (held <= capacity)
                continue;

            var kept = held > 0 ? capacity / held : 0;
            market.Keep(kept);
            stock.Keep(kept);
        }
    }
}
