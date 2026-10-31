using Demos.Domain.Shared;

namespace Demos.Tests.Domain;

public class GameClockTests
{
    [Fact]
    public void Un_jour_ordinaire_ne_change_pas_de_mois()
    {
        var clock = new GameClock(new DateOnly(1900, 1, 15));

        Assert.False(clock.AdvanceDay());
        Assert.Equal(new DateOnly(1900, 1, 16), clock.Today);
    }

    [Fact]
    public void Le_dernier_jour_du_mois_annonce_un_nouveau_mois()
    {
        var clock = new GameClock(new DateOnly(1900, 1, 31));

        Assert.True(clock.AdvanceDay());
        Assert.Equal(new DateOnly(1900, 2, 1), clock.Today);
    }
}
