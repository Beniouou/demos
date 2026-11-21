using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Economy;

namespace Demos.Game;

/// <summary>
/// Les familles de produits dans l'interface (décidé le 21 novembre 2026) : leurs noms, et le rangement d'une liste de
/// produits par famille, dans l'ordre de <see cref="ProductFamily"/>, « Autres » à la fin.
/// </summary>
public static class ProductFamilies
{
    public static string NameOf(ProductFamily? family) => family switch
    {
        ProductFamily.RawMaterial => "Matières premières",
        ProductFamily.Processed => "Produits transformés",
        ProductFamily.ConsumerGoods => "Biens de consommation",
        ProductFamily.Equipment => "Équipement",
        ProductFamily.Construction => "Construction",
        ProductFamily.EnergyAndServices => "Énergie et services",
        _ => "Autres",
    };

    /// <summary>La clé d'une famille, pour retenir ce qui est replié.</summary>
    public static string KeyOf(ProductFamily? family) => family?.ToString() ?? "Autres";

    /// <summary>Le nom affiché d'un produit, son identifiant s'il n'est pas au catalogue.</summary>
    public static string NameOfProduct(GameView view, string productId) =>
        view.Products().FirstOrDefault(p => p.Id == productId)?.Name ?? productId;

    /// <summary>
    /// Les produits rangés par famille : les familles dans l'ordre fixé (« Autres » à la fin), les produits par nom.
    /// Seules les familles qui ont des produits sont rendues.
    /// </summary>
    public static IReadOnlyList<(ProductFamily? Family, IReadOnlyList<T> Items)> Group<T>(
        GameView view, IEnumerable<T> items, System.Func<T, string> productIdOf, System.Func<T, string> nameOf)
    {
        var families = view.Products().ToDictionary(p => p.Id, p => p.Family);
        return items
            .GroupBy(item => families.GetValueOrDefault(productIdOf(item)))
            .OrderBy(g => g.Key is { } family ? (int)family : int.MaxValue)
            .Select(g => (g.Key, (IReadOnlyList<T>)g.OrderBy(nameOf).ToList()))
            .ToList();
    }
}
