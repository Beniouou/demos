using Demos.Application.Ports;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Partie de départ du prototype : un État et une seule commune, née du premier camp de base, où vivent
/// 5 000 habitants. Chaque besoin de base a son bâtiment : une ferme (se nourrir), un atelier de couture
/// (s'habiller) et des logements (se loger). Une carrière et une scierie fournissent les matériaux.
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
                Jobs: Simulation.BaseCampJobs, DailyWage: Simulation.BaseCampDailyWage, HousingPlaces: Simulation.BaseCampHousingPlaces),
            new("ferme", "Ferme coopérative", StructureKind.Building, FirstCommuneId,
                Ownership.Cooperative, "ouvriers", new Production("pain", 3), Jobs: 2_200, DailyWage: 2),
            new("atelier", "Atelier de couture", StructureKind.Building, FirstCommuneId,
                Ownership.Private, "dirigeants", new Production("vetements", 0.35), Jobs: 150, DailyWage: 2),
            new("carriere", "Carrière coopérative", StructureKind.Building, FirstCommuneId,
                Ownership.Cooperative, "ouvriers", new Production("pierre", 0.1), Jobs: 600, DailyWage: 2),
            new("scierie-etat", "Scierie d'État", StructureKind.Building, FirstCommuneId,
                Production: new Production("bois", 0.2), Jobs: 200, DailyWage: 2),
            new("logements-etat", "Logements d'État", StructureKind.Building, FirstCommuneId,
                HousingPlaces: 3_000, DailyRent: 0.2),
            new("logements-prives", "Logements privés", StructureKind.Building, FirstCommuneId,
                Ownership.Private, "dirigeants", HousingPlaces: 2_200, DailyRent: 0.5),
        };

        SocialClass[] classes =
        [
            new("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 4_500) { Wealth = 225_000 },
            new("cadres", "Cadres", Rank.Executives, new Ideology(Pole.A, Pole.A, Pole.B), 450) { Wealth = 135_000 },
            new("dirigeants", "Dirigeants", Rank.Leaders, new Ideology(Pole.B, Pole.A, Pole.B), 50) { Wealth = 100_000 },
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

        var world = new World(content.LoadProducts(), structures, classes, laws);
        return new Simulation(world, new GameClock(new DateOnly(1900, 1, 1)));
    }
}
