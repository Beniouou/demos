using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class MigrationTests
{
    private static readonly Dictionary<string, Structure> Structures = new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("ancienne", "Ancienne commune", StructureKind.PoliticalEntity, "etat"),
        new("nouvelle", "Nouvelle commune", StructureKind.PoliticalEntity, "etat"),
        new("ferme", "Ferme", StructureKind.Building, "ancienne", Jobs: 100),
        new("camp", "Camp de base", StructureKind.Building, "nouvelle", Jobs: 300),
    }.ToDictionary(s => s.Id);

    [Fact]
    public void Une_part_des_sans_emploi_part_vers_la_commune_qui_a_le_plus_d_emplois_libres()
    {
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 1_100);
        workers.SetResidence("ancienne", 1);
        workers.SetEmployment("ferme", 100);
        workers.Wealth = 1_100;
        workers.EducationPoints = 110;

        var arrived = Assert.Single(Migration.RunSeasonal(Structures, [workers]));

        // 1 000 sans-emploi × 5 % = 50 partent : ils forment le groupe des mêmes ouvriers dans la nouvelle commune,
        // avec leur part de richesse et de points d'éducation.
        Assert.Equal("nouvelle", arrived.CommuneId);
        Assert.Equal(workers.Ideology, arrived.Ideology);
        Assert.Equal(50, arrived.MembersLivingIn("nouvelle"), precision: 6);
        Assert.Equal(1_050, workers.MembersLivingIn("ancienne"), precision: 6);
        Assert.Equal(50, arrived.Wealth, precision: 6);
        Assert.Equal(5, arrived.EducationPoints, precision: 6);
    }

    [Fact]
    public void Avec_une_seule_commune_personne_ne_part()
    {
        var oneCommune = Structures.Where(s => s.Key != "nouvelle" && s.Key != "camp").ToDictionary();
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 1_100);
        workers.SetResidence("ancienne", 1);

        Migration.RunSeasonal(oneCommune, [workers]);

        Assert.Equal(1_100, workers.MembersLivingIn("ancienne"), precision: 6);
    }

    [Fact]
    public void La_commune_d_un_batiment_est_celle_juste_sous_l_Etat()
    {
        Assert.Equal("ancienne", Communes.CommuneOf(Structures["ferme"], Structures));
        Assert.Equal("ancienne", Communes.CommuneOf(Structures["ancienne"], Structures));
        Assert.Null(Communes.CommuneOf(Structures["etat"], Structures));
    }
}
