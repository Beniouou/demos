using Demos.Domain.Economy;

namespace Demos.Tests.Domain;

/// <summary>Les compteurs du marché (statistiques) : ils comptent ce qui sort, sans rien changer au marché.</summary>
public class MarketTallyTests
{
    private static readonly ProductDefinition[] Products = [new("bois", "Bois", null, 2)];

    [Fact]
    public void Achats_et_reprises_comptent_comme_utilises()
    {
        var market = new Market(Products);
        market.Offer("bois", 100, "etat:commune");
        market.Offer("bois", 100, "entreprise:scierie");

        market.TryBuy("bois", 30);
        market.TryWithdraw("bois", 20, "etat:commune");

        Assert.Equal(50, market.TallyOf("bois").Used, 6);
        Assert.Equal(150, market.QuantityOf("bois"), 6);
    }

    [Fact]
    public void Un_achat_refuse_ne_compte_pas()
    {
        var market = new Market(Products);
        market.Offer("bois", 10, "etat:commune");

        Assert.False(market.TryBuy("bois", 30));
        Assert.Equal(0, market.TallyOf("bois").Used);
    }

    [Fact]
    public void Envois_et_pertes_ont_leurs_propres_compteurs()
    {
        var market = new Market(Products);
        market.Offer("bois", 100, "etat:commune");

        market.Take("bois", 40);
        market.Keep("bois", 0.5);

        var tally = market.TallyOf("bois");
        Assert.Equal(40, tally.Sent, 6);
        Assert.Equal(30, tally.Lost, 6);
        Assert.Equal(0, tally.Used);
        Assert.Equal(30, market.QuantityOf("bois"), 6);
    }
}
