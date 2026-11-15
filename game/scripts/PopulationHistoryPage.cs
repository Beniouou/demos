using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Population → Évolution, en lecture : en haut une courbe de la mesure choisie (membres, chômeurs, besoins, logés,
/// adhésion, richesse par membre), un point par relevé mensuel, une courbe par commune (ou celle de la commune
/// choisie) ; dessous un tableau avec une ligne par saison (la plus récente en haut) : membres et leur variation,
/// chômeurs, besoins, logés, adhésion, richesse par membre.
/// </summary>
public partial class PopulationHistoryPage : VBoxContainer, IMenuPage
{
    /// <summary>Une mesure de la courbe : son nom, sa valeur, sa mise en forme, et si c'est une part (axe de 0 à 100 %).</summary>
    private sealed record Measure(string Name, System.Func<PopulationStats, double> Value, string Format)
    {
        public bool IsShare => Format == "P0";
    }

    private static readonly Measure[] Measures =
    [
        new("Membres", s => s.Members, "N0"),
        new("Chômeurs", s => s.Unemployed, "N0"),
        new("Besoins couverts", s => s.Needs, "P0"),
        new("Logés", s => s.Housed, "P0"),
        new("Adhésion", s => s.Adhesion, "P0"),
        new("Richesse par membre", s => s.WealthPerMember, "N1"),
    ];

    private readonly GameView _view = null!;
    private readonly PopulationHistory _history = null!;
    private readonly CommuneChoice _commune = new();
    private readonly OptionButton _measure = null!;
    private readonly LineChart _chart = new();
    private readonly RichTextLabel _text = null!;
    private string _chartKey = "";

    public PopulationHistoryPage(GameView view, PopulationHistory history)
    {
        _view = view;
        _history = history;
        AddThemeConstantOverride("separation", 8);
        AddChild(_commune.Row());

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Mesure", CustomMinimumSize = new Vector2(90, 0) });
        _measure = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None };
        foreach (var measure in Measures)
            _measure.AddItem(measure.Name);
        row.AddChild(_measure);
        AddChild(row);

        AddChild(_chart);
        _text = new HintText
        {
            BbcodeEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectionEnabled = false,
        };
        _text.AddThemeColorOverride("default_color", UiTheme.Text);
        AddChild(_text);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PopulationHistoryPage() { }

    public void Refresh()
    {
        var communes = _view.Communes();
        _commune.Fill(communes);
        RefreshChart(communes);
        var text = Describe(_commune.SelectedId);
        // Le texte n'est remis que s'il a changé : sinon il remonterait en haut à chaque image.
        if (text != _text.Text)
            _text.Text = text;
    }

    /// <summary>La courbe n'est refaite que quand un relevé s'ajoute ou que le choix change.</summary>
    private void RefreshChart(IReadOnlyList<CommuneView> communes)
    {
        var snapshots = _history.Snapshots;
        var key = $"{snapshots.Count}|{snapshots.LastOrDefault()?.Date}|{_commune.SelectedId}|{_measure.Selected}|{communes.Count}";
        if (key == _chartKey)
            return;
        _chartKey = key;

        var measure = Measures[System.Math.Max(0, _measure.Selected)];
        var shown = _commune.SelectedId is { } id ? communes.Where(c => c.Id == id).ToList() : communes;
        var series = shown.Select((commune, index) => new LineChart.Series(
            commune.Name,
            UiTheme.SeriesColor(index),
            snapshots.Select(s => s.ByCommune.TryGetValue(commune.Id, out var stats) ? measure.Value(stats) : double.NaN).ToList())).ToList();
        string Format(double value) => value.ToString(measure.Format);
        _chart.SetData(snapshots.Select(s => s.MonthLabel).ToList(), series, Format, measure.IsShare ? 1 : null);
    }

    private string Describe(string? communeId)
    {
        var text = new StringBuilder(SheetText.Section("Par saison", PopulationText.History));

        // Une commune fondée en cours de partie n'a pas de relevé avant sa fondation.
        var rows = _history.SeasonStarts()
            .Select(s => (Label: s.SeasonLabel, Stats: communeId is null ? s.All : s.ByCommune.GetValueOrDefault(communeId)))
            .Where(r => r.Stats is not null)
            .ToList();
        if (rows.Count == 0)
            return text + SheetText.Muted("Aucun relevé encore : le premier se prend après le premier jour joué.");

        text.Append("[table=8]");
        foreach (var (header, hint) in new[]
                 {
                     ("Saison", ""), ("Membres", PopulationText.Members), ("Variation", ""), ("Chômeurs", PopulationText.Unemployed),
                     ("Besoins", PopulationText.Needs), ("Logés", PopulationText.Housed), ("Adhésion", PopulationText.Adhesion),
                     ("Richesse / membre", PopulationText.Wealth),
                 })
            text.Append(PopulationText.Cell(hint == "" ? header : SheetText.Hint(header, hint), header: true));
        for (var index = rows.Count - 1; index >= 0; index--)
        {
            var (label, stats) = rows[index];
            var change = index > 0 ? $"{stats!.Members - rows[index - 1].Stats!.Members:+#,0;−#,0;0}" : "—";
            text.Append(PopulationText.Cell(label));
            text.Append(PopulationText.Cell($"{stats!.Members:N0}"));
            text.Append(PopulationText.Cell(change));
            text.Append(PopulationText.Cell($"{stats.Unemployed:N0}"));
            text.Append(PopulationText.Cell($"{stats.Needs:P0}"));
            text.Append(PopulationText.Cell($"{stats.Housed:P0}"));
            text.Append(PopulationText.Cell($"{stats.Adhesion:P0}"));
            text.Append(PopulationText.Cell($"{stats.WealthPerMember:N1}"));
        }
        text.AppendLine("[/table]");
        return text.ToString();
    }
}
