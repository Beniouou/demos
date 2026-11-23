using System.Collections.Generic;
using System.Linq;
using Demos.Application;

namespace Demos.Game;

/// <summary>
/// Le relevé des produits, pris par l'interface chaque jour joué (décidé le 21 novembre 2026, avant : le 1er de chaque
/// mois) : pour chaque commune et chaque produit, les totaux depuis le début de la partie (produit, utilisé, envoyé,
/// perdu au stockage, perdu en route) et le stock. La différence entre deux relevés donne ce qui s'est passé entre eux.
/// Un relevé de plus d'un an n'est gardé que s'il est du 1er du mois (ou le tout premier). Il ne dure que la partie.
/// </summary>
public sealed class ProductionHistory
{
    /// <summary>Un relevé : sa date et les totaux de chaque produit dans chaque commune.</summary>
    public sealed record Snapshot(System.DateOnly Date, IReadOnlyList<ProductFlowView> Flows);

    private readonly List<Snapshot> _snapshots = [];

    public IReadOnlyList<Snapshot> Snapshots => _snapshots;

    /// <summary>
    /// Les relevés qui ouvrent un mois (le 1er, et le tout premier de la partie) : deux relevés qui se suivent dans cette
    /// liste encadrent un mois fini, sauf le dernier, qui ouvre le mois en cours.
    /// </summary>
    public IReadOnlyList<Snapshot> MonthStarts => _snapshots.Where((s, index) => index == 0 || s.Date.Day == 1).ToList();

    /// <summary>Relève les produits ; un second relevé le même jour remplace le premier. Tasse ce qui a plus d'un an.</summary>
    public void Record(GameView view)
    {
        var snapshot = new Snapshot(view.Today, view.ProductFlows());
        if (_snapshots.Count > 0 && _snapshots[^1].Date == snapshot.Date)
            _snapshots[^1] = snapshot;
        else
            _snapshots.Add(snapshot);

        var limit = view.Today.AddDays(-365);
        for (var index = _snapshots.Count - 1; index > 0; index--)
        {
            if (_snapshots[index].Date < limit && _snapshots[index].Date.Day != 1)
                _snapshots.RemoveAt(index);
        }
    }
}

/// <summary>
/// Les chiffres d'un produit pour une période (ou cumulés) : produit, utilisé, envoyé, perdu au stockage, perdu en route,
/// et, à la fin, ce qui est encore en route et le stock.
/// </summary>
public sealed record ProductFigures(double Produced, double Used, double Sent, double Lost, double LostInTransit, double InTransit, double Stock, double Price)
{
    public static readonly ProductFigures None = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Les totaux d'un produit dans les communes gardées (toutes si <paramref name="communeId"/> est null).</summary>
    public static ProductFigures Of(IEnumerable<ProductFlowView> flows, string productId, string? communeId)
    {
        var chosen = flows.Where(f => f.ProductId == productId && (communeId is null || f.CommuneId == communeId)).ToList();
        return chosen.Count == 0
            ? None
            : new ProductFigures(chosen.Sum(f => f.Produced), chosen.Sum(f => f.Used), chosen.Sum(f => f.Sent), chosen.Sum(f => f.Lost),
                chosen.Sum(f => f.LostInTransit), chosen.Sum(f => f.InTransit), chosen.Sum(f => f.Stock), chosen.Max(f => f.Price));
    }

    /// <summary>
    /// Ce qui s'est passé entre deux relevés de totaux : les différences, et ce qui est en route, le stock (et le prix)
    /// du second.
    /// </summary>
    public ProductFigures Since(ProductFigures before) =>
        new(Produced - before.Produced, Used - before.Used, Sent - before.Sent, Lost - before.Lost,
            LostInTransit - before.LostInTransit, InTransit, Stock, Price);
}
