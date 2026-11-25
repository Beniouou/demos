using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Economy;
using Godot;

namespace Demos.Game;

/// <summary>
/// Économie → Budget (décidé le 25 novembre 2026) : en haut, poste par poste, l'enveloppe votée, ce qui est dépensé
/// depuis le 1er janvier, ce qui reste et la dépense de l'année au rythme actuel ; puis le brouillon du prochain budget,
/// son coût en légitimité et le vote ; en bas, la caisse de chaque commune et sa dotation du mois.
/// </summary>
public partial class BudgetPage : ScrollContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly RichTextLabel _summary = null!;
    private readonly Dictionary<BudgetItem, SpinBox> _spins = [];
    private readonly Dictionary<BudgetItem, Label> _voted = [];
    private readonly Dictionary<BudgetItem, Label> _costs = [];
    private readonly Label _totalCost = null!;
    private readonly Button _vote = null!;
    private readonly Button _cancel = null!;
    private readonly Button _reset = null!;
    private readonly RichTextLabel _pending = null!;
    private readonly Label _message = null!;
    private readonly RichTextLabel _funds = null!;
    private string _summaryText = "", _fundsText = "", _pendingText = "";
    private bool _filling;

    public BudgetPage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        HorizontalScrollMode = ScrollMode.Disabled;
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        AddChild(content);

        _summary = LegitimacyPage.Text();
        content.AddChild(_summary);

        var draftTitle = LegitimacyPage.Text();
        draftTitle.Text = SheetText.Section("Prochain budget (brouillon)", BudgetText.Draft);
        content.AddChild(draftTitle);

        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var header in new[] { "Poste", "Voté", "Nouveau", "Coût" })
            grid.AddChild(UiTheme.Caption(header));
        foreach (var item in BudgetItems.All)
        {
            var name = LegitimacyPage.Text();
            name.Text = SheetText.Hint(BudgetText.Label(item), BudgetText.Hint(item));
            name.CustomMinimumSize = new Vector2(220, 0);
            grid.AddChild(name);

            var voted = new Label { HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(90, 0) };
            _voted[item] = voted;
            grid.AddChild(voted);

            var perResident = BudgetItems.IsPerResident(item);
            var spin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 100_000_000,
                Step = perResident ? 1 : 1_000,
                Suffix = perResident ? "/hab." : "",
                AllowGreater = true,
                CustomMinimumSize = new Vector2(140, 0),
                Alignment = HorizontalAlignment.Right,
            };
            var budgetItem = item;
            spin.ValueChanged += value =>
            {
                if (_filling)
                    return;
                _desk.SetBudgetDraft(budgetItem, value);
                _message.Visible = false;
                Refresh();
            };
            _spins[item] = spin;
            grid.AddChild(spin);

            var cost = new Label { TooltipText = BudgetText.Cost };
            _costs[item] = cost;
            grid.AddChild(cost);
        }
        content.AddChild(grid);

        _totalCost = new Label { TooltipText = BudgetText.Cost };
        content.AddChild(_totalCost);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        _vote = new Button { Text = "Voter le budget", FocusMode = FocusModeEnum.None, Icon = IconRegistry.GetMenuIcon("Lois", 18) };
        _vote.Pressed += () => Say(_desk.VoteBudget());
        buttons.AddChild(_vote);
        _reset = new Button { Text = "Repartir du budget en vigueur", FocusMode = FocusModeEnum.None };
        _reset.Pressed += () =>
        {
            _desk.ResetBudgetDraft();
            _message.Visible = false;
            Refresh();
        };
        buttons.AddChild(_reset);
        _cancel = new Button { Text = "Annuler le vote", FocusMode = FocusModeEnum.None };
        _cancel.Pressed += () => Say(_desk.CancelBudgetVote());
        buttons.AddChild(_cancel);
        content.AddChild(buttons);

        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _message.AddThemeColorOverride("font_color", UiTheme.Accent);
        content.AddChild(_message);

        _pending = LegitimacyPage.Text();
        content.AddChild(_pending);

        _funds = LegitimacyPage.Text();
        content.AddChild(_funds);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public BudgetPage() { }

    private void Say(string message)
    {
        _message.Text = message;
        _message.Visible = true;
        Refresh();
    }

    public void Refresh()
    {
        var budget = _view.Budget();

        // Les textes ne sont remis que s'ils ont changé : sinon la page remonterait en haut à chaque image.
        Set(_summary, ref _summaryText, Summary(budget));
        Set(_pending, ref _pendingText, Pending(budget));
        Set(_funds, ref _fundsText, Funds(budget));

        _filling = true;
        foreach (var line in budget.Lines)
        {
            _voted[line.Item].Text = Amount(line, line.Voted);
            var spin = _spins[line.Item];
            if (!spin.GetLineEdit().HasFocus() && System.Math.Abs(spin.Value - line.Draft) > 0.5)
                spin.Value = line.Draft;
            _costs[line.Item].Text = line.Cost > 0 ? $"{line.Cost:0.#} {line.Pole.Code}" : "–";
        }
        _filling = false;

        _totalCost.Text = budget.DraftChanged
            ? $"Coût du vote : {(budget.DraftCost.Count == 0 ? "rien (aucun poste n'augmente)" : string.Join(", ", budget.DraftCost.Select(c => $"{c.Value:0.#} {c.Key.Code}")))}"
            : "Le brouillon est le budget en vigueur.";
        _vote.Disabled = budget.Pending is not null || !budget.DraftChanged;
        _reset.Disabled = !budget.DraftChanged;
        _cancel.Visible = budget.Pending is not null;
    }

    /// <summary>Une enveloppe, ou un montant par habitant pour les dotations.</summary>
    private static string Amount(BudgetLineView line, double value) => line.PerResident ? $"{value:N0} /hab." : $"{value:N0}";

    private static void Set(RichTextLabel label, ref string shown, string text)
    {
        if (text == shown)
            return;
        shown = text;
        label.Text = text;
    }

    private static string Summary(BudgetView budget)
    {
        var text = new StringBuilder(SheetText.Heading($"BUDGET DE L'ÉTAT — {budget.Year}"));
        text.AppendLine($"Trésor : {budget.Treasury:N0}   " + SheetText.Muted($"dépenses comptées depuis le {WorldText.DayLabel(budget.CountedSince)}"));
        text.Append("[table=5]");
        foreach (var (header, hint) in new[]
                 {
                     ("Poste", ""), ("Voté (an)", BudgetText.Voted), ("Dépensé", BudgetText.Spent),
                     ("Disponible", BudgetText.Available), ("Rythme annuel", BudgetText.Projected),
                 })
            text.Append(PopulationText.Cell(hint == "" ? header : SheetText.Hint(header, hint), header: true));
        foreach (var line in budget.Lines)
        {
            var short_ = !line.PerResident && line.Projected > line.Voted + 0.5;
            text.Append(PopulationText.Cell(SheetText.Hint(BudgetText.Label(line.Item), BudgetText.Hint(line.Item))));
            text.Append(PopulationText.Cell(line.PerResident
                ? SheetText.Hint($"{line.VotedTotal:N0}", $"{line.Voted:N0} par habitant et par an × {budget.Residents:N0} habitants aujourd'hui.")
                : $"{line.VotedTotal:N0}"));
            text.Append(PopulationText.Cell($"{line.Spent:N0}"));
            text.Append(PopulationText.Cell($"{line.Available:N0}"));
            text.Append(PopulationText.Cell(short_
                ? $"[color=#{UiTheme.Danger.ToHtml(false)}]{line.Projected:N0} ⚠[/color]"
                : $"{line.Projected:N0}"));
        }
        text.Append(PopulationText.Cell("Total", header: true));
        text.Append(PopulationText.Cell($"{budget.Lines.Sum(l => l.VotedTotal):N0}"));
        text.Append(PopulationText.Cell($"{budget.Lines.Sum(l => l.Spent):N0}"));
        text.Append(PopulationText.Cell($"{budget.Lines.Sum(l => l.Available):N0}"));
        text.Append(PopulationText.Cell($"{budget.Lines.Sum(l => l.Projected):N0}"));
        text.Append("[/table]");
        return text.ToString();
    }

    private static string Pending(BudgetView budget)
    {
        if (budget.Pending is not { } pending)
            return "";
        var text = new StringBuilder(SheetText.Section("Budget voté, en vigueur le 1er du mois prochain"));
        text.AppendLine(string.Join("   ", BudgetItems.All.Select(i =>
            $"{BudgetText.Name(i)} {pending[i]:N0}{(BudgetItems.IsPerResident(i) ? " par habitant" : "")}")));
        if (budget.PendingPaid.Count > 0)
            text.AppendLine(SheetText.Muted("Payé : " + string.Join(", ", budget.PendingPaid.Select(c => $"{c.Value:0.#} {c.Key.Code}"))
                + " (rendu si le vote est annulé)."));
        return text.ToString();
    }

    private static string Funds(BudgetView budget)
    {
        var text = new StringBuilder(SheetText.Section("Caisses des communes", BudgetText.Funds));
        if (budget.Communes.Count == 0)
            return text.Append(SheetText.Muted("Aucune commune.")).ToString();
        text.Append("[table=4]");
        text.Append(PopulationText.Cell("Commune", header: true));
        text.Append(PopulationText.Cell("Habitants", header: true));
        text.Append(PopulationText.Cell("Caisse", header: true));
        text.Append(PopulationText.Cell("Dotation du mois", header: true));
        foreach (var commune in budget.Communes)
        {
            text.Append(PopulationText.Cell(SheetText.Escape(commune.Name)));
            text.Append(PopulationText.Cell($"{commune.Residents:N0}"));
            text.Append(PopulationText.Cell($"{commune.Fund:N0}"));
            text.Append(PopulationText.Cell($"{commune.MonthlyGrant:N0}"));
        }
        text.Append("[/table]");
        return text.ToString();
    }
}
