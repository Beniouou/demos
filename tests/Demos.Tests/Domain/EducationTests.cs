using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class EducationTests
{
    private static readonly Ideology Communist = new(Pole.A, Pole.A, Pole.A);

    private static Dictionary<string, Structure> Town(Structure school) => new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        school,
    }.ToDictionary(s => s.Id);

    private static SocialClass Workers(long population, double wealth = 0)
    {
        var workers = new SocialClass("ouvriers", "Ouvriers communistes", Rank.Workers, Communist, population) { Wealth = wealth };
        workers.SetResidence("commune", 1);
        return workers;
    }

    [Fact]
    public void Chaque_place_d_eleve_occupee_rapporte_un_point_par_jour()
    {
        var structures = Town(new Structure("ecole", "École", StructureKind.Building, "commune", SchoolPlaces: 50));
        var workers = Workers(100);

        Education.RunDaily(structures, [workers], new Treasury());

        // 50 places pour 100 habitants : 50 élèves, 50 points.
        Assert.Equal(50, workers.EducationPoints, precision: 6);
    }

    [Fact]
    public void Une_ecole_privee_fait_payer_ses_frais_a_son_proprietaire()
    {
        var structures = Town(new Structure("ecole", "École", StructureKind.Building, "commune",
            Ownership.Private, "cadres", SchoolPlaces: 100, DailyTuition: 1));
        var workers = Workers(100, wealth: 20);
        var owners = new SocialClass("cadres", "Cadres", Rank.Executives, Communist, 1);

        Education.RunDaily(structures, [workers, owners], new Treasury());

        // Les ouvriers ne peuvent payer que 20 places sur 100.
        Assert.Equal(20, workers.EducationPoints, precision: 6);
        Assert.Equal(0, workers.Wealth, precision: 6);
        Assert.Equal(20, owners.Wealth, precision: 6);
    }

    [Fact]
    public void Les_points_d_education_font_naitre_des_professions_intermediaires_de_meme_ideologie()
    {
        var workers = Workers(1_000, wealth: 10_000);
        workers.EducationPoints = 365 * 5;

        var created = ClassTransitions.RunSeasonal([workers]);

        var intermediates = Assert.Single(created);
        Assert.Equal(Rank.Intermediate, intermediates.Rank);
        Assert.Equal(Communist, intermediates.Ideology);
        Assert.Equal("Professions intermédiaires communistes", intermediates.Name);
        Assert.Equal(5, intermediates.Population);
        Assert.Equal(995, workers.Population);
        Assert.Equal(0, workers.EducationPoints, precision: 6);

        // Ils emportent leur part de richesse et gardent leur commune.
        Assert.Equal(50, intermediates.Wealth, precision: 6);
        Assert.Equal(5, intermediates.MembersLivingIn("commune"), precision: 6);
    }

    [Fact]
    public void Les_nouvelles_professions_intermediaires_rejoignent_la_classe_qui_existe_deja()
    {
        var workers = Workers(1_000);
        workers.EducationPoints = 365 * 3;
        var intermediates = new SocialClass("intermediaires", "Professions intermédiaires communistes", Rank.Intermediate, Communist, 10);
        intermediates.SetResidence("commune", 1);

        var created = ClassTransitions.RunSeasonal([workers, intermediates]);

        Assert.Empty(created);
        Assert.Equal(13, intermediates.Population);
    }

    [Fact]
    public void Au_plus_deux_pourcent_de_la_classe_change_de_rang_par_saison()
    {
        var workers = Workers(1_000);
        workers.EducationPoints = 365 * 100;

        var intermediates = Assert.Single(ClassTransitions.RunSeasonal([workers]));

        Assert.Equal(20, intermediates.Population);
        Assert.Equal(365 * 80, workers.EducationPoints, precision: 6);
    }
}
