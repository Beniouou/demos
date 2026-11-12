using Demos.Domain.Map;

namespace Demos.Tests.Domain;

public sealed class MapScaleTests
{
    [Fact]
    public void Une_unite_de_carte_vaut_100_m_et_la_carte_fait_360_km_de_long()
    {
        Assert.Equal(0.1, MapScale.ToKm(1), 9);
        Assert.Equal(360, MapScale.ToKm(3600), 9);
        Assert.Equal(80, MapScale.ToUnits(8), 9);
        Assert.Equal(5, MapScale.KmBetween(new(0, 0), new(30, 40)), 9);
    }

    [Fact]
    public void Une_distance_courte_s_ecrit_en_metres()
    {
        Assert.Equal("850 m", MapScale.Format(0.85));
    }
}
