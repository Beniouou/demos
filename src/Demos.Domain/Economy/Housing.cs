using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Le logement est une capacité : les bâtiments de logements offrent des places dans leur commune, louées
/// chaque jour (voir <see cref="PlaceAllocation"/>). La part des membres logés couvre le besoin « se loger ». Les rangs
/// supérieurs occupent des logements plus grands : plus de places par personne, donc plus de loyer.
/// </summary>
public static class Housing
{
    /// <summary>
    /// Places de logement occupées par personne (propositions : 1 pour un ouvrier, 2 pour une profession intermédiaire,
    /// 4 pour un cadre).
    /// </summary>
    public static double PlacesPerPerson(Rank rank) => rank switch
    {
        Rank.Intermediate => 2,
        Rank.Executives => 4,
        _ => 1,
    };

    public static void RunDaily(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury,
        Func<Structure, Treasury>? companyFundOf = null)
    {
        var housed = PlaceAllocation.Allocate(
            structures, classes, treasury, s => s.HousingPlaces, s => s.DailyRent,
            companyFundOf: companyFundOf, placesPerMemberOf: c => PlacesPerPerson(c.Rank));

        foreach (var (socialClass, served) in housed)
        {
            socialClass.HousingCoverage = socialClass.Population > 0 ? Math.Min(1, served.Members / socialClass.Population) : 1;
            socialClass.HousingPlaceShortage = socialClass.Population > 0 ? Math.Min(1, served.LackingPlaces / socialClass.Population) : 0;
        }
    }
}
