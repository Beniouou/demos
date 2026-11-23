using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

/// <summary>L'école enseigne La Promesse (décidé le 23 novembre 2026) : un levier contre la dérive.</summary>
public class PromiseSchoolTests
{
    private static readonly Ideology Communist = new(Pole.A, Pole.A, Pole.A);
    private static readonly Ideology Phalansterian = new(Pole.A, Pole.B, Pole.A);

    /// <summary>La Commune Libre : égalitariste, écologiste, collectiviste.</summary>
    private static readonly PromiseDefinition FreeCommune = new("eg-ec-co", "La Commune Libre", ["EG", "EC", "CO"]);

    /// <summary>1 000 ouvriers communistes, tous au travail, dont 500 à l'école en moyenne sur la saison.</summary>
    private static SocialClass Schooled()
    {
        var workers = new SocialClass("classe", "classe", Rank.Workers, Communist, 1_000) { Wealth = 10_000 };
        workers.SetResidence("commune", 1);
        workers.SetEmployment("ferme", 1_000);
        workers.WealthPerMemberLastSeason = 10;
        for (var day = 0; day < 10; day++)
            workers.RecordSchoolDay(500);
        return workers;
    }

    [Fact]
    public void L_ecole_fait_rejoindre_le_pole_de_la_Promesse_qui_manque()
    {
        var created = Assert.Single(ClassTransitions.RunSeasonal([Schooled()], TestTown.Structures, TestTown.NoPollution, promise: FreeCommune));

        // 2 % des 500 élèves de la saison deviennent écologistes, le seul pôle où la classe s'écarte de La Promesse.
        Assert.Equal(Phalansterian, created.Ideology);
        Assert.Equal(10, created.Population);
    }

    [Fact]
    public void Sans_Promesse_l_ecole_n_a_pas_d_effet_ideologique()
    {
        Assert.Empty(ClassTransitions.RunSeasonal([Schooled()], TestTown.Structures, TestTown.NoPollution));
    }

    [Fact]
    public void Une_classe_deja_fidele_a_la_Promesse_ne_change_pas()
    {
        var promise = new PromiseDefinition("eg-co", "La Table Commune", ["EG", "CO"]);

        Assert.Empty(ClassTransitions.RunSeasonal([Schooled()], TestTown.Structures, TestTown.NoPollution, promise: promise));
    }

    [Fact]
    public void L_education_releve_les_eleves_de_la_saison()
    {
        var school = new Structure("ecole", "École", StructureKind.Building, "commune", SchoolPlaces: 300, SchoolLevel: EducationLevel.Basic);
        var structures = new Dictionary<string, Structure>(TestTown.Structures) { ["ecole"] = school };
        var workers = new SocialClass("classe", "classe", Rank.Workers, Communist, 1_000) { Wealth = 10_000 };
        workers.SetResidence("commune", 1);

        Education.RunDaily(structures, [workers], new Treasury());

        Assert.Equal(300, workers.SeasonPupils, precision: 6);
    }
}
