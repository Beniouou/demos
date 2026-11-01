using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Caisse des entreprises privées et coopératives. Chaque bâtiment privé ou coopératif a sa propre caisse, distincte
/// de la richesse de sa classe propriétaire : ses ventes, loyers et frais de scolarité y arrivent, ses salaires en
/// sortent. Chaque jour, elle garde une réserve de 30 jours de salaires à plein régime (proposition) et verse le reste
/// à sa classe propriétaire, comme un revenu.
/// </summary>
public static class Companies
{
    /// <summary>Proposition : une entreprise garde de quoi payer 30 jours de salaires à plein régime.</summary>
    public const int ReserveDays = 30;

    public static bool HasCompanyFund(Structure building) =>
        building.Kind == StructureKind.Building && building.Ownership != Ownership.Public;

    /// <summary>Réserve que garde la caisse d'une entreprise.</summary>
    public static double ReserveOf(Structure building) =>
        ReserveDays * building.Jobs * Pricing.WageCostPerProductionJob(building.DailyWage);

    /// <summary>Verse aux propriétaires ce qui dépasse la réserve de chaque entreprise.</summary>
    public static void PayDividends(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, SocialClass> classesById,
        Func<Structure, Treasury> companyFundOf)
    {
        foreach (var building in structures.Values.Where(HasCompanyFund))
        {
            if (building.OwnerClassId is not { } ownerId || !classesById.TryGetValue(ownerId, out var owner))
                continue;

            var fund = companyFundOf(building);
            var dividend = fund.Balance - ReserveOf(building);
            if (dividend > 0 && fund.TrySpend(dividend))
                owner.ReceiveIncome(dividend);
        }
    }
}
