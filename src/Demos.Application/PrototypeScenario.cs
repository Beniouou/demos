using Demos.Application.Ports;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>Partie de départ du prototype : trois classes, une par rang.</summary>
public static class PrototypeScenario
{
    public static Simulation Create(IContentSource content)
    {
        var classes = new List<SocialClass>
        {
            new("ouvriers", "Ouvriers", Rank.Workers, new Ideology(-40, -20, -30), 800_000) { NeedsSatisfaction = 0.4 },
            new("cadres", "Cadres", Rank.Executives, new Ideology(10, -40, 20), 150_000) { NeedsSatisfaction = 0.7 },
            new("dirigeants", "Dirigeants", Rank.Leaders, new Ideology(60, -60, 70), 20_000) { NeedsSatisfaction = 0.95 },
        };

        var world = new World(content.LoadProducts(), classes)
        {
            RegimeIdeology = new Ideology(40, -50, 50),
        };

        return new Simulation(world, new GameClock(new DateOnly(1900, 1, 1)));
    }
}
