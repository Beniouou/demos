using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Caisse des entreprises privées et coopératives. Chaque bâtiment privé ou coopératif a sa propre caisse, distincte
/// de la richesse de sa classe propriétaire : ses ventes, loyers et frais de scolarité y arrivent, ses salaires en
/// sortent, avec ses matières et son énergie. Chaque jour, après avoir payé toutes ses charges, elle met de côté une
/// part de son bénéfice jusqu'à remplir sa réserve (des jours de salaires et d'énergie à plein régime) et verse le reste
/// à sa classe propriétaire, comme un revenu ; une fois la réserve pleine, elle verse tout ce qui la dépasse.
/// </summary>
public static class Companies
{
    /// <summary>Une entreprise garde de quoi payer ce nombre de jours (réglage d'équilibrage).</summary>
    public static int ReserveDays => Balance.Current.CompanyReserveDays;

    public static bool HasCompanyFund(Structure building) =>
        building.Kind == StructureKind.Building && building.Ownership != Ownership.Public;

    /// <summary>
    /// Réserve que garde la caisse d'une entreprise : des jours de salaires et, si l'on connaît les prix, d'énergie (un
    /// logement privé, sans salaires, doit pouvoir se chauffer). Par défaut, la durée de la réserve des entreprises.
    /// </summary>
    public static double ReserveOf(Structure building, Func<string, double>? priceOf = null, int? days = null)
    {
        var wages = building.Jobs * Staffing.WageBillPerJob(building);
        var energy = priceOf is null
            ? 0
            : Energy.UnitsOf(building) * (Energy.HeatingPerUnit * Energy.HeatingFactor(Shared.Season.Winter)
                * priceOf(building.HeatingProductId ?? Energy.DefaultHeating) + Energy.WaterPerUnit * priceOf(Energy.Water));
        return (days ?? ReserveDays) * (wages + energy);
    }

    /// <summary>
    /// Verse aux propriétaires ce qui dépasse la réserve de chaque entreprise ; tant que la réserve n'est pas pleine, la
    /// part du bénéfice du jour qui n'est pas mise de côté. <paramref name="balanceAtDayStart"/> donne la caisse au début
    /// de la journée, pour connaître ce bénéfice ; sans elle, l'entreprise garde tout jusqu'à remplir sa réserve.
    /// </summary>
    public static void PayDividends(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, SocialClass> classesById,
        Func<Structure, Treasury> companyFundOf,
        Func<string, double>? priceOf = null,
        Func<Structure, double>? balanceAtDayStart = null)
    {
        var savedShare = Math.Clamp(Balance.Current.ReserveSavingsShare, 0, 1);
        foreach (var building in structures.Values.Where(HasCompanyFund))
        {
            if (building.OwnerClassId is not { } ownerId || !classesById.TryGetValue(ownerId, out var owner))
                continue;

            var fund = companyFundOf(building);
            var dividend = fund.Balance - ReserveOf(building, priceOf);

            // Bénéfice du jour, charges payées : la part qui n'est pas mise de côté va aux propriétaires.
            if (balanceAtDayStart is not null && fund.Balance - balanceAtDayStart(building) is var profit && profit > 0)
                dividend = Math.Max(dividend, profit * (1 - savedShare));

            dividend = Math.Min(dividend, fund.Balance);
            if (dividend > 0 && fund.TrySpend(dividend))
                owner.ReceiveIncome(dividend);
        }
    }
}
