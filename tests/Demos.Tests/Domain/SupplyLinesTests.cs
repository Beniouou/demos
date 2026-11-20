using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

/// <summary>Les lignes d'approvisionnement (règle décidée le 15 novembre 2026).</summary>
public class SupplyLinesTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("bois", "Bois", null, 2),
        new("planches", "Planches", null, 5),
    ];

    private static readonly Production Camp = new("bois", 10);
    private static readonly Production Scierie = new("planches", 1, [new RecipeInput("bois", 2)]);

    /// <summary>Deux communes ; un camp de bûcheron dans la première, une scierie (10 travailleurs) où l'on veut.</summary>
    private static Dictionary<string, Structure> Towns(string sawmillCommune = "a", Ownership sawmillOwner = Ownership.Public) =>
        new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("a", "Commune A", StructureKind.PoliticalEntity, "etat"),
            new("b", "Commune B", StructureKind.PoliticalEntity, "etat"),
            new("camp", "Camp", StructureKind.Building, "a", Ownership.Public, null, Camp, Jobs: 10),
            new("camp2", "Autre camp", StructureKind.Building, "a", Ownership.Public, null, Camp, Jobs: 10),
            new("scierie", "Scierie", StructureKind.Building, sawmillCommune, sawmillOwner,
                sawmillOwner == Ownership.Public ? null : "ouvriers", Scierie, Jobs: 10),
        }.ToDictionary(s => s.Id);

    private sealed class Game
    {
        public Dictionary<string, Market> Markets { get; } = new() { ["a"] = new Market(Products), ["b"] = new Market(Products) };
        public Dictionary<string, Inventory> Reserves { get; } = [];
        public Dictionary<string, Treasury> Funds { get; } = new() { ["a"] = new Treasury(), ["b"] = new Treasury() };
        public Treasury Treasury { get; } = new();
        public List<(string From, string To, double Quantity)> Shipped { get; } = [];

        public Inventory Reserve(string id) => Reserves.TryGetValue(id, out var reserve) ? reserve : Reserves[id] = new Inventory();

        public Dictionary<string, double> Run(IReadOnlyList<SupplyLine> lines, Dictionary<string, Structure> structures,
            Dictionary<string, double> produced, double loss = 0.2, Func<string, string, bool>? canTrade = null) =>
            SupplyLines.RunDaily(lines, structures, produced, new Dictionary<string, double> { ["scierie"] = 10 }, id => Markets[id],
                Reserve, Treasury, id => Funds[id], (_, _) => loss, (from, to, quantity) => Shipped.Add((from, to, quantity)), canTrade);
    }

    [Fact]
    public void Sans_batiment_logistique_la_ligne_entre_communes_ne_livre_pas()
    {
        var game = new Game();
        game.Markets["a"].Offer("bois", 100, Market.StateSellerId("a"));
        game.Funds["b"].Deposit(1_000);
        var line = new SupplyLine("l1", "camp", "scierie", 0.5);

        var delivered = game.Run([line], Towns(sawmillCommune: "b"), new Dictionary<string, double> { ["camp"] = 100 },
            canTrade: (from, to) => Trade.CanTrade(from, to, _ => false));

        Assert.Equal(0, delivered["l1"]);
        Assert.Equal(100, game.Markets["a"].QuantityOf("bois"), 6);
        Assert.Empty(game.Shipped);
    }

    [Fact]
    public void La_ligne_entre_communes_ne_depasse_pas_la_capacite_d_envoi()
    {
        var game = new Game();
        game.Markets["a"].Offer("bois", 100, Market.StateSellerId("a"));
        game.Funds["b"].Deposit(1_000);
        var capacityLeft = new Dictionary<string, double> { ["a"] = 10 };

        var delivered = SupplyLines.RunDaily([new SupplyLine("l1", "camp", "scierie", 0.5)], Towns(sawmillCommune: "b"),
            new Dictionary<string, double> { ["camp"] = 100 }, new Dictionary<string, double> { ["scierie"] = 10 }, id => game.Markets[id],
            game.Reserve, game.Treasury, id => game.Funds[id], (_, _) => 0.2,
            capacityHolderOf: (from, to) => from, capacityLeft: capacityLeft);

        // 25 voudraient partir ; la capacité n'en laisse partir que 10, dont 8 arrivent.
        Assert.Equal(8, delivered["l1"], 6);
        Assert.Equal(0, capacityLeft["a"], 6);
    }

    [Fact]
    public void Sans_batiment_logistique_la_ligne_dans_la_commune_livre_quand_meme()
    {
        var game = new Game();
        game.Markets["a"].Offer("bois", 100, Market.StateSellerId("a"));
        var line = new SupplyLine("l1", "camp", "scierie", 0.5);

        var delivered = game.Run([line], Towns(), new Dictionary<string, double> { ["camp"] = 100 },
            canTrade: (from, to) => Trade.CanTrade(from, to, _ => false));

        Assert.Equal(20, delivered["l1"], 6);
    }

    [Fact]
    public void Dans_la_commune_le_client_recoit_sa_part_sans_payer_et_le_reste_reste_en_vente()
    {
        var game = new Game();
        game.Markets["a"].Offer("bois", 100, Market.StateSellerId("a"));
        var line = new SupplyLine("l1", "camp", "scierie", 0.5);

        var delivered = game.Run([line], Towns(), new Dictionary<string, double> { ["camp"] = 100 });

        // 50 % de 100 = 50 offerts ; la scierie en veut 10 × 1 × 2 = 20 pour une journée : elle n'en reçoit que 20.
        Assert.Equal(20, delivered["l1"], 6);
        Assert.Equal(20, game.Reserve("scierie").QuantityOf("bois"), 6);
        Assert.Equal(80, game.Markets["a"].QuantityOf("bois"), 6);
        Assert.Equal(0, game.Funds["a"].Balance);
        Assert.Empty(game.Shipped);
        Assert.Empty(game.Markets["a"].CollectEarnings());
        Assert.Equal(0, game.Markets["a"].TallyOf("bois").LostInTransit);
    }

    [Fact]
    public void Entre_communes_le_client_paie_au_prix_du_marche_et_perd_une_part_en_route()
    {
        var game = new Game();
        game.Markets["a"].Offer("bois", 100, Market.StateSellerId("a"));
        game.Funds["b"].Deposit(1_000);
        var line = new SupplyLine("l1", "camp", "scierie", 0.5);

        var delivered = game.Run([line], Towns(sawmillCommune: "b"), new Dictionary<string, double> { ["camp"] = 100 }, loss: 0.2);

        // Il faut 20 à l'arrivée : 25 partent (20 % perdus en route), payés 25 × 2 = 50 par la commune B.
        Assert.Equal(20, delivered["l1"], 6);
        Assert.Equal(25, game.Markets["a"].TallyOf("bois").Sent, 6);
        Assert.Equal(5, game.Markets["a"].TallyOf("bois").LostInTransit, 6);
        Assert.Equal(950, game.Funds["b"].Balance, 6);
        Assert.Equal(50, game.Markets["a"].CollectEarnings()[Market.StateSellerId("a")], 6);
        Assert.Equal(("a", "b", 25.0), (game.Shipped[0].From, game.Shipped[0].To, Math.Round(game.Shipped[0].Quantity, 6)));
    }

    [Fact]
    public void Sans_argent_la_ligne_entre_communes_ne_livre_que_ce_que_le_client_peut_payer()
    {
        var game = new Game();
        game.Markets["a"].Offer("bois", 100, Market.StateSellerId("a"));
        game.Funds["b"].Deposit(10);
        var line = new SupplyLine("l1", "camp", "scierie", 0.5);

        var delivered = game.Run([line], Towns(sawmillCommune: "b"), new Dictionary<string, double> { ["camp"] = 100 }, loss: 0.2);

        // 10 d'argent pour un bois à 2 : 5 partent, 4 arrivent.
        Assert.Equal(4, delivered["l1"], 6);
        Assert.Equal(0, game.Funds["b"].Balance, 6);
    }

    [Fact]
    public void La_ligne_la_plus_ancienne_passe_d_abord()
    {
        var game = new Game();
        game.Markets["a"].Offer("bois", 200, Market.StateSellerId("a"));
        SupplyLine[] lines = [new("l1", "camp2", "scierie", 1), new("l2", "camp", "scierie", 1)];

        var delivered = game.Run(lines, Towns(), new Dictionary<string, double> { ["camp"] = 100, ["camp2"] = 100 });

        // La scierie n'a besoin que de 20 : la première ligne les donne tous, la seconde n'a plus rien à livrer.
        Assert.Equal(20, delivered["l1"], 6);
        Assert.Equal(0, delivered["l2"], 6);
    }

    [Fact]
    public void Une_ligne_ne_relie_que_des_batiments_publics_dont_la_recette_utilise_le_produit()
    {
        var towns = Towns(sawmillOwner: Ownership.Cooperative);
        Assert.Equal("les lignes ne relient que des bâtiments publics",
            SupplyLines.ProblemWith(towns["camp"], towns["scierie"], []));

        var publicTowns = Towns();
        Assert.Null(SupplyLines.ProblemWith(publicTowns["camp"], publicTowns["scierie"], []));
        Assert.Contains("n'utilise pas", SupplyLines.ProblemWith(publicTowns["scierie"], publicTowns["camp"], []));
        Assert.Equal("cette ligne existe déjà",
            SupplyLines.ProblemWith(publicTowns["camp"], publicTowns["scierie"], [new SupplyLine("l1", "camp", "scierie", 0.5)]));
    }

    [Fact]
    public void Le_client_prend_d_abord_sa_reserve_sans_rien_acheter()
    {
        var structures = Towns();
        var market = new Market(Products);
        market.Offer("bois", 100, "entreprise:autre");
        var reserve = new Inventory();
        reserve.Add("bois", 20);
        var treasury = new Treasury();
        treasury.Deposit(1_000);

        var share = Supply.Acquire(structures["scierie"], Scierie, 10, market, "a", treasury, null, null, new Dictionary<string, Demos.Domain.Population.SocialClass>(), reserve);

        Assert.Equal(1, share, 6);
        Assert.Equal(0, reserve.QuantityOf("bois"), 6);
        Assert.Equal(100, market.QuantityOf("bois"), 6);
        Assert.Equal(1_000, treasury.Balance, 6);
    }
}
