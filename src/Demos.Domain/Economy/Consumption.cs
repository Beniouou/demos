using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class Consumption
{
    /// <summary>
    /// Besoins de base consommés chaque jour par personne, selon le rang : tout le monde mange et s'habille ; les
    /// professions intermédiaires et les cadres ont aussi besoin de meubles (propositions : 0,02 et 0,1 par jour).
    /// Se loger est aussi un besoin de base, mais c'est une capacité (voir <see cref="Housing"/>).
    /// </summary>
    public static IReadOnlyDictionary<NeedCategory, double> NeedsOf(Rank rank) => rank switch
    {
        Rank.Intermediate => IntermediateNeeds,
        Rank.Executives => ExecutiveNeeds,
        _ => WorkerNeeds,
    };

    /// <summary>Tous les besoins consommés, quel que soit le rang.</summary>
    public static IEnumerable<NeedCategory> AllNeeds => Enum.GetValues<Rank>().SelectMany(r => NeedsOf(r).Keys).Distinct();

    private static readonly Dictionary<NeedCategory, double> WorkerNeeds = new()
    {
        [NeedCategory.Food] = 1,
        [NeedCategory.Clothing] = 0.01,
    };

    private static readonly Dictionary<NeedCategory, double> IntermediateNeeds = new(WorkerNeeds) { [NeedCategory.Furniture] = 0.02 };

    private static readonly Dictionary<NeedCategory, double> ExecutiveNeeds = new(WorkerNeeds) { [NeedCategory.Furniture] = 0.1 };

    /// <summary>
    /// Chaque classe couvre ses besoins avec les produits reçus en nature, puis achète le reste sur le marché
    /// dans la limite de sa richesse. Quand un produit manque, chaque classe est servie au prorata de sa demande.
    /// La satisfaction est la moyenne de la part couverte de chacun de ses besoins : se nourrir, s'habiller et se
    /// loger, plus se meubler pour les professions intermédiaires et les cadres.
    /// </summary>
    public static void RunDaily(IReadOnlyList<SocialClass> classes, IReadOnlyList<ProductDefinition> products, Market market)
    {
        var coverage = classes.ToDictionary(c => c, c => new List<double> { c.HousingCoverage });

        // Part de chaque besoin que la classe ne peut pas acheter faute d'argent (et non faute de produits).
        var moneyShortfall = classes.ToDictionary(c => c, _ => new List<double>());

        // Part de chaque besoin non couverte faute de produits sur le marché, ou de places de logement.
        var supplyShortage = classes.ToDictionary(c => c, c => new List<double> { c.HousingPlaceShortage });

        foreach (var category in AllNeeds)
        {
            var concerned = classes.Where(c => NeedsOf(c.Rank).ContainsKey(category)).ToList();
            if (products.FirstOrDefault(p => p.Need == category) is not { } product)
            {
                foreach (var socialClass in concerned)
                    coverage[socialClass].Add(0);
                continue;
            }

            var price = market.PriceOf(product.Id);
            var demands = new Dictionary<SocialClass, (double Needed, double FromGoods, double Wanted)>();
            foreach (var socialClass in concerned.Where(c => c.Population > 0))
            {
                var needed = socialClass.Population * NeedsOf(socialClass.Rank)[category];
                var fromGoods = Math.Min(needed, socialClass.Goods.QuantityOf(product.Id));
                socialClass.Goods.TryRemove(product.Id, fromGoods);

                var missing = needed - fromGoods;
                var affordable = price > 0 ? Math.Max(0, socialClass.Wealth) / price : missing;
                demands[socialClass] = (needed, fromGoods, Math.Min(missing, affordable));
            }

            var totalWanted = demands.Values.Sum(d => d.Wanted);
            var servedRate = totalWanted > 0 ? Math.Min(1, market.QuantityOf(product.Id) / totalWanted) : 0;

            foreach (var (socialClass, (needed, fromGoods, wanted)) in demands)
            {
                var bought = Math.Min(wanted * servedRate, market.QuantityOf(product.Id));
                if (bought > 0 && market.TryBuy(product.Id, bought))
                    socialClass.Wealth -= bought * price;
                else
                    bought = 0;

                coverage[socialClass].Add((fromGoods + bought) / needed);
                moneyShortfall[socialClass].Add((needed - fromGoods - wanted) / needed);
                supplyShortage[socialClass].Add((wanted - bought) / needed);
            }
        }

        foreach (var (socialClass, parts) in coverage)
        {
            if (socialClass.Population <= 0)
                continue;

            socialClass.NeedsSatisfaction = parts.Average();
            if (moneyShortfall[socialClass].Count > 0)
                socialClass.RecordMoneyShortfall(moneyShortfall[socialClass].Average());
            socialClass.RecordSupplyShortage(supplyShortage[socialClass].Average());
        }
    }
}
