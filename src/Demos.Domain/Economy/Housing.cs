using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Le logement est une capacité : les bâtiments de logements offrent des places dans leur commune, louées
/// chaque jour (voir <see cref="PlaceAllocation"/>). La part des membres logés couvre le besoin « se loger ».
/// </summary>
public static class Housing
{
    public static void RunDaily(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury,
        Func<Structure, Treasury>? companyFundOf = null)
    {
        var housed = PlaceAllocation.Allocate(structures, classes, treasury, s => s.HousingPlaces, s => s.DailyRent, companyFundOf: companyFundOf);

        foreach (var (socialClass, served) in housed)
        {
            socialClass.HousingCoverage = socialClass.Population > 0 ? Math.Min(1, served.Members / socialClass.Population) : 1;
            socialClass.HousingPlaceShortage = socialClass.Population > 0 ? Math.Min(1, served.LackingPlaces / socialClass.Population) : 0;
        }
    }
}
