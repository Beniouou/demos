using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class UnemploymentTests
{
    private static readonly Ideology Communist = new(Pole.A, Pole.A, Pole.A);
    private static readonly Ideology Feudalist = new(Pole.B, Pole.A, Pole.A);

    /// <summary>Une classe de 1 000 membres dont 200 ont un emploi, et dont la richesse par membre a baissé.</summary>
    private static SocialClass HardHit(Rank rank, Ideology ideology, string id = "classe")
    {
        var socialClass = new SocialClass(id, id, rank, ideology, 1_000) { Wealth = 5_000 };
        socialClass.SetResidence("commune", 1);
        socialClass.SetEmployment("ferme", 200);
        socialClass.WealthPerMemberLastSeason = 10;
        return socialClass;
    }

    [Fact]
    public void Des_chomeurs_egalitaristes_descendent_d_un_rang_et_deviennent_segregationnistes()
    {
        var intermediates = HardHit(Rank.Intermediate, Communist);

        var created = Assert.Single(ClassTransitions.RunSeasonal([intermediates], TestTown.Structures, TestTown.NoPollution));

        // 800 chômeurs × 10 % = 80 membres.
        Assert.Equal(Rank.Workers, created.Rank);
        Assert.Equal(Feudalist, created.Ideology);
        Assert.Equal("Ouvriers féodalistes", created.Name);
        Assert.Equal(80, created.Population);
        Assert.Equal(920, intermediates.Population);
    }

    [Fact]
    public void Des_cadres_segregationnistes_descendent_sans_changer_d_ideologie()
    {
        var executives = HardHit(Rank.Executives, Feudalist);

        var created = Assert.Single(ClassTransitions.RunSeasonal([executives], TestTown.Structures, TestTown.NoPollution));

        Assert.Equal(Rank.Intermediate, created.Rank);
        Assert.Equal(Feudalist, created.Ideology);
    }

    [Fact]
    public void Des_ouvriers_egalitaristes_ne_descendent_pas_mais_deviennent_segregationnistes()
    {
        var workers = HardHit(Rank.Workers, Communist);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));

        Assert.Equal(Rank.Workers, created.Rank);
        Assert.Equal("Ouvriers féodalistes", created.Name);
    }

    [Fact]
    public void Des_ouvriers_segregationnistes_ne_changent_pas()
    {
        var workers = HardHit(Rank.Workers, Feudalist);

        Assert.Empty(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));
        Assert.Equal(1_000, workers.Population);
    }

    [Fact]
    public void Si_la_richesse_augmente_le_chomage_ne_fait_rien()
    {
        var workers = HardHit(Rank.Workers, Communist);
        workers.WealthPerMemberLastSeason = 4;

        Assert.Empty(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));
    }

    [Fact]
    public void Une_classe_deja_ruinee_continue_de_subir_le_chomage()
    {
        var workers = HardHit(Rank.Workers, Communist);
        workers.Wealth = 0;
        workers.WealthPerMemberLastSeason = 0;

        Assert.Single(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));
    }

    [Fact]
    public void A_la_premiere_saison_la_richesse_est_seulement_relevee()
    {
        var workers = HardHit(Rank.Workers, Communist);
        workers.WealthPerMemberLastSeason = null;

        Assert.Empty(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));
        Assert.Equal(5, workers.WealthPerMemberLastSeason);
    }

    [Fact]
    public void Le_plein_emploi_ramene_des_segregationnistes_vers_l_egalitarisme()
    {
        var workers = HardHit(Rank.Workers, Feudalist);
        workers.SetEmployment("ferme", 980);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));

        // 2 % de chômeurs : plein emploi ; 5 % des 1 000 membres redeviennent égalitaristes, sans changer de rang.
        Assert.Equal(Rank.Workers, created.Rank);
        Assert.Equal(Communist, created.Ideology);
        Assert.Equal(50, created.Population);
    }

    [Fact]
    public void Le_plein_emploi_ne_change_rien_pour_une_classe_deja_egalitariste()
    {
        var workers = HardHit(Rank.Workers, Communist);
        workers.SetEmployment("ferme", 1_000);

        Assert.Empty(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));
    }

    [Fact]
    public void Sous_le_seuil_de_chomage_rien_ne_change()
    {
        var workers = HardHit(Rank.Workers, Communist);
        workers.SetEmployment("ferme", 850);

        Assert.Empty(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));
    }
}
