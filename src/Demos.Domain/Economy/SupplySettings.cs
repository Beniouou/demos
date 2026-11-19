using Demos.Domain.Politics;

namespace Demos.Domain.Economy;

/// <summary>
/// Les réglages d'achat et de vente d'un bâtiment public (décidés le 19 novembre 2026) : n'acheter que par ses lignes
/// d'approvisionnement, ne pas acheter aux entreprises privées ou aux SCOP (si les lois le permettent, brique « Choix
/// des fournisseurs publics »), garder N jours de sa production hors vente, ne pas laisser les échanges entre communes
/// prendre son produit. Le privé et les SCOP n'en ont pas : ils décident seuls. Sans réglage, rien ne change.
/// </summary>
public sealed record SupplySettings(
    bool OnlyFromLines = false,
    bool RefusePrivate = false,
    bool RefuseCooperative = false,
    int ReserveDays = 0,
    bool NoExport = false)
{
    public static readonly SupplySettings None = new();

    /// <summary>Proposition : la réserve hors vente va jusqu'à 30 jours de production.</summary>
    public const int MaxReserveDays = 30;

    public bool IsNone => this == None;

    /// <summary>
    /// Ce que le bâtiment s'autorise à acheter : rien au marché s'il n'achète que par ses lignes, sinon pas chez les
    /// vendeurs qu'il refuse, quand les lois lui permettent de les refuser. Un bâtiment qui n'est pas public achète
    /// librement.
    /// </summary>
    public PurchaseRules PurchaseRulesFor(Structure building, IReadOnlyDictionary<string, Structure> structures)
    {
        if (building.Ownership != Ownership.Public || IsNone)
            return PurchaseRules.Free;
        var refusePrivate = RefusePrivate && building.Rules.MayRefusePrivate;
        var refuseCooperative = RefuseCooperative && building.Rules.MayRefuseCooperative;
        if (!refusePrivate && !refuseCooperative)
            return new PurchaseRules(!OnlyFromLines);
        return new PurchaseRules(!OnlyFromLines, seller => SellerKind(seller, structures) switch
        {
            Ownership.Private => !refusePrivate,
            Ownership.Cooperative => !refuseCooperative,
            _ => true,
        });
    }

    /// <summary>La sorte d'un vendeur du marché : l'État, une entreprise (privée ou SCOP, d'après son bâtiment), ou une classe (privé).</summary>
    public static Ownership SellerKind(string sellerId, IReadOnlyDictionary<string, Structure> structures) =>
        Market.CommuneOfStateSeller(sellerId) is not null ? Ownership.Public
        : Market.BuildingOfCompanySeller(sellerId) is { } buildingId && structures.TryGetValue(buildingId, out var building) ? building.Ownership
        : Ownership.Private;

    /// <summary>
    /// Applique les réglages de vente aux marchés, chaque jour après le travail : pour l'État de chaque commune et chaque
    /// produit, la réserve hors vente (la somme, sur ses bâtiments publics qui le produisent, de leurs jours de réserve
    /// fois leur production du jour) et le refus d'envoi (dès qu'un de ces bâtiments le demande). Renvoie les couples
    /// (commune, produit) réglés, pour remettre à zéro le lendemain ceux qui ne le sont plus.
    /// </summary>
    public static HashSet<(string Commune, string Product)> ApplySales(
        IReadOnlyDictionary<string, Structure> structures,
        Func<string, SupplySettings> settingsOf,
        IReadOnlyDictionary<string, double> produced,
        Func<string, Market> marketOf,
        IReadOnlySet<(string Commune, string Product)> previous)
    {
        var held = new Dictionary<(string, string), double>();
        var notExported = new HashSet<(string, string)>();
        foreach (var building in structures.Values)
        {
            if (building.Ownership != Ownership.Public || building.Production is not { } production)
                continue;
            var settings = settingsOf(building.Id);
            if (settings.ReserveDays <= 0 && !settings.NoExport)
                continue;
            if (Communes.CommuneOf(building, structures) is not { } communeId)
                continue;
            var key = (communeId, production.ProductId);
            if (settings.ReserveDays > 0)
                held[key] = held.GetValueOrDefault(key) + settings.ReserveDays * produced.GetValueOrDefault(building.Id);
            if (settings.NoExport)
                notExported.Add(key);
        }

        var applied = new HashSet<(string, string)>(held.Keys.Concat(notExported));
        foreach (var key in applied.Concat(previous).Distinct())
        {
            var (communeId, productId) = key;
            var market = marketOf(communeId);
            var seller = Market.StateSellerId(communeId);
            market.SetHeld(productId, seller, held.GetValueOrDefault(key));
            market.SetNotExported(productId, seller, notExported.Contains(key));
        }
        return applied;
    }
}
