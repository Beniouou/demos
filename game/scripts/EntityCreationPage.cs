using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Map;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Structure → Création d'entité politique : en haut, quoi créer (une commune ou une préfecture), puis son formulaire.
/// Commune : nom, commune qui paie le camp de base, charte et coût ; le bouton ferme le menu et passe la carte en mode
/// pose (ni l'eau, ni une falaise, ni un glacier, ni trop près d'une autre commune). Préfecture : nom, ce qui la contient,
/// les entités regroupées, le chef-lieu et le pôle payé. Rattacher une entité se fait dans Gestion des entités
/// politiques. Les décisions passent par le bureau du joueur.
/// </summary>
public partial class EntityCreationPage : VBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly System.Action<string, string, Orientation> _chooseCommuneSite = null!;
    private string _shape = "";

    private CheckBox _communeChoice = null!;
    private CheckBox _prefectureChoice = null!;
    private VBoxContainer _communeForm = null!;
    private VBoxContainer _prefectureForm = null!;

    // Commune
    private LineEdit _communeName = null!;
    private OptionButton _payer = null!;
    private readonly List<string> _payerIds = [];
    private OptionButton _communePole = null!;
    private RichTextLabel _communeCost = null!;
    private Button _chooseSite = null!;
    private string _communeCostText = "";

    // Préfecture
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

    /// <param name="chooseCommuneSite">Ferme le menu et passe la carte en mode pose (nom, commune qui paie, charte).</param>
    public EntityCreationPage(GameView view, PlayerDesk desk, System.Action<string, string, Orientation> chooseCommuneSite)
    {
        _view = view;
        _desk = desk;
        _chooseCommuneSite = chooseCommuneSite;

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);
        var margin = UiTheme.Margin(8);
        scroll.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);

        // Quoi créer : deux cases qui s'excluent.
        var kind = Row(column, "Que créer ?");
        var group = new ButtonGroup();
        _communeChoice = new CheckBox { Text = "Commune", ButtonGroup = group, ButtonPressed = true, FocusMode = FocusModeEnum.None };
        _prefectureChoice = new CheckBox { Text = "Préfecture", ButtonGroup = group, FocusMode = FocusModeEnum.None };
        _communeChoice.Toggled += _ => ShowForm();
        _prefectureChoice.Toggled += _ => ShowForm();
        kind.AddChild(_communeChoice);
        kind.AddChild(_prefectureChoice);
        column.AddChild(new HSeparator());

        _communeForm = new VBoxContainer();
        _communeForm.AddThemeConstantOverride("separation", 8);
        column.AddChild(_communeForm);
        BuildCommune(_communeForm);

        _prefectureForm = new VBoxContainer();
        _prefectureForm.AddThemeConstantOverride("separation", 8);
        column.AddChild(_prefectureForm);
        BuildFounding(_prefectureForm);

        ShowForm();
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public EntityCreationPage() { }

    /// <summary>Montre le formulaire de la commune (touche C, bouton « Camp de base »).</summary>
    public void ShowCommune()
    {
        _communeChoice.ButtonPressed = true;
        ShowForm();
    }

    private void ShowForm()
    {
        _communeForm.Visible = _communeChoice.ButtonPressed;
        _prefectureForm.Visible = !_communeChoice.ButtonPressed;
    }

    public void Refresh()
    {
        var entities = Entities();
        var shape = string.Join(";", entities.Select(e => $"{e.Node.Id}:{e.Node.Name}:{e.Depth}"));
        if (shape != _shape)
        {
            _shape = shape;
            FillChoices(entities);
            FillPayers();
        }

        RefreshCommune();

        var cost = _view.PrefectureCost();
        _foundCost.Text =
            $"Coût : {cost.MaterialQuantity:N0} {cost.MaterialProductId}, payés par le chef-lieu, et {cost.ConstructionDays} jours de chantier ; "
            + $"{cost.FoundingLegitimacy:0.#} de légitimité dans le pôle choisi. Au moins {cost.MinimumMembers} entités. "
            + $"Emplois : {cost.BaseJobs} + {cost.JobsPerEntity} par entité regroupée, à {cost.WageBillPerJob:0.##} par emploi et par jour, "
            + "payés par le trésor.";
        FillPoles(_foundPole, cost.FoundingLegitimacy);
    }

    // ---- Commune ---------------------------------------------------------------------------------------------------

    private void BuildCommune(VBoxContainer column)
    {
        column.AddChild(UiTheme.Title("Fonder une commune (camp de base)"));
        _communeName = new LineEdit { Text = _desk.NextCommuneName(), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Row(column, "Nom").AddChild(_communeName);
        _payer = Choice(column, "Payée par");
        _payer.TooltipText = "La commune qui envoie les matériaux du camp de base (sa réserve de chantier, le stock de l'État, puis des achats avec le trésor)";
        _communePole = Choice(column, "Charte");
        _communePole.TooltipText = "Le pôle où se paie la légitimité devient la charte de la commune : il attire les classes de ce pôle et repousse celles du pôle opposé.";

        _communeCost = new HintText
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _communeCost.AddThemeColorOverride("default_color", UiTheme.Text);
        column.AddChild(_communeCost);

        _chooseSite = new Button { Text = "Choisir l'emplacement sur la carte", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _chooseSite.Pressed += () =>
        {
            if (Pick(_payer, _payerIds) is { } payerId && _communePole.Selected >= 0)
                _chooseCommuneSite(_communeName.Text, payerId, Orientation.All.ElementAt(_communePole.Selected));
        };
        column.AddChild(_chooseSite);
    }

    private void FillPayers()
    {
        var selected = Pick(_payer, _payerIds);
        Fill(_payer, _payerIds, _view.Communes().Select(c => (c.Id, c.Name)), selected);
        // Un nom proposé pour la prochaine commune, tant que le joueur ne l'a pas changé.
        if (_communeName.Text.StartsWith("Commune ") || _communeName.Text.Length == 0)
            _communeName.Text = _desk.NextCommuneName();
    }

    private void RefreshCommune()
    {
        var settings = Balance.Current;
        FillPoles(_communePole, settings.BaseCampLegitimacyCost);

        var text = new StringBuilder();
        if (Pick(_payer, _payerIds) is { } payer && _desk.FoundingOfferFrom(payer) is { } offer)
        {
            var available = offer.Availability;
            text.Append(SheetText.Section("Coût"));
            text.AppendLine($"{offer.MaterialQuantity:N0} {SheetText.Escape(offer.MaterialName)} · {offer.LegitimacyCost:0.#} de légitimité · "
                + SheetText.Hint($"dotation de {offer.Grant:N0}", "Versée par le trésor de l'État à la caisse de la nouvelle commune : de quoi payer les salaires de son camp de base."));
            text.AppendLine(SheetText.Muted($"Dans la commune qui paie : réserve de chantier {available.Reserve:N0}, stock de l'État {available.StateStock:N0}, "
                + $"en vente {available.OnSale:N0} (au prix de {available.Price:N1}). Trésor : {available.Treasury:N0}."));
        }
        text.Append(SheetText.Section("Emplacement"));
        text.AppendLine(SheetText.Muted($"N'importe où sur la carte, sauf sur l'eau, une falaise ou un glacier, et à {MapScale.Format(settings.CommuneMinDistance)} "
            + "au moins de toute autre commune. Le bouton ferme le menu ; un clic sur la carte fonde la commune, Échap annule."));
        var noPole = _communePole.Selected < 0 || _communePole.IsItemDisabled(_communePole.Selected);
        if (noPole)
            text.AppendLine($"[color=#{UiTheme.Danger.ToHtml(false)}]Aucun pôle n'a assez de légitimité ({settings.BaseCampLegitimacyCost:0.#}).[/color]");

        // Le texte n'est remis que s'il change : sinon sa bulle d'aide clignoterait.
        var bbcode = text.ToString();
        if (bbcode != _communeCostText)
        {
            _communeCostText = bbcode;
            _communeCost.Text = bbcode;
        }
        _chooseSite.Disabled = Pick(_payer, _payerIds) is null || noPole;
    }

    // ---- Préfecture ------------------------------------------------------------------------------------------------

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
        var found = new Button { Text = "Fonder la préfecture", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
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

    // ---- Remplissage -----------------------------------------------------------------------------------------------

    private void FillChoices(List<(StructureNode Node, int Depth)> entities)
    {
        var parent = Pick(_parent, _parentIds);
        var parents = entities.Where(e => e.Node.Kind is StructureNodeKind.State or StructureNodeKind.Prefecture).ToList();
        Fill(_parent, _parentIds, parents.Select(p => (p.Node.Id, new string(' ', 4 * p.Depth) + p.Node.Name)), parent);
        FillMembers(entities);
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
