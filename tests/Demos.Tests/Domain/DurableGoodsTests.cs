using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

/// <summary>
/// Les objets durables des habitants (décidé le 20 novembre 2026) : un stock qui s'use, une possession visée par
/// personne, des achats qui remplacent l'usure et comblent 1/30 du manque par jour.
/// </summary>
public class DurableGoodsTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("pain", "Pain", NeedCategory.Food, 1),
        new("vetements", "Vêtements", NeedCategory.Clothing, 8, UsageDays: 365),
        new("meubles", "Meubles", NeedCategory.Furniture, 20, UsageDays: 730),
        new("alcool", "Alcool", NeedCategory.Leisure, 2),
        new("administration", "Administration", NeedCategory.Administration, 1, Stockable: false),
        new("securite", "Sécurité", NeedCategory.Security, 1, Stockable: false),
    ];

    private static SocialClass Workers(long population, double wealth = 1_000_000)
    {
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), population) { Wealth = wealth };
        workers.SetResidence("commune", 1);
        return workers;
    }

    private static Market MarketWith(double meubles)
    {
        var market = new Market(Products);
        market.Offer("pain", 10_000, "ferme");
        market.Offer("vetements", 1_000, "atelier");
        market.Offer("meubles", meubles, "menuiserie");
        market.Offer("alcool", 1_000, "distillerie");
        market.Offer("administration", 1_000, "mairie");
        market.Offer("securite", 1_000, "caserne");
        return market;
    }

    [Fact]
    public void Un_groupe_equipe_rachete_seulement_ce_qui_s_est_use()
    {
        var workers = Workers(730);
        Consumption.EquipFully([workers], Products);
        var market = MarketWith(1_000);

        Consumption.RunDaily([workers], Products, market);

        // 730 ouvriers × 4 meubles = 2 920 ; il s'en use 2 920 ÷ 730 = 4 dans la journée, rachetés aussitôt.
        Assert.Equal(996, market.QuantityOf("meubles"), precision: 6);
        Assert.Equal(2_920, workers.Possessions.QuantityOf("meubles"), precision: 6);
        Assert.Equal(1, workers.NeedsSatisfaction, precision: 6);
    }

    [Fact]
    public void Sans_meubles_a_vendre_le_besoin_baisse_au_rythme_de_l_usure()
    {
        var workers = Workers(730);
        Consumption.EquipFully([workers], Products);
        var market = MarketWith(0);

        Consumption.RunDaily([workers], Products, market);

        // Le stock perd 1/730 : se meubler reste couvert à 99,86 %, et non à 0 % comme un produit consommé.
        Assert.Equal(2_916, workers.Possessions.QuantityOf("meubles"), precision: 6);
        Assert.True(workers.NeedsSatisfaction > 0.99);
    }

    [Fact]
    public void Un_groupe_sans_meubles_comble_un_trentieme_de_son_manque_par_jour()
    {
        var workers = Workers(300);
        var market = MarketWith(1_000);

        Consumption.RunDaily([workers], Products, market);

        // 300 × 4 = 1 200 meubles visés, rien d'usé : il en achète 1 200 ÷ 30 = 40.
        Assert.Equal(40, workers.Possessions.QuantityOf("meubles"), precision: 6);
        Assert.Equal(960, market.QuantityOf("meubles"), precision: 6);
    }

    [Fact]
    public void Des_membres_qui_changent_de_classe_emportent_leur_part_des_objets()
    {
        var workers = Workers(1_000);
        var others = new SocialClass("autres", "Autres", Rank.Workers, new Ideology(Pole.B, Pole.A, Pole.A), 0);
        workers.Possessions.Add("meubles", 4_000);

        workers.TransferMembersTo(others, 250);

        Assert.Equal(3_000, workers.Possessions.QuantityOf("meubles"), precision: 6);
        Assert.Equal(1_000, others.Possessions.QuantityOf("meubles"), precision: 6);
    }
}
