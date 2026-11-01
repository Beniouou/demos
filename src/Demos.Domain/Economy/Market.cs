namespace Demos.Domain.Economy;

/// <summary>
/// Marché national unique. Les structures privées et coopératives y mettent leur production en vente ;
/// l'État et les classes y achètent. Le vendeur est payé quand son produit est acheté.
/// Pour l'instant, les prix restent les prix de départ.
/// </summary>
public sealed class Market(IEnumerable<ProductDefinition> products)
{
    private readonly Dictionary<string, double> _prices = products.ToDictionary(p => p.Id, p => p.BasePrice);

    // Produit → vendeur → quantité en vente.
    private readonly Dictionary<string, Dictionary<string, double>> _offers = [];

    // Vendeur → argent gagné depuis le dernier versement.
    private readonly Dictionary<string, double> _earnings = [];

    public double PriceOf(string productId) => _prices.GetValueOrDefault(productId);

    /// <summary>Fixe le prix d'un produit à la main, en attendant la formation automatique des prix.</summary>
    public void SetPrice(string productId, double price) => _prices[productId] = Math.Max(0, price);

    public double CostOf(string productId, double quantity) => quantity * PriceOf(productId);

    public double QuantityOf(string productId) =>
        _offers.TryGetValue(productId, out var offers) ? offers.Values.Sum() : 0;

    /// <summary>Quantités en vente, par produit.</summary>
    public IReadOnlyDictionary<string, double> Contents =>
        _offers.ToDictionary(offers => offers.Key, offers => offers.Value.Values.Sum());

    public void Offer(string productId, double quantity, string sellerId)
    {
        if (!_offers.TryGetValue(productId, out var offers))
            _offers[productId] = offers = [];
        offers[sellerId] = offers.GetValueOrDefault(sellerId) + quantity;
    }

    /// <summary>
    /// Achète une quantité, prise chez les vendeurs au prorata de leur offre. Le paiement de l'acheteur
    /// est à la charge de l'appelant. Renvoie false, sans rien acheter, si l'offre est insuffisante.
    /// </summary>
    public bool TryBuy(string productId, double quantity)
    {
        var available = QuantityOf(productId);
        if (quantity <= 0)
            return true;
        if (available < quantity)
            return false;

        var offers = _offers[productId];
        foreach (var sellerId in offers.Keys.ToList())
        {
            var taken = quantity * offers[sellerId] / available;
            offers[sellerId] -= taken;
            _earnings[sellerId] = _earnings.GetValueOrDefault(sellerId) + CostOf(productId, taken);
        }

        return true;
    }

    /// <summary>
    /// Retire des produits du marché sans les payer, pris chez les vendeurs au prorata de leur offre (pour les
    /// envoyer dans une autre commune). Renvoie la quantité retirée à chaque vendeur.
    /// </summary>
    public Dictionary<string, double> Take(string productId, double quantity)
    {
        var taken = new Dictionary<string, double>();
        var available = QuantityOf(productId);
        if (quantity <= 0 || available <= 0)
            return taken;

        var share = Math.Min(1, quantity / available);
        var offers = _offers[productId];
        foreach (var sellerId in offers.Keys.ToList())
        {
            taken[sellerId] = offers[sellerId] * share;
            offers[sellerId] -= taken[sellerId];
        }

        return taken;
    }

    /// <summary>Ne garde qu'une part de chaque offre (le reste est perdu, par exemple faute de place pour stocker).</summary>
    public void Keep(double share)
    {
        foreach (var offers in _offers.Values)
        {
            foreach (var sellerId in offers.Keys.ToList())
                offers[sellerId] *= share;
        }
    }

    /// <summary>Renvoie l'argent gagné par chaque vendeur depuis le dernier versement, puis le remet à zéro.</summary>
    public IReadOnlyDictionary<string, double> CollectEarnings()
    {
        var earnings = new Dictionary<string, double>(_earnings);
        _earnings.Clear();
        return earnings;
    }
}
