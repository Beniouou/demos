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

    // Compteurs depuis le début de la partie, pour les statistiques ; ils ne changent rien au marché.
    // Produit → quantité sortie pour être utilisée (achetée, ou reprise par son vendeur).
    private readonly Dictionary<string, double> _used = [];

    // Produit → quantité retirée pour partir vers une autre commune.
    private readonly Dictionary<string, double> _sent = [];

    // Produit → quantité perdue (faute de place pour stocker, ou périmée).
    private readonly Dictionary<string, double> _lost = [];

    /// <summary>Nom de vendeur de l'État dans une commune : ses ventes vont à la caisse de cette commune.</summary>
    public static string StateSellerId(string communeId) => $"etat:{communeId}";

    /// <summary>Nom de vendeur d'une entreprise privée ou coopérative : ses ventes vont à la caisse de l'entreprise.</summary>
    public static string CompanySellerId(string buildingId) => $"entreprise:{buildingId}";

    /// <summary>Le bâtiment dont l'entreprise est ce vendeur, ou null si le vendeur n'est pas une entreprise.</summary>
    public static string? BuildingOfCompanySeller(string sellerId) => sellerId.StartsWith("entreprise:") ? sellerId["entreprise:".Length..] : null;

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

    // Produit → vendeur → quantité gardée hors vente (la réserve des bâtiments publics, voir SupplySettings) : ni les
    // acheteurs ni les échanges ne la prennent ; le vendeur lui-même et ses lignes d'approvisionnement, si.
    private readonly Dictionary<string, Dictionary<string, double>> _held = [];

    // Produit → vendeurs dont les échanges entre communes ne prennent rien.
    private readonly Dictionary<string, HashSet<string>> _notExported = [];

    /// <summary>Garde une quantité d'un vendeur hors vente (0 : plus rien de gardé).</summary>
    public void SetHeld(string productId, string sellerId, double quantity)
    {
        if (quantity > 0)
        {
            if (!_held.TryGetValue(productId, out var held))
                _held[productId] = held = [];
            held[sellerId] = quantity;
        }
        else if (_held.TryGetValue(productId, out var held))
        {
            held.Remove(sellerId);
            if (held.Count == 0)
                _held.Remove(productId);
        }
    }

    /// <summary>Ce qu'un vendeur garde hors vente de ce qu'il a, au plus tout ce qu'il a.</summary>
    public double HeldOf(string productId, string sellerId) =>
        _held.TryGetValue(productId, out var held) ? Math.Min(held.GetValueOrDefault(sellerId), QuantityOf(productId, sellerId)) : 0;

    /// <summary>Les échanges entre communes ne prennent plus (ou de nouveau) ce produit chez ce vendeur.</summary>
    public void SetNotExported(string productId, string sellerId, bool notExported)
    {
        if (notExported)
        {
            if (!_notExported.TryGetValue(productId, out var sellers))
                _notExported[productId] = sellers = [];
            sellers.Add(sellerId);
        }
        else if (_notExported.TryGetValue(productId, out var sellers))
        {
            sellers.Remove(sellerId);
            if (sellers.Count == 0)
                _notExported.Remove(productId);
        }
    }

    private double ForSale(string productId, string sellerId) => QuantityOf(productId, sellerId) - HeldOf(productId, sellerId);

    /// <summary>
    /// Ce qui est à vendre d'un produit : chez tous les vendeurs sauf <paramref name="exceptSellerId"/>, parmi ceux que
    /// <paramref name="allowed"/> retient, sans ce qu'ils gardent hors vente.
    /// </summary>
    public double QuantityForSale(string productId, string? exceptSellerId = null, Func<string, bool>? allowed = null)
    {
        if (!_held.ContainsKey(productId) && allowed is null)
            return exceptSellerId is null ? QuantityOf(productId) : QuantityOfOthers(productId, exceptSellerId);
        return _offers.TryGetValue(productId, out var offers)
            ? offers.Keys.Where(s => s != exceptSellerId && (allowed?.Invoke(s) ?? true)).Sum(s => ForSale(productId, s))
            : 0;
    }

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
    /// Achète une quantité, prise chez les vendeurs au prorata de ce qu'ils ont à vendre (sans ce qu'ils gardent hors
    /// vente), parmi ceux que <paramref name="allowed"/> retient. Le paiement de l'acheteur est à la charge de
    /// l'appelant. Renvoie false, sans rien acheter, si l'offre est insuffisante.
    /// </summary>
    public bool TryBuy(string productId, double quantity, string? exceptSellerId = null, Func<string, bool>? allowed = null)
    {
        var available = QuantityForSale(productId, exceptSellerId, allowed);
        if (quantity <= 0)
            return true;
        if (available < quantity)
            return false;

        var offers = _offers[productId];
        var simple = !_held.ContainsKey(productId) && allowed is null;
        foreach (var sellerId in offers.Keys.Where(s => s != exceptSellerId && (allowed?.Invoke(s) ?? true)).ToList())
        {
            var taken = quantity * (simple ? offers[sellerId] : ForSale(productId, sellerId)) / available;
            offers[sellerId] -= taken;
            _earnings[sellerId] = _earnings.GetValueOrDefault(sellerId) + CostOf(productId, taken);
        }

        Count(_used, productId, quantity);
        return true;
    }

    /// <summary>
    /// Retire des produits du marché sans les payer, pris chez les vendeurs au prorata de leur offre (pour les
    /// envoyer dans une autre commune), sans ce qu'ils gardent hors vente ni rien chez ceux qui refusent l'envoi.
    /// Renvoie la quantité retirée à chaque vendeur.
    /// </summary>
    public Dictionary<string, double> Take(string productId, double quantity)
    {
        var taken = new Dictionary<string, double>();
        var simple = !_held.ContainsKey(productId) && !_notExported.ContainsKey(productId);
        var kept = _notExported.GetValueOrDefault(productId);
        var available = simple ? QuantityOf(productId) : QuantityForSale(productId, allowed: s => kept?.Contains(s) != true);
        if (quantity <= 0 || available <= 0)
            return taken;

        var share = Math.Min(1, quantity / available);
        var offers = _offers[productId];
        foreach (var sellerId in offers.Keys.Where(s => kept?.Contains(s) != true).ToList())
        {
            taken[sellerId] = (simple ? offers[sellerId] : ForSale(productId, sellerId)) * share;
            offers[sellerId] -= taken[sellerId];
        }

        Count(_sent, productId, taken.Values.Sum());
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
        Count(_used, productId, quantity);
        return true;
    }

    /// <summary>
    /// Un vendeur cède une partie de ses propres produits par une ligne d'approvisionnement : ils quittent le marché, le
    /// vendeur gagne <paramref name="payment"/> (versé avec ses ventes). Comptés comme envoyés s'ils partent vers une autre
    /// commune, sinon comme utilisés. Renvoie false, sans rien céder, si le vendeur n'en a pas assez.
    /// </summary>
    public bool TryDeliver(string productId, double quantity, string sellerId, double payment, bool toOtherCommune)
    {
        if (quantity <= 0)
            return true;
        if (QuantityOf(productId, sellerId) < quantity)
            return false;

        _offers[productId][sellerId] -= quantity;
        if (payment > 0)
            _earnings[sellerId] = _earnings.GetValueOrDefault(sellerId) + payment;
        Count(toOtherCommune ? _sent : _used, productId, quantity);
        return true;
    }

    /// <summary>Ne garde qu'une part des offres d'un produit (le reste est perdu, par exemple faute de place pour stocker).</summary>
    public void Keep(string productId, double share)
    {
        if (!_offers.TryGetValue(productId, out var offers))
            return;

        Count(_lost, productId, offers.Values.Sum() * (1 - share));
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

    /// <summary>
    /// Ce qui est sorti du marché depuis le début de la partie, pour les statistiques : utilisé (acheté par les habitants,
    /// pris comme matière ou énergie par un bâtiment, pour un chantier), envoyé vers une autre commune, ou perdu.
    /// </summary>
    public MarketTally TallyOf(string productId) =>
        new(_used.GetValueOrDefault(productId), _sent.GetValueOrDefault(productId), _lost.GetValueOrDefault(productId));

    private static void Count(Dictionary<string, double> tally, string productId, double quantity)
    {
        if (quantity > 0)
            tally[productId] = tally.GetValueOrDefault(productId) + quantity;
    }
}

/// <summary>Ce qui est sorti du marché d'une commune pour un produit, depuis le début de la partie.</summary>
public readonly record struct MarketTally(double Used, double Sent, double Lost);
