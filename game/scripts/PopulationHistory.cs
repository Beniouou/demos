using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Shared;

namespace Demos.Game;

/// <summary>
/// Le relevé de la population, pris par l'interface chaque jour joué (la simulation ne garde pas d'historique ; décidé le
/// 21 novembre 2026, avant : le 1er de chaque mois) : les chiffres de toute la nation et de chaque commune. Un relevé de
/// plus d'un an n'est gardé que s'il est du 1er du mois (ou le tout premier) : les courbes de Population → Évolution ont
/// un point par jour sur les douze derniers mois, un point par mois avant. Il ne dure que le temps de la partie.
/// </summary>
public sealed class PopulationHistory
{
    /// <summary>Au-delà de ce nombre de jours, un relevé n'est gardé que s'il ouvre un mois.</summary>
    private const int DailyDays = 365;

    /// <summary>Un relevé : sa date, toute la nation, et chaque commune.</summary>
    public sealed record Snapshot(System.DateOnly Date, PopulationStats All, IReadOnlyDictionary<string, PopulationStats> ByCommune)
    {
        /// <summary>« janv. 1900 ».</summary>
        public string MonthLabel => WorldText.MonthLabel(Date);

        /// <summary>« 14 mars 1901 », pour l'axe et le survol du graphique.</summary>
        public string DayLabel => WorldText.DayLabel(Date);

        public Season Season => GameClock.SeasonOf(Date);
    }

    private readonly List<Snapshot> _snapshots = [];

    public IReadOnlyList<Snapshot> Snapshots => _snapshots;

    /// <summary>Relève la population ; un second relevé le même jour remplace le premier. Tasse ce qui a plus d'un an.</summary>
    public void Record(GameView view)
    {
        var communes = view.Communes();
        var snapshot = new Snapshot(view.Today, PopulationStats.Of(communes), communes.ToDictionary(c => c.Id, c => PopulationStats.Of([c])));
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
