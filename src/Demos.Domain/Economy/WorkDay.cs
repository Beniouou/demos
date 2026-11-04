using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class WorkDay
{
    /// <summary>
    /// Pour chaque lieu de travail : il prend ses matières (voir <see cref="Supply"/>), produit selon ses travailleurs
    /// de production et ses matières, paie ses emplois, puis livre le reste au stockage de sa commune, en vente au nom
    /// de l'État s'il est public, de l'entreprise sinon.
    /// Le travail est payé en salaire, sauf si les lois qui couvrent le bâtiment autorisent le paiement en nature :
    /// les travailleurs reçoivent alors des produits du bâtiment, au prix du marché.
    /// Le salaire en argent vient de la caisse de la commune puis du trésor (public), ou de la caisse de l'entreprise
    /// (privé, coopératif), et est versé en partie si l'argent manque.
    /// </summary>
    /// <returns>Ce que chaque lieu de travail a produit dans la journée.</returns>
    public static Dictionary<string, double> Run(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> productionWorkers,
        IReadOnlyList<SocialClass> classes,
        LawBook? laws,
        Treasury treasury,
        Func<string, Market> marketOf,
        Func<string, Treasury>? communeFundOf = null,
        WageLedger? wageLedger = null,
        Func<Structure, Treasury>? companyFundOf = null,
        Func<Structure, double>? energyShareOf = null)
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
        foreach (var workplace in structures.Values.Where(s => s.Jobs > 0))
        {
            // Chaque bâtiment livre au stock et au marché de sa propre commune.
            var communeId = Communes.CommuneOf(workplace, structures) ?? "";
            var market = marketOf(communeId);

            var dues = (staffOf.TryGetValue(workplace.Id, out var workers) ? workers : [])
                .Select(c => (Class: c, Due: c.MembersWorkingIn(workplace.Id) * Staffing.WageOf(workplace, c.Rank)))
                .Where(d => d.Due > 0)
                .ToList();
            var totalDue = dues.Sum(d => d.Due);
            var initialDue = totalDue;

            var produced = workplace.Production is { } production
                ? production.QuantityPerWorker * productionWorkers.GetValueOrDefault(workplace.Id)
                : 0;

            // Sans assez d'énergie la veille (chauffage, eau), le bâtiment produit moins.
            produced *= energyShareOf?.Invoke(workplace) ?? 1;

            // Les matières viennent du stockage de la commune ; sans elles, le bâtiment produit moins.
            if (workplace.Production is { } recipe)
                produced *= Supply.Acquire(workplace, recipe, produced, market, communeId, treasury, communeFundOf?.Invoke(communeId), companyFundOf, classesById);

            output[workplace.Id] = produced;

            if (totalDue > 0 && produced > 0 && IsPaidInKind(workplace, laws))
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

            var paidInMoney = PayInMoney(workplace, dues, totalDue, classesById, treasury, communeFundOf?.Invoke(communeId), companyFundOf);
            wageLedger?.Record(workplace.Id, initialDue, initialDue - totalDue + paidInMoney);

            if (produced > 0 && workplace.Production is { } delivered)
            {
                var sellerId = workplace.Ownership == Ownership.Public ? Market.StateSellerId(communeId)
                    : companyFundOf is not null ? Market.CompanySellerId(workplace.Id)
                    : workplace.OwnerClassId ?? "";
                market.Offer(delivered.ProductId, produced, sellerId);
            }
        }

        return output;
    }

    private static bool IsPaidInKind(Structure workplace, LawBook? laws) =>
        laws is not null && laws.ValueFor(Bricks.PaymentInKind, workplace) >= 0.5;

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
