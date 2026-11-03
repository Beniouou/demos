using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

/// <summary>TVA sur la valeur ajoutée et épargne progressive des entreprises (désactivées dans le jeu pour l'instant).</summary>
[Collection(GlobalBalanceCollection.Name)]
public class VatAndSavingsTests
{
    private static T With<T>(BalanceSettings settings, Func<T> action)
    {
        var saved = Balance.Current;
        Balance.Current = settings;
        try
        {
            return action();
        }
        finally
        {
            Balance.Current = saved;
        }
    }

    [Fact]
    public void La_valeur_ajoutee_d_un_moulin_est_le_prix_de_sa_farine_moins_son_ble()
    {
        var mill = new Structure("moulin", "Moulin", StructureKind.Building, "commune", Ownership.Cooperative, "ouvriers",
            new Production("farine", 10, [new RecipeInput("ble", 1)]), Jobs: 10);

        var share = Taxation.ValueAddedShare(mill, id => id == "farine" ? 1 : 0.5);

        Assert.Equal(0.5, share, precision: 9);
    }

    [Fact]
    public void Le_prix_inclut_la_tva_sur_le_travail_mais_pas_sur_les_matieres_deja_taxees()
    {
        // Un atelier de la liste 2 (sans chauffage ni eau) : 2,48 de salaires par emploi, 10 farines par travailleur.
        var mill = new BuildingType("moulin", "Moulin", "Production", 1, "", Jobs: 10, DailyWage: 2,
            Recipes: [new Production("farine", 10, [new RecipeInput("ble", 1)])], Tier: 2);

        var withoutVat = With(new BalanceSettings(), () => Pricing.PriceFrom(mill, _ => 0.5))!.Value;
        var withVat = With(new BalanceSettings { VatStateRate = 0.03, VatCommuneRate = 0.02 }, () => Pricing.PriceFrom(mill, _ => 0.5))!.Value;

        Assert.Equal(1.3 * (0.248 + 0.5), withoutVat, precision: 9);
        Assert.Equal(1.3 * (0.248 / 0.95 + 0.5), withVat, precision: 9);
    }

    [Fact]
    public void Une_entreprise_qui_remplit_sa_reserve_verse_quand_meme_la_part_non_epargnee_de_son_benefice()
    {
        var structures = new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
            new("ferme", "Ferme", StructureKind.Building, "commune", Ownership.Cooperative, "ouvriers",
                new Production("ble", 3), Jobs: 100, DailyWage: 2),
        }.ToDictionary(s => s.Id);
        var owners = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 100);
        var fund = new Treasury();
        fund.Deposit(2_000);

        // Réserve de 30 jours : 7 440, loin d'être pleine. Bénéfice du jour : 1 000, dont 25 % mis de côté.
        With(new BalanceSettings { ReserveSavingsShare = 0.25 }, () =>
        {
            Companies.PayDividends(structures, new Dictionary<string, SocialClass> { ["ouvriers"] = owners }, _ => fund,
                balanceAtDayStart: _ => 1_000);
            return 0;
        });

        Assert.Equal(750, owners.Wealth, precision: 9);
        Assert.Equal(1_250, fund.Balance, precision: 9);
    }
}
