using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Règle commune aux services qui sont des capacités (logement, école) : des bâtiments offrent des places
/// dans leur commune, à certaines classes. Quand il en manque, chaque classe admise est servie au prorata de ses
/// habitants. Chaque place occupée coûte des frais quotidiens au propriétaire (le trésor pour un bâtiment public,
/// la classe propriétaire sinon) ; une classe qui ne peut pas payer n'est servie qu'à hauteur de ce qu'elle paie.
/// </summary>
public static class PlaceAllocation
{
    /// <summary>
    /// Membres d'une classe qui ont une place, ce que ces places leur rapportent (par exemple des points),
    /// et membres restés sans place parce qu'il n'y en avait pas assez (et non faute d'argent).
    /// </summary>
    public readonly record struct Served(double Members, double Value, double LackingPlaces = 0);

    public static Dictionary<SocialClass, Served> Allocate(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury,
        Func<Structure, int> placesOf,
        Func<Structure, double> dailyFeeOf,
        Func<SocialClass, bool>? admits = null,
        Func<Structure, double>? valuePerPlaceOf = null)
    {
        admits ??= _ => true;
        valuePerPlaceOf ??= _ => 1;
        var served = classes.ToDictionary(c => c, _ => new Served(0, 0));
        var admitted = classes.Where(admits).ToList();

        foreach (var commune in Communes.All(structures))
        {
            var providers = structures.Values
                .Where(s => placesOf(s) > 0 && Communes.CommuneOf(s, structures) == commune.Id)
                .ToList();
            double places = providers.Sum(placesOf);
            var residents = admitted.Sum(c => c.MembersLivingIn(commune.Id));
            if (residents <= 0)
                continue;

            var fillRate = places > 0 ? Math.Min(1, places / residents) : 0;
            var totalFees = providers.Sum(s => placesOf(s) * dailyFeeOf(s));
            var averageFee = places > 0 ? totalFees / places : 0;
            var averageValue = places > 0 ? providers.Sum(s => placesOf(s) * valuePerPlaceOf(s)) / places : 0;

            foreach (var socialClass in admitted)
            {
                var living = socialClass.MembersLivingIn(commune.Id);
                var wanted = living * fillRate;
                var fee = wanted * averageFee;
                var paidShare = fee > 0 ? Math.Min(1, Math.Max(0, socialClass.Wealth) / fee) : 1;
                var members = wanted * paidShare;
                var previous = served[socialClass];
                served[socialClass] = new Served(
                    previous.Members + members,
                    previous.Value + members * averageValue,
                    previous.LackingPlaces + living - wanted);

                if (fee <= 0)
                    continue;

                var paid = fee * paidShare;
                socialClass.Wealth -= paid;
                foreach (var provider in providers)
                    PayOwner(provider, paid * placesOf(provider) * dailyFeeOf(provider) / totalFees, classes, treasury);
            }
        }

        return served;
    }

    private static void PayOwner(Structure provider, double amount, IReadOnlyList<SocialClass> classes, Treasury treasury)
    {
        if (amount <= 0)
            return;

        if (provider.Ownership == Ownership.Public)
            treasury.Deposit(amount);
        else if (classes.FirstOrDefault(c => c.Id == provider.OwnerClassId) is { } owner)
            owner.ReceiveIncome(amount);
    }
}
