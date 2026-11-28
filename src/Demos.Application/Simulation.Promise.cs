using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Application;

/// <summary>
/// La Promesse dans la partie (décidé les 21, 23 et 25 novembre 2026, voir le document des règles, Thème : l'exil et la
/// promesse) : le choix de La Promesse et de ses engagements, la jauge, les trahisons repérées sur les actes du joueur,
/// la méfiance des classes trahies, la rupture du pacte et la refondation. L'état d'exception n'est pas encore codé.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Le pacte vient de se déchirer : le joueur doit choisir de terminer, de continuer sans promesse ou de refonder.</summary>
    public event Action? PactTorn;

    /// <summary>Une trahison vient d'être notée : l'engagement trahi et sa cause.</summary>
    public event Action<EngagementDefinition, string>? PromiseBetrayed;

    /// <summary>
    /// Action du joueur : choisir La Promesse et ses engagements (un par axe), en début de partie. Renvoie ce qui ne va pas,
    /// ou null si c'est fait.
    /// </summary>
    public string? ChoosePromise(PromiseDefinition promise, IReadOnlyList<EngagementDefinition> engagements, string peopleName = "")
    {
        if (PromiseState.ProblemWith(promise, engagements) is { } problem)
            return problem;

        World.PromiseState = new PromiseState(promise, engagements, founded: Clock.Today);
        World.PromiseState.NamePeople(peopleName);
        return null;
    }

    /// <summary>Action du joueur : renommer son peuple (en jeu, depuis la page La Promesse).</summary>
    public void NamePeople(string name) => World.PromiseState?.NamePeople(name);

    /// <summary>La signature tracée au serment (création de la partie, refondation), redessinée sous le texte signé.</summary>
    public void SignPromise(IReadOnlyList<IReadOnlyList<Demos.Domain.Map.Point2D>> signature)
    {
        if (World.PromiseState is { } state)
            state.Signature = signature;
    }

    /// <summary>La part de la population qui vit dans une commune dont la charte va contre un pôle de La Promesse.</summary>
    public double ContraryCharterShare()
    {
        if (World.PromiseState is not { } state)
            return 0;

        var opposites = state.Promise.Orientations.Select(p => p.Opposite).ToHashSet();
        var total = World.Communes.Sum(c => World.Residents(c.Id));
        if (total <= 0)
            return 0;
        var contrary = World.Communes
            .Where(c => World.CharterOf(c.Id) is { } charter && opposites.Contains(charter))
            .Sum(c => World.Residents(c.Id));
        return contrary / total;
    }

    /// <summary>La jauge de la promesse aujourd'hui, de 0 à 1 ; null sans Promesse.</summary>
    public double? PromiseGauge =>
        World.PromiseState is { } state ? state.Gauge(World.Classes, ContraryCharterShare()) : null;

    /// <summary>Un acte hors des lois (vendre, nationaliser, fonder…) : les engagements qu'il rompt sont trahis.</summary>
    public void NotePromiseAct(EngagementAct act, string cause, string? need = null, string? typeId = null)
    {
        if (World.PromiseState is not { Torn: false } state)
            return;

        foreach (var engagement in state.Engagements.Where(e => e.IsBrokenBy(act, need, typeId)))
            Betray(state, engagement, cause);
    }

    /// <summary>Un changement de loi qui entre en vigueur : chaque engagement qu'une de ses briques rompt est trahi une fois.</summary>
    private void NotePromiseLawChange(LawDraft draft, Law? before)
    {
        if (World.PromiseState is not { Torn: false } state)
            return;

        var cause = $"{(draft.Kind switch { LawChangeKind.Enact => "vote", LawChangeKind.Amend => "changement", _ => "abrogation" })} de la loi « {draft.Law.Name} »";
        foreach (var engagement in EngagementsBrokenBy(World.LawBook, state.Engagements, draft, before))
            Betray(state, engagement, cause);
    }

    /// <summary>Les engagements qu'un changement de loi romprait, d'après les briques qui bougent.</summary>
    public static IReadOnlyList<EngagementDefinition> EngagementsBrokenBy(
        LawBook book, IEnumerable<EngagementDefinition> engagements, LawDraft draft, Law? before)
    {
        var movements = LawCost.MovementsOf(book, before, draft.Kind == LawChangeKind.Repeal ? null : draft.Law);
        return engagements.Where(e => movements.Any(m => e.IsBrokenBy(m, draft.Kind, draft.Law.Domain))).ToList();
    }

    /// <summary>
    /// Avant un vote (décidé le 21 novembre 2026, jamais bloquant) : les engagements de La Promesse que ce brouillon
    /// romprait s'il entrait en vigueur avec les lois d'aujourd'hui ; vide sans Promesse ou après un pacte déchiré.
    /// </summary>
    public IReadOnlyList<EngagementDefinition> PromiseBreachesOfDraft(string draftId) =>
        World.PromiseState is { Torn: false } state && World.LawCommission.DraftOf(draftId) is { } draft
            ? EngagementsBrokenBy(World.LawBook, state.Engagements, draft, World.Laws.FirstOrDefault(l => l.Id == draft.Law.Id))
            : [];

    /// <summary>Avant un acte hors des lois : les engagements qu'il romprait ; vide sans Promesse ou après un pacte déchiré.</summary>
    public IReadOnlyList<EngagementDefinition> PromiseBreachesOfAct(EngagementAct act, string? need = null, string? typeId = null) =>
        World.PromiseState is { Torn: false } state ? state.Engagements.Where(e => e.IsBrokenBy(act, need, typeId)).ToList() : [];

    /// <summary>Avant de vendre un bâtiment : les engagements que la vente romprait.</summary>
    public IReadOnlyList<EngagementDefinition> PromiseBreachesOfSale(string buildingId) =>
        World.Structures.TryGetValue(buildingId, out var building)
            ? PromiseBreachesOfAct(EngagementAct.Sale, World.Products.FirstOrDefault(p => p.Id == building.Production?.ProductId)?.Need?.Id)
            : [];

    /// <summary>Avant de nationaliser un bâtiment, par rachat ou par expropriation.</summary>
    public IReadOnlyList<EngagementDefinition> PromiseBreachesOfNationalization(NationalizationMode mode) =>
        PromiseBreachesOfAct(mode == NationalizationMode.Expropriation ? EngagementAct.Expropriation : EngagementAct.Nationalization);

    /// <summary>Avant de fonder une commune.</summary>
    public IReadOnlyList<EngagementDefinition> PromiseBreachesOfFounding() => PromiseBreachesOfAct(EngagementAct.Founding);

    /// <summary>Avant de poser un bâtiment : l'extraction au-delà du ressort de la commune peut rompre un engagement.</summary>
    public IReadOnlyList<EngagementDefinition> PromiseBreachesOfConstruction(string typeId, string? productId, string communeId, Demos.Domain.Map.Point2D? position) =>
        position is { } far && World.NeedsExtractionSite(typeId, productId) && IsBeyondCommune(communeId, far)
            ? PromiseBreachesOfAct(EngagementAct.FarExtraction, typeId: typeId)
            : [];

    /// <summary>Une trahison : la blessure de la jauge, et la méfiance des classes attachées au pôle trahi.</summary>
    private void Betray(PromiseState state, EngagementDefinition engagement, string cause)
    {
        state.Betray(engagement, cause, Clock.Today);
        var pole = engagement.Orientation;
        foreach (var socialClass in World.Classes.Where(c => c.Ideology[pole.Axis] == pole.Pole))
            socialClass.Distrust += Balance.Current.BetrayalAdhesionLoss;
        PromiseBetrayed?.Invoke(engagement, cause);
    }

    /// <summary>Le bilan du mois : la méfiance revient, les blessures guérissent, et le pacte peut se déchirer.</summary>
    private void RunPromiseMonthly()
    {
        foreach (var socialClass in World.Classes)
            socialClass.Distrust = Math.Max(0, socialClass.Distrust - Balance.Current.PromiseHealingPerMonth);

        if (World.PromiseState is { Torn: false } state && state.RunMonthly(PromiseGauge ?? 1))
            PactTorn?.Invoke();
    }

    /// <summary>Vrai si cette position est au-delà du ressort de la commune (8 km, réglage).</summary>
    private bool IsBeyondCommune(string communeId, Demos.Domain.Map.Point2D position) =>
        World.CommunePositions.TryGetValue(communeId, out var commune)
        && Demos.Domain.Map.MapScale.KmBetween(commune, position) > Balance.Current.CommuneJurisdictionRadius;

    /// <summary>Après un pacte déchiré : la nation continue sans promesse (bac à sable).</summary>
    public void ContinueWithoutPromise() => World.PromiseState = null;

    /// <summary>
    /// Après un pacte déchiré : refonder avec une nouvelle Promesse. La jauge repart de l'alignement actuel du peuple, avec
    /// une cicatrice permanente ; toutes les classes traversent une crise (méfiance). Renvoie ce qui ne va pas, ou null.
    /// </summary>
    public string? Refound(PromiseDefinition promise, IReadOnlyList<EngagementDefinition> engagements)
    {
        if (World.PromiseState is not { Torn: true })
            return "on ne refonde qu'après un pacte déchiré";
        if (PromiseState.ProblemWith(promise, engagements) is { } problem)
            return problem;

        var settings = Balance.Current;
        foreach (var socialClass in World.Classes)
            socialClass.Distrust += settings.RefoundingAdhesionLoss;
        var peopleName = World.PromiseState.PeopleName;
        World.PromiseState = new PromiseState(promise, engagements, settings.RefoundingScar, Clock.Today);
        World.PromiseState.NamePeople(peopleName);
        return null;
    }
}
