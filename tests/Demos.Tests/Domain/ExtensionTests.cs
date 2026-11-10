using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

/// <summary>Extension et bureaucratie : charge et efficacité des entités politiques (réglages par défaut).</summary>
public class ExtensionTests
{
    private static readonly BalanceSettings Settings = new();

    private static Structure State() => new("etat", "État", StructureKind.PoliticalEntity, null);

    private static Structure Commune(string id, string parentId = "etat") => new(id, id, StructureKind.PoliticalEntity, parentId);

    private static Structure Prefecture(string id, string parentId, string seatId) =>
        new(id, id, StructureKind.PoliticalEntity, parentId, EntityType: EntityLevel.Prefecture, SeatId: seatId);

    private static Structure Building(string id, string parentId, Ownership ownership = Ownership.Public) =>
        new(id, id, StructureKind.Building, parentId, ownership);

    private static Extension Compute(IEnumerable<Structure> structures, double residentsPerCommune) =>
        Extension.Compute(structures.ToDictionary(s => s.Id), _ => residentsPerCommune, Settings);

    [Fact]
    public void La_commune_de_depart_n_est_pas_penalisee()
    {
        // 5 000 habitants, 16 bâtiments publics, 10 SCOP ou privés : 0,25 + 0,40 + 0,0625.
        var structures = new List<Structure> { State(), Commune("commune") };
        structures.AddRange(Enumerable.Range(0, 16).Select(i => Building($"public-{i}", "commune")));
        structures.AddRange(Enumerable.Range(0, 10).Select(i => Building($"scop-{i}", "commune", Ownership.Cooperative)));

        var load = Compute(structures, 5_000).Loads["commune"];

        Assert.Equal(16, load.DirectBuildings);
        Assert.Equal(10, load.AutonomousBuildings);
        Assert.Equal(0.7125, load.Charge, 6);
        Assert.Equal(1, load.Efficiency);
    }

    [Fact]
    public void Au_dela_d_une_charge_de_1_l_efficacite_vaut_1_sur_la_charge()
    {
        var load = Compute([State(), Commune("commune")], 40_000).Loads["commune"];

        Assert.Equal(2, load.Charge, 6);
        Assert.Equal(0.5, load.Efficiency, 6);
    }

    [Fact]
    public void L_Etat_compte_un_quart_de_la_population_de_ses_communes()
    {
        // 80 communes de 5 000 habitants sous l'État : il compte 100 000 habitants, soit une charge de 5.
        var structures = new List<Structure> { State() };
        structures.AddRange(Enumerable.Range(0, 80).Select(i => Commune($"commune-{i}")));

        var state = Compute(structures, 5_000).Loads["etat"];

        Assert.Equal(100_000, state.CountedPopulation, 6);
        Assert.Equal(0.2, state.Efficiency, 6);
    }

    [Fact]
    public void Empiler_des_prefectures_soulage_l_Etat_en_cascade()
    {
        // 8 préfectures de 10 communes : l'État compte 25 000 ; regroupées en 2 grandes préfectures : 6 250.
        var flat = new List<Structure> { State() };
        var deep = new List<Structure> { State(), Prefecture("grande-0", "etat", "c-0-0"), Prefecture("grande-1", "etat", "c-4-0") };
        for (var p = 0; p < 8; p++)
        {
            flat.Add(Prefecture($"pref-{p}", "etat", $"c-{p}-0"));
            deep.Add(Prefecture($"pref-{p}", $"grande-{p / 4}", $"c-{p}-0"));
            for (var c = 0; c < 10; c++)
            {
                flat.Add(Commune($"c-{p}-{c}", $"pref-{p}"));
                deep.Add(Commune($"c-{p}-{c}", $"pref-{p}"));
            }
        }

        Assert.Equal(25_000, Compute(flat, 5_000).Loads["etat"].CountedPopulation, 6);
        var extension = Compute(deep, 5_000);
        Assert.Equal(6_250, extension.Loads["etat"].CountedPopulation, 6);
        Assert.Equal(12_500, extension.Loads["pref-0"].CountedPopulation, 6);
    }

