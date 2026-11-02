using Demos.Domain.Economy;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Tests.Infrastructure;

public class BalanceFileTests
{
    [Fact]
    public void Le_fichier_d_equilibrage_reprend_les_reglages_par_defaut_du_jeu()
    {
        var fromFile = GameData.Content().LoadBalance();
        var defaults = new BalanceSettings();

        // Le fichier est la référence ; ce test vérifie qu'il se lit bien et qu'il n'a pas été déréglé par erreur.
        Assert.Equal(defaults.NeedsByRank[Rank.Executives][NeedCategory.Furniture], fromFile.NeedsByRank[Rank.Executives][NeedCategory.Furniture]);
        Assert.Equal(defaults.HousingPlacesByRank[Rank.Intermediate], fromFile.HousingPlacesByRank[Rank.Intermediate]);
        Assert.Equal(defaults.HeatingBySeason[Season.Summer], fromFile.HeatingBySeason[Season.Summer]);
        Assert.Equal(defaults.WageMultipliers[Rank.Executives], fromFile.WageMultipliers[Rank.Executives]);
        Assert.Equal(defaults.StateTaxRate, fromFile.StateTaxRate);
        Assert.Equal(defaults.FuelReserveDays, fromFile.FuelReserveDays);
        Assert.Equal(defaults.FoundingGrantDays, fromFile.FoundingGrantDays);
    }

    [Fact]
    public void Sans_fichier_d_equilibrage_on_garde_les_reglages_par_defaut()
    {
        var source = new Demos.Infrastructure.JsonContentSource(_ => "");

        Assert.Equal(new BalanceSettings().Margin, source.LoadBalance().Margin);
    }
}
