using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Les outils des bâtiments (décidé le 20 novembre 2026) : chaque bâtiment qui a une recette garde un stock d'outils,
/// hors entrepôt, et en vise <see cref="BalanceSettings.ToolsPerJob"/> par emploi productif. Les outils s'usent (durée
/// d'usage du produit « outils ») ; le bâtiment rachète ce qui manque comme ses matières. Un bâtiment qui manque d'outils
/// produit moins : sa production est multipliée par 1 − <see cref="BalanceSettings.ToolShortagePenalty"/> × la part
/// qui lui manque. Un nouveau bâtiment, et chaque bâtiment de la partie de départ, reçoit son stock visé au complet.
/// </summary>
public static class Tools
{
    public const string ProductId = "outils";

    /// <summary>Les outils que vise un bâtiment : par emploi productif, s'il a une recette.</summary>
    public static double TargetOf(Structure building) =>
        building.Kind == StructureKind.Building && building.Production is not null && building.Jobs > 0
            ? building.Jobs * Balance.Current.ToolsPerJob
            : 0;

    /// <summary>La part de ses outils qui manque à un bâtiment (0 s'il n'en vise aucun).</summary>
    public static double MissingShare(Structure building, double owned) =>
        TargetOf(building) is > 0 and var target ? Math.Clamp(1 - owned / target, 0, 1) : 0;

    /// <summary>L'effet des outils sur la production : 1 avec tous ses outils, 1 − la pénalité sans aucun.</summary>
    public static double EffectOf(Structure building, double owned) =>
        1 - Balance.Current.ToolShortagePenalty * MissingShare(building, owned);

    /// <summary>Donne à un bâtiment son stock d'outils visé au complet (nouveau bâtiment, partie de départ).</summary>
    public static void Equip(Structure building, Inventory stock)
    {
        var missing = TargetOf(building) - stock.QuantityOf(ProductId);
        if (missing > 0)
            stock.Add(ProductId, missing);
    }

    /// <summary>
    /// Fin de la journée de travail : les outils de chaque bâtiment s'usent, puis il rachète ce qui lui manque dans le
    /// stockage de sa commune, avec l'argent de son propriétaire (comme ses matières).
    /// </summary>
    public static void RunDaily(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<ProductDefinition> products,
        Func<string, Market> marketOf,
        Treasury treasury,
        Func<string, Treasury>? communeFundOf,
        Func<Structure, Treasury>? companyFundOf,
        IReadOnlyDictionary<string, SocialClass> classesById,
        Func<Structure, Inventory> toolStockOf)
    {
        if (products.FirstOrDefault(p => p.Id == ProductId) is not { } tool)
            return;

        var wear = Math.Min(1, 1 / (tool.UsageDays ?? 1));
        foreach (var building in structures.Values)
        {
            var target = TargetOf(building);
            if (target <= 0 || Communes.CommuneOf(building, structures) is not { } communeId)
                continue;

            var stock = toolStockOf(building);
            stock.Keep(ProductId, 1 - wear);
            var missing = target - stock.QuantityOf(ProductId);
            if (missing <= 1e-9)
                continue;

            var refill = new Production("", 1, [new RecipeInput(ProductId, missing)]);
            var bought = Supply.Acquire(building, refill, 1, marketOf(communeId), communeId, treasury, communeFundOf?.Invoke(communeId),
                companyFundOf, classesById);
            stock.Add(ProductId, missing * bought);
        }
    }
}
