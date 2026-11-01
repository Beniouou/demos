using Demos.Application.Ports;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Partie de départ du prototype : un État et une seule commune, née du premier camp de base, où vivent
/// 5 000 habitants, avec une école et une université d'État. Les bâtiments viennent du catalogue (buildings.json) ; leurs propriétaires sont
/// arbitraires, pour tester les trois circuits d'argent, et se changent dans le menu d'administration.
/// </summary>
public static class PrototypeScenario
{
    public const string FirstCommuneId = "commune-1";

    public static Simulation Create(IContentSource content)
    {
        var types = content.LoadBuildingTypes().ToDictionary(t => t.Id);

        var structures = new List<Structure>
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new(FirstCommuneId, "Première commune", StructureKind.PoliticalEntity, "etat"),
            types["camp-de-base"].Build("camp-de-base-1", FirstCommuneId),
            types["ferme"].Build("ferme", FirstCommuneId, Ownership.Cooperative, "ouvriers", "Ferme coopérative"),
            types["atelier"].Build("atelier", FirstCommuneId, Ownership.Private, "cadres", "Atelier de couture"),
            types["carriere"].Build("carriere", FirstCommuneId, Ownership.Cooperative, "ouvriers", "Carrière coopérative"),
            types["camp-forestier"].Build("camp-forestier", FirstCommuneId, name: "Camp forestier d'État"),
            types["logements"].Build("logements-etat", FirstCommuneId, name: "Logements d'État") with { HousingPlaces = 3_000 },
            types["logements"].Build("logements-prives", FirstCommuneId, Ownership.Private, "cadres", "Logements privés")
                with { HousingPlaces = 2_200, DailyRent = 0.5 },
            types["ecole"].Build("ecole", FirstCommuneId, name: "École d'État"),
            types["universite"].Build("universite", FirstCommuneId, name: "Université d'État"),
            types["entrepot"].Build("entrepot", FirstCommuneId, name: "Entrepôt d'État"),
        };

        SocialClass[] classes =
        [
            new("ouvriers", "Ouvriers communistes", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 4_500) { Wealth = 225_000 },
            new("intermediaires", "Professions intermédiaires libertaires", Rank.Intermediate, new Ideology(Pole.A, Pole.A, Pole.B), 450) { Wealth = 135_000 },
            new("cadres", "Cadres capitalistes", Rank.Executives, new Ideology(Pole.B, Pole.A, Pole.B), 50) { Wealth = 100_000 },
        ];
        foreach (var socialClass in classes)
            socialClass.SetResidence(FirstCommuneId, 1);

        var laws = new List<Law>
        {
            new("liberte-entreprendre", "Liberté d'entreprendre", "etat",
                [new Orientation(IdeologicalAxis.Property, Pole.B)]),
            new("impot-progressif", "Impôt progressif", FirstCommuneId,
                [new Orientation(IdeologicalAxis.Hierarchy, Pole.A), new Orientation(IdeologicalAxis.Property, Pole.A)]),
            new("quotas-extraction", "Quotas d'extraction", "carriere",
                [new Orientation(IdeologicalAxis.Production, Pole.B)]),
        };

        var world = new World(content.LoadProducts(), types.Values.ToList(), structures, classes, laws);
        return new Simulation(world, new GameClock(new DateOnly(1900, 1, 1)));
    }
}
