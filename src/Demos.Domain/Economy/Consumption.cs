using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class Consumption
{
    /// <summary>Besoins par habitant et par jour, selon le rang. Les autres besoins viendront avec leurs bâtiments.</summary>
    public static IReadOnlyDictionary<NeedCategory, double> NeedsPerPerson(Rank rank) => rank switch
    {
        Rank.Workers => new Dictionary<NeedCategory, double> { [NeedCategory.Food] = 1, [NeedCategory.Furniture] = 0.01 },
        Rank.Executives => new Dictionary<NeedCategory, double> { [NeedCategory.Food] = 1.5, [NeedCategory.Furniture] = 0.03 },
        _ => new Dictionary<NeedCategory, double> { [NeedCategory.Food] = 2, [NeedCategory.Furniture] = 0.1 },
    };

    /// <summary>
    /// Chaque classe achète sur le marché ce qui couvre ses besoins, dans la limite de sa richesse.
    /// Quand un produit manque, chaque classe est servie au prorata de sa demande.
    /// La part des besoins couverte devient la satisfaction de la classe.
    /// </summary>
    public static void RunDaily(IReadOnlyList<SocialClass> classes, IReadOnlyList<ProductDefinition> products, Market market)
    {
        var coverage = classes.ToDictionary(c => c, _ => new List<double>());

        foreach (var category in Enum.GetValues<NeedCategory>())
        {
            if (products.FirstOrDefault(p => p.Need == category) is not { } product)
                continue;

            var price = market.PriceOf(product.Id);
            var demands = new Dictionary<SocialClass, (double Needed, double Wanted)>();
            foreach (var socialClass in classes)
            {
                var needed = socialClass.Population * NeedsPerPerson(socialClass.Rank).GetValueOrDefault(category);
                if (needed <= 0)
                    continue;

                var affordable = price > 0 ? Math.Max(0, socialClass.Wealth) / price : needed;
                demands[socialClass] = (needed, Math.Min(needed, affordable));
            }

            var totalWanted = demands.Values.Sum(d => d.Wanted);
            var servedRate = totalWanted > 0 ? Math.Min(1, market.QuantityOf(product.Id) / totalWanted) : 0;

            foreach (var (socialClass, (needed, wanted)) in demands)
            {
                var bought = Math.Min(wanted * servedRate, market.QuantityOf(product.Id));
                if (bought > 0 && market.TryBuy(product.Id, bought))
                    socialClass.Wealth -= bought * price;
                else
                    bought = 0;

                coverage[socialClass].Add(bought / needed);
            }
        }

        foreach (var (socialClass, parts) in coverage)
        {
            if (parts.Count > 0)
                socialClass.NeedsSatisfaction = parts.Average();
        }
    }
}
