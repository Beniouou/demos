using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using static Demos.Tests.Domain.TestLaws;

namespace Demos.Tests.Domain;

[Collection(GlobalBalanceCollection.Name)]
public class LawCostTests
{
    private static readonly Dictionary<string, Structure> Structures = new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        new("carriere", "Carrière", StructureKind.Building, "commune", Jobs: 100, TypeId: "carriere"),
        new("ferme", "Ferme", StructureKind.Building, "commune", Ownership.Private, Jobs: 100, TypeId: "ferme"),
    }.ToDictionary(s => s.Id);

    /// <summary>1 000 ouvriers et 100 cadres, tous dans la commune.</summary>
    private static SocialClass[] Classes()
    {
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 1_000);
        workers.SetResidence("commune", 1);
        var executives = new SocialClass("cadres", "Cadres", Rank.Executives, new Ideology(Pole.B, Pole.A, Pole.B), 100);
        executives.SetResidence("commune", 1);
        return [workers, executives];
    }

    private static Law Wage(double value, bool locked = false, string level = "commune") =>
        Law("salaire", level, "travail", Brick(MinimumWage, RankSubject("Workers"), value, locked));

    [Fact]
    public void Une_brique_coute_trente_jours_de_ce_qu_elle_rapporterait_payes_dans_ses_poles()
    {
        Balance.Current = new BalanceSettings();

        var cost = LawCost.Of(Book(Structures), Classes(), null, Wage(2.5));

        // Membres pondérés : 1 000 ouvriers visés + 100 cadres × 0,5 = 1 050. 1 050 × 0,001 × 30 × 0,25 = 7,875.
        Assert.Equal(7.875 / 2, cost[EG], precision: 9);
        Assert.Equal(7.875 / 2, cost[PR], precision: 9);
        Assert.Equal(2, cost.Count);
    }

    [Fact]
    public void Verrouiller_une_brique_la_rend_plus_chere()
    {
        Balance.Current = new BalanceSettings();

        var cost = LawCost.Of(Book(Structures), Classes(), null, Wage(2.5, locked: true));

        Assert.Equal(7.875 * 1.5, cost.Values.Sum(), precision: 9);
    }

    [Fact]
    public void Changer_un_curseur_ne_coute_que_le_mouvement()
    {
        Balance.Current = new BalanceSettings();
        var current = Wage(2.5);

        var cost = LawCost.Of(Book(Structures, current), Classes(), current, Wage(3));

        // De 2,5 à 3 : encore un quart du chemin vers la borne.
        Assert.Equal(7.875, cost.Values.Sum(), precision: 9);
    }

    [Fact]
    public void Abroger_se_paie_dans_les_poles_opposes()
    {
        Balance.Current = new BalanceSettings();
        var current = Wage(2.5);

        var cost = LawCost.Of(Book(Structures, current), Classes(), current, null);

        Assert.Equal(7.875 / 2, cost[SE], precision: 9);
        Assert.Equal(7.875 / 2, cost[LI], precision: 9);
        Assert.False(cost.ContainsKey(EG));
    }

    [Fact]
    public void Un_petit_reglage_coute_au_moins_un_point()
    {
        Balance.Current = new BalanceSettings();

        var cost = LawCost.Of(Book(Structures), Classes(), null, Wage(2.01));

        Assert.Equal(1, cost.Values.Sum(), precision: 9);
    }

    [Fact]
    public void Un_domaine_regalien_vote_sous_l_Etat_coute_double()
    {
        Balance.Current = new BalanceSettings();
        var customs = new BrickDefinition("droits", "Droits de douane", "douanes", BrickFamily.Modifier, [SubjectKind.Everyone], 0, -1, 1,
            [new([PR], [LI])]);
        var catalogue = Catalogue with { Bricks = [.. Catalogue.Bricks, customs] };
        Law Customs(string level) => Law("douane", level, "douanes", Brick(customs, LawSubject.Everyone, 0.5));

        var national = LawCost.Of(new LawBook(catalogue, [], Structures), Classes(), null, Customs("etat"));
        var local = LawCost.Of(new LawBook(catalogue, [], Structures), Classes(), null, Customs("commune"));

        Assert.Equal(2 * national[PR], local[PR], precision: 9);
    }
}

