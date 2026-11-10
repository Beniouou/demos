using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Structure → Entité politique, d'après la section « Implication de l'idéologie » du canevas : à gauche, le schéma des
/// structures emboîtées (État, communes, organismes, bâtiments) et des lois créées à chaque niveau ; à droite, la
/// fiche de ce qu'on a choisi, avec toutes les lois qui s'y appliquent. Chaque boîte se replie sur son titre ; un clic
/// sur le titre la déroule (ou la replie) et affiche sa fiche.
/// </summary>
public partial class StructurePage : HBoxContainer, IMenuPage
{
    // Couleurs des titres du canevas : rouge pour les entités politiques, orange pour les organismes. Un bâtiment porte
    // le carré de couleur de son propriétaire (État, SCOP ou privé), comme dans le menu Production.
    private static readonly Color EntityColor = new("#d9534f");
    private static readonly Color UnitColor = new("#e8913a");
    private static readonly Color GroupColor = new("#6b7280");

    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private Tree _tree = null!;
    private RichTextLabel _sheet = null!;
    private readonly HashSet<string> _expanded = [];
    private string? _selected;
    private string _shape = "";

    public StructurePage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 12);

        _tree = new Tree
        {
            HideRoot = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
        };
        _tree.AddThemeColorOverride("font_color", UiTheme.Text);
        _tree.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        // Un seul clic déroule ou replie la boîte et affiche sa fiche.
        _tree.ItemMouseSelected += (_, _) => Choose(_tree.GetSelected());
        // La petite flèche du schéma replie aussi une boîte : on s'en souvient quand le schéma est refait.
        _tree.ItemCollapsed += item =>
        {
            if (item.GetMetadata(0).AsString() is not { Length: > 0 } key)
                return;
            if (item.Collapsed)
                _expanded.Remove(key);
            else
                _expanded.Add(key);
        };
        // Le schéma, avec au-dessus la légende des carrés de couleur des bâtiments.
        var diagram = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        diagram.AddChild(UiTheme.OwnershipLegend());
        diagram.AddChild(_tree);
        AddChild(diagram);

        AddChild(new VSeparator());

        _sheet = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectionEnabled = false,
        };
        _sheet.AddThemeColorOverride("default_color", UiTheme.Text);
        AddChild(_sheet);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public StructurePage() { }

    public void Refresh()
    {
        var roots = _view.StructureTree();
        var shape = Shape(roots);
        if (shape != _shape)
        {
            // Au premier affichage, l'État et la première commune (avec ce qui la contient) sont déroulés, le reste replié.
            if (_shape == "")
            {
                foreach (var root in roots)
                {
                    _expanded.Add(root.Id);
                    foreach (var id in PathToFirstCommune(root))
                        _expanded.Add(id);
                }
                _selected ??= roots.FirstOrDefault()?.Id;
            }
            _shape = shape;
            Build(roots);
        }

        _sheet.Text = Describe(_selected, roots);
    }

    /// <summary>
    /// Affiche la fiche d'une structure (cliquée sur la carte) : ce qui la contient est déroulé, elle est choisie, et le
    /// schéma défile jusqu'à elle.
    /// </summary>
    public void Show(string structureId)
    {
        Refresh();
        var roots = _view.StructureTree();
        foreach (var id in PathTo(roots, structureId))
            _expanded.Add(id);
        _selected = structureId;
        Build(roots);
        _sheet.Text = Describe(_selected, roots);
        if (_tree.GetSelected() is { } item)
            _tree.ScrollToItem(item);
    }

    /// <summary>Les structures qui contiennent celle-ci, de l'État jusqu'à elle (vide si elle n'existe pas).</summary>
    private static List<string> PathTo(IEnumerable<StructureNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
                return [node.Id];
            if (PathTo(node.Children, id) is { Count: > 0 } path)
                return [node.Id, .. path];
        }
        return [];
    }

    private void Choose(TreeItem? item)
    {
        if (item?.GetMetadata(0).AsString() is not { Length: > 0 } key)
            return;

        if (item.GetChildCount() > 0)
        {
            item.Collapsed = !item.Collapsed;
            if (item.Collapsed)
                _expanded.Remove(key);
            else
                _expanded.Add(key);
        }
        _selected = key;
        _sheet.Text = Describe(key, _view.StructureTree());
    }

    // ---- Le schéma -------------------------------------------------------------------------------------------------

    /// <summary>Les préfectures à traverser jusqu'à la première commune, puis la commune : vide s'il n'y en a pas.</summary>
    private static List<string> PathToFirstCommune(StructureNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == StructureNodeKind.Commune)
                return [child.Id];
            if (child.Kind == StructureNodeKind.Prefecture && PathToFirstCommune(child) is { Count: > 0 } path)
                return [child.Id, .. path];
        }
        return [];
    }

    /// <summary>Ce qui oblige à refaire le schéma : une structure ou une loi en plus, en moins ou renommée.</summary>
    private static string Shape(IEnumerable<StructureNode> nodes) =>
        string.Join(";", nodes.Select(n => $"{n.Id}:{n.Name}:{n.Ownership}[{string.Join(",", n.Laws.Select(l => l.Id))}]({Shape(n.Children)})"));

    private void Build(IReadOnlyList<StructureNode> roots)
    {
        _tree.Clear();
        var top = _tree.CreateItem();
        foreach (var root in roots)
            AddNode(top, root);
    }

    private void AddNode(TreeItem parent, StructureNode node)
    {
        var item = Item(parent, node.Id, node.Kind == StructureNodeKind.State ? node.Name.ToUpperInvariant() : node.Name,
            node.Kind is StructureNodeKind.State or StructureNodeKind.Prefecture or StructureNodeKind.Commune ? EntityColor : UnitColor);
        if (node.Kind == StructureNodeKind.Building)
            item.SetIcon(0, UiTheme.Square(UiTheme.OwnershipColor(node.Ownership), 12, 12));
        AddLawChips(item, node.Laws);

        // Déroulée, une boîte montre d'abord ses lois en toutes lettres, puis ce qu'elle contient.
        foreach (var law in node.Laws)
        {
            var lawItem = Item(item, $"loi:{law.Id}", $"Loi : {law.Name}", null);
            lawItem.SetCustomColor(0, UiTheme.Muted);
            AddLawChips(lawItem, [law]);
        }

        if (node.Kind == StructureNodeKind.Commune)
        {
            // Dans une commune, organismes et bâtiments sont rangés dans deux boîtes, comme sur le canevas.
            var organizations = node.Children.Where(c => c.Kind == StructureNodeKind.Organization).ToList();
            var organizationsItem = Item(item, $"organismes:{node.Id}",
                organizations.Count == 0 ? "Organismes (à venir)" : $"Organismes ({organizations.Count})", GroupColor);
            foreach (var child in organizations)
                AddNode(organizationsItem, child);

            var buildings = node.Children.Where(c => c.Kind == StructureNodeKind.Building).ToList();
            var buildingsItem = Item(item, $"batiments:{node.Id}", $"Bâtiments ({buildings.Count})", GroupColor);
            // Les bâtiments sont rangés par catégorie (Extraction, Production…), chacune repliable.
            foreach (var category in buildings.GroupBy(b => b.Category ?? "Autres"))
            {
                var categoryItem = Item(buildingsItem, $"categorie:{node.Id}:{category.Key}", $"{category.Key} ({category.Count()})", GroupColor);
                foreach (var child in category)
                    AddNode(categoryItem, child);
            }

            foreach (var child in node.Children.Where(c => c.Kind is StructureNodeKind.Commune or StructureNodeKind.Prefecture or StructureNodeKind.State))
                AddNode(item, child);
        }
        else
        {
            foreach (var child in node.Children)
                AddNode(item, child);
        }
    }

    private TreeItem Item(TreeItem parent, string key, string text, Color? color)
    {
        var item = _tree.CreateItem(parent);
        item.SetText(0, text);
        item.SetMetadata(0, key);
        if (color is { } marker)
            item.SetIcon(0, UiTheme.Square(marker, 6, 18));
        item.Collapsed = !_expanded.Contains(key);
        if (key == _selected)
            item.Select(0);
        return item;
    }

    /// <summary>Les pastilles des pôles d'une loi, à droite du titre ; le nom de la loi s'affiche au survol.</summary>
    private void AddLawChips(TreeItem item, IEnumerable<LawView> laws)
    {
        var id = 0;
        foreach (var law in laws)
            foreach (var pole in law.Orientations)
                // Un bouton actif garde sa vraie couleur (désactivé, il serait terni) ; un clic dessus ne fait rien.
                item.AddButton(0, UiTheme.Square(UiTheme.PoleColor(pole), 12, 12), id++, disabled: false,
                    tooltipText: $"{law.Name} : {WorldText.PoleName(pole)} ({pole.Code})");
    }

    // ---- La fiche --------------------------------------------------------------------------------------------------

    private string Describe(string? key, IReadOnlyList<StructureNode> roots)
    {
        if (key is null)
            return Muted("Choisissez une structure dans le schéma.");

        var text = new StringBuilder();
        if (key.StartsWith("loi:"))
            DescribeLaw(text, key[4..], roots);
        else if (key.StartsWith("batiments:") && Find(roots, key["batiments:".Length..]) is { } buildingsOf)
            DescribeBuildings(text, buildingsOf, null);
        else if (key.StartsWith("categorie:") && key["categorie:".Length..].Split(':', 2) is [var communeId, var category]
                 && Find(roots, communeId) is { } communeOf)
            DescribeBuildings(text, communeOf, category);
        else if (key.StartsWith("organismes:"))
            text.Append(Heading("ORGANISMES")).Append(Muted(
                "À venir. Les organismes : domaine d'application, pouvoir, postes et fonctionnement."));
        else if (Find(roots, key) is { } node)
            DescribeStructure(text, node);
        return text.ToString();
    }

    private void DescribeStructure(StringBuilder text, StructureNode node)
    {
        text.Append(Heading(node.Name.ToUpperInvariant()));
        switch (node.Kind)
        {
            case StructureNodeKind.State:
                var communes = _view.Communes();
                text.AppendLine($"Trésor {_view.TreasuryBalance:N0}   impôt d'État {_view.StateTaxRate:P2} par jour");
                text.AppendLine($"{communes.Count} commune(s), {communes.Sum(c => c.Residents):N0} habitants");
                text.AppendLine();
                text.AppendLine("Légitimité :");
                foreach (var pole in Demos.Domain.Politics.Orientation.All)
                    text.AppendLine($"  {Chip(pole)} {WorldText.PoleName(pole)} ({pole.Code}) {_view.LegitimacyOf(pole):F0}");
                break;
            case StructureNodeKind.Prefecture when _view.Prefectures().FirstOrDefault(p => p.Id == node.Id) is { } prefecture:
                DescribePrefecture(text, prefecture, All(_view.StructureTree()).ToList());
                break;
            case StructureNodeKind.Commune when _view.Communes().FirstOrDefault(c => c.Id == node.Id) is { } commune:
                text.AppendLine(Escape(WorldText.Commune(commune)));
                break;
            case StructureNodeKind.Building when _desk.SheetOf(node.Id) is { } sheet:
                foreach (var line in sheet.Info)
                    text.AppendLine(Escape(line));
                text.AppendLine();
                text.AppendLine(Muted("Les décisions sur ce bâtiment se prennent dans Production → Unités de production."));
                break;
        }

        if (_view.EntityLoads().FirstOrDefault(l => l.Id == node.Id) is { } load)
            DescribeLoad(text, load, node.Kind == StructureNodeKind.State);

        text.AppendLine();
        text.Append(Heading("LOIS QUI S'APPLIQUENT"));
        var laws = _view.LawsApplyingTo(node.Id);
        if (laws.Count == 0)
            text.AppendLine(Muted("Aucune."));
        foreach (var law in laws)
            text.AppendLine($"{string.Concat(law.Orientations.Select(Chip))} {Escape(law.Name)}  "
                + Muted(law.StructureId == node.Id ? "(créée ici)" : $"(de {law.StructureName})"));
    }

    /// <summary>Une préfecture : son chef-lieu, ce qu'elle regroupe, son bâtiment (ou son chantier), ses emplois et salaires.</summary>
    private static void DescribePrefecture(StringBuilder text, PrefectureView prefecture, List<StructureNode> all)
    {
        string NameOf(string id) => all.FirstOrDefault(n => n.Id == id)?.Name ?? id;
        text.AppendLine($"Chef-lieu : {Escape(prefecture.SeatName ?? "—")}");
        text.AppendLine($"Regroupe : {Escape(string.Join(", ", prefecture.MemberIds.Select(NameOf)))}");
        text.AppendLine(prefecture.BuildingId is null
            ? $"Bâtiment de la préfecture en chantier : encore {prefecture.ConstructionDaysLeft} jour(s)"
            : "Bâtiment de la préfecture en service");
        text.AppendLine($"Emplois : {prefecture.Jobs:N0} · salaires {prefecture.DailyWages:N0} par jour, payés par le trésor");
    }

    /// <summary>
    /// L'extension d'une entité : ce qu'elle gère, sa charge (1 = la limite de ce qu'elle gère sans perte) et son
    /// efficacité ; ailleurs qu'à l'État, l'efficacité qu'y ont les décisions de l'État.
    /// </summary>
    private static void DescribeLoad(StringBuilder text, EntityLoadView load, bool isState)
    {
        text.AppendLine();
        text.Append(Heading("EXTENSION"));
        var charge = $"Charge {load.Charge:0.00} (1 = la limite) · efficacité {load.Efficiency:P0}";
        text.AppendLine(load.Charge > 1 ? $"[color=#{UiTheme.Danger.ToHtml(false)}]{charge}[/color]" : charge);
        text.AppendLine($"Population comptée {load.CountedPopulation:N0} · bâtiments publics gérés en direct {load.DirectBuildings} · "
            + $"bâtiments autonomes {load.AutonomousBuildings}");
        if (!isState)
            text.AppendLine($"Décisions de l'État ici : efficacité {load.StateEfficiencyHere:P0} "
                + Muted($"({load.PrefecturesFromState} préfecture(s) traversée(s) depuis l'État)"));
    }

    /// <summary>Les bâtiments d'une commune, tous ou ceux d'une catégorie.</summary>
    private static void DescribeBuildings(StringBuilder text, StructureNode commune, string? category)
    {
        var buildings = commune.Children
            .Where(c => c.Kind == StructureNodeKind.Building && (category is null || (c.Category ?? "Autres") == category))
            .ToList();
        text.Append(Heading(category is null
            ? $"BÂTIMENTS DE {commune.Name.ToUpperInvariant()}"
            : $"{category.ToUpperInvariant()} — {commune.Name.ToUpperInvariant()}"));
        text.AppendLine($"{buildings.Count} bâtiment(s). Cliquez sur l'un d'eux pour sa fiche.");
        text.AppendLine();
        foreach (var building in buildings)
            text.AppendLine($"{Escape(building.Name)} {string.Concat(building.Laws.SelectMany(l => l.Orientations).Select(Chip))}");
    }

    private void DescribeLaw(StringBuilder text, string lawId, IReadOnlyList<StructureNode> roots)
    {
        if (All(roots).SelectMany(n => n.Laws).FirstOrDefault(l => l.Id == lawId) is not { } law)
            return;

        text.Append(Heading(law.Name.ToUpperInvariant()));
        text.AppendLine($"Créée dans : {Escape(law.StructureName)}");
        text.AppendLine("Elle s'applique à cette structure et à tout ce qu'elle contient.");
        text.AppendLine();
        text.AppendLine("Pôles idéologiques :");
        foreach (var pole in law.Orientations)
            text.AppendLine($"  {Chip(pole)} {WorldText.PoleName(pole)} ({pole.Code})");
    }

    private static StructureNode? Find(IEnumerable<StructureNode> nodes, string id) => All(nodes).FirstOrDefault(n => n.Id == id);

    private static IEnumerable<StructureNode> All(IEnumerable<StructureNode> nodes) =>
        nodes.SelectMany(n => All(n.Children).Prepend(n));

    private static string Heading(string text) => $"[font_size={UiTheme.TitleSize}]{Escape(text)}[/font_size]\n\n";

    private static string Muted(string text) => $"[color=#{UiTheme.Muted.ToHtml(false)}]{Escape(text)}[/color]";

    private static string Chip(Demos.Domain.Politics.Orientation pole) => $"[color=#{UiTheme.PoleColor(pole).ToHtml(false)}]■[/color]";

    // Un crochet ouvrant serait lu comme une balise BBCode.
    private static string Escape(string text) => text.Replace("[", "[lb]");
}
