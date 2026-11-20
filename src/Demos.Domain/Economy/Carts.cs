using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Les charrettes des communes (décidé le 20 novembre 2026) : un objet durable, fabriqué à l'atelier, que l'État de la
/// commune achète jusqu'au nombre voulu par le joueur. Chaque charrette ajoute <see cref="BalanceSettings.CartCapacity"/>
/// à la capacité d'envoi de la commune ; elle s'use (durée d'usage du produit « charrette »).
/// </summary>
public static class Carts
{
    public const string ProductId = "charrette";

    /// <summary>
    /// Les charrettes de chaque commune s'usent, puis l'État de la commune achète ce qui manque au nombre voulu : d'abord
    /// celles qu'il a produites, puis au marché, avec la caisse de la commune puis le trésor.
    /// </summary>
    public static void RunDaily(
        IEnumerable<Structure> communes,
        IReadOnlyList<ProductDefinition> products,
        Func<string, int> wantedOf,
        Func<string, Inventory> cartsOf,
        Func<string, Market> marketOf,
        Treasury treasury,
        Func<string, Treasury>? communeFundOf)
    {
        if (products.FirstOrDefault(p => p.Id == ProductId) is not { } cart)
            return;

        var wear = Math.Min(1, 1 / (cart.UsageDays ?? 1));
        var noClasses = new Dictionary<string, SocialClass>();
        foreach (var commune in communes)
        {
            var carts = cartsOf(commune.Id);
            carts.Keep(ProductId, 1 - wear);
            var missing = wantedOf(commune.Id) - carts.QuantityOf(ProductId);
            if (missing <= 1e-9)
                continue;

            // L'État de la commune paie comme pour un bâtiment public : ses propres charrettes, puis le marché.
            var refill = new Production("", 1, [new RecipeInput(ProductId, missing)]);
            var bought = Supply.Acquire(commune with { Ownership = Ownership.Public }, refill, 1, marketOf(commune.Id), commune.Id, treasury,
                communeFundOf?.Invoke(commune.Id), null, noClasses);
            carts.Add(ProductId, missing * bought);
        }
    }
}
