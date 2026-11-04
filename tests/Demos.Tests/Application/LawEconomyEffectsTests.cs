using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Application;

/// <summary>Effets des modèles de lois sur l'économie, avec le vrai catalogue et la partie de départ.</summary>
public class LawEconomyEffectsTests
{
    /// <summary>Une partie avec ces seules lois de départ, après un jour (les lois s'appliquent chaque jour).</summary>
    private static Simulation With(params string[] templates)
    {
        var simulation = PrototypeScenario.Create(GameData.Content(),
            new StartingLaws(StartingLawsMode.Chosen, templates.Select(t => new ScenarioLaw(t)).ToList()));
        simulation.AdvanceDay();
        return simulation;
    }

    private static Structure Building(Simulation simulation, string id) => simulation.World.Structures[id];

    [Fact]
    public void Sans_loi_les_batiments_n_ont_aucune_regle()
    {
        var simulation = With();

        Assert.All(simulation.World.Structures.Values, s => Assert.Same(AppliedLaws.None, s.Rules));
    }

    [Fact]
    public void Le_salaire_minimum_releve_les_salaires_des_ouvriers_de_la_commune()
    {
        var before = Staffing.WageOf(Building(With(), "ferme"), Rank.Workers);
        var simulation = With("salaire-minimum-ouvrier");

        Assert.True(before < 2.1);
        Assert.Equal(2.1, Staffing.WageOf(Building(simulation, "ferme"), Rank.Workers), precision: 9);
        // Les cadres gagnent déjà plus : le salaire minimum des ouvriers ne les touche pas.
        Assert.Equal(Staffing.WageOf(Building(With(), "ferme"), Rank.Executives), Staffing.WageOf(Building(simulation, "ferme"), Rank.Executives));
    }

    [Fact]
    public void La_hierarchie_des_salaires_augmente_les_cadres_de_vingt_pour_cent()
    {
        var before = Staffing.WageOf(Building(With(), "ferme"), Rank.Executives);

        Assert.Equal(before * 1.2, Staffing.WageOf(Building(With("hierarchie-salaires"), "ferme"), Rank.Executives), precision: 9);
    }

    [Fact]
    public void Les_quotas_d_extraction_baissent_la_production_de_la_carriere_seulement()
    {
        var simulation = With("quotas-extraction");

        Assert.Equal(0.8, Building(simulation, "carriere").Rules.ProductionFactor, precision: 9);
        Assert.Equal(0.8, Building(simulation, "mine").Rules.ProductionFactor, precision: 9);
        Assert.Equal(1, Building(simulation, "ferme").Rules.ProductionFactor);
    }

    [Fact]
    public void La_production_baisse_vraiment_avec_les_quotas()
    {
        var free = With();
        var limited = With("quotas-extraction");
        free.AdvanceDay();
        limited.AdvanceDay();

        Assert.Equal(free.World.Produced["carriere"] * 0.8, limited.World.Produced["carriere"], precision: 6);
    }

    [Fact]
    public void Les_normes_de_pollution_et_la_priorite_a_la_production_s_additionnent()
    {
        var simulation = With("normes-pollution", "priorite-production");
        var quarry = Building(simulation, "carriere").Rules;

        // Production −10 % et +10 % ; pollution −30 % et +20 %.
        Assert.Equal(1, quarry.ProductionFactor, precision: 9);
        Assert.Equal(0.9, quarry.PollutionFactor, precision: 9);
    }

    [Fact]
    public void Le_plafond_des_loyers_baisse_le_loyer_des_logements_prives()
    {
        var simulation = With("liberte-entreprendre", "plafond-loyers");

        Assert.Equal(0.25, Building(simulation, "logements-prives").RentDue, precision: 9);
        Assert.Equal(Building(simulation, "logements-etat").DailyRent, Building(simulation, "logements-etat").RentDue);
    }

    [Fact]
    public void La_liberte_de_licencier_double_le_pas_des_licenciements_du_prive()
    {
        var simulation = With("liberte-entreprendre", "liberte-licencier");

        Assert.Equal(2, Building(simulation, "distillerie").Rules.LayoffFactor, precision: 9);
        Assert.Equal(1, Building(simulation, "ferme").Rules.LayoffFactor);
    }

    [Fact]
    public void L_economie_cooperative_exonere_les_SCOP_d_impot()
    {
        var simulation = With("liberte-entreprendre", "economie-cooperative");

        Assert.Equal(0, Building(simulation, "ferme").Rules.CompanyTaxFactor);
        Assert.Equal(1, Building(simulation, "distillerie").Rules.CompanyTaxFactor);
    }

    [Fact]
    public void L_impot_progressif_fait_payer_cinquante_pour_cent_de_plus_aux_cadres()
    {
        var simulation = With("impot-progressif");
        var laws = simulation.World.LawBook;

        Assert.Equal(1.5, laws.TaxFactorOf(simulation.World.ClassesById["cadres"]), precision: 9);
        Assert.Equal(1, laws.TaxFactorOf(simulation.World.ClassesById["ouvriers"]), precision: 9);
    }

    [Fact]
    public void L_allegement_fiscal_baisse_ce_que_le_tresor_encaisse()
    {
        var normal = With();
        var lighter = With("allegement-fiscal");
        var normalBefore = normal.World.Treasury.Balance;
        var lighterBefore = lighter.World.Treasury.Balance;
        normal.AdvanceDay();
        lighter.AdvanceDay();

        Assert.True(lighter.World.Treasury.Balance - lighterBefore < normal.World.Treasury.Balance - normalBefore);
    }
}
