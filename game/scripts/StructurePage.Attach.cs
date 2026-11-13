using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Gestion des entités politiques, « Rattacher » : sous la fiche d'une commune ou d'une préfecture choisie dans le
/// schéma, où la rattacher (l'État ou une préfecture ; les choix refusés par le moteur sont grisés), le pôle payé et le
/// coût. La décision passe par le bureau du joueur.
/// </summary>
public partial class StructurePage
{
    private VBoxContainer _attach = null!;
    private Label _attachTitle = null!;
    private OptionButton _attachTarget = null!;
    private readonly List<string> _attachTargetIds = [];
    private OptionButton _attachPole = null!;
    private Label _attachCost = null!;
    private Label _attachMessage = null!;
    // L'entité et la forme de l'arbre pour lesquelles les menus ont été remplis : refaits seulement quand elles changent.
    private string _attachShape = "";

    private VBoxContainer BuildAttachPanel()
    {
        _attach = new VBoxContainer { Visible = false };
        _attach.AddThemeConstantOverride("separation", 6);
        _attach.AddChild(new HSeparator());
        _attachTitle = UiTheme.Section("Rattacher");
        _attach.AddChild(_attachTitle);

        _attachTarget = AttachChoice("À");
        _attachTarget.TooltipText = "L'État ou une préfecture ; les choix impossibles sont grisés";
        _attachPole = AttachChoice("Pôle payé");
        _attachCost = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _attachCost.AddThemeColorOverride("font_color", UiTheme.Muted);
        _attach.AddChild(_attachCost);

        var button = new Button { Text = "Rattacher", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        button.Pressed += () =>
        {
            if (_selected is not { } entityId || Pick(_attachTarget, _attachTargetIds) is not { } parentId || _attachPole.Selected < 0)
                return;
            _attachMessage.Text = _desk.Attach(entityId, parentId, Orientation.All.ElementAt(_attachPole.Selected));
            _attachMessage.Visible = true;
            _attachShape = "";
        };
        _attach.AddChild(button);
        _attachMessage = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _attachMessage.AddThemeColorOverride("font_color", UiTheme.Accent);
        _attach.AddChild(_attachMessage);
        return _attach;
    }

    /// <summary>Montre « Rattacher » pour une commune ou une préfecture choisie ; les menus suivent l'arbre.</summary>
    private void RefreshAttach(IReadOnlyList<StructureNode> roots)
    {
        var node = _selected is null ? null : Find(roots, _selected);
        _attach.Visible = node is { Kind: StructureNodeKind.Commune or StructureNodeKind.Prefecture };
        if (!_attach.Visible || node is null)
            return;

        var shape = $"{node.Id}|{_shape}";
        if (shape != _attachShape)
        {
            // Une autre entité choisie : l'ancien message ne la concerne plus.
            if (!_attachShape.StartsWith($"{node.Id}|"))
                _attachMessage.Visible = false;
            _attachShape = shape;
            _attachTitle.Text = $"RATTACHER {node.Name.ToUpperInvariant()}";
            FillAttachTargets(roots, node.Id);
        }

        var cost = _view.PrefectureCost().AttachLegitimacy;
        _attachCost.Text = $"Coût : {cost:0.#} de légitimité dans le pôle choisi.";
        FillAttachPoles(cost);
    }

    /// <summary>L'État et les préfectures, en retrait ; ceux où le moteur refuse le rattachement sont grisés.</summary>
    private void FillAttachTargets(IReadOnlyList<StructureNode> roots, string entityId)
    {
        _attachTargetIds.Clear();
        _attachTarget.Clear();
        foreach (var (target, depth) in Parents(roots, 0))
        {
            _attachTargetIds.Add(target.Id);
            _attachTarget.AddItem(new string(' ', 4 * depth) + target.Name);
            _attachTarget.SetItemDisabled(_attachTarget.ItemCount - 1, !_view.CanAttach(entityId, target.Id));
        }
        var index = Enumerable.Range(0, _attachTarget.ItemCount).FirstOrDefault(i => !_attachTarget.IsItemDisabled(i), 0);
        if (_attachTarget.ItemCount > 0)
            _attachTarget.Select(index);
    }

    private static IEnumerable<(StructureNode Node, int Depth)> Parents(IEnumerable<StructureNode> nodes, int depth) =>
        nodes.Where(n => n.Kind is StructureNodeKind.State or StructureNodeKind.Prefecture or StructureNodeKind.Commune)
            .SelectMany(n => Parents(n.Children, depth + 1).Prepend((n, depth)))
            .Where(p => p.Item1.Kind is StructureNodeKind.State or StructureNodeKind.Prefecture);

    /// <summary>Les six pôles avec la légitimité disponible ; ceux qui n'ont pas assez pour le coût sont grisés.</summary>
    private void FillAttachPoles(double cost)
    {
        var poles = Orientation.All.ToList();
        if (_attachPole.ItemCount != poles.Count)
        {
            _attachPole.Clear();
            foreach (var pole in poles)
                _attachPole.AddIconItem(UiTheme.Square(UiTheme.PoleColor(pole), 12, 12), "");
        }
        for (var i = 0; i < poles.Count; i++)
        {
            var available = _view.LegitimacyOf(poles[i]);
            _attachPole.SetItemText(i, $"{poles[i].Code} — {WorldText.PoleName(poles[i])} ({available:F0} disponible)");
            _attachPole.SetItemDisabled(i, available < cost);
        }
        if (_attachPole.Selected < 0 || _attachPole.IsItemDisabled(_attachPole.Selected))
            _attachPole.Select(Enumerable.Range(0, poles.Count).FirstOrDefault(i => !_attachPole.IsItemDisabled(i), 0));
    }

    private OptionButton AttachChoice(string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90, 0) });
        var menu = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false, ClipText = true };
        row.AddChild(menu);
        _attach.AddChild(row);
        return menu;
    }

    private static string? Pick(OptionButton menu, List<string> ids) =>
        menu.Selected >= 0 && menu.Selected < ids.Count ? ids[menu.Selected] : null;
}
