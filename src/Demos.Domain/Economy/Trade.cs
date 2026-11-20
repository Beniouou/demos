using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Domain.Economy;

/// <summary>Un chargement parti d'une commune vers une autre, qui arrive le lendemain (décidé le 20 novembre 2026).</summary>
public sealed record Shipment(string Origin, string Destination, string ProductId, string SellerId, double Quantity);

/// <summary>
/// Échanges entre communes, en attendant la logistique : chaque jour, pour chaque produit qui se stocke, les surplus
/// des communes (ce qui dépasse ce qu'utilisent leurs habitants et leurs bâtiments) partent vers les communes qui en
/// manquent. Une part se perd en route, selon la distance, et une commune ne reçoit que dans la limite de sa place de
/// stockage libre pour ce produit. Les produits gardent leur vendeur, payé quand ils sont achetés à l'arrivée. Ce qui
/// part d'une commune vers une autre est signalé (<c>shipped</c>) : c'est le trafic qui crée les routes.
/// Depuis le 20 novembre 2026 : les matières voyagent aussi, deux communes n'échangent que si l'une a un bâtiment
/// logistique (<c>canTrade</c>), et les chargements arrivent le lendemain (<c>dispatch</c>, puis <see cref="Arrive"/>).
/// </summary>
public static class Trade
{
    /// <summary>Part perdue en route (réglage d'équilibrage : 20 %). Les bâtiments de logistique viendront la réduire.</summary>
    public static double TransportLoss => Balance.Current.TransportLoss;

    /// <summary>La catégorie du catalogue des bâtiments qui ouvre les échanges (entrepôt, grenier, silo, dépôt logistique).</summary>
    public const string LogisticsCategory = "Logistique";

    /// <summary>
    /// Deux communes peuvent échanger (échanges et lignes d'approvisionnement) si l'une des deux a au moins un bâtiment
    /// logistique, quelle que soit sa sorte (décidé le 20 novembre 2026). Une commune avec elle-même : toujours.
    /// </summary>
    public static bool CanTrade(string origin, string destination, Func<string, bool> hasLogistics) =>
        origin == destination || hasLogistics(origin) || hasLogistics(destination);

    /// <summary>
    /// Ce qu'il faut aux bâtiments de chaque commune pour une journée : leurs matières à plein régime avec leurs emplois
    /// pourvus, et leur combustible du jour. Par commune, puis par produit.
    /// </summary>
    public static Dictionary<string, Dictionary<string, double>> BuildingDemand(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> productionWorkers,
        Func<Structure, int> tierOf,
        Season season,
        Func<Structure, bool>? constantHeatingOf = null)
    {
        var demand = new Dictionary<string, Dictionary<string, double>>();
        void Add(string communeId, string productId, double quantity)
        {
            if (quantity <= 0)
                return;
            if (!demand.TryGetValue(communeId, out var byProduct))
                demand[communeId] = byProduct = [];
            byProduct[productId] = byProduct.GetValueOrDefault(productId) + quantity;
        }

        foreach (var building in structures.Values.Where(s => s.Kind == StructureKind.Building))
        {
            if (Communes.CommuneOf(building, structures) is not { } communeId)
                continue;
            if (building.Jobs > 0 && building.Production is { } recipe)
            {
                var planned = recipe.QuantityPerWorker * productionWorkers.GetValueOrDefault(building.Id);
                foreach (var material in recipe.Materials)
                    Add(communeId, material.ProductId, planned * material.Quantity);
            }

            var units = Energy.UnitsOf(building);
            var tier = tierOf(building);
            if (units > 0 && tier == 1)
            {
                foreach (var need in Energy.NeedsOf(building, tier, season, constantHeatingOf?.Invoke(building) ?? false))
                    Add(communeId, need.ProductId, need.Quantity * units);
            }
        }

        return demand;
    }

    /// <summary>
    /// Les chargements de la veille arrivent : ils entrent au marché de leur commune d'arrivée, au nom de leur vendeur,
    /// moins ce qui s'est périmé pendant la nuit (comme dans un stockage général), compté comme perdu en route dans la
    /// commune de départ.
    /// </summary>
    public static void Arrive(IEnumerable<Shipment> shipments, IReadOnlyList<ProductDefinition> products, Func<string, Market> marketOf)
    {
        foreach (var shipment in shipments)
        {
            var spoiled = products.FirstOrDefault(p => p.Id == shipment.ProductId) is { IsPerishable: true } product
                ? shipment.Quantity * Math.Min(1, 1 / product.ShelfLifeDays!.Value)
                : 0;
            if (spoiled > 0)
                marketOf(shipment.Origin).CountLostInTransit(shipment.ProductId, spoiled);
            marketOf(shipment.Destination).Offer(shipment.ProductId, shipment.Quantity - spoiled, shipment.SellerId);
        }
    }

