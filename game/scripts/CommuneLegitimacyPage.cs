using System.Linq;
using System.Text;
using Demos.Application;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Communes → Légitimité (décidé le 23 novembre 2026), en lecture, pour la commune du menu : ce qu'elle produit par
/// jour et sa part du pays ; le détail par pôle, par groupe (membres, pôles, adhésion, produit) et par loi (efficacité
/// de la loi dans la commune, produit par pôle). Moyennes sur les jours relevés (30 au plus).
/// </summary>
public partial class CommuneLegitimacyPage : ScrollContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly CommuneFocus _focus = null!;
    private readonly RichTextLabel _text = null!;
    private (LegitimacyLedgerView?, string?) _shown;

    public CommuneLegitimacyPage(GameView view, CommuneFocus focus)
    {
        _view = view;
        _focus = focus;
        HorizontalScrollMode = ScrollMode.Disabled;
        _text = LegitimacyPage.Text();
        AddChild(_text);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public CommuneLegitimacyPage() { }

    public void Refresh()
    {
        var ledger = _view.LegitimacyLedger();
        var communes = _view.Communes();
        var commune = _focus.Current(communes);
        // Le texte n'est remis que si le relevé ou la commune ont changé : sinon la page remonterait en haut à chaque image.
        if (_shown == (ledger, commune?.Id))
            return;
        _shown = (ledger, commune?.Id);
        _text.Text = commune is null ? SheetText.Muted("Aucune commune.") : Describe(ledger, commune);
    }

    private string Describe(LegitimacyLedgerView ledger, CommuneView commune)
    {
        var days = System.Math.Max(1, ledger.Days);
        var gains = ledger.Gains.Where(g => g.CommuneId == commune.Id).ToList();
        var total = gains.Sum(g => g.Total) / days;
        var nation = ledger.Gains.Sum(g => g.Total) / days;

        var text = new StringBuilder(SheetText.Heading($"LÉGITIMITÉ PRODUITE À {commune.Name.ToUpperInvariant()}"));
        text.AppendLine($"{SheetText.Hint("Par jour", LegitimacyText.PerDay)} {LegitimacyText.Signed(total)}"
            + (nation > 0 ? $" · {total / nation:P0} du pays" : ""));
        text.AppendLine(SheetText.Muted(ledger.Days == 0
            ? "Aucun jour joué encore : les gains s'affichent après le premier jour."
            : $"Moyenne sur les {ledger.Days} derniers jours joués."));
        text.AppendLine(string.Join("   ", Orientation.All.Select(pole =>
            $"{LegitimacyText.PoleCode(pole)} {LegitimacyText.Signed(gains.Where(g => g.Pole == pole).Sum(g => g.Total) / days)}")));

        text.Append(SheetText.Section("Par groupe", LegitimacyText.Produced));
        text.Append("[table=5]");
        foreach (var (header, hint) in new[]
                 {
                     ("Groupe", ""), ("Membres", ""), ("Pôles", ""), ("Adhésion", LegitimacyText.Adhesion), ("Produit/jour", LegitimacyText.Produced),
                 })
            text.Append(PopulationText.Cell(hint == "" ? header : SheetText.Hint(header, hint), header: true));
        foreach (var group in commune.Classes.OrderByDescending(c => gains.Where(g => g.ClassId == c.Id).Sum(g => g.Total)))
        {
            text.Append(PopulationText.Cell(SheetText.Escape(group.Name)));
            text.Append(PopulationText.Cell($"{group.Population:N0}"));
            text.Append(PopulationText.Cell(string.Join(" ", (group.Poles ?? []).Select(LegitimacyText.PoleCode))));
            text.Append(PopulationText.Cell($"{group.Adhesion:P0}"));
            text.Append(PopulationText.Cell(LegitimacyText.Signed(gains.Where(g => g.ClassId == group.Id).Sum(g => g.Total) / days)));
        }
        text.Append("[/table]\n");

        text.Append(SheetText.Section("Par loi", LegitimacyText.ByLaw));
        // Les lois jugées par les groupes de la commune, même celles qu'ils rejettent (elles ne rapportent rien ici).
        var lawIds = commune.Classes.SelectMany(c => (c.LawStances ?? []).Select(s => (s.LawId, s.LawName)))
            .Concat(gains.Select(g => (g.LawId, g.LawName))).DistinctBy(l => l.LawId)
            .OrderByDescending(l => gains.Where(g => g.LawId == l.LawId).Sum(g => g.Total)).ToList();
        if (lawIds.Count == 0)
        {
            text.AppendLine(SheetText.Muted("Aucune loi ne touche les groupes de cette commune."));
            return text.ToString();
        }
        text.Append("[table=3]");
        text.Append(PopulationText.Cell("Loi", header: true));
        text.Append(PopulationText.Cell(SheetText.Hint("Efficacité ici", LegitimacyText.Efficiency), header: true));
        text.Append(PopulationText.Cell(SheetText.Hint("Produit/jour", LegitimacyText.ByLaw), header: true));
        foreach (var (lawId, lawName) in lawIds)
        {
            var byPole = Orientation.All
                .Select(pole => (Pole: pole, Value: gains.Where(g => g.LawId == lawId && g.Pole == pole).Sum(g => g.Total) / days))
                .Where(p => p.Value >= 0.005).ToList();
            text.Append(PopulationText.Cell(SheetText.Escape(lawName)));
            text.Append(PopulationText.Cell($"{_view.LawEfficiencyIn(lawId, commune.Id):P0}"));
            text.Append(PopulationText.Cell(byPole.Count == 0
                ? SheetText.Muted("rien ou presque")
                : string.Join("   ", byPole.Select(p => $"{LegitimacyText.PoleCode(p.Pole)} {LegitimacyText.Signed(p.Value)}"))));
        }
        text.Append("[/table]\n");
        return text.ToString();
    }
}
