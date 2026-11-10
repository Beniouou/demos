using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Infrastructure;

namespace Demos.Tests.Application;

public class SimulationTests
{
    private static Simulation CreatePrototype() => PrototypeScenario.Create(GameData.Content());

    /// <summary>Une partie sans autre loi que la liberté d'entreprendre, à l'État (LI).</summary>
    private static Simulation WithLiberalStateLaw() =>
        PrototypeScenario.Create(GameData.Content(), new StartingLaws(StartingLawsMode.Chosen, [new ScenarioLaw("liberte-entreprendre")]));

    /// <summary>Réserve de chantier de la première commune, en matériaux de construction.</summary>
    private static double ReserveOf(Simulation simulation, string communeId = "commune-1") =>
        simulation.World.ConstructionReserveOf(communeId).QuantityOf("materiaux-de-construction");

    private static void AdvanceDays(Simulation simulation, int days)
    {
        for (var day = 0; day < days; day++)
            simulation.AdvanceDay();
    }

    [Fact]
    public void Une_annee_compte_quatre_saisons_et_un_bilan()
    {
        var simulation = CreatePrototype();
        var seasons = 0;
        var years = 0;
        simulation.SeasonPassed += () => seasons++;
        simulation.YearPassed += () => years++;

        // Du 1er janvier 1900 au 1er janvier 1901 : 1er mars, 1er juin, 1er septembre, 1er décembre.
        for (var day = 0; day < 365; day++)
            simulation.AdvanceDay();

        Assert.Equal(4, seasons);
        Assert.Equal(1, years);
    }

    [Fact]
    public void La_population_ne_change_qu_au_changement_de_saison()
    {
        var simulation = CreatePrototype();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");
        var start = workers.Population;

        // Du 1er janvier au 28 février : toujours l'hiver.
        for (var day = 0; day < 58; day++)
            simulation.AdvanceDay();
        Assert.Equal(start, workers.Population);

        simulation.AdvanceDay();
        Assert.NotEqual(start, workers.Population);
    }

    [Fact]
    public void Les_classes_produisent_chaque_jour_la_legitimite_des_lois_qu_elles_acceptent()
    {
        var simulation = WithLiberalStateLaw();
        var libertarian = new Orientation(IdeologicalAxis.Property, Pole.B);

        simulation.AdvanceDay();
        var afterOneDay = simulation.World.Legitimacy[libertarian];
        simulation.AdvanceDay();

        // Professions intermédiaires et cadres (LI) acceptent la liberté d'entreprendre, loi de l'État (autorisation,
        // intensité 1, poids 0,5) : 500 × 0,5 × 0,001 par jour.
        Assert.Equal(0.25, afterOneDay, precision: 6);
        Assert.Equal(0.5, simulation.World.Legitimacy[libertarian], precision: 6);
    }

    [Fact]
    public void La_boucle_economique_nourrit_habille_et_loge_les_classes()
    {
        var simulation = CreatePrototype();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");

        simulation.AdvanceDay();

        // 5 600 places pour 5 600 places demandées, et des chaînes qui couvrent ce que consomment les habitants.
        Assert.Equal(1, workers.HousingCoverage, precision: 6);
        Assert.Equal(1, workers.NeedsSatisfaction, precision: 6);
    }

