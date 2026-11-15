using System.Collections.Generic;
using System.Linq;
using Demos.Application;

namespace Demos.Game;

/// <summary>
/// Le relevé des produits le 1er de chaque mois, pris par l'interface : pour chaque commune et chaque produit, les
/// totaux depuis le début de la partie (produit, utilisé, envoyé, perdu) et le stock. La différence entre deux relevés
/// donne ce qui s'est passé pendant le mois. Il ne dure que le temps de la partie.
/// </summary>
public sealed class ProductionHistory
{
    /// <summary>Un relevé : sa date et les totaux de chaque produit dans chaque commune.</summary>
    public sealed record Snapshot(System.DateOnly Date, IReadOnlyList<ProductFlowView> Flows);

    private readonly List<Snapshot> _snapshots = [];

    public IReadOnlyList<Snapshot> Snapshots => _snapshots;

    /// <summary>Relève les produits ; un second relevé dans le même mois remplace le premier.</summary>
    public void Record(GameView view)
    {
        var snapshot = new Snapshot(view.Today, view.ProductFlows());
        if (_snapshots.Count > 0 && _snapshots[^1].Date.Month == snapshot.Date.Month && _snapshots[^1].Date.Year == snapshot.Date.Year)
            _snapshots[^1] = snapshot;
        else
            _snapshots.Add(snapshot);
    }
}

/// <summary>Les chiffres d'un produit pour une période (ou cumulés) : produit, utilisé, envoyé, perdu, et le stock à la fin.</summary>
public sealed record ProductFigures(double Produced, double Used, double Sent, double Lost, double Stock, double Price)
{
    public static readonly ProductFigures None = new(0, 0, 0, 0, 0, 0);

    /// <summary>Les totaux d'un produit dans les communes gardées (toutes si <paramref name="communeId"/> est null).</summary>
    public static ProductFigures Of(IEnumerable<ProductFlowView> flows, string productId, string? communeId)
    {
        var chosen = flows.Where(f => f.ProductId == productId && (communeId is null || f.CommuneId == communeId)).ToList();
        return chosen.Count == 0
            ? None
            : new ProductFigures(chosen.Sum(f => f.Produced), chosen.Sum(f => f.Used), chosen.Sum(f => f.Sent), chosen.Sum(f => f.Lost),
                chosen.Sum(f => f.Stock), chosen.Max(f => f.Price));
    }

    /// <summary>Ce qui s'est passé entre deux relevés de totaux : les différences, et le stock (et le prix) du second.</summary>
    public ProductFigures Since(ProductFigures before) =>
        new(Produced - before.Produced, Used - before.Used, Sent - before.Sent, Lost - before.Lost, Stock, Price);
}
