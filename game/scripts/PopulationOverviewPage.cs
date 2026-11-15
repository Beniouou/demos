using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Population;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Population → Vue d'ensemble, en lecture : pour toutes les communes ou une seule, les habitants, logements, emplois et
/// chômeurs, et les moyennes des besoins, des logés et de l'adhésion ; une barre des membres par rang et une barre par
/// axe idéologique (la part de chaque pôle) ; pour toutes les communes, un tableau commune par commune. Chaque terme a
/// sa bulle d'aide, qui dit la règle.
/// </summary>
public partial class PopulationOverviewPage : ScrollContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly CommuneChoice _commune = new();
    private readonly RichTextLabel _summary = null!;
    private readonly StackedBar _ranks = new();
    private readonly Dictionary<IdeologicalAxis, StackedBar> _axes = [];
    private readonly RichTextLabel _table = null!;

    public PopulationOverviewPage(GameView view)
    {
        _view = view;
        HorizontalScrollMode = ScrollMode.Disabled;
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        AddChild(content);

        content.AddChild(_commune.Row());
        _summary = Text();
        content.AddChild(_summary);

        var ranks = Text();
        // Le titre et la légende des couleurs : une part trop étroite n'a pas la place d'écrire son nom.
        ranks.Text = SheetText.Section("Par rang", string.Join("\n", System.Enum.GetValues<Rank>().Select(PopulationText.Rank)))
            + string.Join("   ", System.Enum.GetValues<Rank>().Select((rank, index) =>
                $"[color=#{UiTheme.SeriesColor(index).ToHtml(false)}]■[/color] {PopulationText.RankName(rank)}"));
        content.AddChild(ranks);
        content.AddChild(_ranks);

        var poles = Text();
        poles.Text = SheetText.Section("Par pôle", PopulationText.Poles);
        content.AddChild(poles);
        foreach (var axis in System.Enum.GetValues<IdeologicalAxis>())
        {
            _axes[axis] = new StackedBar();
            content.AddChild(_axes[axis]);
        }

        _table = Text();
        content.AddChild(_table);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PopulationOverviewPage() { }

    private static RichTextLabel Text()
    {
        var text = new HintText
        {
            BbcodeEnabled = true,
            FitContent = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectionEnabled = false,
        };
        text.AddThemeColorOverride("default_color", UiTheme.Text);
        return text;
    }

    public void Refresh()
    {
        var communes = _view.Communes();
        _commune.Fill(communes);
        var chosen = _commune.SelectedId is { } id ? communes.Where(c => c.Id == id).ToList() : communes;
        var stats = PopulationStats.Of(chosen);

        // Les textes ne sont remis que s'ils ont changé : sinon la page remonterait en haut à chaque image.
        SetText(_summary, Summary(chosen, stats));
        SetText(_table, _commune.SelectedId is null ? Table(communes) : "");

        var ranks = System.Enum.GetValues<Rank>();
        _ranks.SetParts(ranks.Select((rank, index) =>
            new StackedBar.Part(PopulationText.RankName(rank), stats.ByRank[rank], UiTheme.SeriesColor(index))).ToList(),
            (part, share) => $"{part.Name} {part.Value:N0} ({share:P0})");
        foreach (var (axis, bar) in _axes)
            bar.SetParts(new[] { Pole.A, Pole.B }.Select(pole => new Orientation(axis, pole))
                .Select(pole => new StackedBar.Part(pole.Code, stats.ByPole[pole], UiTheme.PoleColor(pole))).ToList(),
                (part, share) => $"{part.Name} {share:P0}");
    }

    private static void SetText(RichTextLabel label, string text)
    {
        if (text != label.Text)
            label.Text = text;
    }

    private static string Summary(IReadOnlyList<CommuneView> communes, PopulationStats stats)
    {
        var title = communes.Count == 1 ? communes[0].Name.ToUpperInvariant() : "TOUTES LES COMMUNES";
        var text = new StringBuilder(SheetText.Heading(title));
        text.AppendLine($"{SheetText.Hint("Habitants", PopulationText.Members)} {stats.Residents:N0} · "
            + $"{SheetText.Hint("logements", PopulationText.Housing)} {stats.HousingPlaces:N0} places · "
            + $"{SheetText.Hint("chômeurs", PopulationText.Unemployed)} {stats.Unemployed:N0}");
        text.AppendLine($"{SheetText.Hint("Emplois pourvus", PopulationText.Jobs)} {stats.JobsFilled:N0} / {stats.Jobs:N0} · "
            + $"groupes locaux {stats.Groups}");
        text.AppendLine($"{SheetText.Hint("Besoins couverts", PopulationText.Needs)} {stats.Needs:P0} · "
            + $"{SheetText.Hint("logés", PopulationText.Housed)} {stats.Housed:P0} · "
            + $"{SheetText.Hint("adhésion", PopulationText.Adhesion)} {stats.Adhesion:P0}");
        text.Append($"{SheetText.Hint("Richesse", PopulationText.Wealth)} {stats.Wealth:N0} ({stats.WealthPerMember:N1} par membre)");
        return text.ToString();
    }

    /// <summary>Le tableau commune par commune, pour toutes les communes (vide s'il n'y en a qu'une).</summary>
    private static string Table(IReadOnlyList<CommuneView> communes)
    {
        if (communes.Count < 2)
            return "";

        var text = new StringBuilder(SheetText.Section("Par commune"));
        text.Append("[table=7]");
        foreach (var (header, hint) in new[]
                 {
                     ("Commune", ""), ("Habitants", PopulationText.Members), ("Logements", PopulationText.Housing),
                     ("Chômeurs", PopulationText.Unemployed), ("Besoins", PopulationText.Needs), ("Logés", PopulationText.Housed),
                     ("Adhésion", PopulationText.Adhesion),
                 })
            text.Append(PopulationText.Cell(hint == "" ? header : SheetText.Hint(header, hint), header: true));
        foreach (var commune in communes)
        {
            var row = PopulationStats.Of([commune]);
            text.Append(PopulationText.Cell(SheetText.Escape(commune.Name)));
            text.Append(PopulationText.Cell($"{row.Residents:N0}"));
            text.Append(PopulationText.Cell($"{row.HousingPlaces:N0}"));
            text.Append(PopulationText.Cell($"{row.Unemployed:N0}"));
            text.Append(PopulationText.Cell($"{row.Needs:P0}"));
            text.Append(PopulationText.Cell($"{row.Housed:P0}"));
            text.Append(PopulationText.Cell($"{row.Adhesion:P0}"));
        }
        text.Append("[/table]");
        return text.ToString();
    }
}
