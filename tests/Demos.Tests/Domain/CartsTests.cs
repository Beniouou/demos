using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

/// <summary>Les charrettes des communes (décidé le 20 novembre 2026) : achat jusqu'au nombre voulu, usure.</summary>
public class CartsTests
{
    private static readonly ProductDefinition[] Products = [new("charrette", "Charrette", null, 10, UsageDays: 100)];

    private static readonly Structure Commune = new("commune", "Commune", StructureKind.PoliticalEntity, "etat");

    [Fact]
    public void L_etat_de_la_commune_prend_ses_charrettes_puis_achete_ce_qui_manque()
    {
        var market = new Market(Products);
        market.Offer("charrette", 2, Market.StateSellerId("commune"));
        market.Offer("charrette", 10, "atelier");
        var carts = new Inventory();
        var treasury = new Treasury();
        treasury.Deposit(1_000);

        Carts.RunDaily([Commune], Products, _ => 5, _ => carts, _ => market, treasury, null);

        // 2 charrettes de l'État, gratuites, puis 3 achetées à 10.
        Assert.Equal(5, carts.QuantityOf("charrette"), 6);
        Assert.Equal(970, treasury.Balance, 6);
        Assert.Equal(7, market.QuantityOf("charrette"), 6);
    }

    [Fact]
    public void Les_charrettes_s_usent_et_ne_sont_pas_rachetees_sans_en_vouloir()
    {
        var carts = new Inventory();
        carts.Add("charrette", 10);

        Carts.RunDaily([Commune], Products, _ => 0, _ => carts, _ => new Market(Products), new Treasury(), null);

        Assert.Equal(9.9, carts.QuantityOf("charrette"), 6);
    }
}
