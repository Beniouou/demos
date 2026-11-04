using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Le logement est une capacité : les bâtiments de logements offrent des places dans leur commune, louées
/// chaque jour (voir <see cref="PlaceAllocation"/>). La part des membres logés couvre le besoin « se loger ». Les rangs
/// supérieurs occupent des logements plus grands : plus de places par personne, donc plus de loyer. Un rang auquel les lois réservent des logements est logé
/// d'abord.
/// </summary>
public static class Housing
{
    /// <summary>Places de logement occupées par personne (réglages d'équilibrage : 1, 2 et 4 selon le rang).</summary>
    public static double PlacesPerPerson(Rank rank) => Balance.Current.HousingPlacesByRank.GetValueOrDefault(rank, 1);

    public static void RunDaily(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury,
        Func<Structure, Treasury>? companyFundOf = null,
        Func<Structure, double>? energyShareOf = null,
        Func<SocialClass, bool>? hasPriority = null)
    {
        // Un logement mal chauffé ou sans eau n'offre qu'une partie de ses places.
        var housed = PlaceAllocation.Allocate(
            structures, classes, treasury, s => s.HousingPlaces * (energyShareOf?.Invoke(s) ?? 1), s => s.RentDue,
            companyFundOf: companyFundOf, placesPerMemberOf: c => PlacesPerPerson(c.Rank), priorityOf: hasPriority);

        foreach (var (socialClass, served) in housed)
        {
            socialClass.HousingCoverage = socialClass.Population > 0 ? Math.Min(1, served.Members / socialClass.Population) : 1;
            socialClass.HousingPlaceShortage = socialClass.Population > 0 ? Math.Min(1, served.LackingPlaces / socialClass.Population) : 0;
        }
    }
}
