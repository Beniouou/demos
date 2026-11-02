using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

public class PerishableTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("pain", "Pain", NeedCategory.Food, 1, ShelfLifeDays: 10),
        new("ble", "Blé", null, 0.4, ShelfLifeDays: 180),
        new("pierre", "Pierre", null, 3),
    ];

    private static Market StoreIn(Structure storage, string productId, double quantity)
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures) { [storage.Id] = storage };
        var market = new Market(Products);
        market.Offer(productId, quantity, "vendeur");
        Storage.ApplyDaily(["commune"], structures, Products, _ => market);
        return market;
    }

    private static Structure Storing(StorageKind kind, string? productId = null) =>
        new("stockage", "Stockage", StructureKind.Building, "commune", StorageCapacity: 10_000, StorageKind: kind, StorageProductId: productId);

    [Fact]
    public void Le_pain_se_perime_plus_vite_dans_un_stockage_general_que_dans_un_grenier_ou_un_silo()
    {
        // Durée de vie du pain : 10 jours au camp de base, 20 au grenier, 30 dans un silo à pain.
        Assert.Equal(900, StoreIn(Storing(StorageKind.General), "pain", 1_000).QuantityOf("pain"), precision: 6);
        Assert.Equal(950, StoreIn(Storing(StorageKind.Food), "pain", 1_000).QuantityOf("pain"), precision: 6);
        Assert.Equal(1_000 - 1_000 / 30.0, StoreIn(Storing(StorageKind.Specific, "pain"), "pain", 1_000).QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Un_produit_qui_ne_se_perime_pas_ne_perd_rien()
    {
        Assert.Equal(1_000, StoreIn(Storing(StorageKind.General), "pierre", 1_000).QuantityOf("pierre"), precision: 6);
    }

    [Fact]
    public void Le_grenier_accepte_le_ble_et_l_entrepot_le_refuse()
    {
        var ble = Products[1];

        Assert.True(Storage.Accepts(Storing(StorageKind.Food), ble, "ble"));
        Assert.False(Storage.Accepts(Storing(StorageKind.NonFood), ble, "ble"));
        Assert.Equal(0, StoreIn(Storing(StorageKind.NonFood), "ble", 100).QuantityOf("ble"), precision: 6);
    }
}
