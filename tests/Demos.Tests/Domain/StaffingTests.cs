using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class StaffingTests
{
    private static readonly Dictionary<Rank, double> FactoryRatios = new() { [Rank.Intermediate] = 0.2, [Rank.Executives] = 0.05 };
    private static readonly Dictionary<Rank, double> FactoryWages = new() { [Rank.Workers] = 3, [Rank.Intermediate] = 5, [Rank.Executives] = 12 };

    private static Structure Factory() => new("usine", "Usine", StructureKind.Building, "commune", Jobs: 100,
        Production: new Production("pain", 1), StaffRatios: FactoryRatios, Wages: FactoryWages);

    private static SocialClass Resident(string id, Rank rank, long population, double wealth = 0)
    {
        var socialClass = new SocialClass(id, id, rank, new Ideology(Pole.A, Pole.A, Pole.A), population) { Wealth = wealth };
        socialClass.SetResidence("commune", 1);
        return socialClass;
    }

    [Fact]
    public void Un_batiment_emploie_chaque_rang_dans_ses_propres_proportions()
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures) { ["usine"] = Factory() };
        structures.Remove("ferme");
        var workers = Resident("ouvriers", Rank.Workers, 1_000);
        var intermediates = Resident("intermediaires", Rank.Intermediate, 1_000);
        var executives = Resident("cadres", Rank.Executives, 1_000);

        Employment.Assign(structures, [workers, intermediates, executives]);

        // 100 ouvriers, 0,2 profession intermédiaire et 0,05 cadre par ouvrier.
        Assert.Equal(100, workers.MembersWorkingIn("usine"), precision: 6);
        Assert.Equal(20, intermediates.MembersWorkingIn("usine"), precision: 6);
        Assert.Equal(5, executives.MembersWorkingIn("usine"), precision: 6);
    }

    [Fact]
    public void Chaque_rang_touche_le_salaire_fixe_par_le_batiment()
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures) { ["usine"] = Factory() };
        structures.Remove("ferme");
        var workers = Resident("ouvriers", Rank.Workers, 1_000);
        var intermediates = Resident("intermediaires", Rank.Intermediate, 1_000);
        var executives = Resident("cadres", Rank.Executives, 1_000);
        SocialClass[] classes = [workers, intermediates, executives];
        var treasury = new Treasury();
        treasury.Deposit(10_000);

        WorkDay.Run(structures, Employment.Assign(structures, classes), classes, [], treasury, _ => new Market([]));

        // 100 × 3 + 20 × 5 + 5 × 12 = 460, payés par le trésor (bâtiment public).
        Assert.Equal(300, workers.Wealth, precision: 6);
        Assert.Equal(100, intermediates.Wealth, precision: 6);
        Assert.Equal(60, executives.Wealth, precision: 6);
        Assert.Equal(10_000 - 460, treasury.Balance, precision: 6);
    }

    [Fact]
    public void Le_cout_salarial_d_un_emploi_productif_compte_ses_proportions_et_ses_salaires()
    {
        // 3 + 0,2 × 5 + 0,05 × 12 = 4,6 ; sans réglage propre, les valeurs par défaut (2 × (1 + 0,1 × 2 + 0,01 × 4)).
        Assert.Equal(4.6, Staffing.WageBillPerJob(Factory()), precision: 6);
        Assert.Equal(2.48, Staffing.WageBillPerJob(Rank.Workers, null, 2, null), precision: 6);
    }
}
