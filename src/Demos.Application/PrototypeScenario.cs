using Demos.Application.Ports;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Partie de départ du prototype : un État et une seule commune, née du premier camp de base, où vit toute
/// la population. Quatre bâtiments (un public, un privé, deux coopératifs), trois classes et trois lois.
/// </summary>
public static class PrototypeScenario
{
    public const string FirstCommuneId = "commune-1";

    public static Simulation Create(IContentSource content)
    {
        var structures = new List<Structure>
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new(FirstCommuneId, "Première commune", StructureKind.PoliticalEntity, "etat"),
            new("camp-de-base-1", "Camp de base", StructureKind.Building, FirstCommuneId,
                Jobs: Simulation.BaseCampJobs, DailyWage: Simulation.BaseCampDailyWage),
            new("ferme", "Ferme coopérative", StructureKind.Building, FirstCommuneId,
                Ownership.Cooperative, "ouvriers", new Production("pain", 3), Jobs: 3_000, DailyWage: 2),
            new("usine", "Usine", StructureKind.Building, FirstCommuneId,
                Ownership.Private, "dirigeants", new Production("meubles", 0.05), Jobs: 1_000, DailyWage: 3),
            new("carriere", "Carrière coopérative", StructureKind.Building, FirstCommuneId,
                Ownership.Cooperative, "ouvriers", new Production("pierre", 0.1), Jobs: 500, DailyWage: 2),
            new("scierie-etat", "Scierie d'État", StructureKind.Building, FirstCommuneId,
                Production: new Production("bois", 0.2), Jobs: 200, DailyWage: 2),
        };

        SocialClass[] classes =
        [
            new("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 8_000) { Wealth = 400_000 },
            new("cadres", "Cadres", Rank.Executives, new Ideology(Pole.A, Pole.A, Pole.B), 1_500) { Wealth = 300_000 },
            new("dirigeants", "Dirigeants", Rank.Leaders, new Ideology(Pole.B, Pole.A, Pole.B), 200) { Wealth = 200_000 },
        ];
        foreach (var socialClass in classes)
            socialClass.SetResidence(FirstCommuneId, 1);

        var laws = new List<Law>
        {
            new("liberte-entreprendre", "Liberté d'entreprendre", "etat",
                [new Orientation(IdeologicalAxis.Property, Pole.B)]),
            new("impot-progressif", "Impôt progressif", FirstCommuneId,
                [new Orientation(IdeologicalAxis.Hierarchy, Pole.A), new Orientation(IdeologicalAxis.Property, Pole.A)]),
            new("quotas-extraction", "Quotas d'extraction", "usine",
                [new Orientation(IdeologicalAxis.Production, Pole.B)]),
        };

        var world = new World(content.LoadProducts(), structures, classes, laws);
        return new Simulation(world, new GameClock(new DateOnly(1900, 1, 1)));
    }
}
