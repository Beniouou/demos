using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Une ligne d'approvisionnement créée par le joueur : une part de la production du fournisseur part chaque jour au
/// client, qui l'utilise comme matière.
/// </summary>
public sealed record SupplyLine(string Id, string SupplierId, string ClientId, double Share);

/// <summary>
/// Les lignes d'approvisionnement (décidé le 15 novembre 2026), seulement entre bâtiments publics. Chaque jour, après
/// le travail, une part de la production du fournisseur part d'abord au client, avant d'être mise en vente : elle quitte
/// le stockage de la commune du fournisseur et va dans la réserve du client, qui s'en sert le lendemain avant
/// d'acheter (voir <see cref="Supply"/>). Le client paie au prix du marché, sauf dans sa propre commune (c'est déjà la
/// production de l'État de la commune). Entre communes, la perte de transport s'applique et le trafic compte pour les
/// routes. Le client ne reçoit que ce dont il a besoin à plein régime pour une journée, moins sa réserve ; le reste
/// reste en vente. Les lignes passent dans l'ordre de la liste : la plus ancienne d'abord, sauf si le joueur change la
/// priorité des lignes d'un client (World.MoveSupplyLine). Pas de délai. Entre deux communes, la ligne ne livre que si
/// l'une des deux a un bâtiment logistique (<see cref="Trade.CanTrade"/>).
/// </summary>
public static class SupplyLines
{
    /// <summary>Part de la production du fournisseur envoyée par une nouvelle ligne (proposition validée : 50 %).</summary>
    public const double DefaultShare = 0.5;

    /// <summary>
    /// Pourquoi une ligne ne peut pas relier ces deux bâtiments, ou null si elle le peut : deux bâtiments publics
    /// différents, le fournisseur avec une recette, le client avec une recette qui utilise son produit, pas deux fois la
    /// même ligne.
    /// </summary>
    public static string? ProblemWith(Structure? supplier, Structure? client, IEnumerable<SupplyLine> lines)
    {
        if (supplier is null || client is null)
            return "bâtiment inconnu";
        if (supplier.Id == client.Id)
            return "un bâtiment ne peut pas s'approvisionner lui-même";
        if (supplier.Ownership != Ownership.Public || client.Ownership != Ownership.Public)
            return "les lignes ne relient que des bâtiments publics";
        if (supplier.Production is not { } production)
            return $"{supplier.Name} ne produit rien";
        if (!Uses(client, production.ProductId))
            return $"la recette de {client.Name} n'utilise pas ce que produit {supplier.Name}";
        if (lines.Any(l => l.SupplierId == supplier.Id && l.ClientId == client.Id))
            return "cette ligne existe déjà";
        return null;
    }

    /// <summary>Si la recette actuelle du bâtiment utilise ce produit comme matière.</summary>
    public static bool Uses(Structure building, string productId) =>
        building.Production is { } recipe && recipe.Materials.Any(m => m.ProductId == productId);

    /// <summary>
    /// Fait passer la production du jour par les lignes, dans l'ordre de la liste (leur priorité). Renvoie ce que chaque
    /// ligne a livré (arrivé chez le client) dans la journée.
    /// </summary>
    public static Dictionary<string, double> RunDaily(
        IReadOnlyList<SupplyLine> lines,
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> produced,
        IReadOnlyDictionary<string, double> productionWorkers,
        Func<string, Market> marketOf,
        Func<string, Inventory> reserveOf,
        Treasury treasury,
        Func<string, Treasury>? communeFundOf = null,
        Func<string, string, double>? transportLossOf = null,
        Action<string, string, double>? shipped = null,
        Func<string, string, bool>? canTrade = null)
    {
        var delivered = new Dictionary<string, double>();
        var noClasses = new Dictionary<string, SocialClass>();
        foreach (var line in lines)
        {
            delivered[line.Id] = 0;
            if (!structures.TryGetValue(line.SupplierId, out var supplier) || !structures.TryGetValue(line.ClientId, out var client)
                || supplier.Production is not { } production || client.Production is not { } recipe
                || supplier.Ownership != Ownership.Public || client.Ownership != Ownership.Public
                || recipe.Materials.FirstOrDefault(m => m.ProductId == production.ProductId) is not { ProductId: not null } material
                || Communes.CommuneOf(supplier, structures) is not { } origin
                || Communes.CommuneOf(client, structures) is not { } destination)
                continue;

            var productId = production.ProductId;
            var originMarket = marketOf(origin);
            var seller = Market.StateSellerId(origin);
            var reserve = reserveOf(client.Id);
            var sameCommune = origin == destination;
            // Entre deux communes, il faut un bâtiment logistique dans l'une des deux (décidé le 20 novembre 2026).
            if (!sameCommune && canTrade?.Invoke(origin, destination) == false)
                continue;
            var loss = sameCommune ? 0 : transportLossOf?.Invoke(origin, destination) ?? Trade.TransportLoss;

            // Ce que la ligne peut prendre (sa part de la production du jour, dans ce qui est encore en stock), et ce
            // qu'il faut au client pour une journée à plein régime, moins sa réserve ; il faut en envoyer plus pour
            // couvrir la perte en route.
            var offered = Math.Min(produced.GetValueOrDefault(supplier.Id) * line.Share, originMarket.QuantityOf(productId, seller));
            var need = recipe.QuantityPerWorker * productionWorkers.GetValueOrDefault(client.Id) * material.Quantity;
            var wanted = Math.Max(0, need - reserve.QuantityOf(productId)) / Math.Max(0.01, 1 - loss);
            var sent = Math.Min(offered, wanted);

            // Le client paie au prix du marché ce qui part, sauf dans sa commune : c'est déjà la production de son État.
            var payer = Supply.PayerOf(client, destination, treasury, communeFundOf?.Invoke(destination), null, noClasses);
            var price = sameCommune ? 0 : originMarket.PriceOf(productId);
            if (price > 0)
                sent = Math.Min(sent, Math.Max(0, payer.Available()) / price);
            if (sent <= 0)
                continue;

            if (!originMarket.TryDeliver(productId, sent, seller, sent * price, toOtherCommune: !sameCommune))
                continue;
            if (price > 0)
                payer.Spend(sent * price);
            if (!sameCommune)
                shipped?.Invoke(origin, destination, sent);

            var arrived = sent * (1 - loss);
            originMarket.CountLostInTransit(productId, sent - arrived);
            reserve.Add(productId, arrived);
            delivered[line.Id] = arrived;
        }

        return delivered;
    }
}
