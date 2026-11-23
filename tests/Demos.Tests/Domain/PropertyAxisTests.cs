using Demos.Domain.Economy;
using Demos.Domain.Population;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

public class PropertyAxisTests
{
    private static readonly Ideology Libertarian = new(Pole.A, Pole.A, Pole.B);
    private static readonly Ideology Communist = new(Pole.A, Pole.A, Pole.A);

    /// <summary>Une classe de 1 000 membres qui a presque tous un emploi.</summary>
    private static SocialClass Working(Ideology ideology)
    {
        var socialClass = new SocialClass("classe", "classe", Rank.Workers, ideology, 1_000) { Wealth = 10_000 };
        socialClass.SetResidence("commune", 1);
        socialClass.SetEmployment("ferme", 900);
        socialClass.WealthPerMemberLastSeason = 1;
        return socialClass;
    }

    private static readonly Ideology SegregationistLibertarian = new(Pole.B, Pole.A, Pole.B);

    /// <summary>La commune de test, avec la ferme au propriétaire et au statut donnés.</summary>
    private static Dictionary<string, Structure> Town(Ownership ownership, string? owner = null) =>
        TestTown.Structures.Values
            .Select(s => s.Id == "ferme" ? s with { Ownership = ownership, OwnerClassId = owner } : s)
            .ToDictionary(s => s.Id);

    private static bool Healthy(string buildingId) => false;

    private static bool Struggling(string buildingId) => true;

    [Fact]
    public void Des_liberaux_qui_travaillent_dans_un_batiment_public_qui_va_bien_deviennent_collectivistes()
    {
        var workers = Working(Libertarian);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers], Town(Ownership.Public), TestTown.NoPollution, Healthy));

        // 5 % des 900 travailleurs basculent, sans changer de rang.
        Assert.Equal(Communist, created.Ideology);
        Assert.Equal(Rank.Workers, created.Rank);
        Assert.Equal(45, created.Population);
    }

    [Fact]
    public void Des_collectivistes_dans_un_batiment_public_en_difficulte_deviennent_liberaux()
    {
        var workers = Working(Communist);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers], Town(Ownership.Public), TestTown.NoPollution, Struggling));

        Assert.Equal(Libertarian, created.Ideology);
        Assert.Equal(45, created.Population);
    }

    [Fact]
    public void Des_collectivistes_dans_une_entreprise_privee_qui_va_bien_deviennent_liberaux()
    {
        var workers = Working(Communist);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers], Town(Ownership.Private, "patrons"), TestTown.NoPollution, Healthy));

        Assert.Equal(Libertarian, created.Ideology);
    }

    [Fact]
    public void Des_liberaux_dans_une_entreprise_privee_en_difficulte_deviennent_collectivistes()
    {
        var workers = Working(Libertarian);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers], Town(Ownership.Private, "patrons"), TestTown.NoPollution, Struggling));

        Assert.Equal(Communist, created.Ideology);
    }

    [Fact]
    public void Posseder_son_entreprise_retient_les_liberaux_meme_en_difficulte()
    {
        var owners = Working(Libertarian);

        Assert.Empty(ClassTransitions.RunSeasonal([owners], Town(Ownership.Private, owners.Id), TestTown.NoPollution, Struggling));
    }

    [Fact]
    public void Posseder_une_SCOP_rend_collectiviste_toute_la_classe()
    {
        var members = Working(Libertarian);

        // La SCOP va bien (900 travailleurs) et la classe la possède (1 000 membres) : chacun compte une fois.
        var created = Assert.Single(ClassTransitions.RunSeasonal([members], Town(Ownership.Cooperative, members.Id), TestTown.NoPollution, Healthy));

        Assert.Equal(Communist, created.Ideology);
        Assert.Equal(50, created.Population);
    }

    [Fact]
    public void Sans_mesure_des_difficultes_le_travail_ne_joue_pas_sur_l_axe_propriete()
    {
        Assert.Empty(ClassTransitions.RunSeasonal([Working(Libertarian)], TestTown.Structures, TestTown.NoPollution));
    }

    [Fact]
    public void Des_segregationnistes_qui_travaillent_sans_couvrir_leurs_besoins_deviennent_egalitaristes()
    {
        var workers = Working(SegregationistLibertarian);
        workers.SetEmployment("ferme", 900);
        workers.RecordMoneyShortfall(0.3);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));

        // Le manque d'argent joue désormais sur l'axe hiérarchie (décidé le 23 novembre 2026), pas sur la propriété.
        Assert.Equal(Libertarian, created.Ideology);
        Assert.Equal(50, created.Population);
    }

    [Fact]
    public void Des_egalitaristes_combles_mais_fortement_preleves_deviennent_segregationnistes()
    {
        var workers = Working(Libertarian);
        workers.NeedsSatisfaction = 1;
        workers.ReceiveIncome(1_000);
        workers.PayTax(400);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));

        Assert.Equal(SegregationistLibertarian, created.Ideology);
        Assert.Equal(50, created.Population);
    }

    [Fact]
    public void Des_prelevements_faibles_ne_changent_rien()
    {
        var workers = Working(Communist);
        workers.NeedsSatisfaction = 1;
        workers.ReceiveIncome(1_000);
        workers.PayTax(100);

        Assert.Empty(ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution));
    }

    [Fact]
    public void Une_classe_au_chomage_ne_bascule_pas_sur_l_axe_propriete()
    {
        var workers = Working(Libertarian);
        workers.SetEmployment("ferme", 500);
        workers.WealthPerMemberLastSeason = 100;
        workers.RecordMoneyShortfall(0.5);

        var created = ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution);

        // Seul le chômage joue : bascule vers le ségrégationnisme, pas vers le protectionnisme.
        Assert.All(created, c => Assert.Equal(Pole.B, c.Ideology[IdeologicalAxis.Property]));
    }

    [Fact]
    public void Le_bilan_de_la_saison_repart_de_zero()
    {
        var workers = Working(Communist);
        workers.ReceiveIncome(1_000);
        workers.PayTax(100);
        workers.RecordMoneyShortfall(0.1);

        ClassTransitions.RunSeasonal([workers], TestTown.Structures, TestTown.NoPollution);

        Assert.Equal(0, workers.SeasonIncome);
        Assert.Equal(0, workers.SeasonTaxes);
        Assert.Equal(0, workers.SeasonMoneyShortfall);
    }

    [Fact]
    public void La_consommation_mesure_ce_qui_manque_faute_d_argent()
    {
        var market = new Market([new ProductDefinition("pain", "Pain", NeedCategory.Food, 1)]);
        market.Offer("pain", 1_000, "vendeur");
        var workers = Working(Communist);
        workers.Wealth = 250;

        Consumption.RunDaily([workers], [new ProductDefinition("pain", "Pain", NeedCategory.Food, 1)], market);

        // 1 000 pains nécessaires, 250 achetables : 75 % du besoin manque faute d'argent.
        Assert.Equal(0.75, workers.SeasonMoneyShortfall, precision: 6);
    }
}