public class LawBookTests
{
    private static readonly Dictionary<string, Structure> Structures = new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        new("carriere", "Carrière", StructureKind.Building, "commune", Jobs: 100, TypeId: "carriere"),
        new("ferme", "Ferme", StructureKind.Building, "commune", Ownership.Private, Jobs: 100, TypeId: "ferme"),
    }.ToDictionary(s => s.Id);

    private static readonly LawSubject Quarries = new(SubjectKind.BuildingType, "carriere");

    [Fact]
    public void Sans_loi_une_brique_garde_sa_valeur_par_defaut()
    {
        Assert.Equal(0, Book(Structures).ValueFor(Bricks.PrivateCompanies, Structures["ferme"]));
    }

    [Fact]
    public void Pour_une_autorisation_le_niveau_le_plus_bas_l_emporte()
    {
        var allowed = Law("liberte", "etat", "propriete", Brick(PrivateCompanies, LawSubject.Everyone, 1));
        var forbiddenHere = Law("interdit", "commune", "propriete", Brick(PrivateCompanies, new LawSubject(SubjectKind.BuildingType, "ferme"), 0));
        var book = Book(Structures, allowed, forbiddenHere);

        Assert.Equal(0, book.ValueFor(Bricks.PrivateCompanies, Structures["ferme"]));
        Assert.Equal(1, book.ValueFor(Bricks.PrivateCompanies, Structures["carriere"]));
    }

    [Fact]
    public void Une_brique_verrouillee_ne_peut_plus_etre_changee_plus_bas()
    {
        var allowed = Law("liberte", "etat", "propriete", Brick(PrivateCompanies, LawSubject.Everyone, 1, locked: true));
        var forbiddenHere = Law("interdit", "commune", "propriete", Brick(PrivateCompanies, LawSubject.Everyone, 0));
        var book = Book(Structures, allowed, forbiddenHere);

        Assert.Equal(1, book.ValueFor(Bricks.PrivateCompanies, Structures["ferme"]));
        Assert.True(book.IsLockedAbove(forbiddenHere, forbiddenHere.Bricks[0]));
        Assert.False(book.IsLockedAbove(allowed, allowed.Bricks[0]));
    }

    [Fact]
    public void Les_modificateurs_de_chaque_niveau_s_additionnent()
    {
        var national = Law("national", "etat", "environnement", Brick(ProductionBrick, LawSubject.Everyone, -0.1));
        var local = Law("local", "commune", "environnement", Brick(ProductionBrick, Quarries, -0.2));
        var book = Book(Structures, national, local);

        Assert.Equal(-0.3, book.ValueFor("production", Structures["carriere"]), precision: 9);
        Assert.Equal(-0.1, book.ValueFor("production", Structures["ferme"]), precision: 9);
        // Un modificateur se mesure depuis 0 : le local fait −20 %, pas −10 % de plus que l'État.
        Assert.Equal(0.4, book.MovementOf(local, local.Bricks[0])!.Value.Intensity, precision: 9);
    }

    [Fact]
    public void Une_loi_a_plus_de_trois_briques_ou_hors_de_son_domaine_n_est_pas_valable()
    {
        var subject = LawSubject.Everyone;
        var tooMany = new Law("l", "l", "environnement", "etat",
        [
            Brick(ProductionBrick, subject, 0.1), Brick(ProductionBrick, Quarries, 0.1),
            Brick(ProductionBrick, new LawSubject(SubjectKind.BuildingType, "ferme"), 0.1),
            Brick(ProductionBrick, new LawSubject(SubjectKind.BuildingType, "mine"), 0.1),
        ]);
        var otherDomain = Law("l", "etat", "travail", Brick(ProductionBrick, subject, 0.1));
        var outOfBounds = Law("l", "etat", "environnement", Brick(ProductionBrick, subject, 0.9));

        Assert.NotNull(Catalogue.ProblemWith(tooMany));
        Assert.NotNull(Catalogue.ProblemWith(otherDomain));
        Assert.NotNull(Catalogue.ProblemWith(outOfBounds));
        Assert.Null(Catalogue.ProblemWith(Law("l", "etat", "environnement", Brick(ProductionBrick, subject, 0.1))));
    }
}
