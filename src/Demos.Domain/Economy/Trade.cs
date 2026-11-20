using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Domain.Economy;

/// <summary>
/// Un chargement parti d'une commune vers une autre (décidé le 20 novembre 2026) : il arrive après <see cref="DaysLeft"/>
/// matins, au marché de sa commune d'arrivée au nom de son vendeur, ou dans la réserve de <see cref="ClientId"/> s'il
/// vient d'une ligne d'approvisionnement.
/// </summary>
public sealed record Shipment(string Origin, string Destination, string ProductId, string SellerId, double Quantity,
    int DaysLeft = 1, string? ClientId = null, int Days = 1);

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
    /// La commune dont la capacité d'envoi sert à un envoi (décidé le 20 novembre 2026) : celle qui envoie si elle a un
    /// bâtiment logistique, sinon celle qui reçoit.
    /// </summary>
    public static string CapacityHolder(string origin, string destination, Func<string, bool> hasLogistics) =>
        hasLogistics(origin) ? origin : destination;

    /// <summary>
    /// Ce qu'il faut aux bâtiments de chaque commune pour une journée : leurs matières à plein régime avec leurs emplois
    /// pourvus, et leur combustible du jour. Par commune, puis par produit.
    /// </summary>
    public static Dictionary<string, Dictionary<string, double>> BuildingDemand(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> productionWorkers,
        Func<Structure, int> tierOf,
        Season season,
        Func<Structure, bool>? constantHeatingOf = null,
        Func<Structure, double>? toolsOf = null,
        double? toolUsageDays = null)
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

            // Les outils (décidé le 20 novembre 2026) : l'usure du jour, plus une part de ce qui manque au stock visé.
            if (toolsOf is not null && toolUsageDays is > 0 && Tools.TargetOf(building) is > 0 and var toolTarget)
            {
                var owned = toolsOf(building);
                Add(communeId, Tools.ProductId, owned / toolUsageDays.Value + Math.Max(0, toolTarget - owned) * Balance.Current.PossessionCatchUp);
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
    /// Chaque matin, les chargements avancent d'un jour : ils perdent ce qui s'est périmé pendant la nuit (comme dans un
    /// stockage général, compté comme perdu en route dans la commune de départ), et ceux qui arrivent entrent au marché
    /// de leur commune d'arrivée, au nom de leur vendeur, ou dans la réserve du client d'une ligne. Renvoie ceux qui
    /// sont encore en route.
    /// </summary>
    public static List<Shipment> Advance(IEnumerable<Shipment> shipments, IReadOnlyList<ProductDefinition> products, Func<string, Market> marketOf,
        Func<string, Inventory>? reserveOf = null)
    {
        var stillOnTheWay = new List<Shipment>();
        foreach (var shipment in shipments)
        {
            var spoiled = products.FirstOrDefault(p => p.Id == shipment.ProductId) is { IsPerishable: true } product
                ? shipment.Quantity * Math.Min(1, 1 / product.ShelfLifeDays!.Value)
                : 0;
            if (spoiled > 0)
                marketOf(shipment.Origin).CountLostInTransit(shipment.ProductId, spoiled);
            var left = shipment with { Quantity = shipment.Quantity - spoiled, DaysLeft = shipment.DaysLeft - 1 };
            if (left.DaysLeft > 0)
                stillOnTheWay.Add(left);
            else if (left.ClientId is { } clientId && reserveOf is not null)
                reserveOf(clientId).Add(left.ProductId, left.Quantity);
            else
                marketOf(left.Destination).Offer(left.ProductId, left.Quantity, left.SellerId);
        }

        return stillOnTheWay;
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
        Func<string, string, double>? expectedProductionOf = null,
        Func<string, string, int>? travelDaysOf = null,
        Func<string, string, double>? inTransitTo = null,
        Func<string, string, string>? capacityHolderOf = null,
        Dictionary<string, double>? capacityLeft = null)
    {
        // D'abord ce que chaque produit enverrait, puis la capacité d'envoi de chaque commune, partagée au prorata entre
        // tous les produits et toutes les destinations quand elle ne suffit pas (décidé le 20 novembre 2026).
        var plans = new List<(ProductDefinition Product, string Origin, string Destination, double Quantity)>();
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
            // Pour chaque commune, le soir : ce qu'elle a à vendre, et sa demande nette de demain (ce que consomment ses
            // habitants, moins ce que couvrent déjà les autres produits du même besoin, plus ce qu'il faut à ses bâtiments,
            // moins ce qu'elle produira demain, estimé par sa production du jour).
            var state = communeIds.ToDictionary(
                communeId => communeId,
                communeId =>
                {
                    var market = marketOf(communeId);
                    var residents = need is { } category
                        ? classes.Sum(c => c.MembersLivingIn(communeId) * Consumption.NeedsOf(c.Rank).GetValueOrDefault(category))
                        : 0;
                    var buildings = buildingDemandOf?.Invoke(communeId, product.Id) ?? 0;
                    var demand = Math.Max(0, residents - substitutes.Sum(s => market.QuantityForSale(s))) + buildings;
                    var expected = expectedProductionOf?.Invoke(communeId, product.Id) ?? 0;
                    return (ForSale: market.QuantityForSale(product.Id), Net: Math.Max(0, demand - expected));
                });
            var balance = state.ToDictionary(s => s.Key, s => s.Value.ForSale - s.Value.Net);

            var surplus = balance.Where(b => b.Value > 0).ToDictionary(b => b.Key, b => b.Value);
            var totalSurplus = surplus.Values.Sum();
            if (totalSurplus <= 0)
                continue;

            // Les communes d'où une commune peut recevoir (l'une des deux a un bâtiment logistique).
            List<string> OriginsFor(string destination) =>
                surplus.Keys.Where(o => canTrade?.Invoke(o, destination) ?? true).ToList();

            // Ce qu'il faut envoyer pour couvrir chaque manque, une fois la perte en route comptée. Ce qui part arrive
            // dans quelques jours : la commune compare ce qu'elle a et ce qui est déjà en route vers elle à ce qu'il lui
            // faut jusqu'à l'arrivée (sa demande nette × la durée du trajet) ; elle ne commande pas deux fois.
            var toSend = balance
                .Where(b => b.Value < 0 && OriginsFor(b.Key).Count > 0)
                .ToDictionary(b => b.Key, b =>
                {
                    var origins = OriginsFor(b.Key);
                    var lossEstimate = transportLossOf is not null
                        ? origins.Average(o => transportLossOf(o, b.Key))
                        : TransportLoss;
                    var days = travelDaysOf is not null ? origins.Average(o => travelDaysOf(o, b.Key)) : 1;
                    var (forSale, net) = state[b.Key];
                    var coming = inTransitTo?.Invoke(b.Key, product.Id) ?? 0;
                    var missing = Math.Max(0, net * days - forSale - coming);
                    return Math.Min(missing, freeStorage[b.Key]) / Math.Max(0.01, 1 - lossEstimate);
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
                    var planned = sent * surplus[origin] / reachable;
                    var loss = transportLossOf?.Invoke(origin, destination) ?? TransportLoss;
                    freeStorage[destination] -= planned * (1 - loss);
                    plans.Add((product, origin, destination, planned));
                }
            }
        }

        // La part de ce qui est prévu que la capacité de chaque commune permet d'envoyer.
        var factorOf = new Dictionary<string, double>();
        if (capacityHolderOf is not null && capacityLeft is not null)
        {
            foreach (var group in plans.GroupBy(p => capacityHolderOf(p.Origin, p.Destination)))
            {
                var total = group.Sum(p => p.Quantity);
                factorOf[group.Key] = total > 0 ? Math.Clamp(capacityLeft.GetValueOrDefault(group.Key) / total, 0, 1) : 1;
            }
        }

        foreach (var (product, origin, destination, planned) in plans)
        {
            var holder = capacityHolderOf?.Invoke(origin, destination);
            var quantityToSend = planned * (holder is not null ? factorOf.GetValueOrDefault(holder, 1) : 1);
            var loss = transportLossOf?.Invoke(origin, destination) ?? TransportLoss;
            var originMarket = marketOf(origin);
            foreach (var (sellerId, quantity) in originMarket.Take(product.Id, quantityToSend))
            {
                if (quantity <= 0)
                    continue;
                if (holder is not null && capacityLeft is not null)
                    capacityLeft[holder] = capacityLeft.GetValueOrDefault(holder) - quantity;
                shipped?.Invoke(origin, destination, quantity);
                var arrived = quantity * (1 - loss);
                originMarket.CountLostInTransit(product.Id, quantity - arrived);
                // Le chargement part sur la route, ou arrive tout de suite sans file d'attente (ancien appel).
                if (dispatch is not null)
                {
                    var days = travelDaysOf?.Invoke(origin, destination) ?? 1;
                    dispatch(new Shipment(origin, destination, product.Id, sellerId, arrived, days, Days: days));
                }
                else
                    marketOf(destination).Offer(product.Id, arrived, sellerId);
            }
        }
    }
}
