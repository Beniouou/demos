using Demos.Domain.Politics;

namespace Demos.Domain.Economy;

/// <summary>Sorte de stockage : ce qu'un bâtiment de stockage accepte.</summary>
public enum StorageKind
{
    /// <summary>Accepte tous les produits (le camp de base, le dépôt logistique).</summary>
    General,

    /// <summary>Produits alimentaires et périssables seulement (le grenier).</summary>
    Food,

    /// <summary>Produits non alimentaires et non périssables seulement (l'entrepôt).</summary>
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

    internal IEnumerable<Structure> Buildings => _buildings;
}

/// <summary>
/// Une commune ne garde que ce que ses bâtiments de stockage contiennent : tous les produits de son marché, ceux
/// des classes comme ceux de l'État. Chaque bâtiment n'accepte que sa sorte de produits (général, alimentaire, non
/// alimentaire, ou un produit précis), et peut limiter la quantité de chaque produit (sans limite par défaut).
/// Les produits sont rangés d'abord dans les stockages spécifiques, puis alimentaires et non alimentaires, puis
/// généraux. En fin de journée, ce qui n'a pas trouvé de place est perdu. Les produits périssables (pain, farine,
/// alcool…) se perdent aussi peu à peu : chaque jour, un bâtiment en perd 1 ÷ leur durée de vie, durée qui est
/// triplée dans un stockage spécifique et doublée dans un grenier. Les produits déjà chez les habitants ne comptent pas.
/// </summary>
public static class Storage
{
    public static int CapacityOf(string communeId, IReadOnlyDictionary<string, Structure> structures) =>
        StorageBuildings(communeId, structures).Sum(s => s.StorageCapacity);

    public static bool Accepts(Structure building, ProductDefinition? product, string productId) => building.StorageKind switch
    {
        StorageKind.Food => IsFood(product),
        StorageKind.NonFood => !IsFood(product),
        StorageKind.Specific => building.StorageProductId == productId,
        _ => true,
    };

    /// <summary>Ce que le grenier accepte : ce qui nourrit, et tout ce qui se périme.</summary>
    public static bool IsFood(ProductDefinition? product) => product is not null && (product.Need == NeedCategory.Food || product.IsPerishable);

    /// <summary>Durée de vie d'un produit périssable dans un bâtiment : triplée en stockage spécifique, doublée au grenier.</summary>
    public static double ShelfLifeIn(Structure building, ProductDefinition product) =>
        (product.ShelfLifeDays ?? double.PositiveInfinity) * building.StorageKind switch
        {
            StorageKind.Specific => 3,
            StorageKind.Food => 2,
            _ => 1,
        };

    public static double LimitOf(Structure building, string productId) =>
        building.StorageLimits is { } limits && limits.TryGetValue(productId, out var limit) ? limit : double.PositiveInfinity;

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
    /// produit, et les produits périssables perdent une part selon leur durée de vie dans le bâtiment qui les garde.
    /// Renvoie le rangement de chaque commune, pour l'affichage.
    /// </summary>
    public static Dictionary<string, StorageAllocation> ApplyDaily(
        IEnumerable<string> communeIds,
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<ProductDefinition> products,
        Func<string, Market> marketOf)
    {
        var result = new Dictionary<string, StorageAllocation>();
        foreach (var communeId in communeIds)
        {
            var market = marketOf(communeId);
            // Ce qui ne se stocke pas (l'eau) est perdu le soir même, sans prendre de place.
            foreach (var product in products.Where(p => !p.Stockable))
                market.Keep(product.Id, 0);

            var held = new Dictionary<string, double>(market.Contents);
            var allocation = Allocate(communeId, structures, products, held);

            foreach (var (productId, quantity) in held)
            {
                // Ce qui a trouvé une place, moins ce qui se périme dans chaque bâtiment.
                var stored = allocation.Stored.GetValueOrDefault(productId);
                if (products.FirstOrDefault(p => p.Id == productId) is { IsPerishable: true } product)
                {
                    foreach (var building in allocation.Buildings)
                        stored -= allocation.In(building.Id).GetValueOrDefault(productId) / ShelfLifeIn(building, product);
                }

                var kept = quantity > 0 ? Math.Clamp(stored / quantity, 0, 1) : 1;
                if (kept >= 1)
                    continue;

                market.Keep(productId, kept);
            }

            result[communeId] = allocation;
        }

        return result;
    }

    private static IEnumerable<Structure> StorageBuildings(string communeId, IReadOnlyDictionary<string, Structure> structures) =>
        structures.Values.Where(s => s.StorageCapacity > 0 && Communes.CommuneOf(s, structures) == communeId);
}
