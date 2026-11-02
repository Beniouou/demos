using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class ServicesTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("administration", "Administration", NeedCategory.Administration, 1, Stockable: false),
    ];

    private static SocialClass Resident(string id, Rank rank, long population)
    {
        var socialClass = new SocialClass(id, id, rank, new Ideology(Pole.A, Pole.A, Pole.A), population) { Wealth = 1_000 };
        socialClass.SetResidence("commune", 1);
        return socialClass;
    }

    [Fact]
    public void Une_mairie_emploie_des_professions_intermediaires_encadrees_par_des_cadres()
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures)
        {
            ["mairie"] = new("mairie", "Mairie", StructureKind.Building, "commune", Jobs: 20, DailyWage: 2,
                Production: new Production("administration", 3), MainRank: Rank.Intermediate),
        };
        structures.Remove("ferme");
        var intermediates = Resident("intermediaires", Rank.Intermediate, 100);
        var executives = Resident("cadres", Rank.Executives, 10);
        var workers = Resident("ouvriers", Rank.Workers, 100);

        var production = Employment.Assign(structures, [workers, intermediates, executives]);

        // 20 emplois principaux pour les professions intermédiaires, 2 d'encadrement pour les cadres, aucun ouvrier.
        Assert.Equal(20, intermediates.MembersWorkingIn("mairie"), precision: 6);
        Assert.Equal(2, executives.MembersWorkingIn("mairie"), precision: 6);
        Assert.Equal(0, workers.MembersWorkingIn("mairie"), precision: 6);
        Assert.Equal(20, production["mairie"], precision: 6);
    }

    [Fact]
    public void Un_emploi_de_services_coute_plus_cher_qu_un_emploi_ouvrier()
    {
        // Ouvrier : 2 × (1 + 0,1 × 2 + 0,01 × 4) = 2,48. Profession intermédiaire : 2 × (2 + 0,1 × 4) = 4,8.
        Assert.Equal(2.48, Pricing.WageCostPerProductionJob(2), precision: 6);
        Assert.Equal(4.8, Pricing.WageCostPerProductionJob(2, Rank.Intermediate), precision: 6);
    }

    [Fact]
    public void Les_services_ne_voyagent_pas_entre_communes_et_ne_se_gardent_pas()
    {
        var markets = new Dictionary<string, Market> { ["a"] = new(Products), ["b"] = new(Products) };
        markets["a"].Offer("administration", 1_000, "mairie");
        var people = Resident("ouvriers", Rank.Workers, 1_000);
        people.SetResidence("commune", 0);
        people.SetResidence("b", 1);

        Trade.RunDaily(["a", "b"], [people], Products, c => markets[c], (_, _) => 10_000);
        Assert.Equal(0, markets["b"].QuantityOf("administration"), precision: 6);

        Storage.ApplyDaily(["commune"], TestTown.Structures, Products, _ => markets["a"]);
        Assert.Equal(0, markets["a"].QuantityOf("administration"), precision: 6);
    }
}
