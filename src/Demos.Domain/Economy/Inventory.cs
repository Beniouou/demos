namespace Demos.Domain.Economy;

/// <summary>Quantités de produits en stock, par identifiant de produit.</summary>
public sealed class Inventory
{
    private readonly Dictionary<string, double> _quantities = [];

    public IReadOnlyDictionary<string, double> Contents => _quantities;

    public double QuantityOf(string productId) => _quantities.GetValueOrDefault(productId);

    public void Add(string productId, double quantity) => _quantities[productId] = QuantityOf(productId) + quantity;

    /// <summary>Retire une quantité. Renvoie false, sans rien retirer, si le stock est insuffisant.</summary>
    public bool TryRemove(string productId, double quantity)
    {
        if (QuantityOf(productId) < quantity)
            return false;

        _quantities[productId] = QuantityOf(productId) - quantity;
        return true;
    }
}
