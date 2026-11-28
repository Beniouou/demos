using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class Consumption
{
    /// <summary>
    /// Besoins consommés chaque jour par personne. Tout le monde a les mêmes besoins ; seules les quantités changent avec
    /// le rang (réglages d'équilibrage, voir <see cref="BalanceSettings.NeedsByRank"/>). Plus tard, elles varieront aussi
    /// selon l'idéologie. Se loger est aussi un besoin, mais c'est une capacité : les rangs supérieurs occupent des
    /// logements plus grands (voir <see cref="Housing"/>).
    /// </summary>
    public static IReadOnlyDictionary<NeedCategory, double> NeedsOf(Rank rank) =>
        Balance.Current.NeedsByRank.TryGetValue(rank, out var needs) ? needs : new Dictionary<NeedCategory, double>();

    /// <summary>Possession visée par personne d'un besoin durable pour un rang (0 : besoin consommé chaque jour).</summary>
    public static double PossessionOf(Rank rank, NeedCategory need) =>
        Balance.Current.PossessionByRank.TryGetValue(rank, out var byNeed) ? byNeed.GetValueOrDefault(need) : 0;

    /// <summary>
    /// Donne à chaque groupe sa possession visée de chaque besoin durable, en objets du produit qui dure le plus
    /// longtemps parmi ceux qui le couvrent (début de partie : chacun commence équipé ; au départ d'exil, une part seulement).
    /// </summary>
    public static void EquipFully(IEnumerable<SocialClass> classes, IReadOnlyList<ProductDefinition> products, double share = 1)
    {
        foreach (var socialClass in classes)
        {
            foreach (var (need, perPerson) in Balance.Current.PossessionByRank.GetValueOrDefault(socialClass.Rank) ?? [])
            {
                if (products.Where(p => p.Need == need && p.IsDurable).OrderByDescending(p => p.UsageDays).FirstOrDefault() is { } product)
                    socialClass.Possessions.Add(product.Id, socialClass.Population * perPerson * share);
            }
        }
    }

    /// <summary>Tous les besoins consommés, quel que soit le rang.</summary>
    public static IEnumerable<NeedCategory> AllNeeds => Enum.GetValues<Rank>().SelectMany(r => NeedsOf(r).Keys).Distinct();

    /// <summary>
    /// Chaque classe couvre ses besoins avec les produits reçus en nature, puis achète le reste sur le marché dans la
    /// limite de sa richesse, en commençant par le produit le moins cher quand plusieurs couvrent le même besoin. Quand
    /// un produit manque, chaque classe est servie au prorata de sa demande. La satisfaction est la moyenne de la part
    /// couverte de chacun de ses besoins : se nourrir, s'habiller, se meubler, se distraire, être administré, être
    /// protégé et se loger. Les services (administratif, sécurité) ne se stockent pas : on consomme ceux du jour.
    /// </summary>
    public static void RunDaily(IReadOnlyList<SocialClass> classes, IReadOnlyList<ProductDefinition> products, Market market)
    {
        var coverage = classes.ToDictionary(c => c, c => new List<double> { c.HousingCoverage });

        // Part de chaque besoin que la classe ne peut pas acheter faute d'argent (et non faute de produits).
        var moneyShortfall = classes.ToDictionary(c => c, _ => new List<double>());

        // Part de chaque besoin non couverte faute de produits sur le marché, ou de places de logement.
        var supplyShortage = classes.ToDictionary(c => c, c => new List<double> { c.HousingPlaceShortage });

        // Le relevé besoin par besoin, pour l'interface (sans effet sur la partie) ; se loger d'abord.
        var details = classes.ToDictionary(c => c, c => new Dictionary<string, NeedRecord>
        {
            [NeedCategory.Housing.Id] = new(c.HousingCoverage, Math.Max(0, 1 - c.HousingCoverage - c.HousingPlaceShortage), c.HousingPlaceShortage),
        });

        foreach (var category in AllNeeds)
        {
            var concerned = classes.Where(c => NeedsOf(c.Rank).ContainsKey(category)).ToList();

            // Plusieurs produits peuvent couvrir un même besoin (pain ou viande pour se nourrir) : on prend d'abord
            // les produits reçus en nature, puis on achète les moins chers d'abord.
            var choices = products.Where(p => p.Need == category).OrderBy(p => market.PriceOf(p.Id)).ToList();
            if (choices.Count == 0)
            {
                foreach (var socialClass in concerned)
                {
                    coverage[socialClass].Add(0);
                    details[socialClass][category.Id] = new NeedRecord(0, 0, 1);
                }
                continue;
            }

            var needed = new Dictionary<SocialClass, double>();
            var covered = new Dictionary<SocialClass, double>();
            // Les besoins durables (décidé le 20 novembre 2026) : le groupe possède un stock qui s'use ; il achète de quoi
            // remplacer l'usure et combler une part de son manque. Possession visée de chaque groupe concerné.
            var targets = new Dictionary<SocialClass, double>();
            var durableNeed = choices.Any(p => p.IsDurable);
            foreach (var socialClass in concerned.Where(c => c.Population > 0))
            {
                if (durableNeed && PossessionOf(socialClass.Rank, category) is > 0 and var perPerson)
                {
                    var worn = 0.0;
                    foreach (var product in choices)
                    {
                        var owned = socialClass.Possessions.QuantityOf(product.Id);
                        var wear = owned * Math.Min(1, 1 / (product.UsageDays ?? 1));
                        socialClass.Possessions.TryRemove(product.Id, wear);
                        worn += wear;
                        // Ce qui a été reçu en nature rejoint les objets possédés.
                        var received = socialClass.Goods.QuantityOf(product.Id);
                        socialClass.Goods.TryRemove(product.Id, received);
                        socialClass.Possessions.Add(product.Id, received);
                    }

                    var target = socialClass.Population * perPerson;
                    var missing = Math.Max(0, target - choices.Sum(p => socialClass.Possessions.QuantityOf(p.Id)));
                    targets[socialClass] = target;
                    needed[socialClass] = Math.Min(missing, worn + Math.Max(0, missing - worn) * Balance.Current.PossessionCatchUp);
                    covered[socialClass] = 0;
                    continue;
                }

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
                var servedRate = totalWanted > 0 ? Math.Min(1, market.QuantityForSale(product.Id) / totalWanted) : 0;
                foreach (var (socialClass, quantity) in wanted)
                {
                    var bought = Math.Min(quantity * servedRate, market.QuantityForSale(product.Id));
                    if (bought <= 0 || !market.TryBuy(product.Id, bought))
                        continue;

                    socialClass.Wealth -= bought * price;
                    covered[socialClass] += bought;
                    // Un objet durable acheté rejoint ce que le groupe possède.
                    if (targets.ContainsKey(socialClass))
                        socialClass.Possessions.Add(product.Id, bought);
                }
            }

            // Ce qui manque encore : faute d'argent pour en acheter plus au prix le plus bas, sinon faute de produits.
            var cheapest = choices.Select(p => market.PriceOf(p.Id)).Where(p => p > 0).DefaultIfEmpty(0).Min();
            foreach (var (socialClass, need) in needed)
            {
                var missing = need - covered[socialClass];
                var stillAffordable = cheapest > 0 ? Math.Max(0, socialClass.Wealth) / cheapest : missing;
                var lackingMoney = Math.Max(0, missing - stillAffordable);

                if (targets.TryGetValue(socialClass, out var target))
                {
                    // Un besoin durable est couvert à hauteur de ce que le groupe possède ; ce qui manque à sa possession
                    // visée se partage entre manque d'argent et manque de produits comme ses achats du jour.
                    var owned = choices.Sum(p => socialClass.Possessions.QuantityOf(p.Id));
                    var short_ = Math.Max(0, 1 - owned / target);
                    var moneyPart = missing > 0 ? lackingMoney / missing : 0;
                    coverage[socialClass].Add(Math.Min(1, owned / target));
                    moneyShortfall[socialClass].Add(short_ * moneyPart);
                    supplyShortage[socialClass].Add(short_ * (1 - moneyPart));
                    details[socialClass][category.Id] = new NeedRecord(Math.Min(1, owned / target), short_ * moneyPart, short_ * (1 - moneyPart));
                    continue;
                }

                coverage[socialClass].Add(covered[socialClass] / need);
                moneyShortfall[socialClass].Add(lackingMoney / need);
                supplyShortage[socialClass].Add((missing - lackingMoney) / need);
                details[socialClass][category.Id] = new NeedRecord(covered[socialClass] / need, lackingMoney / need, (missing - lackingMoney) / need);
            }
        }

        foreach (var (socialClass, parts) in coverage)
        {
            if (socialClass.Population <= 0)
                continue;

            socialClass.NeedsSatisfaction = parts.Average();
            socialClass.NeedDetails = details[socialClass];
            if (moneyShortfall[socialClass].Count > 0)
                socialClass.RecordMoneyShortfall(moneyShortfall[socialClass].Average());
            socialClass.RecordSupplyShortage(supplyShortage[socialClass].Average());
        }
    }
}
