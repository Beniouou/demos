using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class SocialClassTests
{
    private static SocialClass Workers(long population = 1_000_000) =>
        new("ouvriers", "Ouvriers", Rank.Workers, new Ideology(0, 0, 0), population);

    [Fact]
    public void L_approbation_se_rapproche_chaque_jour_de_sa_cible()
    {
        var workers = Workers();
        workers.NeedsSatisfaction = 1;

        workers.UpdateApproval(ideologicalAgreement: 1);

        // Cible 100, départ 50 : 50 + 0,0035 × 50 = 50,175.
        Assert.Equal(50.175, workers.Approval, precision: 6);
    }

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
