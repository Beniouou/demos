using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Échanges entre communes, en attendant la logistique : chaque jour, pour chaque produit de base, les surplus des
/// communes (ce qui dépasse les besoins de leurs habitants, selon leur rang) partent vers les communes qui en
/// manquent. 20 % se perdent en route, et une commune ne reçoit que dans la limite de sa place de stockage libre pour
/// ce produit. Les produits gardent leur vendeur, payé quand ils sont achetés à l'arrivée.
/// </summary>
public static class Trade
{
    /// <summary>Part perdue en route. Les bâtiments de logistique viendront la réduire.</summary>
    public const double TransportLoss = 0.2;

    public static void RunDaily(
        IReadOnlyList<string> communeIds,
        IReadOnlyList<SocialClass> classes,
        IReadOnlyList<ProductDefinition> products,
        Func<string, Market> marketOf,
        Func<string, string, double> freeStorageOf)
    {
        foreach (var product in products)
        {
            // Ce qui ne se stocke pas (l'eau, les services) ne voyage pas entre communes.
            if (product.Need is not { } need || !Consumption.AllNeeds.Contains(need) || !product.Stockable)
                continue;

            // Place libre pour ce produit dans chaque commune, selon les sortes de stockage et les limites.
            var freeStorage = communeIds.ToDictionary(c => c, c => freeStorageOf(c, product.Id));

            // Le besoin d'une commune pour ce produit : ce que ses habitants consomment, moins ce que couvrent déjà les
            // autres produits du même besoin (le pain et la viande nourrissent tous les deux).
            var substitutes = products.Where(p => p.Need == need && p.Id != product.Id).Select(p => p.Id).ToList();
            var balance = communeIds.ToDictionary(
                communeId => communeId,
                communeId =>
                {
                    var market = marketOf(communeId);
                    var demand = classes.Sum(c => c.MembersLivingIn(communeId) * Consumption.NeedsOf(c.Rank).GetValueOrDefault(need));
                    return market.QuantityOf(product.Id) - Math.Max(0, demand - substitutes.Sum(market.QuantityOf));
                });

            var surplus = balance.Where(b => b.Value > 0).ToDictionary(b => b.Key, b => b.Value);
            var totalSurplus = surplus.Values.Sum();
            if (totalSurplus <= 0)
                continue;

            // Ce qu'il faut envoyer pour couvrir chaque manque, une fois la perte en route comptée.
            var toSend = balance
                .Where(b => b.Value < 0)
                .ToDictionary(b => b.Key, b => Math.Min(-b.Value, freeStorage[b.Key]) / (1 - TransportLoss));
            var totalToSend = toSend.Values.Sum();
            if (totalToSend <= 0)
                continue;

            var scale = Math.Min(1, totalSurplus / totalToSend);
            foreach (var (destination, wanted) in toSend)
            {
                var sent = wanted * scale;
                foreach (var (origin, available) in surplus)
                {
                    foreach (var (sellerId, quantity) in marketOf(origin).Take(product.Id, sent * available / totalSurplus))
                    {
                        var arrived = quantity * (1 - TransportLoss);
                        marketOf(destination).Offer(product.Id, arrived, sellerId);
                        freeStorage[destination] -= arrived;
                    }
                }
            }
        }
    }
}
