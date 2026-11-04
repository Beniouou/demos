using Demos.Domain.Politics;
using Demos.Domain.Population;
using static Demos.Tests.Domain.TestLaws;

namespace Demos.Tests.Domain;

public class LawEffectsTests
{
    private static readonly Structure State = new("etat", "État", StructureKind.PoliticalEntity, null);
    private static readonly Structure VillageA = new("village-a", "Village A", StructureKind.PoliticalEntity, "etat");
    private static readonly Structure FactoryB = new("usine-b", "Usine B", StructureKind.Building, "village-a");

    private static readonly Dictionary<string, Structure> Structures = new[] { State, VillageA, FactoryB }.ToDictionary(s => s.Id);

    // Exemple du document : 200 membres, 50 vivent dans le village A, 10 d'entre eux travaillent dans l'usine B.
    // Ouvriers communistes : EG, PO et PR.
    private static SocialClass EgalitarianClass()
    {
        var socialClass = new SocialClass("c", "Classe", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 200);
        socialClass.SetResidence("village-a", 0.25);
        socialClass.SetEmployment("usine-b", 10);
        return socialClass;
    }

    private static Law VillageMinimumWage(string rank, double value) =>
        Law("salaire", "village-a", "travail", Brick(MinimumWage, RankSubject(rank), value));

    [Fact]
    public void Une_loi_s_applique_en_proportion_des_membres_concernes()
    {
        var socialClass = EgalitarianClass();

        Assert.Equal(1, socialClass.ShareSubjectTo(State));
        Assert.Equal(0.25, socialClass.ShareSubjectTo(VillageA));
        Assert.Equal(0.05, socialClass.ShareSubjectTo(FactoryB));
    }

    [Fact]
    public void Une_brique_acceptee_produit_de_la_legitimite_selon_son_intensite()
    {
        var pool = new LegitimacyPool();

        LawEffects.ApplyDaily([EgalitarianClass()], Book(Structures, VillageMinimumWage("Workers", 2.5)), pool);

        // Salaire minimum des ouvriers de 2 à 2,5 (borne 4) : intensité 0,25. 200 membres × 25 % × 0,25 × 0,001 par pôle.
        Assert.Equal(0.0125, pool[EG], precision: 9);
        Assert.Equal(0.0125, pool[PR], precision: 9);
        Assert.Equal(0, pool[SE]);
    }

    [Fact]
    public void Les_membres_que_le_sujet_ne_vise_pas_comptent_pour_moitie()
    {
        var pool = new LegitimacyPool();

        LawEffects.ApplyDaily([EgalitarianClass()], Book(Structures, VillageMinimumWage("Executives", 2.5)), pool);

        // Le sujet vise les cadres : les ouvriers du village comptent pour 0,5.
        Assert.Equal(0.00625, pool[EG], precision: 9);
    }

    [Fact]
    public void Une_brique_a_sa_valeur_par_defaut_ne_fait_reagir_personne()
    {
        var pool = new LegitimacyPool();
        var socialClass = EgalitarianClass();

        LawEffects.ApplyDaily([socialClass], Book(Structures, VillageMinimumWage("Workers", 2)), pool);

        Assert.Equal(0, pool[EG]);
        Assert.Equal(1, socialClass.Adhesion);
    }

    [Fact]
    public void Une_brique_rejetee_ne_produit_rien_et_baisse_l_adhesion()
    {
        var pool = new LegitimacyPool();
        var socialClass = EgalitarianClass();
        var quota = Law("quota", "etat", "environnement", Brick(ProductionBrick, LawSubject.Everyone, -0.2));

        LawEffects.ApplyDaily([socialClass], Book(Structures, quota, VillageMinimumWage("Workers", 2.5)), pool);

        // Quota d'État (EC, rejeté) : 100 % × 0,4. Salaire du village (EG et PR, acceptés) : 2 pôles × 25 % × 0,25.
        Assert.Equal(0.125 / 0.525, socialClass.Adhesion, precision: 9);
        Assert.Equal(0, pool[EC]);
    }

    [Fact]
    public void L_ecart_d_une_loi_se_mesure_depuis_la_valeur_heritee_des_niveaux_superieurs()
    {
        var state = Law("etat-salaire", "etat", "travail", Brick(MinimumWage, RankSubject("Workers"), 3));
        var village = VillageMinimumWage("Workers", 2.5);
        var book = Book(Structures, state, village);

        var movement = book.MovementOf(village, village.Bricks[0])!.Value;

        // L'État fixe 3 : le village baisse à 2,5, un quart du chemin vers la borne basse (1). Pôles SE et LI.
        Assert.Equal(3, movement.Reference);
        Assert.Equal(0.25, movement.Intensity, precision: 9);
        Assert.Equal([SE, LI], movement.Poles);
    }

    [Fact]
    public void Sans_loi_applicable_l_adhesion_est_totale()
    {
        var socialClass = EgalitarianClass();
        var structures = new Dictionary<string, Structure>(Structures)
        {
            ["commune-2"] = new("commune-2", "Commune 2", StructureKind.PoliticalEntity, "etat"),
        };
        var elsewhere = Law("ailleurs", "commune-2", "travail", Brick(MinimumWage, RankSubject("Workers"), 1));

        LawEffects.ApplyDaily([socialClass], Book(structures, elsewhere), new LegitimacyPool());

        Assert.Equal(1, socialClass.Adhesion);
    }
}
