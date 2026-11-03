using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Prix de départ des produits : un bâtiment de référence qui tourne à plein régime et paie toutes ses charges doit
/// dégager 30 % de bénéfice. Le bâtiment de référence d'un produit est le type du catalogue de plus bas niveau qui le
/// fabrique, avec sa première recette pour ce produit. Les charges sont les salaires de tous ses emplois (production,
/// encadrement, direction), son énergie (chauffage et eau) et ses matières, à leur propre prix : le prix se calcule
/// donc en remontant la chaîne de production. Le prix inclut la TVA des entreprises.
/// Un produit sans bâtiment de référence garde son prix du catalogue. Le prix reste national et modifiable à la main.
/// </summary>
public static class Pricing
{
    /// <summary>Bénéfice à plein régime (réglage d'équilibrage : 30 %).</summary>
    public static double Margin => Balance.Current.Margin;

    /// <summary>
    /// Salaires d'un emploi productif avec les proportions et les salaires par défaut (salaire de base × 1, 2 ou 4,
    /// encadrement 0,1, direction 0,01). Un bâtiment du catalogue a ses propres proportions et salaires : voir
    /// <see cref="Staffing.WageBillPerJob(BuildingType)"/>.
    /// </summary>
    public static double WageCostPerProductionJob(double dailyWage, Rank mainRank = Rank.Workers) =>
        Staffing.WageBillPerJob(mainRank, null, dailyWage, null);

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
        var perJob = Staffing.WageBillPerJob(reference) + EnergyCostPerJob(reference, recipe, priceOf);
        // Le prix inclut la TVA, payée sur la seule valeur ajoutée (le prix moins les matières) : la TVA des fournisseurs,
        // déjà dans le prix des matières, est déduite, et la marge se calcule hors taxes. Prix P, TVA t, charges C par unité,
        // matières M : P = (1 + marge) × (C / (1 − t) + M), soit la même chaîne de prix qu'à 0 %, chaque étape augmentée de t/(1 − t).
        var vat = Math.Clamp(Balance.Current.VatRate, 0, 0.9);
        return (1 + Margin) * (perJob / recipe.QuantityPerWorker / (1 - vat) + materials);
    }

    /// <summary>
    /// Énergie d'un emploi de production sur l'année, pour un bâtiment de la liste 1 : son chauffage au bois (en moyenne
    /// sur les quatre saisons, ou constant pour une forge) et son eau.
    /// </summary>
    public static double EnergyCostPerJob(BuildingType reference, Production recipe, Func<string, double>? priceOf)
    {
        if (reference.Tier != 1 || priceOf is null)
            return 0;

        var averageFactor = reference.ConstantHeating ? 1 : Enum.GetValues<Shared.Season>().Average(Energy.HeatingFactor);
        var heating = Energy.HeatingPerUnit * averageFactor * priceOf(Energy.DefaultHeating);
        var water = recipe.ProductId == Energy.Water ? 0 : Energy.WaterPerUnit * priceOf(Energy.Water);
        return heating + water;
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
