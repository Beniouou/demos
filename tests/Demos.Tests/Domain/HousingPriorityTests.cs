using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class HousingPriorityTests
{
    private static readonly Dictionary<string, Structure> Structures = new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        new("logements", "Logements", StructureKind.Building, "commune", HousingPlaces: 100),
    }.ToDictionary(s => s.Id);

    private static SocialClass Resident(string id, Rank rank, long population)
    {
        var socialClass = new SocialClass(id, id, rank, new Ideology(Pole.A, Pole.A, Pole.A), population) { Wealth = 1_000 };
        socialClass.SetResidence("commune", 1);
        return socialClass;
    }

    [Fact]
    public void Sans_priorite_chacun_est_loge_au_prorata()
    {
        var workers = Resident("ouvriers", Rank.Workers, 100);
        var executives = Resident("cadres", Rank.Executives, 25);

        Housing.RunDaily(Structures, [workers, executives], new Treasury());

        // 100 places pour 100 + 25 × 4 = 200 demandées : la moitié de chacun.
        Assert.Equal(0.5, workers.HousingCoverage, precision: 9);
        Assert.Equal(0.5, executives.HousingCoverage, precision: 9);
    }

    [Fact]
    public void Un_rang_prioritaire_est_loge_d_abord_et_les_autres_prennent_ce_qui_reste()
    {
        var workers = Resident("ouvriers", Rank.Workers, 100);
        var executives = Resident("cadres", Rank.Executives, 20);

        Housing.RunDaily(Structures, [workers, executives], new Treasury(), hasPriority: c => c.Rank == Rank.Executives);

        // Les 20 cadres prennent 80 places ; il en reste 20 pour 100 ouvriers.
        Assert.Equal(1, executives.HousingCoverage, precision: 9);
        Assert.Equal(0.2, workers.HousingCoverage, precision: 9);
    }
}
