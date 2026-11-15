using System.Linq;
using System.Text;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Population → Groupes sociaux, en lecture : à gauche les communes et leurs groupes locaux (une classe dans une
/// commune), avec les pastilles de leurs pôles ; à droite la fiche du groupe choisi : rang, idéologie, membres et
/// chômeurs, richesse, revenus et impôts de la saison, besoins, manques, adhésion, éducation et lieux de travail. La
/// fiche d'une commune liste ses groupes.
/// </summary>
public partial class PopulationGroupsPage : HBoxContainer, IMenuPage
{
    private const string CommuneKey = "commune:";
    private const string GroupKey = "groupe:";

    private readonly GameView _view = null!;
    private readonly TreeSheet _sheet = new();
    private System.Collections.Generic.IReadOnlyList<CommuneView> _communes = [];

    public PopulationGroupsPage(GameView view)
    {
        _view = view;
        AddThemeConstantOverride("separation", 12);
        AddChild(_sheet.Tree);
        AddChild(new VSeparator());
        AddChild(_sheet.Sheet);
        _sheet.Build = Build;
        _sheet.Describe = Describe;
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PopulationGroupsPage() { }

    public void Refresh()
    {
        _communes = _view.Communes();
        if (_sheet.Selected is null && _communes.FirstOrDefault() is { } first)
        {
            _sheet.Expand(CommuneKey + first.Id);
            _sheet.Selected = CommuneKey + first.Id;
        }
        // Le préfixe garde le schéma différent du schéma vide de départ, même sans commune.
        _sheet.Refresh("groupes|" + string.Join("|", _communes.Select(c => c.Id + ":" + string.Join(",", c.Classes.Select(g => g.Id)))));
    }

    private void Build(TreeItem root)
    {
        foreach (var commune in _communes)
        {
            var item = _sheet.Item(root, CommuneKey + commune.Id, $"{commune.Name} ({commune.Classes.Count})");
            foreach (var group in commune.Classes.OrderBy(g => g.Rank).ThenByDescending(g => g.Population))
            {
                var groupItem = _sheet.Item(item, GroupKey + group.Id, group.Name);
                TreeSheet.AddPoleChips(groupItem, group.Poles ?? [], "Pôle du groupe");
            }
        }
    }

    private string Describe(string key)
    {
        if (key.StartsWith(CommuneKey) && _communes.FirstOrDefault(c => CommuneKey + c.Id == key) is { } commune)
            return DescribeCommune(commune);
        foreach (var home in _communes)
            if (home.Classes.FirstOrDefault(g => GroupKey + g.Id == key) is { } group)
                return DescribeGroup(home, group);
        return SheetText.Muted("Ce groupe n'existe plus : ses membres ont rejoint d'autres classes ou d'autres communes.");
    }

    private static string DescribeCommune(CommuneView commune)
    {
        var text = new StringBuilder(SheetText.Heading(commune.Name.ToUpperInvariant()));
        if (commune.Classes.Count == 0)
            return text + SheetText.Muted("Aucun groupe local : personne n'habite ici.");

        text.Append("[table=4]");
        foreach (var header in new[] { "Groupe", "Membres", "Besoins", "Adhésion" })
            text.Append(PopulationText.Cell(header, header: true));
        foreach (var group in commune.Classes.OrderBy(g => g.Rank).ThenByDescending(g => g.Population))
        {
            text.Append(PopulationText.Cell($"{SheetText.Chips(group.Poles ?? [])}  {SheetText.Escape(group.Name)}"));
            text.Append(PopulationText.Cell($"{group.Population:N0}"));
            text.Append(PopulationText.Cell($"{group.NeedsSatisfaction:P0}"));
            text.Append(PopulationText.Cell($"{group.Adhesion:P0}"));
        }
        text.AppendLine("[/table]");
        return text.ToString();
    }

    private static string DescribeGroup(CommuneView commune, SocialClassView group)
    {
        var text = new StringBuilder(SheetText.Heading($"{group.Name} — {commune.Name}"));
        text.AppendLine($"{SheetText.Hint("Rang", PopulationText.Rank(group.Rank))} {PopulationText.RankName(group.Rank)} · "
            + $"{SheetText.Hint("idéologie", PopulationText.Ideology(group.IdeologyName))} {SheetText.Escape(group.IdeologyName.ToLowerInvariant())} "
            + SheetText.Chips(group.Poles ?? []));
        text.AppendLine($"{SheetText.Hint("Membres", PopulationText.Members)} {group.Population:N0} · "
            + $"{SheetText.Hint("chômeurs", PopulationText.Unemployed)} {group.Unemployed:N0} "
            + SheetText.Muted($"({(group.Population > 0 ? group.Unemployed / group.Population : 0):P0})"));
        text.AppendLine($"{SheetText.Hint("Richesse", PopulationText.Wealth)} {group.Wealth:N0} ({group.WealthPerMember:N1} par membre)");
        text.AppendLine($"{SheetText.Hint("Revenus de la saison", PopulationText.Taxes)} {group.SeasonIncome:N0} · impôts payés {group.SeasonTaxes:N0}"
            + (group.SeasonIncome > 0 ? SheetText.Muted($" ({group.SeasonTaxes / group.SeasonIncome:P0} des revenus)") : ""));

        text.Append(SheetText.Section("Besoins"));
        text.AppendLine($"{SheetText.Hint("Besoins couverts", PopulationText.Needs)} {group.NeedsSatisfaction:P0} · "
            + $"{SheetText.Hint("logés", PopulationText.Housed)} {group.HousingCoverage:P0} "
            + SheetText.Muted($"(sans place : {group.HousingPlaceShortage:P0})", PopulationText.HousingShortage));
        text.AppendLine($"{SheetText.Hint("Manque d'argent", PopulationText.MoneyShortfall)} {group.MoneyShortfall:P0} · "
            + $"{SheetText.Hint("manque de produits", PopulationText.SupplyShortage)} {group.SupplyShortage:P0}");

        text.Append(SheetText.Section("Politique et éducation"));
        text.AppendLine($"{SheetText.Hint("Adhésion", PopulationText.Adhesion)} {group.Adhesion:P0} · "
            + $"{SheetText.Hint("éducation", PopulationText.Education)} {group.EducationPoints:N0} (avancée {group.AdvancedEducationPoints:N0})");

        text.Append(SheetText.Section("Travaille à", PopulationText.Workplaces));
        var workplaces = group.Workplaces ?? [];
        text.AppendLine(workplaces.Count == 0
            ? SheetText.Muted("Nulle part pour l'instant.")
            : string.Join(" · ", workplaces.Select(w => $"{SheetText.Escape(w.Name)} {w.Members:N0}")));
        return text.ToString();
    }
}
