using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Tests.Domain;

public class EnergyTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("bois", "Bois", null, 2),
        new("charbon", "Charbon", null, 1),
        new("eau", "Eau", null, 0.1, Stockable: false),
    ];

    private static Dictionary<string, Structure> TownWith(Structure building) => new(TestTown.Structures) { [building.Id] = building };

    private static Structure Housing(string? heating = null) =>
        new("logements", "Logements", StructureKind.Building, "commune", HousingPlaces: 1_000, DailyRent: 1, HeatingProductId: heating);

    private static Dictionary<string, double> Run(Structure building, Market market, Season season, Treasury treasury) =>
        Energy.RunDaily(TownWith(building), s => s.Id == building.Id ? 1 : 0, season, _ => market, treasury, null, null, new Dictionary<string, SocialClass>());

    [Fact]
    public void Un_logement_se_chauffe_et_prend_son_eau_et_le_chauffage_double_en_hiver()
    {
        var market = new Market(Products);
        market.Offer("bois", 100, "camp");
        market.Offer("eau", 1_000, "puits");
        var treasury = new Treasury();
        treasury.Deposit(1_000);

        var shares = Run(Housing(), market, Season.Winter, treasury);

        // 1 000 places : 20 bois en hiver (0,01 × 2) et 100 eaux, payés par le trésor (logement public).
        Assert.Equal(1, shares["logements"], precision: 6);
        Assert.Equal(80, market.QuantityOf("bois"), precision: 6);
        Assert.Equal(900, market.QuantityOf("eau"), precision: 6);
        Assert.Equal(1_000 - 40 - 10, treasury.Balance, precision: 6);
    }

    [Fact]
    public void En_ete_on_ne_chauffe_pas_et_le_charbon_peut_remplacer_le_bois()
    {
        var market = new Market(Products);
        market.Offer("charbon", 100, "mine");
        market.Offer("eau", 1_000, "puits");
        var treasury = new Treasury();
        treasury.Deposit(1_000);

        Run(Housing("charbon"), market, Season.Summer, treasury);
        Assert.Equal(100, market.QuantityOf("charbon"), precision: 6);

        Run(Housing("charbon"), market, Season.Spring, treasury);
        Assert.Equal(90, market.QuantityOf("charbon"), precision: 6);
    }

    [Fact]
    public void Un_logement_sans_assez_d_energie_n_offre_qu_une_partie_de_ses_places()
    {
        var market = new Market(Products);
        market.Offer("bois", 100, "camp");
        market.Offer("eau", 50, "puits");
        var treasury = new Treasury();
        treasury.Deposit(1_000);
        var building = Housing();

        var shares = Run(building, market, Season.Winter, treasury);

        // La moitié de l'eau nécessaire : la moitié des places.
        Assert.Equal(0.5, shares["logements"], precision: 6);
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 1_000) { Wealth = 10_000 };
        workers.SetResidence("commune", 1);
        Demos.Domain.Economy.Housing.RunDaily(TownWith(building), [workers], treasury, energyShareOf: s => shares.GetValueOrDefault(s.Id, 1));
        Assert.Equal(0.5, workers.HousingCoverage, precision: 6);
    }

    [Fact]
    public void L_eau_ne_se_stocke_pas()
    {
        var market = new Market(Products);
        market.Offer("eau", 1_000, "puits");
        var structures = new Dictionary<string, Structure>(TestTown.Structures)
        {
            ["camp"] = new("camp", "Camp", StructureKind.Building, "commune", StorageCapacity: 10_000),
        };

        Storage.ApplyDaily(["commune"], structures, Products, _ => market);

        Assert.Equal(0, market.QuantityOf("eau"), precision: 6);
    }

    [Fact]
    public void Un_batiment_de_la_liste_2_n_a_pas_encore_besoin_de_chauffage_ni_d_eau()
    {
        Assert.Empty(Energy.NeedsOf(Housing(), tier: 2, Season.Winter));
    }
}
