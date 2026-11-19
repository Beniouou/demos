using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Le petit schéma d'un bâtiment dans Production → Unités de production, dessiné par Godot : à gauche d'où viennent
/// ses matières, au milieu le bâtiment, à droite où part sa production ; une flèche porte le produit. Le cadre d'une
/// boîte est bleu dans une autre commune, violet dans une autre préfecture ; une ligne d'approvisionnement est une
/// flèche blanche épaisse ; un fournisseur que ses réglages d'achat écartent est voilé et barré, sans flèche. Les boîtes
/// prennent toute la largeur disponible. Un clic sur un autre bâtiment le choisit.
/// </summary>
public partial class FlowDiagram : Control
{
    private const float BoxHeight = 44;
    private const float RowGap = 8;
    private const float Gap = 26;
    private const float Top = 22;
    // La boîte du milieu fait 80 % d'une boîte de côté.
    private const float CenterShare = 0.8f;

    /// <summary>Une boîte : sa colonne (-1 gauche, 0 milieu, 1 droite), sa ligne, ce qu'elle écrit et dessine.</summary>
    private sealed record Box(int Column, int Row, string Title, string Detail, Color? Strip, Color? Frame, string? StructureId, bool Line)
    {
        /// <summary>Un fournisseur que les réglages d'achat écartent : estompé et barré.</summary>
        public bool Blocked { get; init; }
    }

    private readonly List<Box> _boxes = [];
    private int _rows = 1;
    private string _emptyLeft = "";

    /// <summary>Un autre bâtiment a été choisi d'un clic.</summary>
    public event System.Action<string>? BuildingChosen;