    public static void RunDaily(
        IReadOnlyList<string> communeIds,
        IReadOnlyList<SocialClass> classes,
        IReadOnlyList<ProductDefinition> products,
        Func<string, Market> marketOf,
        Func<string, string, double> freeStorageOf,
        Func<string, string, double>? transportLossOf = null,
        Action<string, string, double>? shipped = null,
        Func<string, string, double>? buildingDemandOf = null,
        Func<string, string, bool>? canTrade = null,
        Action<Shipment>? dispatch = null,
        Func<string, string, double>? expectedProductionOf = null)
    {
        foreach (var product in products)
        {
            // Ce qui ne se stocke pas (l'eau, les services) ne voyage pas entre communes. Sans la demande des bâtiments
            // (ancien appel), seuls les produits qui couvrent un besoin des habitants voyagent.
            var need = product.Need is { } n && Consumption.AllNeeds.Contains(n) ? n : (NeedCategory?)null;
            if (!product.Stockable || (need is null && buildingDemandOf is null))
                continue;

            // Place libre pour ce produit dans chaque commune, selon les sortes de stockage et les limites.
            var freeStorage = communeIds.ToDictionary(c => c, c => freeStorageOf(c, product.Id));

            // Le besoin d'une commune pour ce produit : ce que ses habitants consomment, moins ce que couvrent déjà les
            // autres produits du même besoin (le pain et la viande nourrissent tous les deux), plus ce qu'il faut à ses
            // bâtiments pour une journée (matières et combustible).
            var substitutes = need is null ? [] : products.Where(p => p.Need == need && p.Id != product.Id).Select(p => p.Id).ToList();
            var balance = communeIds.ToDictionary(
                communeId => communeId,
                communeId =>
                {
                    var market = marketOf(communeId);
                    var residents = need is { } category
                        ? classes.Sum(c => c.MembersLivingIn(communeId) * Consumption.NeedsOf(c.Rank).GetValueOrDefault(category))
                        : 0;
                    var buildings = buildingDemandOf?.Invoke(communeId, product.Id) ?? 0;
                    var demand = Math.Max(0, residents - substitutes.Sum(s => market.QuantityForSale(s))) + buildings;
                    // Le soir, ce que la commune produira demain (estimé par sa production du jour) couvrira une part de sa
                    // demande de demain.
                    var expected = expectedProductionOf?.Invoke(communeId, product.Id) ?? 0;
                    return market.QuantityForSale(product.Id) - Math.Max(0, demand - expected);
                });

            var surplus = balance.Where(b => b.Value > 0).ToDictionary(b => b.Key, b => b.Value);
            var totalSurplus = surplus.Values.Sum();
            if (totalSurplus <= 0)
                continue;

            // Les communes d'où une commune peut recevoir (l'une des deux a un bâtiment logistique).
            List<string> OriginsFor(string destination) =>
                surplus.Keys.Where(o => canTrade?.Invoke(o, destination) ?? true).ToList();

            // Ce qu'il faut envoyer pour couvrir chaque manque, une fois la perte en route comptée.
            var toSend = balance
                .Where(b => b.Value < 0 && OriginsFor(b.Key).Count > 0)
                .ToDictionary(b => b.Key, b =>
                {
                    var origins = OriginsFor(b.Key);
                    var lossEstimate = transportLossOf is not null
                        ? origins.Average(o => transportLossOf(o, b.Key))
                        : TransportLoss;
                    return Math.Min(-b.Value, freeStorage[b.Key]) / Math.Max(0.01, 1 - lossEstimate);
                });
            var totalToSend = toSend.Values.Sum();
            if (totalToSend <= 0)
                continue;

            var scale = Math.Min(1, totalSurplus / totalToSend);
            foreach (var (destination, wanted) in toSend)
            {
                var sent = wanted * scale;
                var origins = OriginsFor(destination);
                var reachable = origins.Sum(o => surplus[o]);
                foreach (var origin in origins)
                {
                    var loss = transportLossOf?.Invoke(origin, destination) ?? TransportLoss;
                    var originMarket = marketOf(origin);
                    foreach (var (sellerId, quantity) in originMarket.Take(product.Id, sent * surplus[origin] / reachable))
                    {
                        shipped?.Invoke(origin, destination, quantity);
                        var arrived = quantity * (1 - loss);
                        originMarket.CountLostInTransit(product.Id, quantity - arrived);
                        freeStorage[destination] -= arrived;
                        // Le chargement arrive le lendemain, ou tout de suite sans file d'attente (ancien appel).
                        if (dispatch is not null)
                            dispatch(new Shipment(origin, destination, product.Id, sellerId, arrived));
                        else
                            marketOf(destination).Offer(product.Id, arrived, sellerId);
                    }
                }
            }
        }
    }
}
