using Demos.Domain.Politics;

namespace Demos.Domain.Economy;

/// <summary>Sorte de stockage : ce qu'un bâtiment de stockage accepte.</summary>
public enum StorageKind
{
    /// <summary>Accepte tous les produits (le camp de base, le dépôt logistique).</summary>
    General,

    /// <summary>Produits alimentaires seulement (le grenier).</summary>
    Food,

    /// <summary>Produits non alimentaires seulement (l'entrepôt).</summary>
    NonFood,

    /// <summary>Un seul produit, choisi pour le bâtiment (un silo à pierre, par exemple).</summary>
    Specific,
}

/// <summary>Ce que le stockage d'une commune contient, bâtiment par bâtiment et produit par produit.</summary>
public sealed class StorageAllocation
{
    private readonly Dictionary<string, Dictionary<string, double>> _byBuilding = [];
    private readonly Dictionary<string, double> _freeByBuilding = [];
    private readonly List<Structure> _buildings;
    private readonly Func<string, ProductDefinition?> _productOf;

    internal StorageAllocation(List<Structure> buildings, Func<string, ProductDefinition?> productOf)
    {
        _buildings = buildings;
        _productOf = productOf;
        foreach (var building in buildings)
        {
            _byBuilding[building.Id] = [];
            _freeByBuilding[building.Id] = building.StorageCapacity;
        }
    }

    /// <summary>Quantité de chaque produit rangée dans un bâtiment.</summary>
    public IReadOnlyDictionary<string, double> In(string buildingId) =>
        _byBuilding.TryGetValue(buildingId, out var contents) ? contents : new Dictionary<string, double>();

    /// <summary>Quantité de chaque produit qui a trouvé une place dans la commune.</summary>
    public Dictionary<string, double> Stored { get; } = [];

    /// <summary>Place encore libre pour un produit, en tenant compte des sortes de stockage et des limites.</summary>
    public double FreeFor(string productId) => _buildings
        .Where(b => Storage.Accepts(b, _productOf(productId), productId))
        .Sum(b => Math.Min(_freeByBuilding[b.Id], Storage.LimitOf(b, productId) - In(b.Id).GetValueOrDefault(productId)));

    internal void Put(Structure building, string productId, double quantity)
    {
        var contents = _byBuilding[building.Id];
        contents[productId] = contents.GetValueOrDefault(productId) + quantity;
        _freeByBuilding[building.Id] -= quantity;
        Stored[productId] = Stored.GetValueOrDefault(productId) + quantity;
    }

    internal double FreeIn(Structure building) => _freeByBuilding[building.Id];
}

/// <summary>
/// Une commune ne garde que ce que ses bâtiments de stockage contiennent : produits en vente sur son marché et
/// stock de l'État confondus. Chaque bâtiment n'accepte que sa sorte de produits (général, alimentaire, non
/// alimentaire, ou un produit précis), et peut limiter la quantité de chaque produit (sans limite par défaut).
/// Les produits sont rangés d'abord dans les stockages spécifiques, puis alimentaires et non alimentaires, puis
/// généraux. En fin de journée, ce qui n'a pas trouvé de place est perdu. Les produits déjà chez les habitants
/// ne comptent pas.
/// </summary>
public static class Storage
{
    public static int CapacityOf(string communeId, IReadOnlyDictionary<string, Structure> structures) =>
        StorageBuildings(communeId, structures).Sum(s => s.StorageCapacity);

    public static bool Accepts(Structure building, ProductDefinition? product, string productId) => building.StorageKind switch
    {
        StorageKind.Food => product?.Need == NeedCategory.Food,
        StorageKind.NonFood => product?.Need != NeedCategory.Food,
        StorageKind.Specific => building.StorageProductId == productId,
        _ => true,
    };

    public static double LimitOf(Structure building, string productId) =>
        building.StorageLimits is { } limits && limits.TryGetValue(productId, out var limit) ? limit : double.PositiveInfinity;

    /// <summary>Quantité de chaque produit présente dans une commune : en vente sur son marché et dans le stock de l'État.</summary>
    public static Dictionary<string, double> HeldIn(Market market, Inventory stateStock)
    {
        var held = new Dictionary<string, double>(market.Contents);
        foreach (var (productId, quantity) in stateStock.Contents)
            held[productId] = held.GetValueOrDefault(productId) + quantity;
        return held;
    }

    /// <summary>Range les produits présents dans les bâtiments de stockage de la commune.</summary>
    public static StorageAllocation Allocate(
        string communeId,
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<ProductDefinition> products,
        IReadOnlyDictionary<string, double> held)
    {
        ProductDefinition? ProductOf(string id) => products.FirstOrDefault(p => p.Id == id);

        var buildings = StorageBuildings(communeId, structures)
            .OrderBy(b => b.StorageKind switch { StorageKind.Specific => 0, StorageKind.General => 2, _ => 1 })
            .ToList();
        var allocation = new StorageAllocation(buildings, ProductOf);
        var remaining = held.Where(h => h.Value > 0).ToDictionary(h => h.Key, h => h.Value);

        foreach (var building in buildings)
        {
            // Plusieurs passes : la place laissée par un produit limité revient aux autres.
            for (var pass = 0; pass < 5; pass++)
            {
                var free = allocation.FreeIn(building);
                var rooms = remaining
                    .Where(r => r.Value > 1e-9 && Accepts(building, ProductOf(r.Key), r.Key))
                    .ToDictionary(r => r.Key, r => Math.Min(r.Value, LimitOf(building, r.Key) - allocation.In(building.Id).GetValueOrDefault(r.Key)))
                    .Where(r => r.Value > 1e-9)
                    .ToList();
                var totalRoom = rooms.Sum(r => r.Value);
                if (free <= 1e-9 || totalRoom <= 1e-9)
                    break;

                foreach (var (productId, room) in rooms)
                {
                    var put = Math.Min(room, free * room / totalRoom);
                    allocation.Put(building, productId, put);
                    remaining[productId] -= put;
                }
            }
        }

        return allocation;
    }

    /// <summary>
    /// Fin de journée : ce qui n'a pas trouvé de place dans le stockage de chaque commune est perdu, produit par
    /// produit. Renvoie le rangement de chaque commune, pour l'affichage.
    /// </summary>
    public static Dictionary<string, StorageAllocation> ApplyDaily(
        IEnumerable<string> communeIds,
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<ProductDefinition> products,
        Func<string, Market> marketOf,
        Func<string, Inventory> stateStockOf)
    {
        var result = new Dictionary<string, StorageAllocation>();
        foreach (var communeId in communeIds)
        {
            var market = marketOf(communeId);
            var stock = stateStockOf(communeId);
            var held = HeldIn(market, stock);
            var allocation = Allocate(communeId, structures, products, held);

            foreach (var (productId, quantity) in held)
            {
                var kept = quantity > 0 ? Math.Min(1, allocation.Stored.GetValueOrDefault(productId) / quantity) : 1;
                if (kept >= 1)
                    continue;

                market.Keep(productId, kept);
                stock.Keep(productId, kept);
            }

            result[communeId] = allocation;
        }

        return result;
    }

    private static IEnumerable<Structure> StorageBuildings(string communeId, IReadOnlyDictionary<string, Structure> structures) =>
        structures.Values.Where(s => s.StorageCapacity > 0 && Communes.CommuneOf(s, structures) == communeId);
}
