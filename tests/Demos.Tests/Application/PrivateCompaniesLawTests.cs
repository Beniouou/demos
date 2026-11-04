using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Application;

/// <summary>Le privé est interdit par défaut : la liberté d'entreprendre l'autorise ; sans elle, il est nationalisé.</summary>
public class PrivateCompaniesLawTests
{
    private static readonly Orientation LI = Orientation.FromCode("LI");
    private static readonly Orientation PR = Orientation.FromCode("PR");

    private static Simulation With(params string[] templates) => PrototypeScenario.Create(GameData.Content(),
        new StartingLaws(StartingLawsMode.Chosen, templates.Select(t => new ScenarioLaw(t)).ToList()));

    private static void AdvanceDays(Simulation simulation, int days)
    {
        for (var day = 0; day < days; day++)
            simulation.AdvanceDay();
    }

    [Fact]
    public void Sans_liberte_d_entreprendre_les_entreprises_privees_de_depart_sont_nationalisees_sans_indemnite()
    {
        var free = With("liberte-entreprendre");
        var simulation = With();

        Assert.Equal(Ownership.Private, free.World.Structures["distillerie"].Ownership);
        Assert.Equal(Ownership.Public, simulation.World.Structures["distillerie"].Ownership);
        Assert.Null(simulation.World.Structures["distillerie"].OwnerClassId);
        // Expropriée : la caisse de l'entreprise va à la commune, pas aux cadres.
        Assert.Equal(0, simulation.World.CompanyFundOf("distillerie").Balance);
        Assert.True(simulation.World.CommuneFundBalanceOf("commune-1") > free.World.CommuneFundBalanceOf("commune-1"));
        Assert.Equal(free.World.ClassesById["cadres"].Wealth, simulation.World.ClassesById["cadres"].Wealth, precision: 6);
        // Les SCOP ne sont pas des entreprises privées.
        Assert.Equal(Ownership.Cooperative, simulation.World.Structures["ferme"].Ownership);
    }

    [Fact]
    public void Abroger_la_liberte_d_entreprendre_nationalise_le_prive_au_1er_du_mois()
    {
        var simulation = With("liberte-entreprendre");
        IReadOnlyList<Structure>? taken = null;
        simulation.CompaniesNationalizedByLaw += buildings => taken = buildings;
        simulation.World.Legitimacy.Add(PR, 1_000);

        var draftId = simulation.DraftRepeal(simulation.World.Laws.Single().Id)!;
        Assert.Equal(LawVoteOutcome.Voted, simulation.TryVote(draftId));
        AdvanceDays(simulation, 30);
        Assert.Equal(Ownership.Private, simulation.World.Structures["distillerie"].Ownership);
        simulation.AdvanceDay();

        Assert.Equal(Ownership.Public, simulation.World.Structures["distillerie"].Ownership);
        Assert.Contains(taken!, b => b.Id == "distillerie");
    }

    [Fact]
    public void Sans_liberte_d_entreprendre_on_ne_vend_qu_a_des_ouvriers()
    {
        var simulation = With();
        simulation.World.Legitimacy.Add(LI, 100);
        var price = 1_000;

        Assert.Equal(SaleOutcome.PrivateForbidden, simulation.TrySellBuilding("logements-etat", "cadres", price));
        Assert.Equal(Ownership.Public, simulation.World.Structures["logements-etat"].Ownership);
        Assert.Equal(SaleOutcome.Sold, simulation.TrySellBuilding("logements-etat", "ouvriers", price));
        Assert.Equal(Ownership.Cooperative, simulation.World.Structures["logements-etat"].Ownership);
    }

    [Fact]
    public void Avec_la_liberte_d_entreprendre_on_peut_vendre_a_des_cadres()
    {
        var simulation = With("liberte-entreprendre");
        simulation.World.Legitimacy.Add(LI, 100);

        Assert.Equal(SaleOutcome.Sold, simulation.TrySellBuilding("logements-etat", "cadres", 1_000));
        Assert.Equal(Ownership.Private, simulation.World.Structures["logements-etat"].Ownership);
    }

    [Fact]
    public void Les_quartiers_reserves_logent_les_cadres_d_abord()
    {
        var simulation = With("quartiers-reserves");

        Assert.True(simulation.World.LawBook.HasHousingPriority(simulation.World.ClassesById["cadres"]));
        Assert.False(simulation.World.LawBook.HasHousingPriority(simulation.World.ClassesById["ouvriers"]));
    }
}
