using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Le relevé du stock de légitimité de chaque pôle, pris par l'interface chaque jour joué (décidé le 23 novembre 2026) :
/// un point par jour sur les 30 derniers jours, puis un point par mois (le 1er, ou le tout premier relevé) au-delà. Il
/// ne dure que le temps de la partie.
/// </summary>
public sealed class LegitimacyHistory
{
    /// <summary>Au-delà de ce nombre de jours, un relevé n'est gardé que s'il ouvre un mois.</summary>
    private const int DailyDays = 30;

    public sealed record Snapshot(System.DateOnly Date, IReadOnlyDictionary<Orientation, double> Stocks)
    {
        public string DayLabel => WorldText.DayLabel(Date);
    }

    private readonly List<Snapshot> _snapshots = [];

    public IReadOnlyList<Snapshot> Snapshots => _snapshots;

    /// <summary>Relève les stocks ; un second relevé le même jour remplace le premier. Tasse ce qui a plus de 30 jours.</summary>
    public void Record(GameView view)
    {
        var snapshot = new Snapshot(view.Today, Orientation.All.ToDictionary(pole => pole, view.LegitimacyOf));
        if (_snapshots.Count > 0 && _snapshots[^1].Date == snapshot.Date)
            _snapshots[^1] = snapshot;
        else
            _snapshots.Add(snapshot);

        var limit = view.Today.AddDays(-DailyDays);
        for (var index = _snapshots.Count - 1; index > 0; index--)
        {
            if (_snapshots[index].Date < limit && _snapshots[index].Date.Day != 1)
                _snapshots.RemoveAt(index);
        }
    }
}
