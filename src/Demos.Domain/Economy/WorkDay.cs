using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class WorkDay
{
    /// <summary>
    /// Pour chaque lieu de travail : il prend ses matières (voir <see cref="Supply"/>), produit selon ses travailleurs
    /// de production et ses matières, paie ses emplois, puis livre le reste au stockage de sa commune, en vente au nom
    /// de l'État s'il est public, de l'entreprise sinon.
    /// Le travail est payé en salaire, sauf si les lois qui couvrent le bâtiment autorisent le paiement en nature (voir <see cref="AppliedLaws"/>) :
    /// les travailleurs reçoivent alors des produits du bâtiment, au prix du marché.
    /// Le salaire en argent vient de la caisse de la commune puis du trésor (public), ou de la caisse de l'entreprise
    /// (privé, coopératif), et est versé en partie si l'argent manque.
    /// </summary>
    /// <remarks>
    /// Ordre de la journée (décidé le 20 novembre 2026) : les bâtiments travaillent par étapes de la chaîne (voir
    /// <see cref="ChainStages"/>) : d'abord ceux qui n'utilisent aucune matière, puis ceux qui utilisent leurs produits.
    /// La production d'une étape entre au stockage, passe par <paramref name="afterStage"/> (les lignes
    /// d'approvisionnement de la commune), puis peut servir à l'étape suivante le jour même. Dans une étape, les
    /// bâtiments d'une commune qui veulent la même matière sont servis au prorata de leur besoin quand elle manque ; ce
    /// qu'un bâtiment n'a pas pu prendre (argent, vendeurs refusés) est ensuite offert aux autres de l'étape.
    /// </remarks>
    /// <returns>Ce que chaque lieu de travail a produit dans la journée.</returns>
    public static Dictionary<string, double> Run(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> productionWorkers,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury,
        Func<string, Market> marketOf,
        Func<string, Treasury>? communeFundOf = null,
        WageLedger? wageLedger = null,
        Func<Structure, Treasury>? companyFundOf = null,
        Func<Structure, double>? energyShareOf = null,
        Func<Structure, double>? publicEfficiencyOf = null,
        Dictionary<string, double>? supplyShares = null,
        Func<string, Inventory?>? reserveOf = null,
        Func<Structure, PurchaseRules?>? purchaseRulesOf = null,
        Action<IReadOnlyDictionary<string, double>>? afterStage = null)
    {
        // Qui travaille où, rangé une fois par bâtiment, et les classes par identifiant : on ne parcourt pas toutes les
        // classes du pays pour chaque bâtiment.
        var staffOf = new Dictionary<string, List<SocialClass>>();
        foreach (var socialClass in classes)
        {
            foreach (var (structureId, share) in socialClass.Employment)
            {
                if (share <= 0)
                    continue;
                if (!staffOf.TryGetValue(structureId, out var staff))
                    staffOf[structureId] = staff = [];
                staff.Add(socialClass);
            }
        }

        var classesById = new Dictionary<string, SocialClass>();
        foreach (var socialClass in classes)
            classesById.TryAdd(socialClass.Id, socialClass);

        var output = new Dictionary<string, double>();
        var workplaces = structures.Values.Where(s => s.Jobs > 0).ToList();
        var stageOf = ChainStages(workplaces);
        foreach (var stage in workplaces.GroupBy(w => stageOf[w.Id]).OrderBy(g => g.Key))
        {
            // Ce que chaque bâtiment de l'étape prévoit de produire, avant les matières.
            var plans = stage.Select(workplace =>
            {
                var communeId = Communes.CommuneOf(workplace, structures) ?? "";
                var planned = workplace.Production is { } production
                    ? production.QuantityPerWorker * productionWorkers.GetValueOrDefault(workplace.Id)
                    : 0;
                // Les lois peuvent augmenter ou baisser la production (quotas, priorité à la production).
                planned *= workplace.Rules.ProductionFactor;
                // Sans assez d'énergie la veille (chauffage, eau), le bâtiment produit moins.
                planned *= energyShareOf?.Invoke(workplace) ?? 1;
                // Un bâtiment public produit moins quand l'entité qui le gère est débordée (extension).
                planned *= publicEfficiencyOf?.Invoke(workplace) ?? 1;
                return (Workplace: workplace, CommuneId: communeId, Planned: planned, Reserve: reserveOf?.Invoke(workplace.Id));
            }).ToList();

            // Les matières viennent du stockage de la commune ; sans elles, le bâtiment produit moins. Quand une matière
            // manque, chaque bâtiment de l'étape n'en prend que sa part, au prorata de son besoin (hors de sa réserve).
            var ratios = ShortageRatios(plans.Select(p => (p.Workplace, p.CommuneId, p.Planned, p.Reserve)), marketOf);
            var supplied = new Dictionary<string, double>();
            foreach (var (workplace, communeId, planned, reserve) in plans)
            {
                if (workplace.Production is not { } recipe)
                    continue;
                double Cap(string productId) =>
                    Math.Max(0, planned * recipe.Materials.Where(m => m.ProductId == productId).Sum(m => m.Quantity) - (reserve?.QuantityOf(productId) ?? 0))
                    * ratios.GetValueOrDefault((communeId, productId), 1);
                supplied[workplace.Id] = Supply.Acquire(workplace, recipe, planned, marketOf(communeId), communeId, treasury, communeFundOf?.Invoke(communeId),
                    companyFundOf, classesById, reserve, purchaseRulesOf?.Invoke(workplace), ratios.Count > 0 ? Cap : null);
            }

            // Ce que les autres n'ont pas pu prendre est offert à ceux à qui il manque encore des matières.
            if (ratios.Count > 0)
            {
                foreach (var (workplace, communeId, planned, reserve) in plans)
                {
                    if (workplace.Production is not { } recipe || supplied[workplace.Id] >= 1 || planned <= 0)
                        continue;
                    var first = supplied[workplace.Id];
                    var more = Supply.Acquire(workplace, recipe, planned * (1 - first), marketOf(communeId), communeId, treasury,
                        communeFundOf?.Invoke(communeId), companyFundOf, classesById, reserve, purchaseRulesOf?.Invoke(workplace));
                    supplied[workplace.Id] = first + (1 - first) * more;
                }
            }

            var stageOutput = new Dictionary<string, double>();
            foreach (var (workplace, communeId, planned, _) in plans)
            {
                var produced = planned * supplied.GetValueOrDefault(workplace.Id, 1);
                // Retenu pour l'interface (la part que les matières ont permise) ; aucun effet sur la journée.
                if (supplyShares is not null && supplied.TryGetValue(workplace.Id, out var share))
                    supplyShares[workplace.Id] = share;
                Finish(workplace, communeId, produced);
                stageOutput[workplace.Id] = output[workplace.Id];
            }

            afterStage?.Invoke(stageOutput);
        }

        return output;

        // Paiement des emplois et livraison de la production au stockage de sa commune.
        void Finish(Structure workplace, string communeId, double produced)
        {
            var market = marketOf(communeId);
            var dues = (staffOf.TryGetValue(workplace.Id, out var workers) ? workers : [])
                .Select(c => (Class: c, Due: c.MembersWorkingIn(workplace.Id) * Staffing.WageOf(workplace, c.Rank)))
                .Where(d => d.Due > 0)
                .ToList();
            var totalDue = dues.Sum(d => d.Due);
            var initialDue = totalDue;

            output[workplace.Id] = produced;

            if (totalDue > 0 && produced > 0 && workplace.Rules.PaidInKind)
            {
                var productId = workplace.Production!.Value.ProductId;
                var price = market.PriceOf(productId);
                var inKind = price > 0 ? Math.Min(produced, totalDue / price) : produced;

                foreach (var (socialClass, due) in dues)
                {
                    socialClass.Goods.Add(productId, inKind * due / totalDue);
                    socialClass.ReceiveIncomeInKind(inKind * price * due / totalDue);
                }

                produced -= inKind;
                var settled = price > 0 ? inKind * price / totalDue : 1;
                dues = dues.Select(d => (d.Class, d.Due * (1 - settled))).ToList();
                totalDue *= 1 - settled;
            }

            // Les bâtiments d'une préfecture sont payés par le trésor de l'État, pas par la caisse de leur chef-lieu.
            var managedByPrefecture = Extension.ManagerOf(workplace, structures) is { } managerId
                && structures.GetValueOrDefault(managerId)?.IsPrefecture == true;
            var communeFund = managedByPrefecture ? null : communeFundOf?.Invoke(communeId);
            var paidInMoney = PayInMoney(workplace, dues, totalDue, classesById, treasury, communeFund, companyFundOf);
            wageLedger?.Record(workplace.Id, initialDue, initialDue - totalDue + paidInMoney);

            if (produced > 0 && workplace.Production is { } delivered)
            {
                var sellerId = workplace.Ownership == Ownership.Public ? Market.StateSellerId(communeId)
                    : companyFundOf is not null ? Market.CompanySellerId(workplace.Id)
                    : workplace.OwnerClassId ?? "";
                market.Offer(delivered.ProductId, produced, sellerId);
            }
        }
    }

    /// <summary>
    /// L'étape de chaque bâtiment dans la chaîne : 0 s'il n'utilise aucune matière ; sinon 1 de plus que l'étape la plus
    /// haute des bâtiments qui font ses matières (0 pour une matière que personne ne fait). Une boucle dans la chaîne
    /// s'arrête après autant de tours qu'il y a de bâtiments.
    /// </summary>
    public static Dictionary<string, int> ChainStages(IReadOnlyCollection<Structure> workplaces)
    {
        var stages = workplaces.ToDictionary(w => w.Id, _ => 0);
        var producers = workplaces.Where(w => w.Production is not null)
            .GroupBy(w => w.Production!.Value.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(w => w.Id).ToList());

        for (var round = 0; round < workplaces.Count; round++)
        {
            var changed = false;
            foreach (var workplace in workplaces)
            {
                if (workplace.Production is not { } recipe || recipe.Materials.Count == 0)
                    continue;
                var stage = 1 + recipe.Materials.Max(m => producers.TryGetValue(m.ProductId, out var ids) ? ids.Max(id => stages[id]) : 0);
                if (stage <= stages[workplace.Id])
                    continue;
                stages[workplace.Id] = stage;
                changed = true;
            }

            if (!changed)
                break;
        }

        return stages;
    }

    /// <summary>
    /// Pour chaque commune et chaque matière qui manque dans l'étape : la part du besoin de chaque bâtiment qu'il peut
    /// prendre au stockage (ce qu'il y a, divisé par ce que veulent les bâtiments de l'étape, hors de leurs réserves).
    /// Une matière qui ne manque pas n'y est pas.
    /// </summary>
    private static Dictionary<(string CommuneId, string ProductId), double> ShortageRatios(
        IEnumerable<(Structure Workplace, string CommuneId, double Planned, Inventory? Reserve)> plans,
        Func<string, Market> marketOf)
    {
        var wanted = new Dictionary<(string, string), double>();
        foreach (var (workplace, communeId, planned, reserve) in plans)
        {
            if (workplace.Production is not { } recipe || planned <= 0)
                continue;
            foreach (var material in recipe.Materials)
            {
                var need = Math.Max(0, planned * material.Quantity - (reserve?.QuantityOf(material.ProductId) ?? 0));
                wanted[(communeId, material.ProductId)] = wanted.GetValueOrDefault((communeId, material.ProductId)) + need;
            }
        }

        var ratios = new Dictionary<(string, string), double>();
        foreach (var ((communeId, productId), total) in wanted)
        {
            var available = marketOf(communeId).QuantityOf(productId);
            if (total > 0 && available < total)
                ratios[(communeId, productId)] = available / total;
        }

        return ratios;
    }

    /// <summary>Verse les salaires en argent ; renvoie ce qui a été versé.</summary>
    private static double PayInMoney(
        Structure workplace,
        List<(SocialClass Class, double Due)> dues,
        double totalDue,
        IReadOnlyDictionary<string, SocialClass> classesById,
        Treasury treasury,
        Treasury? communeFund,
        Func<Structure, Treasury>? companyFundOf)
    {
        if (totalDue <= 0)
            return 0;

        double paid;
        if (workplace.Ownership == Ownership.Public)
        {
            // La caisse de la commune paie d'abord ; le trésor national prend le relais quand elle est vide.
            var fromFund = Math.Min(totalDue, communeFund?.Balance ?? 0);
            communeFund?.TrySpend(fromFund);
            var fromTreasury = Math.Min(totalDue - fromFund, treasury.Balance);
            treasury.TrySpend(fromTreasury);
            paid = fromFund + fromTreasury;
        }
        else if (companyFundOf?.Invoke(workplace) is { } companyFund)
        {
            paid = Math.Min(totalDue, companyFund.Balance);
            companyFund.TrySpend(paid);
        }
        else if (workplace.OwnerClassId is { } ownerId && classesById.TryGetValue(ownerId, out var owner))
        {
            paid = Math.Min(totalDue, Math.Max(0, owner.Wealth));
            owner.Wealth -= paid;
        }
        else
        {
            return 0;
        }

        foreach (var (socialClass, due) in dues)
            socialClass.ReceiveIncome(paid * due / totalDue);

        return paid;
    }
}
