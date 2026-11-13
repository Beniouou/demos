using Demos.Application;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

/// <summary>Commission des lois, modèles et lois de départ, avec le vrai catalogue (game/data/lois.json).</summary>
public class LawCommissionTests
{
    private static readonly Orientation EG = Orientation.FromCode("EG");
    private static readonly Orientation PR = Orientation.FromCode("PR");
    private static readonly Orientation EC = Orientation.FromCode("EC");

    private static Simulation WithoutLaws() => PrototypeScenario.Create(GameData.Content(), StartingLaws.None);

    private static void AdvanceDays(Simulation simulation, int days)
    {
        for (var day = 0; day < days; day++)
            simulation.AdvanceDay();
    }

    [Fact]
    public void Chaque_modele_du_catalogue_est_une_loi_valable_a_son_niveau_par_defaut()
    {
        var world = WithoutLaws().World;

        Assert.Equal(12, world.LawCatalogue.Templates.Count);
        foreach (var template in world.LawCatalogue.Templates)
        {
            Assert.Null(world.LawCatalogue.ProblemWith(template.ToLaw("essai")));
            Assert.True(world.Structures.ContainsKey(template.Level), template.Id);
        }
    }

    [Fact]
    public void Chaque_pole_a_au_moins_un_modele_qui_le_favorise()
    {
        var simulation = WithoutLaws();
        var book = simulation.World.LawBook;

        var poles = simulation.World.LawCatalogue.Templates
            .Select(t => t.ToLaw(t.Id))
            .SelectMany(law => law.Bricks.Select(s => book.MovementOf(law, s)!.Value))
            .SelectMany(m => m.Poles)
            .ToHashSet();

        Assert.Equal(Orientation.All.ToHashSet(), poles);
    }

    [Fact]
    public void La_partie_commence_avec_les_lois_choisies_a_sa_creation()
    {
        var preset = PrototypeScenario.Create(GameData.Content());
        var none = WithoutLaws();
        var chosen = PrototypeScenario.Create(GameData.Content(),
            new StartingLaws(StartingLawsMode.Chosen, [new ScenarioLaw("impot-progressif"), new ScenarioLaw("plafond-loyers")]));

        Assert.Equal(["liberte-entreprendre", "economie-cooperative", "salaire-minimum-ouvrier", "quotas-extraction"],
            preset.World.Laws.Select(l => l.TemplateId));
        Assert.Empty(none.World.Laws);
        Assert.Equal(["impot-progressif", "plafond-loyers"], chosen.World.Laws.Select(l => l.TemplateId));
    }

    [Fact]
    public void Une_loi_vierge_ne_se_vote_qu_une_fois_qu_elle_a_une_brique()
    {
        var simulation = WithoutLaws();
        simulation.World.Legitimacy.Add(EG, 100);
        simulation.World.Legitimacy.Add(PR, 100);
        var state = simulation.World.Structures.Values.Single(s => s.IsNational).Id;

        Assert.Null(simulation.DraftBlank("Essai", "domaine-inconnu", state));
        var draftId = simulation.DraftBlank("Salaire des ouvriers", "travail", state)!;
        var draft = simulation.World.LawCommission.DraftOf(draftId)!;
        Assert.Empty(draft.Law.Bricks);
        Assert.Equal(LawVoteOutcome.Invalid, simulation.TryVote(draftId));

        simulation.UpdateDraft(draftId, draft.Law with
        {
            Bricks = [new BrickSetting("salaire-minimum", new LawSubject(SubjectKind.Rank, "Workers"), 3)],
        });
        Assert.Equal(LawVoteOutcome.Voted, simulation.TryVote(draftId));
        AdvanceDays(simulation, 31);
        Assert.Equal("Salaire des ouvriers", simulation.World.Laws.Single().Name);
    }

