using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Prix de départ des produits : un bâtiment de référence qui tourne à plein régime et paie toutes ses charges doit
/// dégager 30 % de bénéfice. Le bâtiment de référence d'un produit est le type du catalogue de plus bas niveau qui le
/// fabrique. Les charges sont, pour l'instant, les salaires de tous ses emplois (production, encadrement, direction) ;
/// les matières s'ajouteront avec les chaînes de production. Un produit sans bâtiment de référence garde son prix du
/// catalogue. Le prix reste national et modifiable à la main.
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

    /// <summary>Prix d'une unité produite par ce type de bâtiment, ou null s'il ne produit rien.</summary>
    public static double? PriceFrom(BuildingType reference) =>
        reference.Production is { QuantityPerWorker: > 0 } production
            ? (1 + Margin) * WageCostPerProductionJob(reference.DailyWage) / production.QuantityPerWorker
            : null;

    /// <summary>Le type de plus bas niveau qui fabrique ce produit.</summary>
    public static BuildingType? ReferenceFor(string productId, IEnumerable<BuildingType> catalogue) =>
        catalogue
            .Where(t => t.Production?.ProductId == productId && t.Jobs > 0)
            .OrderBy(t => t.Level)
            .FirstOrDefault();

    /// <summary>Les produits avec leur prix de départ calculé d'après leur bâtiment de référence.</summary>
    public static IReadOnlyList<ProductDefinition> WithReferencePrices(IEnumerable<ProductDefinition> products, IReadOnlyList<BuildingType> catalogue) =>
        products
            .Select(p => ReferenceFor(p.Id, catalogue) is { } reference && PriceFrom(reference) is { } price
                ? p with { BasePrice = price }
                : p)
            .ToList();
}
