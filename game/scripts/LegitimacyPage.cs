using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Population → Légitimité (décidé le 23 novembre 2026), en lecture, pour tout le pays : pôle par pôle, le stock, le
/// gain moyen par jour, ce qui a été gagné et dépensé sur les 30 derniers jours ; le gain de chaque loi ; les dépenses
/// récentes ; la courbe du stock de chaque pôle (<see cref="LegitimacyHistory"/>).
/// </summary>
public partial class LegitimacyPage : ScrollContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly LegitimacyHistory _history = null!;
    private readonly RichTextLabel _text = null!;
    private readonly LineChart _chart = new();
    private LegitimacyLedgerView? _shownLedger;
    private string _chartKey = "";

    public LegitimacyPage(GameView view, LegitimacyHistory history)
    {
        _view = view;
        _history = history;
        HorizontalScrollMode = ScrollMode.Disabled;
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        AddChild(content);

        _text = Text();
        content.AddChild(_text);
        var title = Text();
        title.Text = SheetText.Section("Évolution du stock", LegitimacyText.History);
        content.AddChild(title);
        content.AddChild(_chart);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public LegitimacyPage() { }

    /// <summary>Un texte BBCode à bulles d'aide, à la hauteur de son contenu.</summary>
    public static RichTextLabel Text()
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
        var ledger = _view.LegitimacyLedger();
        // Le texte n'est remis que si le relevé a changé : sinon la page remonterait en haut à chaque image.
        if (!ReferenceEquals(ledger, _shownLedger))
        {
            _shownLedger = ledger;
            _text.Text = Describe(ledger);
        }

        var snapshots = _history.Snapshots;
        var key = $"{snapshots.Count}|{snapshots.LastOrDefault()?.Date}";
        if (key == _chartKey)
            return;
        _chartKey = key;
        _chart.SetData(snapshots.Select(s => s.DayLabel).ToList(),
            Orientation.All.Select(pole => new LineChart.Series($"{pole.Code} {WorldText.PoleName(pole)}", UiTheme.PoleColor(pole),
                snapshots.Select(s => s.Stocks.GetValueOrDefault(pole)).ToList())).ToList(),
            v => v.ToString("N0"));
    }

    private string Describe(LegitimacyLedgerView ledger)
    {
        var days = System.Math.Max(1, ledger.Days);
        var text = new StringBuilder(SheetText.Heading("LÉGITIMITÉ DU PAYS"));
        text.AppendLine(SheetText.Muted(ledger.Days == 0
            ? "Aucun jour joué encore : les gains s'affichent après le premier jour."
            : $"Sur les {ledger.Days} derniers jours joués."));

        text.Append("[table=6]");
        foreach (var (header, hint) in new[]
                 {
                     ("Pôle", ""), ("Stock", LegitimacyText.Stock), ("Gain/jour", LegitimacyText.PerDay),
                     ("Gagné", LegitimacyText.Earned), ("Dépensé", LegitimacyText.Spent), ("Solde", LegitimacyText.Balance),
                 })
            text.Append(PopulationText.Cell(hint == "" ? header : SheetText.Hint(header, hint), header: true));
        foreach (var pole in Orientation.All)
        {
            var earned = ledger.Gains.Where(g => g.Pole == pole).Sum(g => g.Total);
            var spent = ledger.Spendings.Where(s => s.Pole == pole).Sum(s => s.Amount);
            text.Append(PopulationText.Cell(LegitimacyText.Pole(pole)));
            text.Append(PopulationText.Cell($"{_view.LegitimacyOf(pole):N1}"));
            text.Append(PopulationText.Cell(LegitimacyText.Signed(earned / days)));
            text.Append(PopulationText.Cell(LegitimacyText.Signed(earned, "N1")));
            text.Append(PopulationText.Cell(LegitimacyText.Signed(-spent, "N1")));
            text.Append(PopulationText.Cell(LegitimacyText.Signed(earned - spent, "N1")));
        }
        text.Append("[/table]\n");

        text.Append(SheetText.Section("D'où vient le gain (par jour)", LegitimacyText.ByLaw));
        var laws = ledger.Gains.GroupBy(g => g.LawId).OrderByDescending(l => l.Sum(g => g.Total)).ToList();
        if (laws.Count == 0)
            text.AppendLine(SheetText.Muted("Aucune loi n'a rapporté de légitimité."));
        else
        {
            text.Append("[table=7]");
            text.Append(PopulationText.Cell("Loi", header: true));
            foreach (var pole in Orientation.All)
                text.Append(PopulationText.Cell(LegitimacyText.PoleCode(pole), header: true));
            foreach (var law in laws)
            {
                text.Append(PopulationText.Cell(SheetText.Escape(law.First().LawName)));
                foreach (var pole in Orientation.All)
                    text.Append(PopulationText.Cell(LegitimacyText.Signed(law.Where(g => g.Pole == pole).Sum(g => g.Total) / days)));
            }
            text.Append("[/table]\n");
        }

        text.Append(SheetText.Section("Dépenses récentes", LegitimacyText.Spent));
        if (ledger.Spendings.Count == 0)
            text.AppendLine(SheetText.Muted("Aucune dépense sur ces jours."));
        foreach (var spending in ledger.Spendings)
            text.AppendLine($"{WorldText.DayLabel(spending.Date)} · {LegitimacyText.Spending(spending)} · "
                + $"{LegitimacyText.Signed(-spending.Amount, "N1")} {LegitimacyText.PoleCode(spending.Pole)}");
        return text.ToString();
    }
}