    [Fact]
    public void Fonder_une_commune_cree_une_ville_fantome_qui_attire_ensuite_des_habitants()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 10);
        var pole = new Orientation(IdeologicalAxis.Property, Pole.B);
        simulation.World.Legitimacy.Add(pole, 20);
        var reserveBefore = ReserveOf(simulation);

        var commune = simulation.TryFoundCommune("Nouvelle commune", "commune-1", PaymentMode.StockFirst, pole, 2);

        Assert.NotNull(commune);
        Assert.True(simulation.World.IsGhostTown(commune.Id));

        // Le pôle payé devient la charte de fondation de la commune ; ce n'est pas une loi.
        Assert.Equal(pole, simulation.World.CharterOf(commune.Id));
        Assert.DoesNotContain(simulation.World.Laws, l => l.StructureId == commune.Id);
        // Le camp de base est en chantier 30 jours : pas encore de logements.
        var camp = simulation.World.BuildingTypes[Simulation.BaseCampTypeId];
        Assert.Equal(0, simulation.World.HousingPlaces(commune.Id));
        // Le camp de base est payé par la réserve de chantier de la commune qui l'envoie ; la nouvelle reçoit la sienne.
        Assert.Equal(reserveBefore - camp.Construction!.Quantity, ReserveOf(simulation), precision: 6);
        Assert.Equal(Balance.Current.FoundingMaterials, ReserveOf(simulation, commune.Id), precision: 6);
        AdvanceDays(simulation, camp.Construction!.Days);
        Assert.Equal(camp.HousingPlaces, simulation.World.HousingPlaces(commune.Id));

        // Au changement de saison (1er mars), des sans-emploi rejoignent la nouvelle commune.
        AdvanceDays(simulation, 50 - camp.Construction.Days);
        Assert.False(simulation.World.IsGhostTown(commune.Id));
    }

    [Fact]
    public void La_reserve_de_chantier_de_depart_ne_se_perd_pas_et_ne_se_vend_pas()
    {
        var simulation = CreatePrototype();
        Assert.Equal(Balance.Current.FoundingMaterials, ReserveOf(simulation), precision: 6);

        // L'atelier de matériaux d'État en fabrique : le marché ne contient que ce qu'il a produit, rien de la réserve.
        var produced = 0.0;
        for (var day = 0; day < 60; day++)
        {
            simulation.AdvanceDay();
            produced += simulation.World.Produced.GetValueOrDefault("atelier-materiaux");
        }

        Assert.Equal(Balance.Current.FoundingMaterials, ReserveOf(simulation), precision: 6);
        Assert.True(produced > 0);
        Assert.InRange(simulation.World.MarketOf("commune-1").QuantityOf("materiaux-de-construction"), 0, produced + 1e-6);
    }

    [Fact]
    public void Fonder_une_commune_verse_sa_dotation_du_tresor_a_sa_caisse()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 10);
        simulation.World.Legitimacy.Add(new Orientation(IdeologicalAxis.Property, Pole.B), 20);
        var grant = Simulation.FoundingGrantOf(simulation.World.BuildingTypes[Simulation.BaseCampTypeId]);
        var treasuryBefore = simulation.World.Treasury.Balance;

        var commune = simulation.TryFoundCommune(
            "Nouvelle commune", "commune-1", PaymentMode.StockFirst,
            new Orientation(IdeologicalAxis.Property, Pole.B), 2);

        // 60 jours × 300 emplois × 1,5 = 27 000.
        Assert.NotNull(commune);
        Assert.Equal(27_000, grant, precision: 6);
        Assert.Equal(grant, simulation.World.CommuneFundOf(commune.Id).Balance, precision: 6);
        Assert.Equal(treasuryBefore - grant, simulation.World.Treasury.Balance, precision: 6);
    }

    [Fact]
    public void Sans_assez_d_argent_pour_la_dotation_aucune_commune_n_est_fondee()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 10);
        simulation.World.Treasury.TrySpend(simulation.World.Treasury.Balance - 100);
        var stockBefore = simulation.World.StateQuantityOf("commune-1", "bois");

        var commune = simulation.TryFoundCommune(
            "Nouvelle commune", "commune-1", PaymentMode.StockFirst,
            new Orientation(IdeologicalAxis.Property, Pole.B), 2);

        Assert.Null(commune);
        Assert.Equal(100, simulation.World.Treasury.Balance, precision: 6);
        Assert.Equal(stockBefore, simulation.World.StateQuantityOf("commune-1", "bois"));
    }

    [Fact]
    public void Sans_legitimite_suffisante_aucune_commune_n_est_fondee()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 10);
        var stockBefore = simulation.World.StateQuantityOf("commune-1", "bois");

        var commune = simulation.TryFoundCommune(
            "Nouvelle commune", "commune-1", PaymentMode.StockFirst,
            new Orientation(IdeologicalAxis.Hierarchy, Pole.B), 1_000);

        Assert.Null(commune);
        Assert.Equal(stockBefore, simulation.World.StateQuantityOf("commune-1", "bois"));
    }

    [Fact]
    public void Construire_lance_un_chantier_paye_par_le_stock_de_l_Etat_et_le_batiment_arrive_a_la_fin()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 5);
        var type = simulation.World.BuildingTypes["logements"];
        var cost = type.Construction!;
        Assert.Equal(("materiaux-de-construction", 30), (cost.ProductId, cost.Days));
        var reserveBefore = ReserveOf(simulation);
        var completed = new List<Structure>();
        simulation.BuildingCompleted += completed.Add;

        var housing = type.Build("logements-neufs", "commune-1");
        Assert.True(simulation.TryBuild(housing, PaymentMode.StockFirst));

        // Payé en entier au lancement, par la réserve de chantier ; le bâtiment n'existe pas encore.
        Assert.Equal(reserveBefore - cost.Quantity, ReserveOf(simulation), precision: 6);
        Assert.DoesNotContain("logements-neufs", simulation.World.Structures.Keys);
        Assert.Single(simulation.World.ConstructionSites);
        Assert.False(simulation.TryBuild(housing, PaymentMode.StockFirst));

        AdvanceDays(simulation, cost.Days - 1);
        Assert.DoesNotContain("logements-neufs", simulation.World.Structures.Keys);
        simulation.AdvanceDay();
        Assert.Contains("logements-neufs", simulation.World.Structures.Keys);
        Assert.Empty(simulation.World.ConstructionSites);
        Assert.Equal(["logements-neufs"], completed.Select(b => b.Id));
    }

    [Fact]
    public void Sans_materiaux_ou_sans_cout_de_construction_aucun_chantier_ne_demarre()
    {
        var simulation = CreatePrototype();
        var treasuryBefore = simulation.World.Treasury.Balance;

        // Sans réserve de chantier, et personne ne vend de matériaux de construction au départ.
        simulation.World.ConstructionReserveOf("commune-1").TryRemove("materiaux-de-construction", ReserveOf(simulation));
        Assert.False(simulation.TryBuild(simulation.World.BuildingTypes["logements"].Build("logements-neufs", "commune-1"), PaymentMode.StockFirst));
        // Un bâtiment de la liste 3 n'a pas encore de coût de construction.
        var palace = simulation.World.BuildingTypes["palais"];
        Assert.False(palace.CanBeBuilt);
        Assert.False(simulation.TryBuild(palace.Build("palais-neuf", "commune-1"), PaymentMode.MoneyOnly));

        Assert.Empty(simulation.World.ConstructionSites);
        Assert.Equal(treasuryBefore, simulation.World.Treasury.Balance, precision: 6);
    }

    [Fact]
    public void Le_menu_d_administration_peut_changer_le_proprietaire_les_postes_et_le_prix()
    {
        var simulation = CreatePrototype();
        var farm = simulation.World.Structures["ferme"];
        Assert.Equal(1, farm.Posts);

        simulation.UpdateBuilding(farm with { Ownership = Ownership.Public, OwnerClassId = null, Posts = 3 });
        // Le moulin est arrêté, pour que le blé reste dans le stockage.
        simulation.UpdateBuilding(simulation.World.Structures["moulin"] with { Jobs = 0 });
        simulation.World.SetPrice("ble", 2);
        simulation.AdvanceDay();

        // Ferme publique : son blé arrive au stockage de la commune au nom de l'État, plus au nom de l'entreprise.
        Assert.True(simulation.World.StateQuantityOf("commune-1", "ble") > 0);
        Assert.Equal(0, simulation.World.MarketOf("commune-1").QuantityOf("ble", Market.CompanySellerId("ferme")));
        Assert.Equal(2, simulation.World.PriceOf("ble"));
        Assert.Equal(3, simulation.World.Structures["ferme"].Posts);
    }

    [Fact]
    public void L_ecole_fait_naitre_des_professions_intermediaires_communistes_le_1er_janvier()
    {
        var simulation = CreatePrototype();

        // Du 1er janvier au 31 décembre 1900 : les points s'accumulent, personne ne monte encore.
        AdvanceDays(simulation, 364);
        Assert.DoesNotContain(simulation.World.Classes, c => c.Name == "Professions intermédiaires communistes");

        // 1er janvier 1901 : les points d'éducation des ouvriers communistes en font monter certains au rang supérieur.
        simulation.AdvanceDay();
        var intermediates = Assert.Single(simulation.World.Classes, c => c.Name == "Professions intermédiaires communistes");
        Assert.True(intermediates.Population > 0);
        Assert.Same(intermediates, simulation.World.ClassesById[intermediates.Id]);

        // L'université forme, elle, des cadres parmi les professions intermédiaires libertaires.
        var executives = Assert.Single(simulation.World.Classes, c => c.Name == "Cadres libertaires");
        Assert.True(executives.Population > 0);
    }

    [Fact]
    public void Les_impots_remplissent_le_tresor_chaque_jour()
    {
        var simulation = CreatePrototype();

        simulation.AdvanceDay();

        Assert.True(simulation.World.Treasury.Balance > 0);
    }

    [Fact]
    public void Les_ouvriers_communistes_rejettent_la_liberte_d_entreprendre()
    {
        var simulation = WithLiberalStateLaw();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");

        simulation.AdvanceDay();

        Assert.True(workers.Adhesion < 1);
    }
}
