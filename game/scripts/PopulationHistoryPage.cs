using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Population;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Population → Évolution (refaite le 21 novembre 2026), en lecture : des courbes, un point par relevé mensuel, pour
/// toute la nation ou une commune : satisfaction de chaque besoin (pour un rang ou tous), membres par rang, adhésion
/// par pôle, chômage par rang, richesse par membre. Le relevé est pris par l'interface (<see cref="PopulationHistory"/>).
/// </summary>
public partial class PopulationHistoryPage : VBoxContainer, IMenuPage
{
    private static readonly string[] NeedOrder = ["Food", "Clothing", "Furniture", "Leisure", "Administration", "Security", "Housing"];

    private static readonly string[] Measures =
        ["Satisfaction de chaque besoin", "Membres par rang", "Adhésion par pôle", "Chômage par rang", "Richesse par membre"];

    private readonly GameView _view = null!;
    private readonly PopulationHistory _history = null!;
    private readonly CommuneChoice _commune = new();
    private readonly OptionButton _measure = null!;
    private readonly OptionButton _rank = null!;
    private readonly LineChart _chart = new();
    private readonly Label _note = null!;
    private string _chartKey = "";

    public PopulationHistoryPage(GameView view, PopulationHistory history)
    {
        _view = view;
        _history = history;
        AddThemeConstantOverride("separation", 8);
        AddChild(_commune.Row());

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Courbes", CustomMinimumSize = new Vector2(90, 0) });
        _measure = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None };
        foreach (var measure in Measures)
            _measure.AddItem(measure);
        row.AddChild(_measure);
        row.AddChild(new Label { Text = "Rang" });
        _rank = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(180, 0) };
        _rank.AddItem("Tous les rangs");
        foreach (var rank in System.Enum.GetValues<Rank>())
        {
            if (IconRegistry.GetRankIcon(rank, 16) is { } icon)
                _rank.AddIconItem(icon, ClassTransitions.RankName(rank));
            else
                _rank.AddItem(ClassTransitions.RankName(rank));
        }
        row.AddChild(_rank);
        AddChild(row);

        _chart.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(_chart);
        _note = UiTheme.Caption(PopulationText.History);
        _note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_note);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PopulationHistoryPage() { }

    private Rank? SelectedRank => _rank.Selected > 0 ? (Rank)(_rank.Selected - 1) : null;

    public void Refresh()
    {
        var communes = _view.Communes();
        _commune.Fill(communes);
        if (_commune.Button.ItemCount > 0)
            _commune.Button.SetItemText(0, "Toute la nation");
        // Le rang ne sert qu'aux besoins.
        _rank.Disabled = _measure.Selected != 0;

        var snapshots = _history.Snapshots;
        var key = $"{snapshots.Count}|{snapshots.LastOrDefault()?.Date}|{_commune.SelectedId}|{_measure.Selected}|{_rank.Selected}";
        if (key == _chartKey)
            return;
        _chartKey = key;

        // Une commune fondée en cours de partie n'a pas de relevé avant sa fondation : sa courbe commence plus tard.
        var stats = snapshots.Select(s => _commune.SelectedId is { } id ? s.ByCommune.GetValueOrDefault(id) : s.All).ToList();
        var labels = snapshots.Select(s => s.DayLabel).ToList();
        LineChart.Series Series(string name, Color color, System.Func<PopulationStats, double> value) =>
            new(name, color, stats.Select(s => s is null ? double.NaN : value(s)).ToList());

        var ranks = System.Enum.GetValues<Rank>();
        switch (_measure.Selected)
        {
            case 0:
                var rank = SelectedRank;
                var needs = stats.Where(s => s is not null).SelectMany(s => s!.NeedCells.Keys.Where(k => k.Rank == rank).Select(k => k.Need)).Distinct()
                    .OrderBy(n => System.Array.IndexOf(NeedOrder, n) is var i && i >= 0 ? i : int.MaxValue).ToList();
                _chart.SetData(labels,
                    needs.Select((need, index) => Series(FlowText.NeedName(need), UiTheme.SeriesColor(index),
                        s => s.NeedCells.TryGetValue((need, rank), out var cell) ? cell.Coverage : double.NaN)).ToList(),
                    v => v.ToString("P0"), 1);
                break;
            case 1:
                _chart.SetData(labels,
                    ranks.Select((r, index) => Series(ClassTransitions.RankName(r), UiTheme.SeriesColor(index), s => s.ByRank.GetValueOrDefault(r)))
                        .Append(Series("Total", UiTheme.Text, s => s.Members)).ToList(),
                    v => v.ToString("N0"));
                break;
            case 2:
                _chart.SetData(labels,
                    Orientation.All.Select(pole => Series($"{pole.Code} {WorldText.PoleName(pole)}", UiTheme.PoleColor(pole),
                        s => s.AdhesionByPole.GetValueOrDefault(pole, double.NaN))).ToList(),
                    v => v.ToString("P0"), 1);
                break;
            case 3:
                _chart.SetData(labels,
                    ranks.Select((r, index) => Series(ClassTransitions.RankName(r), UiTheme.SeriesColor(index), s => s.UnemploymentRate(r)))
                        .Append(Series("Tous", UiTheme.Text, s => s.UnemploymentRate(null))).ToList(),
                    v => v.ToString("P0"));
                break;
            default:
                _chart.SetData(labels, [Series("Richesse par membre", UiTheme.SeriesColor(0), s => s.WealthPerMember)], v => v.ToString("N1"));
                break;
        }
    }
}
