using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Population;
using Godot;

namespace Demos.Game;

/// <summary>
/// Population → Groupes sociaux, en lecture : à gauche les communes, leurs rangs (ouvriers, professions
/// intermédiaires, cadres), chacun repliable, et dans chaque rang ses groupes locaux (une classe dans une commune) avec
/// les pastilles de leurs pôles ; à droite la fiche de ce qui est choisi. La fiche d'une commune ou d'un rang liste ses
/// groupes ; celle d'un groupe donne son rang, son idéologie, ses membres et chômeurs, sa richesse, ses revenus et
/// impôts de la saison, ses besoins, ses manques, son adhésion, son éducation et ses lieux de travail.
/// </summary>
public partial class PopulationGroupsPage : HBoxContainer, IMenuPage
{
    private const string CommuneKey = "commune:";
    private const string RankKey = "rang:";
    private const string GroupKey = "groupe:";

    private readonly GameView _view = null!;
    private readonly TreeSheet _sheet = new();
    private IReadOnlyList<CommuneView> _communes = [];

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

    private static string KeyOf(CommuneView commune, Rank rank) => $"{RankKey}{commune.Id}:{rank}";

    private static IEnumerable<SocialClassView> Ordered(IEnumerable<SocialClassView> groups) =>
        groups.OrderBy(g => g.Rank).ThenByDescending(g => g.Population);

    private void Build(TreeItem root)
    {
        foreach (var commune in _communes)
        {
            var communeIcon = IconRegistry.GetCategoryIcon("Administration", 20);
            var item = _sheet.Item(root, CommuneKey + commune.Id, $"{commune.Name} ({commune.Classes.Count})", icon: communeIcon);
            // Un rang par branche repliable, seulement s'il a des groupes dans la commune.
            foreach (var rank in commune.Classes.Select(g => g.Rank).Distinct().Order())
            {
                var groups = commune.Classes.Where(g => g.Rank == rank).ToList();
                var rankIcon = IconRegistry.GetRankIcon(rank, 20);
                var rankItem = _sheet.Item(item, KeyOf(commune, rank),
                    $"{ClassTransitions.RankName(rank)} ({groups.Count}) · {groups.Sum(g => g.Population):N0}", icon: rankIcon);
                foreach (var group in Ordered(groups))
                {
                    var poleIcon = group.Poles?.FirstOrDefault() is { } primaryPole
                        ? IconRegistry.GetPoleIcon(primaryPole, 18)
                        : rankIcon;
                    var groupItem = _sheet.Item(rankItem, GroupKey + group.Id, $"{group.IdeologyName} ({group.Population:N0})", icon: poleIcon);
                    TreeSheet.AddPoleChips(groupItem, group.Poles ?? [], "Pôle du groupe");
                }
            }
        }
    }

    private string Describe(string key)
    {
        foreach (var commune in _communes)
        {
            if (key == CommuneKey + commune.Id)
                return DescribeGroups(commune.Name.ToUpperInvariant(), commune.Classes, "Aucun groupe local : personne n'habite ici.");
            foreach (var rank in System.Enum.GetValues<Rank>())
                if (key == KeyOf(commune, rank))
                    return DescribeGroups($"{ClassTransitions.RankName(rank)} — {commune.Name}", commune.Classes.Where(g => g.Rank == rank).ToList(),
                        "Plus aucun groupe de ce rang dans la commune.", PopulationText.Rank(rank), byIdeology: true);
            if (commune.Classes.FirstOrDefault(g => GroupKey + g.Id == key) is { } group)
                return DescribeGroup(commune, group);
        }
        return SheetText.Muted("Ce groupe n'existe plus : ses membres ont rejoint d'autres classes ou d'autres communes.");
    }

    /// <summary>
    /// La fiche d'une commune ou d'un rang : ses groupes, leurs membres, leurs besoins et leur adhésion. Dans un rang, un
    /// groupe se nomme par son idéologie (le rang est dans le titre).
    /// </summary>
    private static string DescribeGroups(string title, IReadOnlyList<SocialClassView> groups, string empty, string? hint = null, bool byIdeology = false)
    {
        var text = new StringBuilder(SheetText.Heading(title));
        if (hint is not null)
            text.AppendLine(SheetText.Muted(hint)).AppendLine();
        if (groups.Count == 0)
            return text + SheetText.Muted(empty);

        text.AppendLine($"{SheetText.Hint("Membres", PopulationText.Members)} {groups.Sum(g => g.Population):N0} · "
            + $"{SheetText.Hint("chômeurs", PopulationText.Unemployed)} {groups.Sum(g => g.Unemployed):N0}");
        text.Append("[table=4]");
        foreach (var header in new[] { "Groupe", "Membres", "Besoins", "Adhésion" })
            text.Append(PopulationText.Cell(header, header: true));
        foreach (var group in Ordered(groups))
        {
            text.Append(PopulationText.Cell($"{SheetText.Chips(group.Poles ?? [])}  {SheetText.Escape(byIdeology ? group.IdeologyName : group.Name)}"));
            text.Append(PopulationText.Cell($"{group.Population:N0}"));
            text.Append(PopulationText.Cell($"{group.NeedsSatisfaction:P0}"));
            text.Append(PopulationText.Cell($"{group.Adhesion:P0}"));
        }
        text.AppendLine("[/table]");
        return text.ToString();
    }

    private string DescribeGroup(CommuneView commune, SocialClassView group)
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
        // Les objets durables, besoin par besoin, par personne.
        if (group.Possessions is { Count: > 0 } possessions && group.Population > 0)
        {
            text.AppendLine(SheetText.Hint("Possessions", PopulationText.Possessions));
            foreach (var possession in possessions)
                text.AppendLine($"    {FlowText.NeedName(possession.NeedId)} : {possession.Owned / group.Population:N1} par personne sur "
                    + $"{possession.Target / group.Population:N1} visés · "
                    + SheetText.Muted($"usure {possession.WearPerDay / group.Population:N3} par jour"));
        }

        text.Append(SheetText.Section("Politique et éducation"));
        text.AppendLine($"{SheetText.Hint("Adhésion", PopulationText.Adhesion)} {group.Adhesion:P0} · "
            + $"{SheetText.Hint("éducation", PopulationText.Education)} {group.EducationPoints:N0} (avancée {group.AdvancedEducationPoints:N0})");

        text.Append(SheetText.Section("Travaille à", PopulationText.Workplaces));
        var workplaces = group.Workplaces ?? [];
        text.AppendLine(workplaces.Count == 0
            ? SheetText.Muted("Nulle part pour l'instant.")
            : string.Join(" · ", workplaces.Select(w => $"{SheetText.Structure(_view, w.StructureId)} {w.Members:N0}")));
        return text.ToString();
    }
}
