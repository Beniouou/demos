using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class LawEffectsTests
{
    private static readonly Structure State = new("etat", "État", StructureKind.PoliticalEntity, null);
    private static readonly Structure VillageA = new("village-a", "Village A", StructureKind.PoliticalEntity, "etat");
    private static readonly Structure FactoryB = new("usine-b", "Usine B", StructureKind.Building, "village-a");

    private static readonly Dictionary<string, Structure> Structures = new[] { State, VillageA, FactoryB }.ToDictionary(s => s.Id);

    private static readonly Orientation Egalitarian = new(IdeologicalAxis.Hierarchy, Pole.A);
    private static readonly Orientation Segregationist = new(IdeologicalAxis.Hierarchy, Pole.B);

    // Exemple du document : 200 membres, 50 vivent dans le village A, 10 d'entre eux travaillent dans l'usine B.
    private static SocialClass EgalitarianClass()
    {
        var socialClass = new SocialClass("c", "Classe", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 200);
        socialClass.SetShareSubjectTo("village-a", 0.25);
        socialClass.SetShareSubjectTo("usine-b", 0.05);
        return socialClass;
    }

    [Fact]
    public void Une_loi_s_applique_en_proportion_des_membres_concernes()
    {
        var socialClass = EgalitarianClass();

        Assert.Equal(1, socialClass.ShareSubjectTo(State));
        Assert.Equal(0.25, socialClass.ShareSubjectTo(VillageA));
        Assert.Equal(0.05, socialClass.ShareSubjectTo(FactoryB));
    }

    [Fact]
    public void Une_loi_acceptee_produit_de_la_legitimite_de_son_pole()
    {
        var pool = new LegitimacyPool();
        Law[] laws = [new("l", "Loi du village", "village-a", [Egalitarian])];

        LawEffects.ApplyDaily([EgalitarianClass()], laws, Structures, pool);

        // 200 membres × 25 % × 0,0001 = 0,005 point EG.
        Assert.Equal(0.005, pool[Egalitarian], precision: 9);
        Assert.Equal(0, pool[Segregationist]);
    }

    [Fact]
    public void Une_loi_rejetee_ne_produit_rien_et_baisse_l_adhesion()
    {
        var pool = new LegitimacyPool();
        var socialClass = EgalitarianClass();
        Law[] laws =
        [
            new("etat", "Loi d'État", "etat", [Segregationist]),
            new("village", "Loi du village", "village-a", [Egalitarian]),
        ];

        LawEffects.ApplyDaily([socialClass], laws, Structures, pool);

        // Acceptée sur 25 % des membres, applicable sur 100 % + 25 % : 0,25 / 1,25 = 0,2.
        Assert.Equal(0.2, socialClass.Adhesion, precision: 9);
        Assert.Equal(0, pool[Segregationist]);
    }

    [Fact]
    public void Sans_loi_applicable_l_adhesion_est_totale()
    {
        var socialClass = EgalitarianClass();
        Law[] laws = [new("l", "Loi d'une autre commune", "commune-2", [Segregationist])];
        var structures = new Dictionary<string, Structure>(Structures)
        {
            ["commune-2"] = new("commune-2", "Commune 2", StructureKind.PoliticalEntity, "etat"),
        };

        LawEffects.ApplyDaily([socialClass], laws, structures, new LegitimacyPool());

        Assert.Equal(1, socialClass.Adhesion);
    }
}
