using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Infrastructure;

namespace Demos.Tests.Application;

public class SimulationTests
{
    private const string ProductsJson = """
        [
          { "id": "pain", "name": "Pain", "need": "Food", "basePrice": 1 },
          { "id": "meubles", "name": "Meubles", "need": "Furniture", "basePrice": 20 },
          { "id": "pierre", "name": "Pierre", "basePrice": 3 },
          { "id": "bois", "name": "Bois", "basePrice": 2 }
        ]
        """;

    private static Simulation CreatePrototype() =>
        PrototypeScenario.Create(new JsonContentSource(_ => ProductsJson));

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

        // Cadres et dirigeants (LI) acceptent la liberté d'entreprendre, loi de l'État : 1 700 × 0,001 par jour.
        Assert.Equal(1.7, afterOneDay, precision: 6);
        Assert.Equal(3.4, simulation.World.Legitimacy[libertarian], precision: 6);
    }

    [Fact]
    public void La_boucle_economique_nourrit_les_classes_et_paie_les_vendeurs()
    {
        var simulation = CreatePrototype();
        var workers = simulation.World.Classes.Single(c => c.Id == "ouvriers");

        simulation.AdvanceDay();

        // La ferme coopérative produit 9 000 pains pour une demande de 10 650 : les besoins sont en partie couverts.
        Assert.InRange(workers.NeedsSatisfaction, 0.1, 0.99);
        Assert.Equal(0, simulation.World.Market.QuantityOf("pain"), precision: 6);
    }

    [Fact]
    public void Fonder_une_commune_cree_une_ville_fantome_qui_attire_ensuite_des_habitants()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 10);
        var pole = new Orientation(IdeologicalAxis.Property, Pole.B);

        var commune = simulation.TryFoundCommune("Nouvelle commune", [new MaterialCost("bois", 100)], PaymentMode.StockFirst, pole, 10);

        Assert.NotNull(commune);
        Assert.True(simulation.World.IsGhostTown(commune.Id));
        Assert.Contains(simulation.World.Structures.Values, s => s.ParentId == commune.Id && s.Jobs == Simulation.BaseCampJobs);

        // Au changement de saison (1er mars), des sans-emploi rejoignent la nouvelle commune.
        AdvanceDays(simulation, 50);
        Assert.False(simulation.World.IsGhostTown(commune.Id));
    }

    [Fact]
    public void Sans_legitimite_suffisante_aucune_commune_n_est_fondee()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 10);
        var stockBefore = simulation.World.StateStock.QuantityOf("bois");

        var commune = simulation.TryFoundCommune(
            "Nouvelle commune", [new MaterialCost("bois", 100)], PaymentMode.StockFirst,
            new Orientation(IdeologicalAxis.Hierarchy, Pole.B), 1_000);

        Assert.Null(commune);
        Assert.Equal(stockBefore, simulation.World.StateStock.QuantityOf("bois"));
    }

    [Fact]
    public void Construire_avec_le_stock_de_l_Etat_ajoute_la_structure()
    {
        var simulation = CreatePrototype();
        AdvanceDays(simulation, 5);

        // La scierie d'État (200 postes pourvus × 0,2) produit 40 bois par jour : 200 en 5 jours.
        var housing = new Structure("logements", "Logements", StructureKind.Building, "commune-1");
        var built = simulation.TryBuild(housing, [new MaterialCost("bois", 150)], PaymentMode.StockFirst);

        Assert.True(built);
        Assert.Contains("logements", simulation.World.Structures.Keys);
        Assert.Equal(50, simulation.World.StateStock.QuantityOf("bois"));
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
