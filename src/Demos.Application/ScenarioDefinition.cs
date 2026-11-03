using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Application;

/// <summary>
/// Description d'une partie de départ, lue dans game/data/partie-depart.json : les communes, les bâtiments (tirés du
/// catalogue, avec ce qui change par rapport à lui), les classes, les lois et le trésor de départ.
/// </summary>
/// <param name="HeatingFuel">Combustible de tous les bâtiments de départ (le charbon) ; celui du catalogue si absent.</param>
public sealed record ScenarioDefinition(
    double StartingTreasury,
    string? HeatingFuel,
    IReadOnlyList<ScenarioCommune> Communes,
    IReadOnlyList<ScenarioBuilding> Buildings,
    IReadOnlyList<ScenarioClass> Classes,
    IReadOnlyList<ScenarioLaw> Laws);

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

public sealed record ScenarioLaw(string Id, string Name, string Structure, IReadOnlyList<Orientation> Orientations);