    [Fact]
    public void Une_entite_contenue_ajoute_10_pourcent_de_sa_charge_sans_population()
    {
        // La commune gère 40 bâtiments publics (charge hors population : 1) : l'État reçoit 0,1.
        var structures = new List<Structure> { State(), Commune("commune") };
        structures.AddRange(Enumerable.Range(0, 40).Select(i => Building($"public-{i}", "commune")));

        var extension = Compute(structures, 0);

        Assert.Equal(1, extension.Loads["commune"].ChargeWithoutPopulation, 6);
        Assert.Equal(0.1, extension.Loads["etat"].Charge, 6);
        Assert.Equal(0, extension.Loads["etat"].DirectBuildings);
    }

    [Fact]
    public void Le_batiment_d_une_prefecture_est_gere_par_elle()
    {
        var structures = new[]
        {
            State(), Prefecture("pref", "etat", "a"), Commune("a", "pref"), Commune("b", "pref"),
            Building("batiment-prefecture", "pref"),
        }.ToDictionary(s => s.Id);

        Assert.Equal("pref", Extension.ManagerOf(structures["batiment-prefecture"], structures));
        Assert.Equal(1, Extension.Compute(structures, _ => 0, Settings).Loads["pref"].DirectBuildings);
    }

    [Fact]
    public void Chaque_prefecture_traversee_fait_perdre_10_pourcent()
    {
        var structures = new[]
        {
            State(), Prefecture("grande", "etat", "a"), Prefecture("pref", "grande", "a"), Commune("a", "pref"),
            Commune("directe"), Building("ferme", "a"),
        }.ToDictionary(s => s.Id);
        var extension = Extension.Compute(structures, _ => 1_000, Settings);

        Assert.Equal(2, extension.LevelsBetween("etat", structures["ferme"]));
        Assert.Equal(0.81, extension.EfficiencyFor("etat", structures["ferme"]), 6);
        Assert.Equal(0.9, extension.EfficiencyFor("grande", structures["a"]), 6);
        Assert.Equal(1, extension.EfficiencyFor("pref", structures["a"]), 6);
        Assert.Equal(1, extension.EfficiencyFor("etat", structures["directe"]), 6);
        Assert.Equal(1, extension.EfficiencyFor("a", structures["ferme"]), 6);
    }

    [Fact]
    public void Le_terme_distance_est_inclus_dans_la_charge_d_une_entite()
    {
        var structures = new[] { State(), Commune("commune") }.ToDictionary(s => s.Id);
        var extension = Extension.Compute(structures, _ => 0, Settings, id => id == "etat" ? 0.5 : 0);

        Assert.Equal(0.5, extension.Loads["etat"].Distance);
        Assert.Equal(0.5, extension.Loads["etat"].ChargeWithoutPopulation);
        Assert.Equal(0.5, extension.Loads["etat"].Charge);
        Assert.Equal(1, extension.Loads["etat"].Efficiency);
        Assert.Equal(0, extension.Loads["commune"].Distance);
    }

    [Fact]
    public void Une_prefecture_calcule_sa_charge_selon_la_distance_reelle_a_ses_communes()
    {
        var structures = new[]
        {
            State(),
            Prefecture("pref", "etat", "chef-lieu"),
            Commune("chef-lieu", "pref"),
            Commune("commune-eloignee", "pref"),
        }.ToDictionary(s => s.Id);

        var positions = new Dictionary<string, Demos.Domain.Map.Point2D>
        {
            ["chef-lieu"] = new(100, 100),
            ["commune-eloignee"] = new(1300, 1700), // Distance euclidienne = sqrt(1200^2 + 1600^2) = 2 000 lieues
        };

        var distOf = (string id) => Demos.Domain.Map.AdministrativeDistance.LoadOf(id, structures, positions);
        var extension = Extension.Compute(structures, _ => 0, Settings, distOf);

        // Chef-lieu à 0 lieue, commune éloignée à 2 000 lieues ÷ 2 000 lieues par point = 1,0 de charge distance
        Assert.Equal(1.0, extension.Loads["pref"].Distance, 6);
        Assert.Equal(1.0, extension.Loads["pref"].Charge, 6);
        Assert.Equal(1.0, extension.Loads["pref"].Efficiency);

        // Les communes ne souffrent d'aucune distance administrative directe
        Assert.Equal(0.0, extension.Loads["chef-lieu"].Distance);
        Assert.Equal(0.0, extension.Loads["commune-eloignee"].Distance);

        // L'État hérite de 10% de la charge hors population de la préfecture (0.1 * 1.0 = 0.1)
        Assert.Equal(0.1, extension.Loads["etat"].Charge, 6);
    }
}
