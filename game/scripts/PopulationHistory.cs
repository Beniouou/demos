using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Shared;

namespace Demos.Game;

/// <summary>
/// Le relevé de la population le 1er de chaque mois, pris par l'interface (la simulation ne garde pas d'historique) :
/// les chiffres de toutes les communes et de chacune. La courbe de Population → Évolution montre chaque relevé, le
/// tableau le premier de chaque saison. Il ne dure que le temps de la partie.
/// </summary>
public sealed class PopulationHistory
{
    /// <summary>Un relevé : sa date, toutes les communes, et chaque commune.</summary>
    public sealed record Snapshot(System.DateOnly Date, PopulationStats All, IReadOnlyDictionary<string, PopulationStats> ByCommune)
    {
        /// <summary>« janv. 1900 », pour l'axe du graphique.</summary>
        public string MonthLabel => $"{MonthNames[Date.Month - 1]} {Date.Year}";

        public Season Season => GameClock.SeasonOf(Date);

        /// <summary>« Hiver 1901 » : l'hiver commence en décembre, il prend le nom de l'année qui suit.</summary>
        public string SeasonLabel => $"{WorldText.SeasonName(Season)} {(Date.Month == 12 ? Date.Year + 1 : Date.Year)}";
    }

    private static readonly string[] MonthNames =
        ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."];

    private readonly List<Snapshot> _snapshots = [];

    public IReadOnlyList<Snapshot> Snapshots => _snapshots;

    /// <summary>Les relevés qui ouvrent une saison (le premier de la partie compris), pour le tableau.</summary>
    public IEnumerable<Snapshot> SeasonStarts() =>
        _snapshots.Where((snapshot, index) => index == 0 || snapshot.SeasonLabel != _snapshots[index - 1].SeasonLabel);

    /// <summary>Relève la population ; un second relevé dans le même mois remplace le premier.</summary>
    public void Record(GameView view)
    {
        var communes = view.Communes();
        var snapshot = new Snapshot(view.Today, PopulationStats.Of(communes), communes.ToDictionary(c => c.Id, c => PopulationStats.Of([c])));
        if (_snapshots.Count > 0 && _snapshots[^1].MonthLabel == snapshot.MonthLabel)
            _snapshots[^1] = snapshot;
        else
            _snapshots.Add(snapshot);
    }
}
