using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class CompaniesTests
{
    private static readonly ProductDefinition[] Products = [new("pain", "Pain", NeedCategory.Food, 1)];

    private static readonly Dictionary<string, Structure> Structures = new Structure[]
    {
        new("etat", "État", StructureKind.PoliticalEntity, null),
        new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        new("ferme", "Ferme", StructureKind.Building, "commune", Ownership.Cooperative, "ouvriers",
            new Production("pain", 3), Jobs: 100, DailyWage: 2),
    }.ToDictionary(s => s.Id);

    private static SocialClass Workers(double wealth)
    {
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 100) { Wealth = wealth };
        workers.SetResidence("commune", 1);
        return workers;
    }

    [Fact]
    public void L_entreprise_paie_ses_salaires_avec_sa_caisse_et_non_avec_la_richesse_de_ses_proprietaires()
    {
        var workers = Workers(wealth: 0);
        var fund = new Treasury();
        fund.Deposit(1_000);
        var market = new Market(Products);
        SocialClass[] classes = [workers];

        WorkDay.Run(Structures, Employment.Assign(Structures, classes), classes, null, new Treasury(), _ => market, companyFundOf: _ => fund);

        // 100 ouvriers × 2 = 200 de salaires, pris dans la caisse ; le pain est en vente au nom de l'entreprise.
        Assert.Equal(800, fund.Balance, precision: 6);
        Assert.Equal(200, workers.Wealth, precision: 6);
        Assert.Equal(300, market.QuantityOf("pain", Market.CompanySellerId("ferme")), precision: 6);
    }

    [Fact]
    public void L_entreprise_garde_sa_reserve_et_verse_le_reste_a_ses_proprietaires()
    {
        var workers = Workers(wealth: 0);
        var fund = new Treasury();
        var reserve = Companies.ReserveOf(Structures["ferme"]);
        fund.Deposit(reserve + 500);

        Companies.PayDividends(Structures, new Dictionary<string, SocialClass> { ["ouvriers"] = workers }, _ => fund);

        // Réserve : 30 jours × 100 emplois × 2,48 = 7 440.
        Assert.Equal(7_440, reserve, precision: 6);
        Assert.Equal(reserve, fund.Balance, precision: 6);
        Assert.Equal(500, workers.Wealth, precision: 6);
        Assert.Equal(500, workers.SeasonIncome, precision: 6);
    }

    [Fact]
    public void Sous_sa_reserve_l_entreprise_ne_verse_rien()
    {
        var workers = Workers(wealth: 0);
        var fund = new Treasury();
        fund.Deposit(100);

        Companies.PayDividends(Structures, new Dictionary<string, SocialClass> { ["ouvriers"] = workers }, _ => fund);

        Assert.Equal(100, fund.Balance, precision: 6);
        Assert.Equal(0, workers.Wealth, precision: 6);
    }
}
