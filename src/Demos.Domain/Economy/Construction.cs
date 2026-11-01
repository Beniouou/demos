namespace Demos.Domain.Economy;

public enum PaymentMode
{
    /// <summary>Tout est acheté sur le marché avec l'argent de l'État.</summary>
    MoneyOnly,

    /// <summary>Le stock de l'État d'abord ; le reste est acheté sur le marché.</summary>
    StockFirst,
}

/// <summary>Quantité de matière nécessaire à une construction.</summary>
public readonly record struct MaterialCost(string ProductId, double Quantity);

public static class Construction
{
    /// <summary>
    /// Paie une construction en matériaux du stock, en argent, ou en mélangeant les deux.
    /// Tout ou rien : si le marché ou le trésor ne suffit pas, rien n'est dépensé.
    /// </summary>
    public static bool TryPay(
        IReadOnlyList<MaterialCost> cost,
        PaymentMode mode,
        Inventory stateStock,
        Treasury treasury,
        Market market)
    {
        var plan = new List<(string ProductId, double FromStock, double FromMarket)>();
        double money = 0;

        foreach (var item in cost)
        {
            var fromStock = mode == PaymentMode.StockFirst
                ? Math.Min(stateStock.QuantityOf(item.ProductId), item.Quantity)
                : 0;
            var fromMarket = item.Quantity - fromStock;

            if (market.Stock.QuantityOf(item.ProductId) < fromMarket)
                return false;

            money += market.CostOf(item.ProductId, fromMarket);
            plan.Add((item.ProductId, fromStock, fromMarket));
        }

        if (!treasury.TrySpend(money))
            return false;

        foreach (var (productId, fromStock, fromMarket) in plan)
        {
            stateStock.TryRemove(productId, fromStock);
            market.Stock.TryRemove(productId, fromMarket);
        }

        return true;
    }
}
