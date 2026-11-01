using Demos.Application.Ports;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Partie de départ du prototype : un État, deux communes, une usine, trois classes et trois lois,
/// une à chaque niveau comme sur le schéma Canva.
/// </summary>
public static class PrototypeScenario
{
    public static Simulation Create(IContentSource content)
    {
        var structures = new List<Structure>
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("commune-1", "Commune 1", StructureKind.PoliticalEntity, "etat"),
            new("commune-2", "Commune 2", StructureKind.PoliticalEntity, "etat"),
            new("usine", "Usine", StructureKind.Building, "commune-1"),
        };

        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 800_000) { NeedsSatisfaction = 0.4 };
        workers.SetShareSubjectTo("commune-1", 0.6);
        workers.SetShareSubjectTo("commune-2", 0.4);
        workers.SetShareSubjectTo("usine", 0.3);

        var executives = new SocialClass("cadres", "Cadres", Rank.Executives, new Ideology(Pole.A, Pole.A, Pole.B), 150_000) { NeedsSatisfaction = 0.7 };
        executives.SetShareSubjectTo("commune-1", 0.5);
        executives.SetShareSubjectTo("commune-2", 0.5);
        executives.SetShareSubjectTo("usine", 0.1);

        var leaders = new SocialClass("dirigeants", "Dirigeants", Rank.Leaders, new Ideology(Pole.B, Pole.A, Pole.B), 20_000) { NeedsSatisfaction = 0.95 };
        leaders.SetShareSubjectTo("commune-1", 0.3);
        leaders.SetShareSubjectTo("commune-2", 0.7);
        leaders.SetShareSubjectTo("usine", 0.05);

        var laws = new List<Law>
        {
            new("liberte-entreprendre", "Liberté d'entreprendre", "etat",
                [new Orientation(IdeologicalAxis.Property, Pole.B)]),
            new("impot-progressif", "Impôt progressif", "commune-1",
                [new Orientation(IdeologicalAxis.Hierarchy, Pole.A), new Orientation(IdeologicalAxis.Property, Pole.A)]),
            new("quotas-extraction", "Quotas d'extraction", "usine",
                [new Orientation(IdeologicalAxis.Production, Pole.B)]),
        };

        var world = new World(content.LoadProducts(), structures, [workers, executives, leaders], laws);
        return new Simulation(world, new GameClock(new DateOnly(1900, 1, 1)));
    }
}
