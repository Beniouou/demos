namespace Demos.Domain.Economy;

/// <summary>
/// Marché et stockage d'une commune. Toute la production de la commune y arrive : celle des structures privées et
/// coopératives, au nom de leur classe propriétaire, et celle des structures publiques, au nom de l'État de la commune.
/// Les habitants y achètent tout, y compris ce que l'État a produit ; le vendeur est payé quand son produit est
/// acheté. L'État peut aussi y reprendre gratuitement ce qu'il a produit (pour construire) et acheter le reste.
/// Pour l'instant, les prix restent les prix de départ.
/// </summary>
public sealed class Market(IEnumerable<ProductDefinition> products)
{
    private readonly Dictionary<string, double> _prices = products.ToDictionary(p => p.Id, p => p.BasePrice);

    // Produit → vendeur → quantité en vente.
    private readonly Dictionary<string, Dictionary<string, double>> _offers = [];

    // Vendeur → argent gagné depuis le dernier versement.
    private readonly Dictionary<string, double> _earnings = [];

    /// <summary>Nom de vendeur de l'État dans une commune : ses ventes vont à la caisse de cette commune.</summary>
    public static string StateSellerId(string communeId) => $"etat:{communeId}";

    /// <summary>La commune dont l'État est ce vendeur, ou null si le vendeur n'est pas l'État.</summary>
    public static string? CommuneOfStateSeller(string sellerId) => sellerId.StartsWith("etat:") ? sellerId["etat:".Length..] : null;

    public double PriceOf(string productId) => _prices.GetValueOrDefault(productId);

    /// <summary>Fixe le prix d'un produit à la main, en attendant la formation automatique des prix.</summary>
    public void SetPrice(string productId, double price) => _prices[productId] = Math.Max(0, price);

    public double CostOf(string productId, double quantity) => quantity * PriceOf(productId);

    public double QuantityOf(string productId) =>
        _offers.TryGetValue(productId, out var offers) ? offers.Values.Sum() : 0;

    /// <summary>Quantité d'un produit en vente chez un vendeur.</summary>
    public double QuantityOf(string productId, string sellerId) =>
        _offers.TryGetValue(productId, out var offers) ? offers.GetValueOrDefault(sellerId) : 0;

    /// <summary>Quantité d'un produit en vente chez les autres vendeurs.</summary>
    public double QuantityOfOthers(string productId, string sellerId) => QuantityOf(productId) - QuantityOf(productId, sellerId);

    /// <summary>Quantités d'un vendeur, par produit.</summary>
    public IReadOnlyDictionary<string, double> ContentsOf(string sellerId) =>
        _offers.Where(o => o.Value.GetValueOrDefault(sellerId) > 0).ToDictionary(o => o.Key, o => o.Value[sellerId]);

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
    public bool TryBuy(string productId, double quantity, string? exceptSellerId = null)
    {
        var available = exceptSellerId is null ? QuantityOf(productId) : QuantityOfOthers(productId, exceptSellerId);
        if (quantity <= 0)
            return true;
        if (available < quantity)
            return false;

        var offers = _offers[productId];
        foreach (var sellerId in offers.Keys.Where(s => s != exceptSellerId).ToList())
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

    /// <summary>Un vendeur reprend une partie de ses propres produits, sans paiement. Renvoie false si ce n'est pas possible.</summary>
    public bool TryWithdraw(string productId, double quantity, string sellerId)
    {
        if (quantity <= 0)
            return true;
        if (QuantityOf(productId, sellerId) < quantity)
            return false;

        _offers[productId][sellerId] -= quantity;
        return true;
    }

    /// <summary>Ne garde qu'une part des offres d'un produit (le reste est perdu, par exemple faute de place pour stocker).</summary>
    public void Keep(string productId, double share)
    {
        if (!_offers.TryGetValue(productId, out var offers))
            return;

        foreach (var sellerId in offers.Keys.ToList())
            offers[sellerId] *= share;
    }

    /// <summary>Renvoie l'argent gagné par chaque vendeur depuis le dernier versement, puis le remet à zéro.</summary>
    public IReadOnlyDictionary<string, double> CollectEarnings()
    {
        var earnings = new Dictionary<string, double>(_earnings);
        _earnings.Clear();
        return earnings;
    }
}
