using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using static Demos.Tests.Domain.TestLaws;

namespace Demos.Tests.Domain;

/// <summary>
/// Effets de l'efficacité : une entité débordée applique mal ses lois, produit moins de légitimité, collecte moins
/// d'impôts et sa production publique baisse.
/// </summary>
public class ExtensionEffectsTests
{
    private static readonly BalanceSettings Settings = new();

    // Une commune de 39 375 habitants, une ferme publique et une SCOP : charge 1,96875 + 0,025 + 0,00625 = 2,
    // efficacité 50 %, sous un État à 100 %.
    private static Dictionary<string, Structure> Town() => new[]
    {
        new Structure("etat", "État", StructureKind.PoliticalEntity, null),
        new Structure("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        new Structure("ferme", "Ferme", StructureKind.Building, "commune", Production: new Production("pain", 1), Jobs: 10),
        new Structure("scop", "SCOP", StructureKind.Building, "commune", Ownership.Cooperative, "ouvriers", new Production("pain", 1), Jobs: 10),
    }.ToDictionary(s => s.Id);

    private static Extension Overloaded(IReadOnlyDictionary<string, Structure> structures) =>
        Extension.Compute(structures, id => id == "commune" ? 39_375 : 0, Settings);

    private static LawBook Book(Dictionary<string, Structure> structures, Extension extension, params Law[] laws) =>
        new(Catalogue, laws, structures, extension: extension);

    [Fact]
    public void Un_parametre_ne_fait_qu_une_part_du_chemin()
    {
        var structures = Town();
        var law = Law("salaire", "commune", "travail", Brick(MinimumWage, RankSubject("Workers"), 3));

        var book = Book(structures, Overloaded(structures), law);

        // Défaut 2, loi 3, efficacité 50 % : 2,5.
        Assert.Equal(2.5, book.LegislatedValue(MinimumWage.Id, "ferme", _ => true)!.Value, 6);
    }

    [Fact]
    public void Un_modificateur_ne_donne_qu_une_part_de_son_pourcentage()
    {
        var structures = Town();
        var law = Law("quota", "commune", "environnement", Brick(ProductionBrick, new LawSubject(SubjectKind.Everyone), -0.2));

        var rules = Book(structures, Overloaded(structures), law).RulesFor(structures["ferme"]);

        Assert.Equal(0.9, rules.ProductionFactor, 6);
    }

    [Fact]
    public void Une_autorisation_n_est_pas_touchee()
    {
        var structures = Town();
        var law = Law("prive", "commune", "propriete", Brick(PrivateCompanies, new LawSubject(SubjectKind.Everyone), 1));

        Assert.Equal(1, Book(structures, Overloaded(structures), law).ValueFor(PrivateCompanies.Id, structures["ferme"]), 6);
    }

    [Fact]
    public void Une_loi_mal_appliquee_produit_moins_de_legitimite()
    {
        var structures = Town();
        var law = Law("salaire", "commune", "travail", Brick(MinimumWage, RankSubject("Workers"), 4));

        double LegitimacyWith(Extension extension)
        {
            var socialClass = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 1_000);
            socialClass.SetResidence("commune", 1);
            var pool = new LegitimacyPool();
            LawEffects.ApplyDaily([socialClass], Book(structures, extension, law), pool);
            return pool[EG];
        }

        var full = LegitimacyWith(Extension.None(structures));
        Assert.True(full > 0);
        Assert.Equal(full * 0.5, LegitimacyWith(Overloaded(structures)), 6);
    }

    [Fact]
    public void L_impot_non_collecte_reste_a_l_habitant()
    {
        var socialClass = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 100) { Wealth = 1_000 };
        socialClass.SetResidence("commune", 1);
        var treasury = new Treasury();
        var fund = new Treasury();

        Taxation.CollectDaily([socialClass], 0.01, _ => 0.01, treasury, _ => fund, stateCollectionOf: _ => 0.81, communeCollectionOf: _ => 0.5);

        Assert.Equal(8.1, treasury.Balance, 6);
        Assert.Equal(5, fund.Balance, 6);
        Assert.Equal(1_000 - 8.1 - 5, socialClass.Wealth, 6);
    }

    [Fact]
    public void La_production_publique_baisse_avec_l_efficacite()
    {
        var structures = Town();
        var extension = Overloaded(structures);
        var socialClass = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 20);
        socialClass.SetResidence("commune", 1);
        SocialClass[] classes = [socialClass];
        var treasury = new Treasury();
        treasury.Deposit(1_000);
        var market = new Market([new ProductDefinition("pain", "Pain", NeedCategory.Food, 1)]);

        double EfficiencyOf(Structure building) =>
            building.Ownership == Ownership.Public && Extension.ManagerOf(building, structures) is { } manager
                ? extension.EfficiencyOf(manager)
                : 1;

        var produced = WorkDay.Run(structures, Employment.Assign(structures, classes), classes, treasury, _ => market,
            publicEfficiencyOf: EfficiencyOf);

        Assert.Equal(produced["scop"] * 0.5, produced["ferme"], 6);
    }
}
