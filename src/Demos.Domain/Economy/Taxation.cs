using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Deux impôts prélevés chaque jour sur la richesse de chaque groupe local : l'impôt d'État va au trésor, l'impôt
/// communal à la caisse de la commune où vit le groupe. Chacun a son taux, réglé indépendamment (l'impôt communal
/// commune par commune). Les lois peuvent les augmenter ou les baisser pour un rang ou pour tous
/// (1,5 = +50 %).
/// </summary>
public static class Taxation
{
    /// <summary>
    /// Part des ventes d'une entreprise qui est sa valeur ajoutée, celle sur laquelle elle paie la TVA : le prix de son
    /// produit moins les matières de sa recette, au prix du marché. La TVA payée par ses fournisseurs est déjà dans le prix de
    /// ces matières.
    /// </summary>
    public static double ValueAddedShare(Structure building, Func<string, double> priceOf)
    {
        if (building.Production is not { } recipe || priceOf(recipe.ProductId) is var price && price <= 0)
            return 1;

        var materials = recipe.Materials.Sum(m => m.Quantity * priceOf(m.ProductId));
        return Math.Clamp(1 - materials / price, 0, 1);
    }

    public static void CollectDaily(
        IEnumerable<SocialClass> classes,
        double stateRate,
        Func<string, double> communeRateOf,
        Treasury treasury,
        Func<string, Treasury> communeFundOf,
        Func<SocialClass, double>? taxFactorOf = null)
    {
        foreach (var socialClass in classes)
        {
            var communeId = socialClass.CommuneId;
            var wealth = socialClass.Wealth * (taxFactorOf?.Invoke(socialClass) ?? 1);

            var stateTax = wealth * stateRate;
            socialClass.PayTax(stateTax);
            treasury.Deposit(stateTax);

            if (communeId is null)
                continue;

            var communeTax = wealth * communeRateOf(communeId);
            socialClass.PayTax(communeTax);
            communeFundOf(communeId).Deposit(communeTax);
        }
    }
}
