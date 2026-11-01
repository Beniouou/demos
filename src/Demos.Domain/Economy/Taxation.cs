using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class Taxation
{
    /// <summary>Prélèvement quotidien : chaque classe verse richesse × taux du jour au trésor de l'État.</summary>
    public static void CollectDaily(IEnumerable<SocialClass> classes, double dailyRate, Treasury treasury)
    {
        foreach (var socialClass in classes)
        {
            var tax = socialClass.Wealth * dailyRate;
            socialClass.PayTax(tax);
            treasury.Deposit(tax);
        }
    }
}
