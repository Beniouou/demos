using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Application;

/// <summary>Le début de partie scénarisé (décidé le 27 novembre 2026) : départ d'exil, acte fondateur, serment, contrôle avant un acte.</summary>
[Collection(GlobalBalanceCollection.Name)]
public class ExileStartTests
{
    private static readonly IReadOnlyList<EngagementDefinition> Catalogue = GameData.Content().LoadEngagements();
    private static readonly IReadOnlyList<PromiseDefinition> Promises = GameData.Content().LoadPromises();

    private static PromiseDefinition PromiseOf(string id) => Promises.Single(p => p.Id == id);

    private static EngagementDefinition Engagement(string id) => Catalogue.Single(e => e.Id == id);

    private static Simulation Exile(string promise, string[] engagements, params string[] laws) =>
        PrototypeScenario.Create(
            GameData.Content(),
            new StartingLaws(StartingLawsMode.Chosen, laws.Select(l => new ScenarioLaw(l)).ToList()),
            new ExileStart("Bobo", PromiseOf(promise), engagements.Select(Engagement).ToList()));

    [Fact]
    public void Chaque_Promesse_a_sa_premiere_phrase_de_serment()
    {
        Assert.Equal(26, Promises.Count);
        Assert.All(Promises, p => Assert.False(string.IsNullOrWhiteSpace(p.Opening), p.Id));
    }

    [Fact]
    public void Le_serment_joint_les_phrases_des_poles_dans_l_ordre_des_axes()
    {
        var oath = PromiseOath.Of(PromiseOf("eg-ec"), "Bobo");

        Assert.Equal("Nous, peuple de Bobo, avons fui une terre que les puissants épuisaient.", oath.First);
        Assert.Equal("Sur cette terre qui ne nous doit rien, nous faisons serment qu'aucun ne sera placé au-dessus d'un autre "
            + "et de prendre à la terre sans la ruiner.", oath.Middle);
        Assert.Equal(PromiseOath.LastSentence, oath.Last);
        Assert.Equal("qu'aucun ne sera placé au-dessus d'un autre, de prendre à la terre sans la ruiner et que ce qui fait vivre appartiendra à tous.",
            PromiseOath.Of(PromiseOf("eg-ec-co"), "Bobo").Middle["Sur cette terre qui ne nous doit rien, nous faisons serment ".Length..]);
    }

    [Fact]
    public void Le_depart_d_exil_compte_mille_exiles_qui_partagent_La_Promesse()
    {
        var simulation = Exile("eg-ec", ["eg-logement", "ec-aucune-loi-contraire"], "normes-pollution");
        var world = simulation.World;

        Assert.Equal(1_000, world.Classes.Sum(c => c.Population));
        Assert.Equal(3, world.Classes.Count);
        Assert.All(world.Classes, c => Assert.Equal(Pole.A, c.Ideology[IdeologicalAxis.Hierarchy]));
        Assert.All(world.Classes, c => Assert.Equal(Pole.B, c.Ideology[IdeologicalAxis.Production]));
        // Sur l'axe libre, chaque rang garde son pôle du bac à sable.
        Assert.Equal(Pole.A, world.Classes.Single(c => c.Rank == Rank.Workers).Ideology[IdeologicalAxis.Property]);
        Assert.Equal(Pole.B, world.Classes.Single(c => c.Rank == Rank.Executives).Ideology[IdeologicalAxis.Property]);
        Assert.Equal(1, simulation.PromiseGauge!.Value, precision: 6);
        Assert.Equal("Bobo", world.PromiseState!.PeopleName);
        Assert.Equal(["Normes de pollution"], world.PromiseState.FoundingLaws);
    }

    [Fact]
    public void Le_depart_d_exil_est_pauvre()
    {
        var world = Exile("eg", ["eg-logement"]).World;
        var buildings = world.Structures.Values.Where(s => s.Kind == StructureKind.Building).ToList();

        Assert.Equal(8, buildings.Count);
        Assert.All(buildings, b => Assert.Equal(Ownership.Public, b.Ownership));
        Assert.DoesNotContain(buildings, b => b.TypeId is "mairie" or "ecole" or "forge" or "mine" or "carriere");
        Assert.Equal(5_000, world.Treasury.Balance, precision: 0);
        Assert.Equal(10, world.CartsOf(PrototypeScenario.FirstCommuneId), precision: 6);
        // Les vivres tiennent au grenier : 5 jours de farine pour 1 000 bouches (décidé le 27 novembre 2026).
        Assert.Equal(5_000, world.MarketOf(PrototypeScenario.FirstCommuneId).QuantityOf("farine"), precision: 0);
        Assert.False(world.LawBook.AllowsExtension(PrototypeScenario.FirstCommuneId));
    }

    [Fact]
    public void Une_loi_de_l_An_I_qui_romprait_un_engagement_est_refusee()
    {
        var content = GameData.Content();
        var terreGardee = new[] { Engagement("ec-aucune-loi-contraire") };

        Assert.NotEmpty(PrototypeScenario.FoundingLawBreaches(content.LoadLawCatalogue(), new ScenarioLaw("loi-extension"), terreGardee));
        Assert.Empty(PrototypeScenario.FoundingLawBreaches(content.LoadLawCatalogue(), new ScenarioLaw("normes-pollution"), terreGardee));
        Assert.Throws<InvalidDataException>(() => Exile("ec", ["ec-aucune-loi-contraire"], "loi-extension"));
    }

    [Fact]
    public void Le_jeu_previent_avant_un_acte_qui_trahit_sans_rien_noter()
    {
        var simulation = Exile("ec", ["ec-nouvelles-terres"]);

        var breach = Assert.Single(simulation.PromiseBreachesOfFounding());
        Assert.Equal("ec-nouvelles-terres", breach.Id);
        Assert.Empty(simulation.World.PromiseState!.Wounds);
        Assert.Empty(simulation.PromiseBreachesOfNationalization(NationalizationMode.Buyout));
    }

    [Fact]
    public void Le_jeu_previent_avant_de_voter_une_loi_qui_trahit()
    {
        var simulation = Exile("ec", ["ec-aucune-loi-contraire"]);
        var template = simulation.World.LawCatalogue.TemplateOf("loi-extension")!;
        var draft = simulation.DraftFromTemplate(template.Id, null);

        var view = new GameView(simulation).PromiseBreachesOfDraft(draft!);
        var breach = Assert.Single(view);
        Assert.Equal(1, breach.Article);
        Assert.Equal(Balance.Current.PromiseBetrayalWound, breach.Wound, precision: 6);
        Assert.Equal(3, breach.ClassesHit);
        Assert.Empty(simulation.World.PromiseState!.Wounds);
    }
}
