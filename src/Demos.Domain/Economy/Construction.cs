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

public static class Construction
{
    /// <summary>
    /// Paie une construction avec les produits de l'État dans le stockage de la commune, en argent, ou en mélangeant
    /// les deux. Tout ou rien : si le stockage ou le trésor ne suffit pas, rien n'est dépensé.
    /// </summary>
    public static bool TryPay(
        IReadOnlyList<MaterialCost> cost,
        PaymentMode mode,
        string stateSellerId,
        Treasury treasury,
        Market market)
    {
        var plan = new List<(string ProductId, double FromState, double Bought)>();
        double money = 0;

        foreach (var item in cost)
        {
            var fromState = mode == PaymentMode.StockFirst
                ? Math.Min(market.QuantityOf(item.ProductId, stateSellerId), item.Quantity)
                : 0;
            var bought = item.Quantity - fromState;

            if (market.QuantityOfOthers(item.ProductId, stateSellerId) < bought)
                return false;

            money += market.CostOf(item.ProductId, bought);
            plan.Add((item.ProductId, fromState, bought));
        }

        if (!treasury.TrySpend(money))
            return false;

        foreach (var (productId, fromState, bought) in plan)
        {
            market.TryWithdraw(productId, fromState, stateSellerId);
            market.TryBuy(productId, bought, exceptSellerId: stateSellerId);
        }

        return true;
    }
}
