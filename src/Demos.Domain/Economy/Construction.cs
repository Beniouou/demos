namespace Demos.Domain.Economy;

public enum PaymentMode
{
    /// <summary>Tout est acheté aux autres vendeurs de la commune avec l'argent de l'État.</summary>
    MoneyOnly,

    /// <summary>
    /// L'État reprend d'abord, dans le stockage de la commune, ce qu'il a lui-même produit ; le reste est acheté aux
    /// autres vendeurs.
    /// </summary>
    StockFirst,
}

/// <summary>Quantité de matière nécessaire à une construction.</summary>
public readonly record struct MaterialCost(string ProductId, double Quantity);

/// <summary>
/// Coût de construction d'un type de bâtiment : un matériau préfabriqué (matériaux de construction pour la liste 1,
/// matériaux de construction technique pour la liste 2), sa quantité, et la durée du chantier en jours.
/// </summary>
public sealed record ConstructionCost(string ProductId, double Quantity, int Days)
{
    public IReadOnlyList<MaterialCost> Materials => [new(ProductId, Quantity)];
}

/// <summary>
/// Un bâtiment en chantier : payé au lancement, il n'a ni emplois, ni production, ni places, ni énergie tant qu'il
/// reste des jours de chantier ; il rejoint alors la partie et devient actif le lendemain.
/// </summary>
public sealed record ConstructionSite(Politics.Structure Building, int DaysLeft);

public static class Construction
{
    /// <summary>
    /// Paie une construction avec les produits de l'État dans le stockage de la commune, en argent, ou en mélangeant
    /// les deux. La réserve de chantier de la commune, s'il y en a une, passe avant tout le reste. Tout ou rien : si
    /// le stockage ou le trésor ne suffit pas, rien n'est dépensé.
    /// </summary>
    public static bool TryPay(
        IReadOnlyList<MaterialCost> cost,
        PaymentMode mode,
        string stateSellerId,
        Treasury treasury,
        Market market,
        Inventory? reserve = null)
    {
        var plan = new List<(string ProductId, double FromReserve, double FromState, double Bought)>();
        double money = 0;

        foreach (var item in cost)
        {
            var fromReserve = Math.Min(reserve?.QuantityOf(item.ProductId) ?? 0, item.Quantity);
            var fromState = mode == PaymentMode.StockFirst
                ? Math.Min(market.QuantityOf(item.ProductId, stateSellerId), item.Quantity - fromReserve)
                : 0;
            var bought = item.Quantity - fromReserve - fromState;

            if (market.QuantityOfOthers(item.ProductId, stateSellerId) < bought)
                return false;

            money += market.CostOf(item.ProductId, bought);
            plan.Add((item.ProductId, fromReserve, fromState, bought));
        }

        if (!treasury.TrySpend(money))
            return false;

        foreach (var (productId, fromReserve, fromState, bought) in plan)
        {
            reserve?.TryRemove(productId, fromReserve);
            market.TryWithdraw(productId, fromState, stateSellerId);
            market.TryBuy(productId, bought, exceptSellerId: stateSellerId);
        }

        return true;
    }
}
