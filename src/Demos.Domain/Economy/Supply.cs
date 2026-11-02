using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Approvisionnement en matières d'un bâtiment de transformation, chaque jour, avant de produire. Il prend ses
/// matières dans le stockage de sa commune : d'abord ce qu'il y possède lui-même (pour l'État, ce que l'État y a
/// produit), puis il achète le reste aux autres vendeurs, avec l'argent de son propriétaire (caisse de la commune puis
/// trésor pour un bâtiment public, caisse de l'entreprise sinon). Quand une matière manque, ou l'argent pour
/// l'acheter, la production baisse en proportion.
/// </summary>
public static class Supply
{
    /// <summary>
    /// Prend les matières nécessaires à la production prévue et renvoie la part de cette production réellement
    /// possible (entre 0 et 1).
    /// </summary>
    public static double Acquire(
        Structure workplace,
        Production recipe,
        double plannedOutput,
        Market market,
        string communeId,
        Treasury treasury,
        Treasury? communeFund,
        Func<Structure, Treasury>? companyFundOf,
        IReadOnlyDictionary<string, SocialClass> classesById)
    {
        var materials = recipe.Materials;
        if (materials.Count == 0 || plannedOutput <= 0)
            return 1;

        var payer = PayerOf(workplace, communeId, treasury, communeFund, companyFundOf, classesById);
        var needs = materials.Select(m => (m.ProductId, Needed: plannedOutput * m.Quantity)).ToList();

        // Part possible avec ce qui est en stock…
        var share = 1.0;
        foreach (var (productId, needed) in needs)
        {
            if (needed > 0)
                share = Math.Min(share, (market.QuantityOf(productId, payer.SellerId) + market.QuantityOfOthers(productId, payer.SellerId)) / needed);
        }

        // … puis avec l'argent pour acheter ce que le bâtiment ne possède pas déjà.
        double CostOf(double part) => needs.Sum(n =>
            Math.Max(0, n.Needed * part - market.QuantityOf(n.ProductId, payer.SellerId)) * market.PriceOf(n.ProductId));

        var money = payer.Available();
        if (CostOf(share) > money)
        {
            double low = 0, high = share;
            for (var step = 0; step < 40; step++)
            {
                var middle = (low + high) / 2;
                if (CostOf(middle) <= money)
                    low = middle;
                else
                    high = middle;
            }

            share = low;
        }

        if (share <= 0)
            return 0;

        var cost = CostOf(share);
        payer.Spend(cost);
        foreach (var (productId, needed) in needs)
        {
            var taken = needed * share;
            var own = Math.Min(taken, market.QuantityOf(productId, payer.SellerId));
            market.TryWithdraw(productId, own, payer.SellerId);
            var bought = Math.Min(taken - own, market.QuantityOfOthers(productId, payer.SellerId));
            if (bought > 0)
                market.TryBuy(productId, bought, exceptSellerId: payer.SellerId);
        }

        return share;
    }

    private sealed record Payer(string SellerId, Func<double> Available, Action<double> Spend);

    private static Payer PayerOf(
        Structure workplace,
        string communeId,
        Treasury treasury,
        Treasury? communeFund,
        Func<Structure, Treasury>? companyFundOf,
        IReadOnlyDictionary<string, SocialClass> classesById)
    {
        if (workplace.Ownership == Ownership.Public)
        {
            return new Payer(
                Market.StateSellerId(communeId),
                () => (communeFund?.Balance ?? 0) + treasury.Balance,
                amount =>
                {
                    var fromFund = Math.Min(amount, communeFund?.Balance ?? 0);
                    communeFund?.TrySpend(fromFund);
                    treasury.TrySpend(Math.Min(amount - fromFund, treasury.Balance));
                });
        }

        if (companyFundOf?.Invoke(workplace) is { } fund)
            return new Payer(Market.CompanySellerId(workplace.Id), () => fund.Balance, amount => fund.TrySpend(Math.Min(amount, fund.Balance)));

        var owner = workplace.OwnerClassId is { } ownerId ? classesById.GetValueOrDefault(ownerId) : null;
        return new Payer(
            workplace.OwnerClassId ?? "",
            () => Math.Max(0, owner?.Wealth ?? 0),
            amount => { if (owner is not null) owner.Wealth -= amount; });
    }
}
