using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Politics;

namespace Demos.Game;

/// <summary>Une réponse du bureau : le message pour le joueur, et l'identifiant de ce qui a été créé (un brouillon), s'il y en a un.</summary>
public sealed record DeskAnswer(string Message, string? Id = null);

/// <summary>Un sujet possible pour une brique de loi, et ce qu'en lit le joueur.</summary>
public sealed record SubjectChoice(LawSubject Subject, string Label);

/// <summary>
/// Le bureau du joueur, commission des lois : partir d'un modèle, amender ou abroger une loi en vigueur, régler les
/// briques d'un brouillon, le supprimer, le voter (légitimité payée tout de suite, entrée en vigueur le 1er du mois)
/// ou annuler un vote. Chaque décision passe par <see cref="Demos.Application.Simulation"/>.
/// </summary>
public sealed partial class PlayerDesk
{
    private static readonly string[] Months =
        ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];

    /// <summary>« le 1er mars 1850 » : le jour où un vote d'aujourd'hui entre en vigueur.</summary>
    public string NextEnactmentDay()
    {
        var today = Simulation.Clock.Today;
        var next = new System.DateOnly(today.Year, today.Month, 1).AddMonths(1);
        return $"le 1er {Months[next.Month - 1]} {next.Year}";
    }

    /// <summary>Copie un modèle dans un nouveau brouillon, au niveau choisi (ou à celui du modèle).</summary>
    public DeskAnswer DraftFromTemplate(string templateId, string? levelId)
    {
        if (levelId is not null && !World.Structures.ContainsKey(levelId))
            return new("Niveau inconnu.");
        return Simulation.DraftFromTemplate(templateId, levelId) is { } draftId
            ? new($"Brouillon créé : {DraftName(draftId)}.", draftId)
            : new("Ce modèle n'existe plus.");
    }

    /// <summary>Éditeur libre : une loi vierge, nommée, dans un domaine et à un niveau ; on y ajoute ensuite ses briques.</summary>
    public DeskAnswer DraftBlank(string name, string domainId, string levelId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new("Donnez un nom à la loi.");
        return Simulation.DraftBlank(name.Trim(), domainId, levelId) is { } draftId
            ? new($"Brouillon créé : {name.Trim()}. Ajoutez-lui une à {LawCatalogue.MaxBricksPerLaw} briques.", draftId)
            : new("Domaine ou niveau inconnu.");
    }

    /// <summary>
    /// Raccourci de Ressources → Revenus : un brouillon de loi d'impôt au niveau choisi (l'État ou une commune), avec la
    /// brique « Impôt sur la richesse » pour tout le monde, à 0 %. Le joueur la règle et la vote dans l'éditeur de lois.
    /// </summary>
    public DeskAnswer DraftTaxLaw(string levelId)
    {
        if (World.LawCatalogue.BrickOf(Bricks.Tax) is not { } tax || !World.Structures.TryGetValue(levelId, out var level))
            return new("Impossible : brique d'impôt ou niveau inconnu.");

        var answer = DraftBlank($"Impôt ({level.Name})", tax.Domain, levelId);
        if (answer.Id is not { } draftId)
            return answer;
        AddDraftBrick(draftId, tax.Id);
        return new($"Brouillon créé : Impôt ({level.Name}). Réglez son curseur et votez-le dans Lois → Éditeur de lois.", draftId);
    }

    /// <summary>Renomme la loi d'un brouillon.</summary>
    public string RenameDraft(string draftId, string name)
    {
        if (World.LawCommission.DraftOf(draftId) is not { } draft)
            return "Ce brouillon n'existe plus.";
        if (string.IsNullOrWhiteSpace(name))
            return "Donnez un nom à la loi.";
        if (draft.Law.Name == name.Trim())
            return "";
        Simulation.UpdateDraft(draftId, draft.Law with { Name = name.Trim() });
        return $"Loi renommée : {name.Trim()}.";
    }

    /// <summary>
    /// Ajoute une brique du domaine de la loi, à sa valeur par défaut, pour un sujet qu'elle accepte et que la loi ne
    /// règle pas encore pour elle (« tout le monde » d'abord).
    /// </summary>
    public string AddDraftBrick(string draftId, string brickId)
    {
        if (World.LawCommission.DraftOf(draftId) is not { } draft)
            return "Ce brouillon n'existe plus.";
        if (draft.Kind == LawChangeKind.Repeal)
            return "Une abrogation n'a pas de brique à régler.";
        if (draft.Law.Bricks.Count >= LawCatalogue.MaxBricksPerLaw)
            return $"Une loi a {LawCatalogue.MaxBricksPerLaw} briques au plus.";
        if (World.LawCatalogue.BrickOf(brickId) is not { } brick || brick.Domain != draft.Law.Domain)
            return "Cette brique n'est pas du domaine de la loi.";

        var used = draft.Law.Bricks.Where(b => b.Brick == brickId).Select(b => b.Subject).ToList();
        if (SubjectChoices(brickId).Select(c => c.Subject).FirstOrDefault(s => !used.Contains(s)) is not { } subject)
            return $"« {brick.Name} » est déjà réglée pour tous ses sujets.";

        Simulation.UpdateDraft(draftId, draft.Law with { Bricks = [.. draft.Law.Bricks, new BrickSetting(brickId, subject, brick.Default)] });
        return $"Brique ajoutée : {brick.Name}.";
    }

    /// <summary>Retire une brique d'un brouillon.</summary>
    public string RemoveDraftBrick(string draftId, int brickIndex)
    {
        if (World.LawCommission.DraftOf(draftId) is not { } draft)
            return "Ce brouillon n'existe plus.";
        if (brickIndex < 0 || brickIndex >= draft.Law.Bricks.Count)
            return "Brique inconnue.";

        var bricks = draft.Law.Bricks.ToList();
        var name = World.LawCatalogue.BrickOf(bricks[brickIndex].Brick)?.Name ?? bricks[brickIndex].Brick;
        bricks.RemoveAt(brickIndex);
        Simulation.UpdateDraft(draftId, draft.Law with { Bricks = bricks });
        return $"Brique retirée : {name}.";
    }

    /// <summary>Change à qui ou à quoi s'applique une brique d'un brouillon.</summary>
    public string SetDraftBrickSubject(string draftId, int brickIndex, LawSubject subject)
    {
        if (World.LawCommission.DraftOf(draftId) is not { } draft)
            return "Ce brouillon n'existe plus.";
        if (brickIndex < 0 || brickIndex >= draft.Law.Bricks.Count)
            return "Brique inconnue.";
        var setting = draft.Law.Bricks[brickIndex];
        if (draft.Law.Bricks.Where((b, i) => i != brickIndex).Any(b => b.Brick == setting.Brick && b.Subject == subject))
            return "Cette brique est déjà réglée pour ce sujet dans la loi.";

        return EditBrick(draftId, brickIndex, (s, _) => s with { Subject = subject }) is { Length: > 0 } problem
            ? problem
            : $"Sujet : {SheetText.Subject(subject)}.";
    }

    /// <summary>
    /// Les sujets qu'une brique accepte, un par choix : tout le monde, un rang, un statut d'entreprise, un type ou une
    /// catégorie de bâtiments, un besoin.
    /// </summary>
    public IReadOnlyList<SubjectChoice> SubjectChoices(string brickId)
    {
        if (World.LawCatalogue.BrickOf(brickId) is not { } brick)
            return [];

        var choices = new List<SubjectChoice>();
        foreach (var kind in brick.Subjects)
        {
            IEnumerable<(string Value, string Label)> values = kind switch
            {
                SubjectKind.Everyone => [("", "tout le monde")],
                SubjectKind.Rank => new[] { "Workers", "Intermediate", "Executives" }.Select(r => (r, "rang : ")),
                SubjectKind.Ownership => new[] { "Public", "Private", "Cooperative" }.Select(o => (o, "")),
                SubjectKind.BuildingType => World.BuildingTypes.Values.OrderBy(t => t.Name).Select(t => (t.Id, $"bâtiment : {t.Name}")),
                SubjectKind.Category => World.BuildingTypes.Values.Select(t => t.Category).Distinct().Order().Select(c => (c, "")),
                SubjectKind.Need => World.Products.Where(p => p.Need is not null).Select(p => p.Need!.Value.Id).Distinct().Select(n => (n, "")),
                _ => [],
            };
            foreach (var (value, label) in values)
            {
                var subject = kind == SubjectKind.Everyone ? LawSubject.Everyone : new LawSubject(kind, value);
                // Un type de bâtiment a son nom ; les autres sujets se disent comme dans les fiches.
                choices.Add(new SubjectChoice(subject, kind is SubjectKind.Everyone or SubjectKind.BuildingType
                    ? label
                    : label + SheetText.Subject(subject)));
            }
        }
        return choices;
    }

    /// <summary>Un brouillon qui change les curseurs d'une loi en vigueur (au départ, les mêmes).</summary>
    public DeskAnswer DraftAmendment(string lawId) =>
        Simulation.DraftAmendment(lawId) is { } draftId
            ? new($"Brouillon de modification créé dans l'éditeur de lois : {DraftName(draftId)}.", draftId)
            : new("Cette loi n'est plus en vigueur.");

    /// <summary>Un brouillon qui abroge une loi en vigueur.</summary>
    public DeskAnswer DraftRepeal(string lawId) =>
        Simulation.DraftRepeal(lawId) is { } draftId
            ? new($"Brouillon d'abrogation créé dans l'éditeur de lois : {DraftName(draftId)}.", draftId)
            : new("Cette loi n'est plus en vigueur.");

    public string DeleteDraft(string draftId) =>
        Simulation.DeleteDraft(draftId) ? "Brouillon supprimé." : "Ce brouillon n'existe plus.";

    /// <summary>Règle le curseur d'une brique d'un brouillon, dans ses bornes.</summary>
    public string SetDraftBrick(string draftId, int brickIndex, double value) =>
        EditBrick(draftId, brickIndex, (setting, definition) =>
            setting with { Value = System.Math.Clamp(value, definition.Min, definition.Max) });

    /// <summary>Coche ou décoche la case « verrouillée » d'une brique : verrouillée, les niveaux inférieurs ne peuvent plus la changer.</summary>
    public string SetDraftBrickLocked(string draftId, int brickIndex, bool locked) =>
        EditBrick(draftId, brickIndex, (setting, _) => setting with { Locked = locked });

    /// <summary>Change le niveau d'une nouvelle loi (une loi en vigueur garde le sien).</summary>
    public string SetDraftLevel(string draftId, string levelId)
    {
        if (World.LawCommission.DraftOf(draftId) is not { } draft)
            return "Ce brouillon n'existe plus.";
        if (draft.Kind != LawChangeKind.Enact)
            return "Une loi en vigueur garde son niveau.";
        if (!World.Structures.TryGetValue(levelId, out var level))
            return "Niveau inconnu.";

        Simulation.UpdateDraft(draftId, draft.Law with { StructureId = levelId });
        return $"Niveau : {level.Name}.";
    }

    private string EditBrick(string draftId, int brickIndex, System.Func<BrickSetting, BrickDefinition, BrickSetting> change)
    {
        if (World.LawCommission.DraftOf(draftId) is not { } draft)
            return "Ce brouillon n'existe plus.";
        if (draft.Kind == LawChangeKind.Repeal)
            return "Une abrogation n'a pas de curseur à régler.";
        if (brickIndex < 0 || brickIndex >= draft.Law.Bricks.Count
            || World.LawCatalogue.BrickOf(draft.Law.Bricks[brickIndex].Brick) is not { } definition)
            return "Brique inconnue.";

        var bricks = draft.Law.Bricks.ToList();
        bricks[brickIndex] = change(bricks[brickIndex], definition);
        Simulation.UpdateDraft(draftId, draft.Law with { Bricks = bricks });
        return "";
    }

    /// <summary>Vote un brouillon : la légitimité est payée tout de suite, le changement entre en vigueur le 1er du mois.</summary>
    public string Vote(string draftId)
    {
        var cost = Simulation.CostOf(draftId);
        var draft = World.LawCommission.DraftOf(draftId);
        return Simulation.TryVote(draftId) switch
        {
            LawVoteOutcome.Voted => $"Voté. Payé : {Paid(cost)}. Entre en vigueur {NextEnactmentDay()}.",
            LawVoteOutcome.UnknownLevel => "Le niveau de cette loi n'existe plus.",
            LawVoteOutcome.Invalid when draft is not null =>
                $"Loi non valable : {World.LawCatalogue.ProblemWith(draft.Law)}.",
            LawVoteOutcome.LockedAbove => "Impossible : une brique est verrouillée par une loi d'un niveau au-dessus.",
            LawVoteOutcome.AlreadyVoted => "Un changement de cette loi est déjà voté ce mois-ci : annulez-le d'abord.",
            LawVoteOutcome.NotEnoughLegitimacy => $"Pas assez de légitimité : il manque {Missing(cost)}.",
            _ => "Ce brouillon n'existe plus, ou sa loi n'est plus en vigueur.",
        };
    }

    /// <summary>Annule un vote avant le 1er du mois : la légitimité est rendue et le changement redevient un brouillon.</summary>
    public string CancelVote(string draftId)
    {
        var paid = World.LawCommission.Voted.FirstOrDefault(v => v.Draft.Id == draftId)?.Paid;
        return Simulation.CancelVote(draftId)
            ? $"Vote annulé : {Paid(paid ?? new Dictionary<Orientation, double>())} rendu. Le changement redevient un brouillon."
            : "Ce vote n'existe plus (déjà entré en vigueur ?).";
    }

    private string DraftName(string draftId) =>
        World.LawCommission.DraftOf(draftId)?.Law.Name ?? draftId;

    private static string Paid(IReadOnlyDictionary<Orientation, double> cost) =>
        cost.Count == 0 ? "rien" : string.Join(", ", cost.Select(c => $"{c.Value:0.#} {c.Key.Code}"));

    private string Missing(IReadOnlyDictionary<Orientation, double> cost) =>
        string.Join(", ", cost
            .Where(c => World.Legitimacy[c.Key] < c.Value)
            .Select(c => $"{c.Value - World.Legitimacy[c.Key]:0.#} {c.Key.Code} (il en faut {c.Value:0.#})"));
}
