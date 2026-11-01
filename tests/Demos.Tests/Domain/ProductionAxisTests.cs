using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class ProductionAxisTests
{
    private static readonly Ideology Communist = new(Pole.A, Pole.A, Pole.A);
    private static readonly Ideology Phalansterian = new(Pole.A, Pole.B, Pole.A);

    /// <summary>1 000 membres : 600 dans la commune, 400 dans la commune 2, presque tous avec un emploi.</summary>
    private static SocialClass SpreadOverTwoCommunes(Ideology ideology)
    {
        var socialClass = new SocialClass("classe", "classe", Rank.Workers, ideology, 1_000) { Wealth = 10_000 };
        socialClass.SetResidence("commune", 0.6);
        socialClass.SetResidence("commune-2", 0.4);
        socialClass.SetEmployment("ferme", 580);
        socialClass.SetEmployment("ferme-2", 390);
        socialClass.WealthPerMemberLastSeason = 1;
        return socialClass;
    }

    [Fact]
    public void La_pollution_pese_surtout_sur_la_commune_du_batiment()
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures)
        {
            ["carriere"] = new("carriere", "Carrière", StructureKind.Building, "commune", PollutionPerWorker: 1),
        };
        var residents = SpreadOverTwoCommunes(Communist);

        var pollution = Pollution.PerResidentByCommune(structures, new Dictionary<string, double> { ["carriere"] = 120 }, [residents]);

        // 120 de pollution produite dans la commune (600 habitants) ; la commune 2 n'en reçoit que 10 % (400 habitants).
        Assert.Equal(0.2, pollution["commune"], precision: 6);
        Assert.Equal(0.03, pollution["commune-2"], precision: 6);
    }

    [Fact]
    public void La_pollution_venue_de_loin_ne_depasse_pas_celle_de_la_commune_ou_elle_est_produite()
    {
        var structures = new Dictionary<string, Structure>(TestTown.Structures)
        {
            ["carriere"] = new("carriere", "Carrière", StructureKind.Building, "commune", PollutionPerWorker: 1),
        };
        var residents = new SocialClass("classe", "classe", Rank.Workers, Communist, 10_010);
        residents.SetResidence("commune", 10_000 / 10_010.0);
        residents.SetResidence("commune-2", 10 / 10_010.0);

        var pollution = Pollution.PerResidentByCommune(structures, new Dictionary<string, double> { ["carriere"] = 1_000 }, [residents]);

        // La grande ville : 1 000 pour 10 000 habitants, soit 0,1. Le village de 10 habitants en recevrait
        // 100, soit 10 par habitant : il n'en garde que 0,1, comme la grande ville.
        Assert.Equal(0.1, pollution["commune"], precision: 6);
        Assert.Equal(0.1, pollution["commune-2"], precision: 6);
    }

    [Fact]
    public void Seuls_les_habitants_de_la_commune_polluee_deviennent_ecologistes()
    {
        var workers = SpreadOverTwoCommunes(Communist);
        var pollution = new Dictionary<string, double> { ["commune"] = 0.2, ["commune-2"] = 0.01 };

        var ecologists = Assert.Single(ClassTransitions.RunSeasonal([workers], TestTown.Structures, pollution));

        // 5 % des 600 habitants de la commune polluée ; personne ne quitte la commune 2.
        Assert.Equal(Phalansterian, ecologists.Ideology);
        Assert.Equal(30, ecologists.Population);
        Assert.Equal(30, ecologists.MembersLivingIn("commune"), precision: 6);
        Assert.Equal(400, workers.MembersLivingIn("commune-2"), precision: 6);
        Assert.Equal(570, workers.MembersLivingIn("commune"), precision: 6);
    }

    [Fact]
    public void Une_classe_ecologiste_qui_manque_de_produits_devient_positiviste()
    {
        var ecologists = SpreadOverTwoCommunes(Phalansterian);
        ecologists.RecordSupplyShortage(0.3);

        var positivists = ClassTransitions.RunSeasonal([ecologists], TestTown.Structures, TestTown.NoPollution);

        // 5 % des membres de chaque commune, chacun dans le groupe local de sa commune : 30 + 20.
        Assert.All(positivists, group => Assert.Equal(Communist, group.Ideology));
        Assert.Equal(30, positivists.Single(g => g.CommuneId == "commune").Population);
        Assert.Equal(20, positivists.Single(g => g.CommuneId == "commune-2").Population);
    }

    [Fact]
    public void Le_manque_d_argent_ne_compte_pas_comme_une_penurie()
    {
        var market = new Market([new ProductDefinition("pain", "Pain", NeedCategory.Food, 1)]);
        market.Offer("pain", 1_000, "vendeur");
        var ecologists = SpreadOverTwoCommunes(Phalansterian);
        ecologists.Wealth = 100;

        Consumption.RunDaily([ecologists], [new ProductDefinition("pain", "Pain", NeedCategory.Food, 1)], market);

        Assert.Equal(0, ecologists.SeasonSupplyShortage, precision: 6);
        Assert.True(ecologists.SeasonMoneyShortfall > 0.8);
    }

    [Fact]
    public void Un_marche_vide_est_une_penurie()
    {
        var market = new Market([new ProductDefinition("pain", "Pain", NeedCategory.Food, 1)]);
        var ecologists = SpreadOverTwoCommunes(Phalansterian);
        ecologists.HousingPlaceShortage = 0;

        Consumption.RunDaily([ecologists], [new ProductDefinition("pain", "Pain", NeedCategory.Food, 1)], market);

        // Logement sans manque, pain introuvable : la moitié des besoins manque faute de produits.
        Assert.Equal(0.5, ecologists.SeasonSupplyShortage, precision: 6);
    }

    [Fact]
    public void Le_chomage_ne_fait_descendre_que_les_habitants_de_la_commune_sans_emploi()
    {
        var intermediates = new SocialClass("classe", "classe", Rank.Intermediate, Communist, 1_000) { Wealth = 0 };
        intermediates.SetResidence("commune", 0.5);
        intermediates.SetResidence("commune-2", 0.5);
        intermediates.SetEmployment("ferme", 500);
        intermediates.WealthPerMemberLastSeason = 0;

        var demoted = Assert.Single(ClassTransitions.RunSeasonal([intermediates], TestTown.Structures, TestTown.NoPollution));

        // Commune : plein emploi. Commune 2 : 500 chômeurs, dont 10 % descendent.
        Assert.Equal(50, demoted.Population);
        Assert.Equal(50, demoted.MembersLivingIn("commune-2"), precision: 6);
        Assert.Equal(500, intermediates.MembersLivingIn("commune"), precision: 6);
    }
}
