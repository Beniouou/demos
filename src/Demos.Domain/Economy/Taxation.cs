using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>Qui touche un impôt : l'État (son trésor) ou la commune (sa caisse).</summary>
public enum TaxLevel
{
    State,
    Commune,
}

/// <summary>Ce qu'une loi fait à un impôt : le plafonner ou y ajouter un supplément.</summary>
public enum TaxAdjustmentKind
{
    /// <summary>Le taux ne peut pas dépasser la valeur donnée.</summary>
    Maximum,

    /// <summary>La valeur donnée s'ajoute au taux.</summary>
    Surcharge,
}

/// <summary>Effet d'une loi sur un impôt, en part de la richesse par jour (0,001 = 0,1 %).</summary>
public readonly record struct TaxAdjustment(TaxLevel Level, TaxAdjustmentKind Kind, double Rate);

/// <summary>
/// Deux impôts prélevés chaque jour sur la richesse de chaque groupe local : l'impôt d'État va au trésor, l'impôt
/// communal à la caisse de la commune où vit le groupe. Chacun a son taux, réglé indépendamment (l'impôt communal
/// commune par commune). Les lois qui couvrent la commune du groupe (créées dans la commune ou dans l'État) peuvent
/// ajouter un supplément ou fixer un plafond : les suppléments s'ajoutent d'abord, puis le plus bas des plafonds
/// s'applique.
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
        IReadOnlyList<Law> laws,
        IReadOnlyDictionary<string, Structure> structures)
    {
        foreach (var socialClass in classes)
        {
            var communeId = socialClass.CommuneId;
            var adjustments = AdjustmentsFor(communeId, laws, structures);
            var wealth = socialClass.Wealth;

            var stateTax = wealth * RateOf(TaxLevel.State, stateRate, adjustments);
            socialClass.PayTax(stateTax);
            treasury.Deposit(stateTax);

            if (communeId is null)
                continue;

            var communeTax = wealth * RateOf(TaxLevel.Commune, communeRateOf(communeId), adjustments);
            socialClass.PayTax(communeTax);
            communeFundOf(communeId).Deposit(communeTax);
        }
    }

    /// <summary>Taux d'un impôt une fois les lois appliquées : suppléments ajoutés, puis plafond le plus bas.</summary>
    public static double RateOf(TaxLevel level, double baseRate, IEnumerable<TaxAdjustment> adjustments)
    {
        var own = adjustments.Where(a => a.Level == level).ToList();
        var rate = baseRate + own.Where(a => a.Kind == TaxAdjustmentKind.Surcharge).Sum(a => a.Rate);
        foreach (var maximum in own.Where(a => a.Kind == TaxAdjustmentKind.Maximum))
            rate = Math.Min(rate, maximum.Rate);
        return Math.Max(0, rate);
    }

    /// <summary>Effets sur les impôts des lois qui couvrent une commune.</summary>
    public static List<TaxAdjustment> AdjustmentsFor(string? communeId, IReadOnlyList<Law> laws, IReadOnlyDictionary<string, Structure> structures)
    {
        if (communeId is null || !structures.TryGetValue(communeId, out var commune))
            return laws.Where(l => structures.TryGetValue(l.StructureId, out var s) && s.IsNational).SelectMany(l => l.TaxAdjustments ?? []).ToList();

        return laws
            .Where(law => StructureTree.IsWithin(commune, law.StructureId, structures))
            .SelectMany(law => law.TaxAdjustments ?? [])
            .ToList();
    }
}
