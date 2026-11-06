using Demos.Domain.Politics;
using Demos.Domain.Population;
using static Demos.Tests.Domain.TestLaws;

namespace Demos.Tests.Domain;

/// <summary>
/// L'arbre des entités : l'État et la commune sont les unités de base, les préfectures s'imbriquent librement entre eux.
/// </summary>
public class PrefectureTreeTests
{
    // État → grande préfecture → préfecture (chef-lieu : commune A) → communes A et B ; commune C directement sous l'État.
    private static readonly Structure State = new("etat", "État", StructureKind.PoliticalEntity, null);
    private static readonly Structure Big = new("grande", "Grande préfecture", StructureKind.PoliticalEntity, "etat",
        EntityType: EntityLevel.Prefecture, SeatId: "commune-a");
    private static readonly Structure Prefecture = new("prefecture", "Préfecture", StructureKind.PoliticalEntity, "grande",
        EntityType: EntityLevel.Prefecture, SeatId: "commune-a");
    private static readonly Structure CommuneA = new("commune-a", "Commune A", StructureKind.PoliticalEntity, "prefecture");
    private static readonly Structure CommuneB = new("commune-b", "Commune B", StructureKind.PoliticalEntity, "prefecture");
    private static readonly Structure CommuneC = new("commune-c", "Commune C", StructureKind.PoliticalEntity, "etat");
    private static readonly Structure Seat = new("batiment-prefecture", "Bâtiment de préfecture", StructureKind.Building, "prefecture");
    private static readonly Structure Farm = new("ferme", "Ferme", StructureKind.Building, "commune-b");

    private static readonly Dictionary<string, Structure> Structures =
        new[] { State, Big, Prefecture, CommuneA, CommuneB, CommuneC, Seat, Farm }.ToDictionary(s => s.Id);

    [Fact]
    public void Le_type_d_une_entite_se_deduit_de_sa_place()
    {
        Assert.Equal(EntityLevel.State, State.Level);
        Assert.Equal(EntityLevel.Prefecture, Prefecture.Level);
        Assert.Equal(EntityLevel.Commune, CommuneA.Level);
        Assert.Null(Farm.Level);
    }

    [Fact]
    public void Une_commune_dans_une_prefecture_reste_une_commune()
    {
        var communes = Communes.All(Structures).Select(c => c.Id).Order().ToList();

        Assert.Equal(["commune-a", "commune-b", "commune-c"], communes);
    }

    [Fact]
    public void Un_batiment_de_prefecture_se_trouve_dans_son_chef_lieu()
    {
        Assert.Equal("commune-b", Communes.CommuneOf(Farm, Structures));
        Assert.Equal("commune-a", Communes.CommuneOf(Seat, Structures));
        Assert.Equal("commune-a", Communes.CommuneOf(CommuneA, Structures));
        Assert.Null(Communes.CommuneOf(Prefecture, Structures));
        Assert.Null(Communes.CommuneOf(State, Structures));
    }

    [Fact]
    public void Les_communes_d_une_prefecture_se_comptent_a_toute_profondeur()
    {
        Assert.Equal(["commune-a", "commune-b"], Communes.Within(Big, Structures).Select(c => c.Id).Order().ToList());
        Assert.Equal(3, Communes.Within(State, Structures).Count());
    }

    [Fact]
    public void Une_loi_de_prefecture_touche_les_habitants_de_ses_communes()
    {
        var socialClass = new SocialClass("c", "Classe", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 200);
        socialClass.SetResidence("commune-a", 0.25);
        socialClass.SetResidence("commune-b", 0.25);
        socialClass.SetResidence("commune-c", 0.5);
        var book = Book(Structures);

        Assert.Equal(0.5, book.WeightedShare(socialClass, new LawSubject(SubjectKind.Everyone), Big, 0.5), 6);
    }
}
