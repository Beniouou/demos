using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class FoundingCharterTests
{
    private static readonly Dictionary<string, Structure> Structures = new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("ancienne", "Ancienne commune", StructureKind.PoliticalEntity, "etat"),
        new("verte", "Commune verte", StructureKind.PoliticalEntity, "etat"),
        new("grise", "Commune grise", StructureKind.PoliticalEntity, "etat"),
        new("ferme", "Ferme", StructureKind.Building, "ancienne", Jobs: 100),
        new("camp-verte", "Camp de base", StructureKind.Building, "verte", Jobs: 300),
        new("camp-grise", "Camp de base", StructureKind.Building, "grise", Jobs: 400),
    }.ToDictionary(s => s.Id);

    private static readonly Law[] Charters =
    [
        new("charte-verte", "Charte", "verte", [new Orientation(IdeologicalAxis.Production, Pole.B)], [LawRule.FoundingCharter]),
        new("charte-grise", "Charte", "grise", [new Orientation(IdeologicalAxis.Production, Pole.A)], [LawRule.FoundingCharter]),
    ];

    private static SocialClass Unemployed(string id, Pole production)
    {
        var workers = new SocialClass(id, id, Rank.Workers, new Ideology(Pole.A, production, Pole.A), 1_100);
        workers.SetResidence("ancienne", 1);
        workers.SetEmployment("ferme", 100);
        return workers;
    }

    [Fact]
    public void Les_ecologistes_vont_vers_la_commune_ecologiste_meme_si_elle_a_moins_d_emplois()
    {
        var ecologists = Unemployed("ecologistes", Pole.B);
        var positivists = Unemployed("positivistes", Pole.A);

        var arrived = Migration.RunSeasonal(Structures, [ecologists, positivists], Charters);

        // Écologistes : 300 emplois × 2 = 600 dans la commune verte, contre 400 × 0,5 = 200 dans la grise.
        // Positivistes : 300 × 0,5 = 150 dans la verte, contre 400 × 2 = 800 dans la grise.
        Assert.Equal("verte", arrived.Single(c => c.Ideology == ecologists.Ideology).CommuneId);
        Assert.Equal("grise", arrived.Single(c => c.Ideology == positivists.Ideology).CommuneId);
    }

    [Fact]
    public void Sans_charte_tout_le_monde_va_vers_la_commune_qui_a_le_plus_d_emplois()
    {
        var ecologists = Unemployed("ecologistes", Pole.B);

        var arrived = Assert.Single(Migration.RunSeasonal(Structures, [ecologists]));

        Assert.Equal("grise", arrived.CommuneId);
    }

    [Fact]
    public void L_attrait_double_pour_le_meme_pole_et_diminue_de_moitie_pour_le_pole_oppose()
    {
        Assert.Equal(2, Migration.AttractionOf("verte", Unemployed("ecologistes", Pole.B), Charters));
        Assert.Equal(0.5, Migration.AttractionOf("verte", Unemployed("positivistes", Pole.A), Charters));
        Assert.Equal(1, Migration.AttractionOf("ancienne", Unemployed("positivistes", Pole.A), Charters));
    }
}
