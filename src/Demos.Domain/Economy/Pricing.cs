using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Prix de départ des produits : un bâtiment de référence qui tourne à plein régime et paie toutes ses charges doit
/// dégager 30 % de bénéfice. Le bâtiment de référence d'un produit est le type du catalogue de plus bas niveau qui le
/// fabrique, avec sa première recette pour ce produit. Les charges sont les salaires de tous ses emplois (production,
/// encadrement, direction) et ses matières, à leur propre prix : le prix se calcule donc en remontant la chaîne de
/// production. Un produit sans bâtiment de référence garde son prix du catalogue. Le prix reste national et modifiable
/// à la main.
/// </summary>
public static class Pricing
{
    /// <summary>Proposition : 30 % de bénéfice à plein régime.</summary>
    public const double Margin = 0.3;

    /// <summary>Salaires d'un emploi de production, avec la part d'encadrement et de direction qui l'accompagne.</summary>
    public static double WageCostPerProductionJob(double dailyWage) =>
        dailyWage * (Staffing.WageMultiplier(Rank.Workers)
            + Staffing.IntermediatesPerProductionJob * Staffing.WageMultiplier(Rank.Intermediate)
            + Staffing.ExecutivesPerProductionJob * Staffing.WageMultiplier(Rank.Executives));

    /// <summary>
    /// Prix d'une unité produite par ce type avec sa première recette, ou null s'il ne produit rien. Les matières sont
    /// comptées au prix donné par <paramref name="priceOf"/> (zéro par défaut).
    /// </summary>
    public static double? PriceFrom(BuildingType reference, Func<string, double>? priceOf = null) =>
        reference.DefaultProduction is { } recipe ? PriceFrom(reference, recipe, priceOf) : null;

    public static double? PriceFrom(BuildingType reference, Production recipe, Func<string, double>? priceOf = null)
    {
        if (recipe.QuantityPerWorker <= 0)
            return null;

        var materials = recipe.Materials.Sum(m => m.Quantity * (priceOf?.Invoke(m.ProductId) ?? 0));
        return (1 + Margin) * (WageCostPerProductionJob(reference.DailyWage) / recipe.QuantityPerWorker + materials);
    }

    /// <summary>Le type de plus bas niveau qui fabrique ce produit, et sa première recette pour ce produit.</summary>
    public static (BuildingType Type, Production Recipe)? ReferenceFor(string productId, IEnumerable<BuildingType> catalogue) =>
        catalogue
            .Where(t => t.Jobs > 0)
            .OrderBy(t => t.Level)
            .SelectMany(t => t.AvailableRecipes.Where(r => r.ProductId == productId).Select(r => (t, r)))
            .Select(x => ((BuildingType Type, Production Recipe)?)x)
            .FirstOrDefault();

    /// <summary>Les produits avec leur prix de départ calculé d'après leur bâtiment de référence et leurs matières.</summary>
    public static IReadOnlyList<ProductDefinition> WithReferencePrices(IEnumerable<ProductDefinition> products, IReadOnlyList<BuildingType> catalogue)
    {
        var list = products.ToList();
        var prices = new Dictionary<string, double>();
        var inProgress = new HashSet<string>();

        double PriceOf(string productId)
        {
            if (prices.TryGetValue(productId, out var known))
                return known;

            var catalogPrice = list.FirstOrDefault(p => p.Id == productId)?.BasePrice ?? 0;

            // Une chaîne qui boucle sur elle-même garde le prix du catalogue.
            if (!inProgress.Add(productId))
                return catalogPrice;

            var price = ReferenceFor(productId, catalogue) is { } reference
                ? PriceFrom(reference.Type, reference.Recipe, PriceOf) ?? catalogPrice
                : catalogPrice;
            inProgress.Remove(productId);
            return prices[productId] = price;
        }

        return list.Select(p => p with { BasePrice = PriceOf(p.Id) }).ToList();
    }
}