    [Fact]
    public void Une_loi_votee_est_payee_tout_de_suite_et_entre_en_vigueur_le_1er_du_mois_suivant()
    {
        var simulation = WithoutLaws();
        simulation.World.Legitimacy.Add(EG, 100);
        simulation.World.Legitimacy.Add(PR, 100);
        IReadOnlyList<LawDraft>? enacted = null;
        simulation.LawsEnacted += changes => enacted = changes;

        var draftId = simulation.DraftFromTemplate("salaire-minimum-ouvrier")!;
        var cost = simulation.CostOf(draftId);

        Assert.Equal(LawVoteOutcome.Voted, simulation.TryVote(draftId));
        Assert.Equal(100 - cost[EG], simulation.World.Legitimacy[EG], precision: 6);
        Assert.Empty(simulation.World.Laws);

        // La partie commence le 1er janvier : la loi entre en vigueur le 1er février.
        AdvanceDays(simulation, 30);
        Assert.Empty(simulation.World.Laws);
        simulation.AdvanceDay();

        var law = Assert.Single(simulation.World.Laws);
        Assert.Equal("salaire-minimum-ouvrier", law.TemplateId);
        Assert.Equal(law.Id, Assert.Single(enacted!).Law.Id);
        Assert.Empty(simulation.World.LawCommission.Voted);
    }

    [Fact]
    public void Sans_assez_de_legitimite_le_vote_est_refuse_et_rien_n_est_paye()
    {
        var simulation = WithoutLaws();
        simulation.World.Legitimacy.Add(EG, 1);

        var draftId = simulation.DraftFromTemplate("salaire-minimum-ouvrier")!;

        Assert.Equal(LawVoteOutcome.NotEnoughLegitimacy, simulation.TryVote(draftId));
        Assert.Equal(1, simulation.World.Legitimacy[EG]);
        Assert.Single(simulation.World.LawCommission.Drafts);
    }

    [Fact]
    public void Annuler_un_vote_avant_le_1er_du_mois_rembourse_tout()
    {
        var simulation = WithoutLaws();
        simulation.World.Legitimacy.Add(EC, 100);
        var draftId = simulation.DraftFromTemplate("quotas-extraction")!;
        Assert.Equal(LawVoteOutcome.Voted, simulation.TryVote(draftId));

        Assert.True(simulation.CancelVote(draftId));
        AdvanceDays(simulation, 31);

        Assert.Equal(100, simulation.World.Legitimacy[EC], precision: 6);
        Assert.Empty(simulation.World.Laws);
        Assert.Single(simulation.World.LawCommission.Drafts);
    }

    [Fact]
    public void Abroger_une_loi_la_retire_le_1er_du_mois_et_se_paie_dans_les_poles_opposes()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(),
            new StartingLaws(StartingLawsMode.Chosen, [new ScenarioLaw("quotas-extraction")]));
        var lawId = simulation.World.Laws.Single().Id;
        var positivist = Orientation.FromCode("PO");
        simulation.World.Legitimacy.Add(positivist, 100);

        var draftId = simulation.DraftRepeal(lawId)!;

        Assert.True(simulation.CostOf(draftId).ContainsKey(positivist));
        Assert.Equal(LawVoteOutcome.Voted, simulation.TryVote(draftId));
        AdvanceDays(simulation, 31);
        Assert.Empty(simulation.World.Laws);
    }

    [Fact]
    public void Une_brique_verrouillee_par_l_Etat_ne_peut_pas_etre_changee_par_une_commune()
    {
        var simulation = WithoutLaws();
        var locked = simulation.World.LawCatalogue.TemplateOf("liberte-entreprendre")!.ToLaw("liberte");
        simulation.EnactAtStart(locked with { Bricks = [locked.Bricks[0] with { Locked = true }] });
        simulation.World.Legitimacy.Add(PR, 1_000);
        simulation.World.Legitimacy.Add(Orientation.FromCode("LI"), 1_000);

        var draftId = simulation.DraftFromTemplate("economie-cooperative")!;

        Assert.Equal(LawVoteOutcome.LockedAbove, simulation.TryVote(draftId));
    }

    [Fact]
    public void Une_commune_qui_interdit_le_prive_malgre_l_Etat_fait_un_vrai_mouvement_PR()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(),
            new StartingLaws(StartingLawsMode.Chosen, [new ScenarioLaw("liberte-entreprendre"), new ScenarioLaw("economie-cooperative")]));
        var book = simulation.World.LawBook;
        var cooperative = simulation.World.Laws.Single(l => l.TemplateId == "economie-cooperative");

        var ban = book.MovementOf(cooperative, cooperative.Bricks.Single(b => b.Brick == Bricks.PrivateCompanies))!.Value;

        // La liberté d'entreprendre de l'État autorise (1) ; la commune interdit dans l'alimentation (0).
        Assert.Equal(1, ban.Reference);
        Assert.Equal(1, ban.Intensity, precision: 9);
        Assert.Equal([PR], ban.Poles);
    }
}
