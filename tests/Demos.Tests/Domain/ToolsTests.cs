using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

/// <summary>Les outils des bâtiments (décidé le 20 novembre 2026) : un stock visé, l'usure, le rachat, la pénalité.</summary>
public class ToolsTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("bois", "Bois", null, 2),
        new("outils", "Outils", null, 5, UsageDays: 100),
    ];

    private static Dictionary<string, Structure> Town() =>
        new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
            new("camp", "Camp", StructureKind.Building, "commune", Ownership.Public, null, new Production("bois", 1), Jobs: 100),
            new("logements", "Logements", StructureKind.Building, "commune", Ownership.Public, HousingPlaces: 100),
        }.ToDictionary(s => s.Id);

    [Fact]
    public void Un_batiment_vise_un_dixieme_d_outil_par_emploi_et_perd_jusqu_a_20_pourcent_sans_outils()
    {
        var camp = Town()["camp"];

        Assert.Equal(10, Demos.Domain.Economy.Tools.TargetOf(camp), 6);
        Assert.Equal(0, Demos.Domain.Economy.Tools.TargetOf(Town()["logements"]));
        Assert.Equal(1, Demos.Domain.Economy.Tools.EffectOf(camp, 10), 6);
        Assert.Equal(0.9, Demos.Domain.Economy.Tools.EffectOf(camp, 5), 6);
        Assert.Equal(0.8, Demos.Domain.Economy.Tools.EffectOf(camp, 0), 6);
    }

    [Fact]
    public void Les_outils_s_usent_et_le_batiment_rachete_ce_qui_manque()
    {
        var structures = Town();
        var market = new Market(Products);
        market.Offer("outils", 100, "atelier");
        var stocks = new Dictionary<string, Inventory>();
        Inventory StockOf(Structure b) => stocks.TryGetValue(b.Id, out var s) ? s : stocks[b.Id] = new Inventory();
        Demos.Domain.Economy.Tools.Equip(structures["camp"], StockOf(structures["camp"]));
        var treasury = new Treasury();
        treasury.Deposit(1_000);

        Demos.Domain.Economy.Tools.RunDaily(structures, Products, _ => market, treasury, null, null, new Dictionary<string, SocialClass>(), StockOf);

        // 10 outils ; 1/100 s'use (0,1), racheté aussitôt au prix de 5.
        Assert.Equal(10, StockOf(structures["camp"]).QuantityOf("outils"), 6);
        Assert.Equal(99.9, market.QuantityOf("outils"), 6);
        Assert.Equal(999.5, treasury.Balance, 6);
    }

    [Fact]
    public void Sans_outils_a_vendre_le_stock_baisse_au_rythme_de_l_usure()
    {
        var structures = Town();
        var market = new Market(Products);
        var stock = new Inventory();
        Demos.Domain.Economy.Tools.Equip(structures["camp"], stock);

        Demos.Domain.Economy.Tools.RunDaily(structures, Products, _ => market, new Treasury(), null, null, new Dictionary<string, SocialClass>(), _ => stock);

        Assert.Equal(9.9, stock.QuantityOf("outils"), 6);
    }
}
