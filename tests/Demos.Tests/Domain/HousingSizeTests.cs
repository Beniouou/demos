using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class HousingSizeTests
{
    private static SocialClass Resident(string id, Rank rank, long population)
    {
        var socialClass = new SocialClass(id, id, rank, new Ideology(Pole.A, Pole.A, Pole.A), population) { Wealth = 10_000 };
        socialClass.SetResidence("commune", 1);
        return socialClass;
    }

    private static Dictionary<string, Structure> TownWith(int places) => new(TestTown.Structures)
    {
        ["logements"] = new("logements", "Logements", StructureKind.Building, "commune", HousingPlaces: places, DailyRent: 1),
    };

    [Fact]
    public void Un_cadre_occupe_quatre_places_et_paie_quatre_loyers()
    {
        var workers = Resident("ouvriers", Rank.Workers, 100);
        var executives = Resident("cadres", Rank.Executives, 10);
        var treasury = new Treasury();

        Housing.RunDaily(TownWith(140), [workers, executives], treasury);

        // 100 places pour les ouvriers, 40 pour les 10 cadres : tout le monde est logé.
        Assert.Equal(1, workers.HousingCoverage, precision: 6);
        Assert.Equal(1, executives.HousingCoverage, precision: 6);
        Assert.Equal(10_000 - 40, executives.Wealth, precision: 6);
        Assert.Equal(140, treasury.Balance, precision: 6);
    }

    [Fact]
    public void Quand_les_places_manquent_chacun_est_servi_au_prorata_des_places_demandees()
    {
        var workers = Resident("ouvriers", Rank.Workers, 100);
        var executives = Resident("cadres", Rank.Executives, 25);

        Housing.RunDaily(TownWith(100), [workers, executives], new Treasury());

        // 200 places demandées (100 + 25 × 4) pour 100 places : la moitié de chacun est logée.
        Assert.Equal(0.5, workers.HousingCoverage, precision: 6);
        Assert.Equal(0.5, executives.HousingCoverage, precision: 6);
        Assert.Equal(0.5, executives.HousingPlaceShortage, precision: 6);
    }
}
