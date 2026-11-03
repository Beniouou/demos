using Demos.Domain.Shared;

namespace Demos.Tests.Domain;

public class GameClockTests
{
    [Fact]
    public void Un_jour_ordinaire_ne_change_rien_au_calendrier()
    {
        var clock = new GameClock(new DateOnly(1900, 1, 15));

        Assert.Equal(CalendarChange.None, clock.AdvanceDay());
        Assert.Equal(new DateOnly(1900, 1, 16), clock.Today);
    }

    [Fact]
    public void Le_1er_mars_commence_le_printemps()
    {
        var clock = new GameClock(new DateOnly(1900, 2, 28));

        Assert.Equal(CalendarChange.NewMonth | CalendarChange.NewSeason, clock.AdvanceDay());
        Assert.Equal(Season.Spring, clock.Season);
    }

    [Fact]
    public void Le_1er_janvier_commence_une_annee_sans_changer_de_saison()
    {
        var clock = new GameClock(new DateOnly(1900, 12, 31));

        Assert.Equal(CalendarChange.NewMonth | CalendarChange.NewYear, clock.AdvanceDay());
        Assert.Equal(Season.Winter, clock.Season);
    }

    [Fact]
    public void Le_1er_du_mois_commence_un_nouveau_mois()
    {
        var clock = new GameClock(new DateOnly(1900, 4, 30));

        Assert.Equal(CalendarChange.NewMonth, clock.AdvanceDay());
    }

    [Theory]
    [InlineData(12, Season.Winter)]
    [InlineData(5, Season.Spring)]
    [InlineData(8, Season.Summer)]
    [InlineData(11, Season.Autumn)]
    public void La_saison_depend_du_mois(int month, Season expected)
    {
        Assert.Equal(expected, GameClock.SeasonOf(new DateOnly(1900, month, 1)));
    }
}
