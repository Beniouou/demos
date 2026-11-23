using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Population;
using Godot;

namespace Demos.Game;

/// <summary>
/// Population → Besoins (décidé le 21 novembre 2026), en lecture : pour toute la nation ou une commune, un tableau en
/// couleurs avec une ligne par besoin et une colonne par rang (plus « Tous »), du vert au rouge selon la part couverte
/// au dernier jour. Un clic sur une case montre dessous la cause du manque (couvert, faute d'argent, faute de produits
/// ou de places), les groupes de ce rang et ce que leurs manques produisent (croissance, pôles).
/// </summary>
public partial class PopulationNeedsPage : ScrollContainer, IMenuPage
{
    /// <summary>L'ordre des besoins dans le tableau ; un besoin inconnu vient après.</summary>
    private static readonly string[] NeedOrder = ["Food", "Clothing", "Furniture", "Leisure", "Administration", "Security", "Housing"];

    private readonly GameView _view = null!;
    private readonly CommuneChoice _commune = new();
    private readonly GridContainer _grid = null!;
    private readonly StackedBar _causes = new();
    private readonly RichTextLabel _detail = null!;
    private (string Need, Rank? Rank) _selected = ("Food", Rank.Workers);
    private string _gridKey = "";

    public PopulationNeedsPage(GameView view)
    {
        _view = view;
        HorizontalScrollMode = ScrollMode.Disabled;
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        AddChild(content);

        content.AddChild(_commune.Row());

        var title = Text();
        title.Text = SheetText.Section("Part de chaque besoin couverte au dernier jour", PopulationText.NeedsTable)
            + SheetText.Muted("Cliquez sur une case pour voir pourquoi ça manque.");
        content.AddChild(title);

        _grid = new GridContainer { Columns = System.Enum.GetValues<Rank>().Length + 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 4);
        _grid.AddThemeConstantOverride("v_separation", 4);
        content.AddChild(_grid);

        content.AddChild(new HSeparator());
        _detail = Text();
        content.AddChild(_detail);
        content.AddChild(_causes);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PopulationNeedsPage() { }

    private static RichTextLabel Text()
    {
        var text = new HintText { BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SelectionEnabled = false };
        text.AddThemeColorOverride("default_color", UiTheme.Text);
        return text;
    }

    public void Refresh()
    {
        var communes = _view.Communes();
        _commune.Fill(communes);
        if (_commune.Button.ItemCount > 0)
            _commune.Button.SetItemText(0, "Toute la nation");
        var chosen = _commune.SelectedId is { } id ? communes.Where(c => c.Id == id).ToList() : communes;
        var stats = PopulationStats.Of(chosen);

        var needs = stats.NeedCells.Keys.Select(k => k.Need).Distinct()
            .OrderBy(n => System.Array.IndexOf(NeedOrder, n) is var i && i >= 0 ? i : int.MaxValue).ThenBy(n => n).ToList();
        var key = $"{_commune.SelectedId}|{_selected}|" + string.Join(";", stats.NeedCells.Select(c => $"{c.Key}:{c.Value.Coverage:P0}"));
        if (key != _gridKey)
        {
            _gridKey = key;
            BuildGrid(stats, needs);
        }

        var detail = Detail(chosen, stats);
        if (detail != _detail.Text)
            _detail.Text = detail;
        if (stats.NeedCells.TryGetValue(_selected, out var cell))
        {
            _causes.Visible = true;
            _causes.SetParts(
                [
                    new StackedBar.Part("Couvert", cell.Coverage, Good),
                    new StackedBar.Part("Faute d'argent", cell.MoneyShortfall, UiTheme.Accent),
                    new StackedBar.Part("Faute de produits ou de places", cell.SupplyShortage, UiTheme.Danger),
                ],
                (part, share) => $"{part.Name} {share:P0}");
        }
        else
        {
            _causes.Visible = false;
        }
    }

    private void BuildGrid(PopulationStats stats, IReadOnlyList<string> needs)
    {
        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }

        var ranks = System.Enum.GetValues<Rank>().Cast<Rank?>().Append(null).ToList();
        _grid.AddChild(new Label { Text = "", CustomMinimumSize = new Vector2(150, 0) });
        foreach (var rank in ranks)
        {
            var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            if (rank is { } r && IconRegistry.GetRankIcon(r, 16) is { } icon)
                header.AddChild(new TextureRect { Texture = icon, StretchMode = TextureRect.StretchModeEnum.KeepCentered, SizeFlagsVertical = SizeFlags.ShrinkCenter });
            var label = UiTheme.Caption(rank is { } rr ? ClassTransitions.RankName(rr) : "Tous");
            header.AddChild(label);
            _grid.AddChild(header);
        }

        if (needs.Count == 0)
        {
            _grid.AddChild(UiTheme.Caption("Aucun relevé encore : il se prend chaque jour joué."));
            return;
        }

        foreach (var need in needs)
        {
            _grid.AddChild(new Label { Text = FlowText.NeedName(need), TooltipText = PopulationText.NeedHint(need), MouseFilter = MouseFilterEnum.Pass });
            foreach (var rank in ranks)
                _grid.AddChild(Cell(stats, need, rank));
        }
    }

