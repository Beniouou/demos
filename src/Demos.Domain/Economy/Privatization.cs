using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>Résultat d'une tentative de vente d'un bâtiment public.</summary>
public enum SaleOutcome
{
    Sold,
    NotPublic,
    BuyerNotInCommune,
    BuyerTooPoor,
    PriceAboveProfit,
    NotEnoughLegitimacy,
}

/// <summary>
/// Vente d'un bâtiment public à une classe de sa commune. Le joueur choisit l'acheteur et fixe le prix ; la classe
/// n'achète que si elle a l'argent et si le prix ne dépasse pas un an du bénéfice qu'elle peut attendre du bâtiment.
/// Une fois vendu, le bâtiment paie lui-même ses salaires et vend sa production au profit de son propriétaire :
/// propriété privée pour des professions intermédiaires ou des cadres, coopérative pour des ouvriers.
/// </summary>
public static class Privatization
{
    /// <summary>Proposition : une classe paie au plus un an du bénéfice attendu.</summary>
    public const int ProfitDaysAccepted = 365;

    /// <summary>
    /// Bénéfice attendu par jour avec le personnel actuel : valeur de la production moins celle de ses matières, loyers
    /// et frais de scolarité possibles, moins les salaires.
    /// </summary>
    public static double DailyProfitOf(
        Structure building,
        double productionWorkers,
        IReadOnlyList<SocialClass> classes,
        Func<string, double> priceOf)
    {
        var output = building.Production is { } p ? p.QuantityPerWorker * productionWorkers : 0;
        var production = building.Production is { } recipe
            ? output * (priceOf(recipe.ProductId) - recipe.Materials.Sum(m => m.Quantity * priceOf(m.ProductId)))
            : 0;
        var places = building.HousingPlaces * building.DailyRent + building.SchoolPlaces * building.DailyTuition;
        var wages = classes.Sum(c => c.MembersWorkingIn(building.Id) * Staffing.WageOf(building, c.Rank));
        return production + places - wages;
    }

    /// <summary>Prix le plus haut qu'une classe accepte de payer : un an de bénéfice attendu (rien si le bâtiment perd de l'argent).</summary>
    public static double MaximumPriceOf(double dailyProfit) => Math.Max(0, dailyProfit * ProfitDaysAccepted);

    /// <summary>Les ouvriers rachètent en coopérative, les autres rangs en propriété privée.</summary>
    public static Ownership OwnershipFor(SocialClass buyer) =>
        buyer.Rank == Rank.Workers ? Ownership.Cooperative : Ownership.Private;

    /// <summary>Vérifie la vente, sans rien changer.</summary>
    public static SaleOutcome Check(Structure building, string? communeId, SocialClass buyer, double price, double dailyProfit)
    {
        if (building.Ownership != Ownership.Public)
            return SaleOutcome.NotPublic;
        if (communeId is null || buyer.CommuneId != communeId)
            return SaleOutcome.BuyerNotInCommune;
        if (buyer.Wealth < price)
            return SaleOutcome.BuyerTooPoor;
        if (dailyProfit < 0 || price > MaximumPriceOf(dailyProfit))
            return SaleOutcome.PriceAboveProfit;
        return SaleOutcome.Sold;
    }
}
