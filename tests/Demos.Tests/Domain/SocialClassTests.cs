using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class SocialClassTests
{
    private static SocialClass Workers(long population = 1_000_000) =>
        new("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), population);

    [Theory]
    [InlineData(1.0, 1_006_000)]
    [InlineData(0.5, 1_000_000)]
    [InlineData(0.0, 994_000)]
    public void La_croissance_depend_de_la_satisfaction(double satisfaction, long expected)
    {
        var workers = Workers();
        workers.NeedsSatisfaction = satisfaction;

        workers.Grow();

        Assert.Equal(expected, workers.Population);
    }
}
