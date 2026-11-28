using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Application;

/// <summary>
/// Description d'une partie de départ, lue dans game/data/partie-depart.json : les communes, les bâtiments (tirés du
/// catalogue, avec ce qui change par rapport à lui), les classes, le trésor de départ et les lois du départ
/// préréglé (quelques lois qui régulent déjà un peu).
/// </summary>
/// <param name="HeatingFuel">Combustible de tous les bâtiments de départ (le charbon) ; celui du catalogue si absent.</param>
/// <param name="Provisions">Les vivres apportés, en jours de nourriture de tout le peuple (départ d'exil).</param>
/// <param name="Carts">Les charrettes de la première commune au départ.</param>
/// <param name="ToolShare">La part du stock d'outils visé que reçoit chaque bâtiment (1 : au complet).</param>
/// <param name="PossessionShare">La part de la possession visée (vêtements, meubles) que reçoit chaque groupe (1 : au complet).</param>
public sealed record ScenarioDefinition(
    double StartingTreasury,
    string? HeatingFuel,
    IReadOnlyList<ScenarioCommune> Communes,
    IReadOnlyList<ScenarioBuilding> Buildings,
    IReadOnlyList<ScenarioClass> Classes,
    IReadOnlyList<ScenarioLaw>? PresetLaws = null,
    IReadOnlyList<ScenarioProvision>? Provisions = null,
    int Carts = 0,
    double ToolShare = 1,
    double PossessionShare = 1);

/// <summary>Des vivres de départ : un produit, en jours de nourriture de tout le peuple, mis en vente par l'État de la commune.</summary>
public sealed record ScenarioProvision(string Product, double Days);

/// <summary>
/// Le départ d'exil choisi à la création de la partie (décidé le 27 novembre 2026) : le nom du peuple, La Promesse et un
/// engagement par axe (null et vide en bac à sable avec une terre d'exil), et le tirage de la terre d'exil parmi celles
/// de la carte, et la signature tracée au serment. Les lois de départ (l'acte fondateur) passent par <see cref="StartingLaws"/>.
/// </summary>
public sealed record ExileStart(
    string PeopleName,
    PromiseDefinition? Promise,
    IReadOnlyList<EngagementDefinition> Engagements,
    int Seed = 0,
    IReadOnlyList<IReadOnlyList<Demos.Domain.Map.Point2D>>? Signature = null);

public sealed record ScenarioCommune(string Id, string Name);

/// <summary>
/// Un bâtiment de départ : son type du catalogue, sa commune, et ce qui diffère du catalogue (nom, produit fabriqué,
/// propriétaire, emplois productifs, places de logement, loyer). Ce qui n'est pas donné vient du catalogue.
/// </summary>
public sealed record ScenarioBuilding(
    string Id,
    string Type,
    string Commune,
    string? Name = null,
    string? Product = null,
    Ownership Ownership = Ownership.Public,
    string? Owner = null,
    int? Jobs = null,
    int? HousingPlaces = null,
    double? DailyRent = null);

/// <summary>Une classe de départ : son rang, son idéologie (un pôle par axe), sa population, sa richesse, sa commune.</summary>
public sealed record ScenarioClass(
    string Id,
    string Name,
    Rank Rank,
    Pole Hierarchy,
    Pole Production,
    Pole Property,
    long Population,
    double Wealth,
    string Commune);

/// <summary>Une loi de départ : un modèle du catalogue des lois, à son niveau par défaut ou à celui donné.</summary>
public sealed record ScenarioLaw(string Template, string? Level = null);

/// <summary>Les lois en vigueur au début de la partie, choisies à sa création.</summary>
public enum StartingLawsMode
{
    /// <summary>Aucune loi.</summary>
    None,

    /// <summary>Le départ préréglé de la partie : quelques lois qui régulent déjà un peu (celui des simulations).</summary>
    Preset,

    /// <summary>Les modèles cochés par le joueur.</summary>
    Chosen,
}

public sealed record StartingLaws(StartingLawsMode Mode, IReadOnlyList<ScenarioLaw>? Chosen = null)
{
    public static readonly StartingLaws None = new(StartingLawsMode.None);
    public static readonly StartingLaws Preset = new(StartingLawsMode.Preset);
}
