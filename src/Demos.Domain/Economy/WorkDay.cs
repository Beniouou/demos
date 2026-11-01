using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class WorkDay
{
    /// <summary>
    /// Pour chaque lieu de travail : il produit selon ses travailleurs de production, paie ses emplois, puis livre
    /// le reste (au stock de l'État s'il est public, sinon en vente sur le marché pour sa classe propriétaire).
    /// Le travail est payé en salaire, sauf si une loi qui couvre le bâtiment impose le paiement en nature :
    /// les travailleurs reçoivent alors des produits du bâtiment, au prix du marché.
    /// Le salaire en argent vient du trésor (public) ou du propriétaire, et est versé en partie si l'argent manque.
    /// </summary>
    public static void Run(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> productionWorkers,
        IReadOnlyList<SocialClass> classes,
        IReadOnlyList<Law> laws,
        Treasury treasury,
        Inventory stateStock,
        Market market)
    {
        foreach (var workplace in structures.Values.Where(s => s.Jobs > 0))
        {
            var dues = classes
                .Select(c => (Class: c, Due: c.MembersWorkingIn(workplace.Id) * workplace.DailyWage * Staffing.WageMultiplier(c.Rank)))
                .Where(d => d.Due > 0)
                .ToList();
            var totalDue = dues.Sum(d => d.Due);

            var produced = workplace.Production is { } production
                ? production.QuantityPerWorker * productionWorkers.GetValueOrDefault(workplace.Id)
                : 0;

            if (totalDue > 0 && produced > 0 && IsPaidInKind(workplace, structures, laws))
            {
                var productId = workplace.Production!.Value.ProductId;
                var price = market.PriceOf(productId);
                var inKind = price > 0 ? Math.Min(produced, totalDue / price) : produced;

                foreach (var (socialClass, due) in dues)
                    socialClass.Goods.Add(productId, inKind * due / totalDue);

                produced -= inKind;
                var settled = price > 0 ? inKind * price / totalDue : 1;
                dues = dues.Select(d => (d.Class, d.Due * (1 - settled))).ToList();
                totalDue *= 1 - settled;
            }

            PayInMoney(workplace, dues, totalDue, classes, treasury);

            if (produced > 0 && workplace.Production is { } delivered)
            {
                if (workplace.Ownership == Ownership.Public)
                    stateStock.Add(delivered.ProductId, produced);
                else
                    market.Offer(delivered.ProductId, produced, workplace.OwnerClassId ?? "");
            }
        }
    }

    private static bool IsPaidInKind(Structure workplace, IReadOnlyDictionary<string, Structure> structures, IReadOnlyList<Law> laws) =>
        laws.Any(law => law.Has(LawRule.PaymentInKind) && StructureTree.IsWithin(workplace, law.StructureId, structures));

    private static void PayInMoney(
        Structure workplace,
        List<(SocialClass Class, double Due)> dues,
        double totalDue,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury)
    {
        if (totalDue <= 0)
            return;

        double paid;
        if (workplace.Ownership == Ownership.Public)
        {
            paid = Math.Min(totalDue, treasury.Balance);
            treasury.TrySpend(paid);
        }
        else if (classes.FirstOrDefault(c => c.Id == workplace.OwnerClassId) is { } owner)
        {
            paid = Math.Min(totalDue, Math.Max(0, owner.Wealth));
            owner.Wealth -= paid;
        }
        else
        {
            return;
        }

        foreach (var (socialClass, due) in dues)
            socialClass.Wealth += paid * due / totalDue;
    }
}