    /// <summary>Une case : la part couverte, sur un fond du vert (tout couvert) au rouge (rien) ; un clic la choisit.</summary>
    private Control Cell((string Need, Rank? Rank) key, PopulationStats stats)
    {
        var button = new Button { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 30) };
        if (!stats.NeedCells.TryGetValue(key, out var cell))
        {
            button.Text = "—";
            button.Disabled = true;
            button.TooltipText = "Ce rang n'a pas ce besoin, ou personne dans ce rang.";
            return button;
        }

        button.Text = $"{cell.Coverage:P0}";
        var color = UiTheme.Danger.Lerp(UiTheme.Accent, (float)System.Math.Clamp(cell.Coverage * 2, 0, 1));
        if (cell.Coverage > 0.5)
            color = UiTheme.Accent.Lerp(Good, (float)System.Math.Clamp((cell.Coverage - 0.5) * 2, 0, 1));
        var selected = key == _selected;
        foreach (var state in new[] { "normal", "hover", "pressed" })
        {
            var style = new StyleBoxFlat
            {
                BgColor = color with { A = state == "hover" ? 0.55f : 0.4f },
                BorderColor = selected ? UiTheme.Text : color,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            };
            style.SetBorderWidthAll(selected ? 2 : 0);
            button.AddThemeStyleboxOverride(state, style);
        }
        button.AddThemeColorOverride("font_color", UiTheme.Text);
        button.AddThemeColorOverride("font_hover_color", UiTheme.Text);
        button.TooltipText = $"Couvert {cell.Coverage:P0} · faute d'argent {cell.MoneyShortfall:P0} · faute de produits ou de places {cell.SupplyShortage:P0}";
        button.Pressed += () =>
        {
            _selected = key;
            _gridKey = "";
        };
        return button;
    }

    private Control Cell(PopulationStats stats, string need, Rank? rank) => Cell((need, rank), stats);

    /// <summary>Le vert d'un besoin couvert (le rouge est UiTheme.Danger, l'orange UiTheme.Accent).</summary>
    private static readonly Color Good = new("#46a758");

    /// <summary>Sous le tableau : la case choisie, ses causes, les groupes du rang, et ce que leurs manques produisent.</summary>
    private string Detail(IReadOnlyList<CommuneView> communes, PopulationStats stats)
    {
        var (need, rank) = _selected;
        var text = new StringBuilder(SheetText.Heading($"{FlowText.NeedName(need)} — {(rank is { } chosenRank ? ClassTransitions.RankName(chosenRank) : "tous les rangs")}"));
        if (!stats.NeedCells.TryGetValue(_selected, out var cell))
            return text + SheetText.Muted("Pas de relevé pour cette case : ce rang n'a pas ce besoin, ou personne dans ce rang.");

        text.AppendLine($"Couvert {cell.Coverage:P0} · {SheetText.Hint("faute d'argent", PopulationText.MoneyShortfall)} {cell.MoneyShortfall:P0} · "
            + $"{SheetText.Hint("faute de produits ou de places", PopulationText.SupplyShortage)} {cell.SupplyShortage:P0}");

        // Les groupes concernés, du plus mal servi au mieux servi.
        var groups = communes
            .SelectMany(c => c.Classes.Select(g => (Commune: c, Group: g)))
            .Where(x => (rank is null || x.Group.Rank == rank) && x.Group.NeedDetails?.ContainsKey(need) == true && x.Group.Population > 0)
            .OrderBy(x => x.Group.NeedDetails![need].Coverage)
            .ToList();
        text.Append(SheetText.Section($"Groupes ({groups.Count})"));
        text.Append("[table=6]");
        foreach (var header in new[] { "Groupe", "Commune", "Membres", "Couvert", "Argent", "Produits" })
            text.Append(PopulationText.Cell(header, header: true));
        foreach (var (commune, group) in groups)
        {
            var record = group.NeedDetails![need];
            text.Append(PopulationText.Cell($"{SheetText.Chips(group.Poles ?? [])} {SheetText.Escape(rank is null ? group.Name : group.IdeologyName)}"));
            text.Append(PopulationText.Cell(SheetText.Escape(commune.Name)));
            text.Append(PopulationText.Cell($"{group.Population:N0}"));
            text.Append(PopulationText.Cell($"{record.Coverage:P0}"));
            text.Append(PopulationText.Cell($"{record.MoneyShortfall:P0}"));
            text.Append(PopulationText.Cell($"{record.SupplyShortage:P0}"));
        }
        text.AppendLine("[/table]");

        // Ce que produisent les manques : la croissance (tous besoins) et les pôles vers lesquels ils poussent.
        text.Append(SheetText.Section("Ce que ça produit", PopulationText.Needs));
        var ranks = rank is { } only ? [only] : System.Enum.GetValues<Rank>();
        foreach (var r in ranks)
        {
            if (stats.NeedsByRank.GetValueOrDefault(r, double.NaN) is var satisfaction && double.IsNaN(satisfaction))
                continue;
            text.AppendLine($"{ClassTransitions.RankName(r)} : besoins couverts {satisfaction:P0} (tous besoins) → "
                + $"{PopulationText.Growth(satisfaction)} par saison");
        }

        var all = groups.Select(x => x.Group).ToList();
        var egalitarian = new Demos.Domain.Politics.Orientation(IdeologicalAxis.Hierarchy, Pole.A);
        var positivist = new Demos.Domain.Politics.Orientation(IdeologicalAxis.Production, Pole.A);
        var money = all.Where(g => g.MoneyShortfall >= ClassTransitions.MoneyShortfallThreshold && g.Poles?.Contains(egalitarian) != true).ToList();
        var supply = all.Where(g => g.SupplyShortage >= ClassTransitions.SupplyShortageThreshold && g.Poles?.Contains(positivist) != true).ToList();
        text.AppendLine(money.Count == 0
            ? SheetText.Muted($"Aucun groupe au-delà de {ClassTransitions.MoneyShortfallThreshold:P0} de manque d'argent sur la saison.")
            : $"Manque d'argent au-delà de {ClassTransitions.MoneyShortfallThreshold:P0} sur la saison : {money.Count} groupe(s) poussé(s) vers "
              + $"{SheetText.Chip(egalitarian)} égalitariste s'ils travaillent ({ClassTransitions.PropertyShareAffected:P0} des membres par saison).");
        text.AppendLine(supply.Count == 0
            ? SheetText.Muted($"Aucun groupe au-delà de {ClassTransitions.SupplyShortageThreshold:P0} de manque de produits sur la saison.")
            : $"Manque de produits au-delà de {ClassTransitions.SupplyShortageThreshold:P0} sur la saison : {supply.Count} groupe(s) poussé(s) vers "
              + $"{SheetText.Chip(positivist)} positiviste ({ClassTransitions.ProductionShareAffected:P0} des membres par saison).");
        return text.ToString();
    }
}
