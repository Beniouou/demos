using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

public class PricingTests
{
    [Fact]
    public void Le_prix_laisse_30_pourcent_de_benefice_au_batiment_de_reference_a_plein_regime()
    {
        var menuiserie = new BuildingType("menuiserie", "Menuiserie", "Production", 1, "", Jobs: 150, DailyWage: 2, Production: new Production("meubles", 0.1));

        // Salaires par emploi de production : 2 + 0,1 × 4 + 0,01 × 8 = 2,48. Pour 0,1 meuble : 24,8, × 1,3 = 32,24.
        Assert.Equal(32.24, Pricing.PriceFrom(menuiserie)!.Value, precision: 6);

        var revenue = 150 * 0.1 * Pricing.PriceFrom(menuiserie)!.Value;
        var wages = 150 * Pricing.WageCostPerProductionJob(2);
        Assert.Equal(0.3, (revenue - wages) / wages, precision: 6);
    }

    [Fact]
    public void Le_batiment_de_reference_est_celui_de_plus_bas_niveau()
    {
        BuildingType[] catalogue =
        [
            new("usine", "Usine", "Production", 2, "", Jobs: 100, DailyWage: 3, Production: new Production("vetements", 1)),
            new("atelier", "Atelier", "Production", 1, "", Jobs: 150, DailyWage: 2, Production: new Production("vetements", 0.35)),
        ];
        ProductDefinition[] products = [new("vetements", "Vêtements", NeedCategory.Clothing, 8), new("services", "Services", NeedCategory.Services, 10)];

        var priced = Pricing.WithReferencePrices(products, catalogue);

        Assert.Equal(1.3 * 2.48 / 0.35, priced[0].BasePrice, precision: 6);
        // Aucun bâtiment ne fait de services : le prix du catalogue reste.
        Assert.Equal(10, priced[1].BasePrice);
    }
}
