using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Structure → Préfectures, en deux boîtes. À gauche, fonder une préfecture : son nom, ce qui la contient (l'État ou une
/// préfecture), les entités qu'elle regroupe, son chef-lieu et le pôle de légitimité payé, avec le coût affiché avant.
/// À droite, rattacher une commune ou une préfecture ailleurs (les choix impossibles sont grisés), puis la liste des
/// préfectures. Les décisions passent par le bureau du joueur.
/// </summary>
public partial class PrefecturesPage : HBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private string _shape = "";

    private LineEdit _name = null!;
    private OptionButton _parent = null!;
    private readonly List<string> _parentIds = [];
    private VBoxContainer _members = null!;
    private readonly Dictionary<string, CheckBox> _memberBoxes = [];
    private OptionButton _seat = null!;
    private readonly List<string> _seatIds = [];
    private OptionButton _foundPole = null!;
    private Label _foundCost = null!;
    private Label _foundMessage = null!;

    private OptionButton _entity = null!;
    private readonly List<string> _entityIds = [];
    private OptionButton _newParent = null!;
    private readonly List<string> _newParentIds = [];
    private OptionButton _attachPole = null!;
    private Label _attachCost = null!;
    private Label _attachMessage = null!;
    private Label _list = null!;

    public PrefecturesPage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 12);
        BuildFounding(Column(this));
        AddChild(new VSeparator());
        BuildAttaching(Column(this));
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PrefecturesPage() { }

    public void Refresh()
    {
        var entities = Entities();
        var shape = string.Join(";", entities.Select(e => $"{e.Node.Id}:{e.Node.Name}:{e.Depth}"));
        if (shape != _shape)
        {
            _shape = shape;
            FillChoices(entities);
        }

        var cost = _view.PrefectureCost();
        _foundCost.Text =
            $"Coût : {cost.MaterialQuantity:N0} {cost.MaterialProductId}, payés par le chef-lieu, et {cost.ConstructionDays} jours de chantier ; "
            + $"{cost.FoundingLegitimacy:0.#} de légitimité dans le pôle choisi. Au moins {cost.MinimumMembers} entités. "
            + $"Emplois : {cost.BaseJobs} + {cost.JobsPerEntity} par entité regroupée, à {cost.WageBillPerJob:0.##} par emploi et par jour, "
            + "payés par le trésor.";
        _attachCost.Text = $"Coût : {cost.AttachLegitimacy:0.#} de légitimité dans le pôle choisi.";
        FillPoles(_foundPole, cost.FoundingLegitimacy);
        FillPoles(_attachPole, cost.AttachLegitimacy);
        _list.Text = DescribePrefectures(entities);
    }

    // ---- Fonder ----------------------------------------------------------------------------------------------------

    private void BuildFounding(VBoxContainer column)
    {
        column.AddChild(UiTheme.Title("Fonder une préfecture"));
        var nameRow = Row(column, "Nom");
        _name = new LineEdit { Text = "Nouvelle préfecture", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        nameRow.AddChild(_name);
        _parent = Choice(column, "Dans");
        _parent.TooltipText = "Ce qui contient la préfecture : l'État ou une autre préfecture";
        _parent.ItemSelected += _ => FillMembers(Entities());

        column.AddChild(UiTheme.Section("Entités regroupées"));
        _members = new VBoxContainer();
        column.AddChild(_members);

        _seat = Choice(column, "Chef-lieu");
        _seat.TooltipText = "Une commune regroupée : elle accueille le bâtiment et paie son chantier";
        _foundPole = Choice(column, "Pôle payé");
        _foundCost = Note(column);
        var found = new Button { Text = "Fonder la préfecture", FocusMode = FocusModeEnum.None };
        found.Pressed += Found;
        column.AddChild(found);
        _foundMessage = Message(column);
    }

    private void Found()
    {
        var members = _memberBoxes.Where(m => m.Value.ButtonPressed).Select(m => m.Key).ToList();
        if (Pick(_parent, _parentIds) is not { } parentId || Pick(_seat, _seatIds) is not { } seatId)
        {
            Show(_foundMessage, "Choisissez ce qui contient la préfecture et son chef-lieu.");
            return;
        }
        Show(_foundMessage, _desk.FoundPrefecture(_name.Text, parentId, members, seatId, Orientation.All.ElementAt(_foundPole.Selected)));
        _shape = "";
    }

    /// <summary>
    /// Les entités qu'on peut regrouper : les communes et préfectures qui ne contiennent pas l'entité choisie comme
    /// parent (une entité ne regroupe pas ce qui la contient). Les cases déjà cochées le restent.
    /// </summary>
    private void FillMembers(List<(StructureNode Node, int Depth)> entities)
    {
        var parentId = Pick(_parent, _parentIds);
        var parentPath = PathTo(parentId);
        var checkedIds = _memberBoxes.Where(m => m.Value.ButtonPressed).Select(m => m.Key).ToHashSet();
        foreach (var child in _members.GetChildren())
        {
            _members.RemoveChild(child);
            child.QueueFree();
        }
        _memberBoxes.Clear();

        foreach (var (node, depth) in entities.Where(e => e.Node.Kind != StructureNodeKind.State && !parentPath.Contains(e.Node.Id)))
        {
            var box = new CheckBox
            {
                Text = new string(' ', 4 * System.Math.Max(0, depth - 1)) + node.Name + (node.Kind == StructureNodeKind.Prefecture ? " (préfecture)" : ""),
                ButtonPressed = checkedIds.Contains(node.Id),
                FocusMode = FocusModeEnum.None,
            };
            box.Toggled += _ => FillSeats();
            _memberBoxes[node.Id] = box;
            _members.AddChild(box);
        }
        FillSeats();
    }

    /// <summary>Le chef-lieu : une commune contenue dans les entités cochées (une commune, ou une commune d'une préfecture).</summary>
    private void FillSeats()
    {
        var selected = Pick(_seat, _seatIds);
        var communes = _memberBoxes.Where(m => m.Value.ButtonPressed)
            .SelectMany(m => CommunesWithin(Find(m.Key)))
            .DistinctBy(c => c.Id)
            .ToList();
        _seatIds.Clear();
        _seat.Clear();
        foreach (var commune in communes)
        {
            _seatIds.Add(commune.Id);
            _seat.AddItem(commune.Name);
        }
        if (communes.Count == 0)
            _seat.AddItem("Cochez d'abord des entités");
        _seat.Select(System.Math.Max(0, selected is null ? 0 : _seatIds.IndexOf(selected)));
        _seat.Disabled = communes.Count == 0;
    }

    // ---- Rattacher -------------------------------------------------------------------------------------------------

    private void BuildAttaching(VBoxContainer column)
    {
        column.AddChild(UiTheme.Title("Rattacher une entité"));
        _entity = Choice(column, "Entité");
        _entity.ItemSelected += _ => FillNewParents(Entities());
        _newParent = Choice(column, "À");
        _newParent.TooltipText = "L'État ou une préfecture ; les choix impossibles sont grisés";
        _attachPole = Choice(column, "Pôle payé");
        _attachCost = Note(column);
        var attach = new Button { Text = "Rattacher", FocusMode = FocusModeEnum.None };
        attach.Pressed += () =>
        {
            if (Pick(_entity, _entityIds) is { } entityId && Pick(_newParent, _newParentIds) is { } parentId)
            {
                Show(_attachMessage, _desk.Attach(entityId, parentId, Orientation.All.ElementAt(_attachPole.Selected)));
                _shape = "";
            }
        };
        column.AddChild(attach);
        _attachMessage = Message(column);

        column.AddChild(new HSeparator());
        column.AddChild(UiTheme.Section("Préfectures"));
        _list = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_list);
    }

    /// <summary>Où rattacher l'entité choisie : l'État ou une préfecture ; les choix refusés par le moteur sont grisés.</summary>
    private void FillNewParents(List<(StructureNode Node, int Depth)> entities)
    {
        var entityId = Pick(_entity, _entityIds);
        var selected = Pick(_newParent, _newParentIds);
        var parents = entities.Where(e => e.Node.Kind is StructureNodeKind.State or StructureNodeKind.Prefecture).ToList();
        _newParentIds.Clear();
        _newParent.Clear();
        foreach (var (node, depth) in parents)
        {
            _newParentIds.Add(node.Id);
            _newParent.AddItem(new string(' ', 4 * depth) + node.Name);
            _newParent.SetItemDisabled(_newParent.ItemCount - 1, entityId is null || !_view.CanAttach(entityId, node.Id));
        }
        var index = selected is null ? -1 : _newParentIds.IndexOf(selected);
        if (index < 0 || _newParent.IsItemDisabled(index))
            index = Enumerable.Range(0, _newParent.ItemCount).FirstOrDefault(i => !_newParent.IsItemDisabled(i), 0);
        if (_newParent.ItemCount > 0)
            _newParent.Select(index);
    }

    // ---- Remplissage -----------------------------------------------------------------------------------------------

    private void FillChoices(List<(StructureNode Node, int Depth)> entities)
    {
        var parent = Pick(_parent, _parentIds);
        var parents = entities.Where(e => e.Node.Kind is StructureNodeKind.State or StructureNodeKind.Prefecture).ToList();
        Fill(_parent, _parentIds, parents.Select(p => (p.Node.Id, new string(' ', 4 * p.Depth) + p.Node.Name)), parent);
        FillMembers(entities);

        var entity = Pick(_entity, _entityIds);
        var attachable = entities.Where(e => e.Node.Kind is StructureNodeKind.Commune or StructureNodeKind.Prefecture);
        Fill(_entity, _entityIds, attachable.Select(e => (e.Node.Id, new string(' ', 4 * System.Math.Max(0, e.Depth - 1)) + e.Node.Name
            + (e.Node.Kind == StructureNodeKind.Prefecture ? " (préfecture)" : ""))), entity);
        FillNewParents(entities);
    }

    /// <summary>Les six pôles avec la légitimité disponible ; ceux qui n'ont pas assez pour le coût sont grisés.</summary>
    private void FillPoles(OptionButton menu, double cost)
    {
        var poles = Orientation.All.ToList();
        if (menu.ItemCount != poles.Count)
        {
            menu.Clear();
            foreach (var pole in poles)
                menu.AddIconItem(UiTheme.Square(UiTheme.PoleColor(pole), 12, 12), "");
        }
        for (var i = 0; i < poles.Count; i++)
        {
            var available = _view.LegitimacyOf(poles[i]);
            menu.SetItemText(i, $"{poles[i].Code} — {WorldText.PoleName(poles[i])} ({available:F0} disponible)");
            menu.SetItemDisabled(i, available < cost);
        }
        if (menu.Selected < 0 || menu.IsItemDisabled(menu.Selected))
            menu.Select(Enumerable.Range(0, poles.Count).FirstOrDefault(i => !menu.IsItemDisabled(i), 0));
    }

    private string DescribePrefectures(List<(StructureNode Node, int Depth)> entities)
    {
        var prefectures = _view.Prefectures();
        if (prefectures.Count == 0)
            return "Aucune préfecture pour l'instant.";

        string NameOf(string? id) => entities.FirstOrDefault(e => e.Node.Id == id).Node?.Name ?? "—";
        return string.Join("\n\n", prefectures.Select(p =>
            $"{p.Name} (dans {NameOf(p.ParentId)}), chef-lieu {p.SeatName ?? "—"}\n"
            + $"    regroupe : {string.Join(", ", p.MemberIds.Select(NameOf))}\n"
            + (p.BuildingId is null ? $"    bâtiment en chantier : encore {p.ConstructionDaysLeft} jour(s)" : $"    {p.Jobs} emplois, {p.DailyWages:N0} de salaires par jour")));
    }

    // ---- Hiérarchie ------------------------------------------------------------------------------------------------

    /// <summary>Les entités politiques dans l'ordre de la hiérarchie, avec leur profondeur (l'État à 0).</summary>
    private List<(StructureNode Node, int Depth)> Entities()
    {
        var found = new List<(StructureNode, int)>();
        void Visit(StructureNode node, int depth)
        {
            found.Add((node, depth));
            foreach (var child in node.Children.Where(c => c.Kind is StructureNodeKind.Prefecture or StructureNodeKind.Commune))
                Visit(child, depth + 1);
        }
        foreach (var root in _view.StructureTree())
            Visit(root, 0);
        return found;
    }

    private StructureNode? Find(string id) => Entities().FirstOrDefault(e => e.Node.Id == id).Node;

    /// <summary>Les identifiants de l'entité et de tout ce qui la contient, jusqu'à l'État.</summary>
    private HashSet<string> PathTo(string? id)
    {
        var path = new HashSet<string>();
        bool Visit(StructureNode node)
        {
            if (node.Id == id || node.Children.Any(Visit))
            {
                path.Add(node.Id);
                return true;
            }
            return false;
        }
        foreach (var root in _view.StructureTree())
            Visit(root);
        return path;
    }

    private static IEnumerable<StructureNode> CommunesWithin(StructureNode? node) =>
        node is null ? []
        : node.Kind == StructureNodeKind.Commune ? [node]
        : node.Children.Where(c => c.Kind is StructureNodeKind.Commune or StructureNodeKind.Prefecture).SelectMany(CommunesWithin);

    // ---- Petits éléments -------------------------------------------------------------------------------------------

    private static VBoxContainer Column(HBoxContainer page)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        page.AddChild(scroll);
        var margin = UiTheme.Margin(8);
        scroll.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        return column;
    }

    private static HBoxContainer Row(VBoxContainer column, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90, 0) });
        column.AddChild(row);
        return row;
    }

    private static OptionButton Choice(VBoxContainer column, string label)
    {
        var menu = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            FitToLongestItem = false,
            ClipText = true,
        };
        Row(column, label).AddChild(menu);
        return menu;
    }

    private static Label Note(VBoxContainer column)
    {
        var note = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        note.AddThemeColorOverride("font_color", UiTheme.Muted);
        column.AddChild(note);
        return note;
    }

    private static Label Message(VBoxContainer column)
    {
        var message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        message.AddThemeColorOverride("font_color", UiTheme.Accent);
        column.AddChild(message);
        return message;
    }

    private static void Show(Label message, string text)
    {
        message.Text = text;
        message.Visible = true;
    }

    private static void Fill(OptionButton menu, List<string> ids, IEnumerable<(string Id, string Name)> items, string? selected)
    {
        ids.Clear();
        menu.Clear();
        foreach (var (id, name) in items)
        {
            ids.Add(id);
            menu.AddItem(name);
        }
        if (ids.Count > 0)
            menu.Select(System.Math.Max(0, selected is null ? 0 : ids.IndexOf(selected)));
    }

    private static string? Pick(OptionButton menu, List<string> ids) =>
        menu.Selected >= 0 && menu.Selected < ids.Count ? ids[menu.Selected] : null;
}