    public FlowDiagram()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Resized += QueueRedraw;
    }

    /// <summary>Change le bâtiment montré (null : rien).</summary>
    public void SetFlows(GameView view, ProductionHistory history, BuildingFlowView? flows)
    {
        _boxes.Clear();
        if (flows is null)
        {
            CustomMinimumSize = Vector2.Zero;
            QueueRedraw();
            return;
        }

        Box BoxOf(FlowEndView end, int column, int row) => new(column, row, TypeOf(end.Name),
            $"{end.ProductName} · {FlowText.Detail(view, history, flows, end)}",
            end.Ownership is { } ownership ? UiTheme.OwnershipColor(ownership) : null,
            UiTheme.RelationColor(end.Relation), end.StructureId, end.Kind == FlowKind.Line) { Blocked = end.Blocked };

        for (var row = 0; row < flows.Sources.Count; row++)
            _boxes.Add(BoxOf(flows.Sources[row], -1, row));
        for (var row = 0; row < flows.Destinations.Count; row++)
            _boxes.Add(BoxOf(flows.Destinations[row], 1, row));
        _emptyLeft = flows.Sources.Count > 0 ? "" : flows.HasMaterials ? "aucune source" : "aucune matière";

        // Au milieu : le type en titre, le produit et la production du dernier jour dessous.
        var name = view.NameOf(flows.BuildingId);
        var produced = view.ProductionChain(flows.CommuneId).FirstOrDefault(b => b.Id == flows.BuildingId);
        var detail = produced is null ? "sans recette" : $"{produced.ProductName} · {produced.ProducedToday:N0} par jour";
        _rows = System.Math.Max(1, System.Math.Max(flows.Sources.Count, flows.Destinations.Count));
        _boxes.Add(new Box(0, 0, TypeOf(name), detail,
            view.OwnershipOf(flows.BuildingId) is { } owner ? UiTheme.OwnershipColor(owner) : null, null, flows.BuildingId, false));
        CustomMinimumSize = new Vector2(0, Top + _rows * (BoxHeight + RowGap));
        QueueRedraw();
    }

    /// <summary>« Atelier (Farine) 2 » → « Atelier 2 » : le produit est écrit sous le titre.</summary>
    private static string TypeOf(string name)
    {
        var open = name.IndexOf(" (", System.StringComparison.Ordinal);
        var close = name.LastIndexOf(')');
        return open < 0 || close < open ? name : name[..open] + name[(close + 1)..];
    }

    /// <summary>La place d'une boîte, selon la largeur du moment : les colonnes se partagent toute la largeur.</summary>
    private Rect2 RectOf(Box box)
    {
        var side = (Size.X - 2 * Gap) / (2 + CenterShare);
        var center = side * CenterShare;
        var height = _rows * (BoxHeight + RowGap) - RowGap;
        return box.Column switch
        {
            < 0 => new Rect2(0, Top + box.Row * (BoxHeight + RowGap), side, BoxHeight),
            > 0 => new Rect2(side + center + 2 * Gap, Top + box.Row * (BoxHeight + RowGap), side, BoxHeight),
            _ => new Rect2(side + Gap, Top + (height - BoxHeight) / 2, center, BoxHeight),
        };
    }

    private Box? BoxAt(Vector2 position) => _boxes.FirstOrDefault(b => RectOf(b).HasPoint(position));

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
            && BoxAt(click.Position) is { Column: not 0, StructureId: { } id })
        {
            BuildingChosen?.Invoke(id);
            AcceptEvent();
        }
    }

    public override string _GetTooltip(Vector2 atPosition) => BoxAt(atPosition) is { } box ? $"{box.Title} : {box.Detail}" : "";

    public override Control? _MakeCustomTooltip(string forText) => UiTheme.Tooltip(forText);

    public override void _Draw()
    {
        if (_boxes.FirstOrDefault(b => b.Column == 0) is not { } center)
            return;
        var font = GetThemeDefaultFont();
        var size = UiTheme.FontSize - 2;
        var middle = RectOf(center);
        var right = middle.End.X + Gap;
        DrawString(font, new Vector2(0, 14), "D'OÙ VIENNENT SES MATIÈRES", fontSize: size - 2, modulate: UiTheme.Muted);
        DrawString(font, new Vector2(right, 14), "OÙ PART SA PRODUCTION", fontSize: size - 2, modulate: UiTheme.Muted);
        if (_emptyLeft.Length > 0)
            DrawString(font, new Vector2(0, middle.GetCenter().Y + 5), _emptyLeft, fontSize: size - 1, modulate: UiTheme.Muted);

        foreach (var box in _boxes.Where(b => b.Column != 0))
        {
            var rect = RectOf(box);
            if (box.Column < 0 && !box.Blocked)
                DrawArrow(new Vector2(rect.End.X, rect.GetCenter().Y), new Vector2(middle.Position.X, middle.GetCenter().Y), box.Line);
            else if (box.Column < 0)
                continue;
            else
                DrawArrow(new Vector2(middle.End.X, middle.GetCenter().Y), new Vector2(rect.Position.X, rect.GetCenter().Y), box.Line);
        }
        foreach (var box in _boxes)
            DrawBox(font, size, box, RectOf(box));
    }

    private void DrawBox(Font font, int size, Box box, Rect2 rect)
    {
        DrawRect(rect, box.Column == 0 ? UiTheme.Hover : UiTheme.Raised);
        if (box.Strip is { } strip)
            DrawRect(new Rect2(rect.Position, new Vector2(5, rect.Size.Y)), strip);
        if (box.Frame is { } frame)
            DrawRect(rect, frame, filled: false, width: 2);
        DrawString(font, rect.Position + new Vector2(12, 18), box.Title, HorizontalAlignment.Left, rect.Size.X - 18, size, UiTheme.Text);
        DrawString(font, rect.Position + new Vector2(12, 36), box.Detail, HorizontalAlignment.Left, rect.Size.X - 18, size - 2, UiTheme.Muted);
        // Un fournisseur écarté par les réglages d'achat : voilé et barré.
        if (box.Blocked)
        {
            DrawRect(rect, UiTheme.Background with { A = 0.55f });
            DrawLine(rect.Position + new Vector2(6, rect.Size.Y - 6), rect.End - new Vector2(6, rect.Size.Y - 6), UiTheme.Danger, 2);
        }
    }

    /// <summary>Une courbe douce avec une pointe ; plus épaisse et blanche pour une ligne d'approvisionnement.</summary>
    private void DrawArrow(Vector2 from, Vector2 to, bool line)
    {
        var bend = System.Math.Max(12, (to.X - from.X) / 2);
        var points = new Vector2[13];
        for (var index = 0; index <= 12; index++)
        {
            var t = index / 12f;
            var u = 1 - t;
            points[index] = u * u * u * from + 3 * u * u * t * (from + new Vector2(bend, 0)) + 3 * u * t * t * (to - new Vector2(bend, 0)) + t * t * t * to;
        }
        var color = line ? UiTheme.Text : UiTheme.Muted;
        DrawPolyline(points, color, line ? 3 : 1.5f, antialiased: true);
        DrawColoredPolygon([to, to + new Vector2(-8, -4), to + new Vector2(-8, 4)], color);
    }
}
