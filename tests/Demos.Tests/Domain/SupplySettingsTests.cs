using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

/// <summary>Les réglages d'achat et de vente des bâtiments publics (règle décidée le 19 novembre 2026).</summary>
public class SupplySettingsTests
{
    private static readonly ProductDefinition[] Products = [new("bois", "Bois", NeedCategory.Furniture, 2), new("planche", "Planche", null, 5)];
    private static readonly Production Scierie = new("planche", 1, [new RecipeInput("bois", 1)]);
    private static readonly string State = Market.StateSellerId("commune");

    private static Dictionary<string, Structure> Town(Ownership sawmill = Ownership.Public) => new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        new("camp", "Camp", StructureKind.Building, "commune", Ownership.Public, null, new Production("bois", 10), Jobs: 10),
        new("prive", "Camp privé", StructureKind.Building, "commune", Ownership.Private, "riches", new Production("bois", 10), Jobs: 10),
        new("scop", "Camp SCOP", StructureKind.Building, "commune", Ownership.Cooperative, "ouvriers", new Production("bois", 10), Jobs: 10),
        new("scierie", "Scierie", StructureKind.Building, "commune", sawmill, sawmill == Ownership.Public ? null : "riches", Scierie, Jobs: 10),
    }.ToDictionary(s => s.Id);

    private static double Acquire(Dictionary<string, Structure> structures, Market market, PurchaseRules rules)
    {
        var treasury = new Treasury();
        treasury.Deposit(10_000);
        return Supply.Acquire(structures["scierie"], Scierie, 30, market, "commune", treasury, null, null,
            new Dictionary<string, SocialClass>(), rules: rules);
    }

    [Fact]
    public void Une_reserve_hors_vente_echappe_aux_acheteurs_et_aux_echanges_mais_pas_a_son_vendeur()
    {
        var market = new Market(Products);
        market.Offer("bois", 100, State);
        market.SetHeld("bois", State, 60);

        Assert.Equal(40, market.QuantityForSale("bois"), 6);
        Assert.False(market.TryBuy("bois", 50));
        Assert.True(market.TryBuy("bois", 40));
        Assert.Equal(60, market.QuantityOf("bois"), 6);
        Assert.Empty(market.Take("bois", 10).Where(t => t.Value > 0));
        // L'État lui-même (ses chantiers, ses bâtiments) s'en sert.
        Assert.True(market.TryWithdraw("bois", 60, State));
    }

    [Fact]
    public void Les_echanges_ne_prennent_rien_chez_un_vendeur_qui_refuse_l_envoi()
    {
        var market = new Market(Products);
        market.Offer("bois", 100, State);
        market.Offer("bois", 100, "entreprise:prive");
        market.SetNotExported("bois", State, true);

        var taken = market.Take("bois", 50);

        Assert.Equal(50, taken.GetValueOrDefault("entreprise:prive"), 6);
        Assert.Equal(0, taken.GetValueOrDefault(State), 6);
        Assert.Equal(100, market.QuantityOf("bois", State), 6);
    }

    [Fact]
    public void N_acheter_que_par_ses_lignes_empeche_tout_achat_au_marche()
    {
        var market = new Market(Products);
        market.Offer("bois", 100, "entreprise:prive");

        var share = Acquire(Town(), market, new PurchaseRules(MarketAllowed: false));

        Assert.Equal(0, share, 6);
        Assert.Equal(100, market.QuantityOf("bois"), 6);
    }

    [Fact]
    public void Un_batiment_public_peut_refuser_d_acheter_aux_entreprises_privees()
    {
        var structures = Town();
        var market = new Market(Products);
        market.Offer("bois", 20, "entreprise:prive");
        market.Offer("bois", 20, "entreprise:scop");
        var rules = new SupplySettings(RefusePrivate: true).PurchaseRulesFor(structures["scierie"], structures);

        var share = Acquire(structures, market, rules);

        // 30 bois voulus : la SCOP en a 20, le privé est refusé.
        Assert.Equal(20.0 / 30, share, 6);
        Assert.Equal(20, market.QuantityOf("bois", "entreprise:prive"), 6);
        Assert.Equal(0, market.QuantityOf("bois", "entreprise:scop"), 6);
    }

    [Fact]
    public void Une_loi_qui_interdit_le_refus_le_rend_sans_effet()
    {
        var structures = Town();
        var law = TestLaws.Law("libre-concurrence", "etat", "propriete",
            TestLaws.Brick(TestLaws.SupplierChoice, new LawSubject(SubjectKind.Ownership, "Private"), 0));
        TestLaws.Apply(structures, law);

        Assert.False(structures["scierie"].Rules.MayRefusePrivate);
        Assert.True(structures["scierie"].Rules.MayRefuseCooperative);
        var rules = new SupplySettings(RefusePrivate: true).PurchaseRulesFor(structures["scierie"], structures);
        Assert.Null(rules.SellerAllowed);
    }

    [Fact]
    public void Un_batiment_qui_n_est_pas_public_achete_librement()
    {
        var structures = Town(sawmill: Ownership.Private);

        Assert.Same(PurchaseRules.Free, new SupplySettings(OnlyFromLines: true, RefusePrivate: true).PurchaseRulesFor(structures["scierie"], structures));
    }

    [Fact]
    public void La_reserve_de_l_etat_additionne_ses_batiments_et_disparait_quand_on_la_retire()
    {
        var structures = Town();
        var market = new Market(Products);
        market.Offer("bois", 500, State);
        var settings = new Dictionary<string, SupplySettings> { ["camp"] = new(ReserveDays: 3, NoExport: true) };
        var produced = new Dictionary<string, double> { ["camp"] = 100 };

        var applied = SupplySettings.ApplySales(structures, id => settings.GetValueOrDefault(id, SupplySettings.None), produced, _ => market, new HashSet<(string, string)>());

        Assert.Equal(300, market.HeldOf("bois", State), 6);
        Assert.Empty(market.Take("bois", 50).Where(t => t.Value > 0));

        settings.Clear();
        SupplySettings.ApplySales(structures, id => settings.GetValueOrDefault(id, SupplySettings.None), produced, _ => market, applied);

        Assert.Equal(0, market.HeldOf("bois", State), 6);
        Assert.Equal(50, market.Take("bois", 50).Values.Sum(), 6);
    }

    [Fact]
    public void L_ordre_des_lignes_d_un_client_est_sa_priorite()
    {
        var game = new Dictionary<string, Market> { ["commune"] = new(Products) };
        game["commune"].Offer("bois", 200, State);
        SupplyLine[] lines = [new("l1", "camp", "scierie", 1), new("l2", "camp", "scierie", 1)];
        var reserve = new Inventory();

        var delivered = SupplyLines.RunDaily(lines.Reverse().ToList(), Town(), new Dictionary<string, double> { ["camp"] = 100 },
            new Dictionary<string, double> { ["scierie"] = 10 }, id => game[id], _ => reserve, new Treasury());

        // La scierie a besoin de 10 : la première ligne de la liste (l2, mise en tête) les donne.
        Assert.Equal(10, delivered["l2"], 6);
        Assert.Equal(0, delivered["l1"], 6);
    }
}
