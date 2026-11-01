using Demos.Domain.Economy;
using Demos.Domain.Population;

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

    [Fact]
    public void Une_classe_liberale_qui_travaille_sans_couvrir_ses_besoins_devient_protectionniste()
    {
        var workers = Working(Libertarian);
        workers.RecordMoneyShortfall(0.3);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers]));

        // 5 % des membres basculent, sans changer de rang.
        Assert.Equal(Communist, created.Ideology);
        Assert.Equal(Rank.Workers, created.Rank);
        Assert.Equal(50, created.Population);
    }

    [Fact]
    public void Une_classe_protectionniste_comblee_mais_fortement_prelevee_devient_liberale()
    {
        var workers = Working(Communist);
        workers.NeedsSatisfaction = 1;
        workers.ReceiveIncome(1_000);
        workers.PayTax(400);

        var created = Assert.Single(ClassTransitions.RunSeasonal([workers]));

        Assert.Equal(Libertarian, created.Ideology);
        Assert.Equal(50, created.Population);
    }

    [Fact]
    public void Des_prelevements_faibles_ne_changent_rien()
    {
        var workers = Working(Communist);
        workers.NeedsSatisfaction = 1;
        workers.ReceiveIncome(1_000);
        workers.PayTax(100);

        Assert.Empty(ClassTransitions.RunSeasonal([workers]));
    }

    [Fact]
    public void Une_classe_au_chomage_ne_bascule_pas_sur_l_axe_propriete()
    {
        var workers = Working(Libertarian);
        workers.SetEmployment("ferme", 500);
        workers.WealthPerMemberLastSeason = 100;
        workers.RecordMoneyShortfall(0.5);

        var created = ClassTransitions.RunSeasonal([workers]);

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

        ClassTransitions.RunSeasonal([workers]);

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
