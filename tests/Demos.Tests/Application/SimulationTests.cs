using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Infrastructure;

namespace Demos.Tests.Application;

public class SimulationTests
{
    private static Simulation CreatePrototype() => PrototypeScenario.Create(GameData.Content());

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
        var simulation = CreatePrototype();
        var libertarian = new Orientation(IdeologicalAxis.Property, Pole.B);

        simulation.AdvanceDay();
        var afterOneDay = simulation.World.Legitimacy[libertarian];
        simulation.AdvanceDay();

        // Professions intermédiaires et cadres (LI) acceptent la liberté d'entreprendre, loi de l'État : 500 × 0,001 par jour.
        Assert.Equal(0.5, afterOneDay, precision: 6);
        Assert.Equal(1, simulation.World.Legitimacy[libertarian], precision: 6);
    }

    [Fact]
    public void La_boucle_economique_nourrit_habille_et_loge_les_classes()
    {
        var simulation = CreatePrototype();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");

        simulation.AdvanceDay();

        // 5 500 places pour 5 000 habitants, 6 600 pains pour 5 000 et 52 vêtements pour 50 par jour.
        Assert.Equal(1, workers.HousingCoverage, precision: 6);
        Assert.Equal(1, workers.NeedsSatisfaction, precision: 6);
    }

    [Fact]
    public void Fonder_une_commune_cree_une_ville_fantome_qui_attire_ensuite_des_habitants()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 10);
        var pole = new Orientation(IdeologicalAxis.Property, Pole.B);

        var commune = simulation.TryFoundCommune("Nouvelle commune", "commune-1", [new MaterialCost("bois", 100)], PaymentMode.StockFirst, pole, 2);

        Assert.NotNull(commune);
        Assert.True(simulation.World.IsGhostTown(commune.Id));
        Assert.Equal(simulation.World.BuildingTypes[Simulation.BaseCampTypeId].HousingPlaces, simulation.World.HousingPlaces(commune.Id));

        // Au changement de saison (1er mars), des sans-emploi rejoignent la nouvelle commune.
        AdvanceDays(simulation, 50);
        Assert.False(simulation.World.IsGhostTown(commune.Id));
    }

    [Fact]
    public void Fonder_une_commune_verse_sa_dotation_du_tresor_a_sa_caisse()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 10);
        var grant = Simulation.FoundingGrantOf(simulation.World.BuildingTypes[Simulation.BaseCampTypeId]);
        var treasuryBefore = simulation.World.Treasury.Balance;

        var commune = simulation.TryFoundCommune(
            "Nouvelle commune", "commune-1", [new MaterialCost("bois", 100)], PaymentMode.StockFirst,
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
            "Nouvelle commune", "commune-1", [new MaterialCost("bois", 100)], PaymentMode.StockFirst,
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
            "Nouvelle commune", "commune-1", [new MaterialCost("bois", 100)], PaymentMode.StockFirst,
            new Orientation(IdeologicalAxis.Hierarchy, Pole.B), 1_000);

        Assert.Null(commune);
        Assert.Equal(stockBefore, simulation.World.StateQuantityOf("commune-1", "bois"));
    }

    [Fact]
    public void Construire_avec_le_stock_de_l_Etat_ajoute_la_structure()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 5);

        // Le camp de bûcheron d'État produit 40 bois par jour ; la scierie d'État en utilise une partie.
        var stockBefore = simulation.World.StateQuantityOf("commune-1", "bois");
        var housing = new Structure("logements", "Logements", StructureKind.Building, "commune-1");
        var built = simulation.TryBuild(housing, [new MaterialCost("bois", 50)], PaymentMode.StockFirst);

        Assert.True(stockBefore >= 50);
        Assert.True(built);
        Assert.Contains("logements", simulation.World.Structures.Keys);
        Assert.Equal(stockBefore - 50, simulation.World.StateQuantityOf("commune-1", "bois"), precision: 6);
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
        var simulation = CreatePrototype();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");

        simulation.AdvanceDay();

        Assert.True(workers.Adhesion < 1);
    }
}
