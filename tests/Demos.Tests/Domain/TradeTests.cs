using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class TradeTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("pain", "Pain", NeedCategory.Food, 1),
        new("bois", "Bois", null, 2),
        new("pierre", "Pierre", null, 3),
    ];

    private static SocialClass Residents(string communeId, long population)
    {
        var residents = new SocialClass($"ouvriers-{communeId}", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), population);
        residents.SetResidence(communeId, 1);
        return residents;
    }

    [Fact]
    public void Les_surplus_partent_vers_la_commune_qui_manque_avec_une_perte_de_20_pourcent()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 80)];

        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], (_, _) => 10_000);

        // La commune b manque de 80 pains : il en part 100 de a, 80 arrivent. Le vendeur reste le même.
        Assert.Equal(80, markets["b"].QuantityOf("pain"), precision: 6);
        Assert.Equal(900, markets["a"].QuantityOf("pain"), precision: 6);
        Assert.True(markets["b"].TryBuy("pain", 80));
        Assert.Equal(80, markets["b"].CollectEarnings()["ferme"], precision: 6);
    }

    [Fact]
    public void Une_commune_ne_recoit_que_dans_la_limite_de_sa_place_libre()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 300)];

        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], (c, _) => c == "b" ? 50 : 10_000);

        Assert.Equal(50, markets["b"].QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Sans_surplus_rien_ne_part()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 100, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 80)];

        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], (_, _) => 10_000);

        Assert.Equal(0, markets["b"].QuantityOf("pain"));
    }

    [Fact]
    public void Ce_qui_depasse_le_stockage_est_perdu_en_fin_de_journee()
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures)
        {
            ["entrepot"] = new("entrepot", "Entrepôt", StructureKind.Building, "commune", StorageCapacity: 300),
        };
        var market = new Market(Products);
        market.Offer("pain", 400, "ferme");
        market.Offer("bois", 200, Market.StateSellerId("commune"));

        Storage.ApplyDaily(["commune"], structures, Products, _ => market);

        // 600 produits pour 300 places : chacun garde la moitié.
        Assert.Equal(200, market.QuantityOf("pain"), precision: 6);
        Assert.Equal(100, market.QuantityOf("bois", Market.StateSellerId("commune")), precision: 6);
    }

    private static Dictionary<string, Structure> TownWith(params Structure[] storages)
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures);
        foreach (var storage in storages)
            structures[storage.Id] = storage;
        return structures;
    }

    [Fact]
    public void Le_pain_ne_peut_pas_chasser_la_pierre_d_un_entrepot_non_alimentaire()
    {
        var structures = TownWith(
            new Structure("grenier", "Grenier", StructureKind.Building, "commune", StorageCapacity: 100, StorageKind: StorageKind.Food),
            new Structure("entrepot", "Entrepôt", StructureKind.Building, "commune", StorageCapacity: 300, StorageKind: StorageKind.NonFood));
        var market = new Market(Products);
        market.Offer("pain", 1_000, "ferme");
        market.Offer("pierre", 200, "carriere");

        Storage.ApplyDaily(["commune"], structures, Products, _ => market);

        // Le pain n'a que les 100 places du grenier ; la pierre tient dans l'entrepôt.
        Assert.Equal(100, market.QuantityOf("pain"), precision: 6);
        Assert.Equal(200, market.QuantityOf("pierre"), precision: 6);
    }

    [Fact]
    public void Un_stockage_specifique_ne_prend_que_son_produit_et_passe_en_premier()
    {
        var structures = TownWith(
            new Structure("silo", "Silo", StructureKind.Building, "commune", StorageCapacity: 100, StorageKind: StorageKind.Specific, StorageProductId: "pierre"),
            new Structure("camp", "Camp", StructureKind.Building, "commune", StorageCapacity: 100));
        var market = new Market(Products);
        market.Offer("pierre", 100, "carriere");
        market.Offer("bois", 100, "foret");

        Storage.ApplyDaily(["commune"], structures, Products, _ => market);

        // La pierre remplit le silo, le stockage général reste au bois.
        Assert.Equal(100, market.QuantityOf("pierre"), precision: 6);
        Assert.Equal(100, market.QuantityOf("bois"), precision: 6);
    }

    [Fact]
    public void Une_limite_par_produit_laisse_la_place_aux_autres()
    {
        var limits = new Dictionary<string, double> { ["bois"] = 50 };
        var structures = TownWith(new Structure("depot", "Dépôt", StructureKind.Building, "commune", StorageCapacity: 300, StorageLimits: limits));
        var market = new Market(Products);
        market.Offer("bois", 400, "foret");
        market.Offer("pierre", 250, "carriere");

        Storage.ApplyDaily(["commune"], structures, Products, _ => market);

        Assert.Equal(50, market.QuantityOf("bois"), precision: 6);
        Assert.Equal(250, market.QuantityOf("pierre"), precision: 6);
    }

    [Fact]
    public void Sans_limite_reglee_un_produit_n_est_pas_limite()
    {
        var structures = TownWith(new Structure("depot", "Dépôt", StructureKind.Building, "commune", StorageCapacity: 300));
        var allocation = Storage.Allocate("commune", structures, Products, new Dictionary<string, double> { ["bois"] = 100 });

        Assert.Equal(200, allocation.FreeFor("bois"), precision: 6);
        Assert.Equal(100, allocation.In("depot")["bois"], precision: 6);
    }

    [Fact]
    public void La_perte_de_transport_peut_etre_modulee_par_un_delegue()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 90)];

        // Perte personnalisée de 10 % (au lieu de 20 %)
        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], (_, _) => 10_000, (_, _) => 0.10);

        // La commune b manque de 90 pains : avec 10 % de perte, il en part 100 de a, 90 arrivent.
        Assert.Equal(90, markets["b"].QuantityOf("pain"), precision: 6);
        Assert.Equal(900, markets["a"].QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Les_matieres_voyagent_au_dela_de_ce_qu_utilisent_les_batiments_de_la_commune()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("bois", 500, "camp");
        Dictionary<string, Dictionary<string, double>> demand = new() { ["a"] = new() { ["bois"] = 300 }, ["b"] = new() { ["bois"] = 160 } };

        Trade.RunDaily(["a", "b"], [], Products, c => markets[c], (_, _) => 10_000, (_, _) => 0.20,
            buildingDemandOf: (c, p) => demand[c].GetValueOrDefault(p));

        // a garde les 300 de sa scierie ; b manque de 160 : 200 partent, 160 arrivent.
        Assert.Equal(300, markets["a"].QuantityOf("bois"), precision: 6);
        Assert.Equal(160, markets["b"].QuantityOf("bois"), precision: 6);
    }

    [Fact]
    public void La_demande_des_batiments_compte_leurs_matieres_et_leur_combustible_du_jour()
    {
        var structures = new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("a", "Commune A", StructureKind.PoliticalEntity, "etat"),
            new("scierie", "Scierie", StructureKind.Building, "a", Ownership.Public, null,
                new Production("planche", 1, [new RecipeInput("bois", 2)]), Jobs: 10),
        }.ToDictionary(s => s.Id);

        var demand = Trade.BuildingDemand(structures, new Dictionary<string, double> { ["scierie"] = 8 }, _ => 1,
            Demos.Domain.Shared.Season.Spring);

        // Matières : 8 travailleurs × 1 × 2 = 16 bois ; chauffage : 10 emplois × 0,01 = 0,1 bois au printemps.
        Assert.Equal(16 + 10 * Energy.HeatingPerUnit, demand["a"]["bois"], precision: 6);
        Assert.Equal(10 * Energy.WaterPerUnit, demand["a"]["eau"], precision: 6);
    }

    [Fact]
    public void Sans_batiment_logistique_dans_l_une_des_deux_communes_rien_ne_part()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products), ["c"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 80), Residents("c", 80)];

        // Seule c a un bâtiment logistique : a peut envoyer à c, pas à b.
        Trade.RunDaily(["a", "b", "c"], classes, Products, c => markets[c], (_, _) => 10_000, (_, _) => 0.20,
            canTrade: (from, to) => Trade.CanTrade(from, to, commune => commune == "c"));

        Assert.Equal(0, markets["b"].QuantityOf("pain"));
        Assert.Equal(80, markets["c"].QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Le_soir_une_commune_compte_sur_sa_production_de_demain()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 100, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 80)];

        // a a 100 pains le soir et en mangera 200 demain, mais en produira 250 : les 100 peuvent partir (80 sont demandés).
        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], (_, _) => 10_000, (_, _) => 0.20,
            expectedProductionOf: (c, p) => c == "a" && p == "pain" ? 250 : 0);

        Assert.Equal(80, markets["b"].QuantityOf("pain"), precision: 6);
        Assert.Equal(0, markets["a"].QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Les_chargements_arrivent_le_lendemain_et_se_periment_pendant_la_nuit()
    {
        ProductDefinition[] products = [new("pain", "Pain", NeedCategory.Food, 1, ShelfLifeDays: 10)];
        var markets = new Dictionary<string, Market> { ["a"] = new(products), ["b"] = new(products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 80)];
        var inTransit = new List<Shipment>();

        Trade.RunDaily(["a", "b"], classes, products, c => markets[c], (_, _) => 10_000, (_, _) => 0.20, dispatch: inTransit.Add);

        // Rien n'est encore arrivé ; le lendemain, 80 arrivent moins un dixième périmé dans la nuit (8, perdus en route à a).
        Assert.Equal(0, markets["b"].QuantityOf("pain"));
        Assert.Empty(Trade.Advance(inTransit, products, c => markets[c]));
        Assert.Equal(72, markets["b"].QuantityOf("pain"), precision: 6);
        Assert.Equal(20 + 8, markets["a"].TallyOf("pain").LostInTransit, precision: 6);
    }

    [Fact]
    public void La_capacite_d_envoi_est_partagee_au_prorata_entre_les_produits_et_les_destinations()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products), ["c"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        markets["a"].Offer("bois", 1_000, "camp");
        SocialClass[] classes = [Residents("a", 100), Residents("b", 300), Residents("c", 100)];
        Dictionary<string, Dictionary<string, double>> demand = new() { ["a"] = [], ["b"] = [], ["c"] = new() { ["bois"] = 200 } };
        var capacityLeft = new Dictionary<string, double> { ["a"] = 300 };

        // Sans perte : b veut 300 pains, c 100 pains et 200 bois, soit 600 ; a ne peut envoyer que 300, la moitié de chacun.
        Trade.RunDaily(["a", "b", "c"], classes, Products, c => markets[c], (_, _) => 10_000, (_, _) => 0,
            buildingDemandOf: (c, p) => demand[c].GetValueOrDefault(p),
            capacityHolderOf: (from, to) => Trade.CapacityHolder(from, to, commune => commune == "a"), capacityLeft: capacityLeft);

        Assert.Equal(150, markets["b"].QuantityOf("pain"), precision: 6);
        Assert.Equal(50, markets["c"].QuantityOf("pain"), precision: 6);
        Assert.Equal(100, markets["c"].QuantityOf("bois"), precision: 6);
        Assert.Equal(0, capacityLeft["a"], precision: 6);
    }

    [Fact]
    public void Sans_batiment_logistique_une_commune_envoie_avec_la_capacite_de_celle_qui_recoit()
    {
        Assert.Equal("a", Trade.CapacityHolder("a", "b", c => c == "a"));
        Assert.Equal("b", Trade.CapacityHolder("a", "b", c => c == "b"));
    }

    [Fact]
    public void La_duree_du_trajet_suit_la_distance_et_la_liaison()
    {
        var settings = new BalanceSettings();

        // 20, 40 et 80 km par jour ; arrondi au jour supérieur, au moins 1 jour.
        Assert.Equal(3, Demos.Domain.Map.Roads.TravelDays(Demos.Domain.Map.RoadLevel.None, 50, settings));
        Assert.Equal(3, Demos.Domain.Map.Roads.TravelDays(Demos.Domain.Map.RoadLevel.Trail, 50, settings));
        Assert.Equal(2, Demos.Domain.Map.Roads.TravelDays(Demos.Domain.Map.RoadLevel.Path, 50, settings));
        Assert.Equal(1, Demos.Domain.Map.Roads.TravelDays(Demos.Domain.Map.RoadLevel.Road, 50, settings));
        Assert.Equal(2, Demos.Domain.Map.Roads.TravelDays(Demos.Domain.Map.RoadLevel.Road, 100, settings));
        Assert.Equal(1, Demos.Domain.Map.Roads.TravelDays(Demos.Domain.Map.RoadLevel.None, 5, settings));
    }

    [Fact]
    public void Un_chargement_de_trois_jours_se_perime_chaque_nuit_et_arrive_au_troisieme_matin()
    {
        ProductDefinition[] products = [new("pain", "Pain", NeedCategory.Food, 1, ShelfLifeDays: 10)];
        var markets = new Dictionary<string, Market> { ["a"] = new(products), ["b"] = new(products) };
        List<Shipment> onTheWay = [new Shipment("a", "b", "pain", "ferme", 100, DaysLeft: 3, Days: 3)];

        onTheWay = Trade.Advance(onTheWay, products, c => markets[c]);
        onTheWay = Trade.Advance(onTheWay, products, c => markets[c]);
        Assert.Single(onTheWay);
        Assert.Equal(0, markets["b"].QuantityOf("pain"));
        onTheWay = Trade.Advance(onTheWay, products, c => markets[c]);

        // 100 × 0,9 × 0,9 × 0,9 = 72,9 arrivent ; 27,1 perdus en route, comptés à a.
        Assert.Empty(onTheWay);
        Assert.Equal(72.9, markets["b"].QuantityOf("pain"), precision: 6);
        Assert.Equal(27.1, markets["a"].TallyOf("pain").LostInTransit, precision: 6);
    }

    [Fact]
    public void Une_commune_commande_ce_qu_il_lui_faut_jusqu_a_l_arrivee_moins_ce_qui_est_deja_en_route()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 80)];
        var inTransit = new List<Shipment>();

        // Trois jours de trajet, sans perte : b a besoin de 3 × 80 = 240 jusqu'à l'arrivée, dont 160 déjà en route.
        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], (_, _) => 10_000, (_, _) => 0, dispatch: inTransit.Add,
            travelDaysOf: (_, _) => 3, inTransitTo: (c, p) => c == "b" && p == "pain" ? 160 : 0);

        Assert.Equal(80, inTransit.Sum(s => s.Quantity), precision: 6);
        Assert.All(inTransit, s => Assert.Equal(3, s.DaysLeft));
    }

    [Fact]
    public void La_perte_en_route_est_comptee_dans_la_commune_de_depart()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("pain", 1_000, "ferme");
        SocialClass[] classes = [Residents("a", 200), Residents("b", 90)];

        Trade.RunDaily(["a", "b"], classes, Products, c => markets[c], (_, _) => 10_000, (_, _) => 0.10);

        // 100 pains partent de a, 10 se perdent en route : comptés à a, pas comme perdus au stockage.
        var tally = markets["a"].TallyOf("pain");
        Assert.Equal(100, tally.Sent, precision: 6);
        Assert.Equal(10, tally.LostInTransit, precision: 6);
        Assert.Equal(0, tally.Lost);
        Assert.Equal(0, markets["b"].TallyOf("pain").LostInTransit);
    }

    [Fact]
    public void GeographicEconomics_Calcule_La_Perte_Selon_La_Distance()
    {
        var positions = new Dictionary<string, Demos.Domain.Map.Point2D>
        {
            ["c1"] = new(0, 0),
            ["c2"] = new(500, 0), // d = 500 unités
            ["c3"] = new(1000, 0), // d = 1000 unités
            ["c_proche"] = new(50, 0), // d = 50 unités
            ["c_tres_loin"] = new(3000, 0), // d = 3000 unités
        };

        // Même commune : 0 %
        Assert.Equal(0.0, Demos.Domain.Map.GeographicEconomics.TransportLoss("c1", "c1", positions));

        // Distance de référence (500 unités) : 20 % de perte
        Assert.Equal(0.20, Demos.Domain.Map.GeographicEconomics.TransportLoss("c1", "c2", positions), precision: 6);

        // Distance proche (50 unités) : 20% * (0.5 + 0.5 * 0.1) = 11 %
        Assert.Equal(0.11, Demos.Domain.Map.GeographicEconomics.TransportLoss("c1", "c_proche", positions), precision: 6);

        // Distance double (1000 unités) : 20% * (0.5 + 0.5 * 2) = 30 %
        Assert.Equal(0.30, Demos.Domain.Map.GeographicEconomics.TransportLoss("c1", "c3", positions), precision: 6);

        // Très lointain : plafonné à 60 %
        Assert.Equal(0.60, Demos.Domain.Map.GeographicEconomics.TransportLoss("c1", "c_tres_loin", positions), precision: 6);
    }
}
