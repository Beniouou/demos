using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class Consumption
{
    /// <summary>
    /// Besoins consommés chaque jour par personne, selon le rang : tout le monde mange, s'habille et a besoin de
    /// loisirs (propositions : 0,05 par jour pour les ouvriers, 0,1 pour les professions intermédiaires, 0,2 pour les
    /// cadres) ; les professions intermédiaires et les cadres ont aussi besoin de meubles (0,02 et 0,1 par jour).
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
        [NeedCategory.Leisure] = 0.05,
    };

    private static readonly Dictionary<NeedCategory, double> IntermediateNeeds = new(WorkerNeeds) { [NeedCategory.Furniture] = 0.02, [NeedCategory.Leisure] = 0.1 };

    private static readonly Dictionary<NeedCategory, double> ExecutiveNeeds = new(WorkerNeeds) { [NeedCategory.Furniture] = 0.1, [NeedCategory.Leisure] = 0.2 };

    /// <summary>
    /// Chaque classe couvre ses besoins avec les produits reçus en nature, puis achète le reste sur le marché dans la
    /// limite de sa richesse, en commençant par le produit le moins cher quand plusieurs couvrent le même besoin. Quand
    /// un produit manque, chaque classe est servie au prorata de sa demande. La satisfaction est la moyenne de la part
    /// couverte de chacun de ses besoins : se nourrir, s'habiller, se loger et se distraire, plus se meubler pour les
    /// professions intermédiaires et les cadres.
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

            // Plusieurs produits peuvent couvrir un même besoin (pain ou viande pour se nourrir) : on prend d'abord
            // les produits reçus en nature, puis on achète les moins chers d'abord.
            var choices = products.Where(p => p.Need == category).OrderBy(p => market.PriceOf(p.Id)).ToList();
            if (choices.Count == 0)
            {
                foreach (var socialClass in concerned)
                    coverage[socialClass].Add(0);
                continue;
            }

            var needed = new Dictionary<SocialClass, double>();
            var covered = new Dictionary<SocialClass, double>();
            foreach (var socialClass in concerned.Where(c => c.Population > 0))
            {
                needed[socialClass] = socialClass.Population * NeedsOf(socialClass.Rank)[category];
                covered[socialClass] = 0;
                foreach (var product in choices)
                {
                    var fromGoods = Math.Min(needed[socialClass] - covered[socialClass], socialClass.Goods.QuantityOf(product.Id));
                    socialClass.Goods.TryRemove(product.Id, fromGoods);
                    covered[socialClass] += fromGoods;
                }
            }

            foreach (var product in choices)
            {
                var price = market.PriceOf(product.Id);
                var wanted = needed.Keys.ToDictionary(
                    c => c,
                    c =>
                    {
                        var missing = needed[c] - covered[c];
                        return Math.Min(missing, price > 0 ? Math.Max(0, c.Wealth) / price : missing);
                    });

                var totalWanted = wanted.Values.Sum();
                var servedRate = totalWanted > 0 ? Math.Min(1, market.QuantityOf(product.Id) / totalWanted) : 0;
                foreach (var (socialClass, quantity) in wanted)
                {
                    var bought = Math.Min(quantity * servedRate, market.QuantityOf(product.Id));
                    if (bought <= 0 || !market.TryBuy(product.Id, bought))
                        continue;

                    socialClass.Wealth -= bought * price;
                    covered[socialClass] += bought;
                }
            }

            // Ce qui manque encore : faute d'argent pour en acheter plus au prix le plus bas, sinon faute de produits.
            var cheapest = choices.Select(p => market.PriceOf(p.Id)).Where(p => p > 0).DefaultIfEmpty(0).Min();
            foreach (var (socialClass, need) in needed)
            {
                var missing = need - covered[socialClass];
                var stillAffordable = cheapest > 0 ? Math.Max(0, socialClass.Wealth) / cheapest : missing;
                var lackingMoney = Math.Max(0, missing - stillAffordable);

                coverage[socialClass].Add(covered[socialClass] / need);
                moneyShortfall[socialClass].Add(lackingMoney / need);
                supplyShortage[socialClass].Add((missing - lackingMoney) / need);
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
