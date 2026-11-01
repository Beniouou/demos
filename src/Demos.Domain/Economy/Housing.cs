using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Le logement est une capacité : les bâtiments de logements offrent des places dans leur commune.
/// Quand il manque des places, chaque classe est logée au prorata de ses habitants. L'occupant paie un loyer
/// chaque jour au propriétaire (le trésor pour un logement public, la classe propriétaire sinon) ;
/// une classe qui ne peut pas payer n'est logée qu'à hauteur de ce qu'elle paie.
/// </summary>
public static class Housing
{
    public static void RunDaily(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury)
    {
        var housed = classes.ToDictionary(c => c, _ => 0.0);

        foreach (var commune in Communes.All(structures))
        {
            var lodgings = structures.Values
                .Where(s => s.HousingPlaces > 0 && Communes.CommuneOf(s, structures) == commune.Id)
                .ToList();
            double places = lodgings.Sum(s => s.HousingPlaces);
            var residents = classes.Sum(c => c.MembersLivingIn(commune.Id));
            if (places <= 0 || residents <= 0)
                continue;

            var fillRate = Math.Min(1, places / residents);
            var averageRent = lodgings.Sum(s => s.HousingPlaces * s.DailyRent) / places;

            foreach (var socialClass in classes)
            {
                var wanted = socialClass.MembersLivingIn(commune.Id) * fillRate;
                var rent = wanted * averageRent;
                var paidShare = rent > 0 ? Math.Min(1, Math.Max(0, socialClass.Wealth) / rent) : 1;
                housed[socialClass] += wanted * paidShare;

                if (rent <= 0)
                    continue;

                socialClass.Wealth -= rent * paidShare;
                foreach (var lodging in lodgings)
                    PayOwner(lodging, rent * paidShare * lodging.HousingPlaces * lodging.DailyRent / (places * averageRent), classes, treasury);
            }
        }

        foreach (var (socialClass, members) in housed)
            socialClass.HousingCoverage = socialClass.Population > 0 ? Math.Min(1, members / socialClass.Population) : 1;
    }

    private static void PayOwner(Structure lodging, double amount, IReadOnlyList<SocialClass> classes, Treasury treasury)
    {
        if (amount <= 0)
            return;

        if (lodging.Ownership == Ownership.Public)
            treasury.Deposit(amount);
        else if (classes.FirstOrDefault(c => c.Id == lodging.OwnerClassId) is { } owner)
            owner.Wealth += amount;
    }
}
