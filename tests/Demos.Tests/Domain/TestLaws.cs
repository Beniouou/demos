using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

/// <summary>Un petit catalogue de lois pour les tests, indépendant de game/data/lois.json.</summary>
internal static class TestLaws
{
    public static readonly Orientation EG = Orientation.FromCode("EG");
    public static readonly Orientation SE = Orientation.FromCode("SE");
    public static readonly Orientation PR = Orientation.FromCode("PR");
    public static readonly Orientation LI = Orientation.FromCode("LI");
    public static readonly Orientation PO = Orientation.FromCode("PO");
    public static readonly Orientation EC = Orientation.FromCode("EC");

    /// <summary>Salaire minimum : 2 par défaut, de 1 à 4 ; EG et PR en hausse, SE et LI en baisse.</summary>
    public static readonly BrickDefinition MinimumWage = new("salaire-minimum", "Salaire minimum", "travail", BrickFamily.Parameter,
        [SubjectKind.Rank], 2, 1, 4, [new([EG, PR], [SE, LI])]);

    public static readonly BrickDefinition PaymentInKind = new(Bricks.PaymentInKind, "Paiement en nature", "travail", BrickFamily.Permission,
        [SubjectKind.Everyone, SubjectKind.Ownership], 0, 0, 1, [new([SE], [EG])], Weight: 0.5);

    /// <summary>Production : modificateur de −50 % à +50 % ; PO en hausse, EC en baisse.</summary>
    public static readonly BrickDefinition ProductionBrick = new("production", "Production", "environnement", BrickFamily.Modifier,
        [SubjectKind.Everyone, SubjectKind.BuildingType], 0, -0.5, 0.5, [new([PO], [EC])]);

    /// <summary>Entreprises privées : interdites par défaut ; LI quand on les autorise, PR quand on les interdit.</summary>
    public static readonly BrickDefinition PrivateCompanies = new(Bricks.PrivateCompanies, "Entreprises privées", "propriete",
        BrickFamily.Permission, [SubjectKind.Everyone, SubjectKind.BuildingType], 0, 0, 1, [new([LI], [PR])], Weight: 0.5);

    /// <summary>Impôt : PR en hausse, LI en baisse ; sur les cadres, EG et PR en hausse.</summary>
    public static readonly BrickDefinition Tax = new("impot", "Impôt", "impots", BrickFamily.Modifier,
        [SubjectKind.Everyone, SubjectKind.Rank], 0, -1, 1,
        [new([PR], [LI]), new([EG, PR], [SE, LI], new LawSubject(SubjectKind.Rank, "Executives"))]);

    public static readonly LawCatalogue Catalogue = new(
        [new("travail", "Travail"), new("environnement", "Environnement"), new("propriete", "Propriété"), new("impots", "Impôts"),
         new("douanes", "Douanes", Regalian: true)],
        [MinimumWage, PaymentInKind, ProductionBrick, PrivateCompanies, Tax],
        [new("salaire-minimum-ouvrier", "Salaire minimum ouvrier", "travail", "commune",
            [new("salaire-minimum", new LawSubject(SubjectKind.Rank, "Workers"), 2.5)])]);

    public static LawBook Book(IReadOnlyDictionary<string, Structure> structures, params Law[] laws) => new(Catalogue, laws, structures);

    /// <summary>Applique les lois aux bâtiments, comme la simulation le fait chaque jour.</summary>
    public static void Apply(Dictionary<string, Structure> structures, params Law[] laws)
    {
        var book = Book(structures, laws);
        foreach (var building in structures.Values.Where(s => s.Kind == StructureKind.Building).ToList())
            structures[building.Id] = building with { Laws = book.RulesFor(building) };
    }

    public static Law Law(string id, string structureId, string domain, params BrickSetting[] bricks) => new(id, id, domain, structureId, bricks);

    public static BrickSetting Brick(BrickDefinition brick, LawSubject subject, double value, bool locked = false) =>
        new(brick.Id, subject, value, locked);

    public static LawSubject RankSubject(string rank) => new(SubjectKind.Rank, rank);
}
