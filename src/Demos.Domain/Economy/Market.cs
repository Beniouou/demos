namespace Demos.Domain.Economy;

/// <summary>
/// Marché national unique. Les structures privées et coopératives y vendent leur production ;
/// l'État et les classes y achètent. Pour l'instant, les prix restent les prix de départ.
/// </summary>
public sealed class Market(IEnumerable<ProductDefinition> products)
{
    private readonly Dictionary<string, double> _prices = products.ToDictionary(p => p.Id, p => p.BasePrice);

    /// <summary>Produits disponibles à l'achat.</summary>
    public Inventory Stock { get; } = new();

    public double PriceOf(string productId) => _prices.GetValueOrDefault(productId);

    public double CostOf(string productId, double quantity) => quantity * PriceOf(productId);

    /// <summary>Met des produits en vente et renvoie ce que le vendeur touche au prix du jour.</summary>
    public double Sell(string productId, double quantity)
    {
        Stock.Add(productId, quantity);
        return CostOf(productId, quantity);
    }
}
