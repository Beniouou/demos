using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Population;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// La carte des groupes sociaux (décidé le 21 novembre 2026), dessinée par Godot : deux axes idéologiques au choix, un
/// quart par paire de pôles ; chaque groupe est une bulle de la taille de ses membres, placée dans son quart selon son
/// rang (colonnes) et son pôle sur le troisième axe (lignes), colorée par ce pôle. Un clic choisit un groupe.
/// </summary>
public partial class GroupMap : Control
{
    /// <summary>Un groupe sur la carte : sa clé, son rang, ses pôles, ses membres.</summary>
    public sealed record Bubble(string Key, Rank Rank, IReadOnlyList<Orientation> Poles, double Members);

    private const float Margin = 30;

    private IReadOnlyList<Bubble> _bubbles = [];
    private readonly List<(string Key, Vector2 Center, float Radius)> _drawn = [];
    private IdeologicalAxis _x = IdeologicalAxis.Hierarchy;
    private IdeologicalAxis _y = IdeologicalAxis.Property;

    public string? Selected { get; private set; }

    /// <summary>Un groupe a été choisi d'un clic.</summary>
    public event System.Action<string>? Chosen;

    public GroupMap()
    {
        CustomMinimumSize = new Vector2(360, 320);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>Le troisième axe, celui que montre la couleur des bulles.</summary>
    public IdeologicalAxis ThirdAxis => System.Enum.GetValues<IdeologicalAxis>().First(a => a != _x && a != _y);

    public void SetAxes(IdeologicalAxis x, IdeologicalAxis y)
    {
        _x = x;
        _y = y;
        QueueRedraw();
    }

    public void SetGroups(IReadOnlyList<Bubble> bubbles, string? selected)
    {
        _bubbles = bubbles;
        Selected = selected;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            var hit = _drawn.Where(d => d.Center.DistanceTo(click.Position) <= d.Radius + 2)
                .OrderBy(d => d.Center.DistanceTo(click.Position)).FirstOrDefault();
            if (hit.Key is { } key)
            {
                Selected = key;
                QueueRedraw();
                Chosen?.Invoke(key);
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            var hover = _drawn.FirstOrDefault(d => d.Center.DistanceTo(motion.Position) <= d.Radius + 2);
            TooltipText = hover.Key is { } key && _bubbles.FirstOrDefault(b => b.Key == key) is { } bubble
                ? $"{ClassTransitions.RankName(bubble.Rank)} · {string.Join(" ", bubble.Poles.Select(p => p.Code))} · {bubble.Members:N0} membres"
                : "";
        }
    }

    public override Control? _MakeCustomTooltip(string forText) => string.IsNullOrEmpty(forText) ? null : UiTheme.Tooltip(forText);

    public override void _Draw()
    {
        _drawn.Clear();
        var font = GetThemeDefaultFont();
        var size = GetThemeDefaultFontSize();
        var area = new Rect2(Margin, Margin, Size.X - 2 * Margin, Size.Y - 2 * Margin);
        var center = area.GetCenter();

        DrawRect(area, UiTheme.Raised, filled: true);
        DrawLine(new Vector2(area.Position.X, center.Y), new Vector2(area.End.X, center.Y), UiTheme.Border, 1);
        DrawLine(new Vector2(center.X, area.Position.Y), new Vector2(center.X, area.End.Y), UiTheme.Border, 1);

        // Les pôles aux bouts des axes : A à gauche et en bas, B à droite et en haut.
        void PoleLabel(Orientation pole, Vector2 position, HorizontalAlignment align)
        {
            var text = WorldText.PoleName(pole);
            var width = font.GetStringSize(text, align, -1, size - 2).X;
            var x = align == HorizontalAlignment.Right ? position.X - width : align == HorizontalAlignment.Center ? position.X - width / 2 : position.X;
            if (IconRegistry.GetPoleIcon(pole, 12) is { } icon)
            {
                DrawTextureRect(icon, new Rect2(new Vector2(x - icon.GetWidth() - 3, position.Y - icon.GetHeight() + 3), icon.GetSize()), false);
            }
            DrawString(font, new Vector2(x, position.Y), text, HorizontalAlignment.Left, -1, size - 2, UiTheme.Muted);
        }
        PoleLabel(new Orientation(_x, Pole.A), new Vector2(area.Position.X + 22, center.Y - 6), HorizontalAlignment.Left);
        PoleLabel(new Orientation(_x, Pole.B), new Vector2(area.End.X - 4, center.Y - 6), HorizontalAlignment.Right);
        PoleLabel(new Orientation(_y, Pole.B), new Vector2(center.X, area.Position.Y - 8), HorizontalAlignment.Center);
        PoleLabel(new Orientation(_y, Pole.A), new Vector2(center.X, area.End.Y + 20), HorizontalAlignment.Center);

        if (_bubbles.Count == 0)
            return;

        // Chaque quart : 3 colonnes (rangs) × 2 lignes (pôle du troisième axe).
        var third = ThirdAxis;
        var quarter = area.Size / 2;
        var slot = new Vector2(quarter.X / 3, quarter.Y / 2);
        var maxRadius = System.Math.Min(slot.X, slot.Y) / 2 - 2;
        var maxMembers = _bubbles.Max(b => b.Members);

        foreach (var bubble in _bubbles.OrderByDescending(b => b.Members))
        {
            Pole PoleOn(IdeologicalAxis axis) => bubble.Poles.FirstOrDefault(p => p.Axis == axis).Pole;
            var quarterOrigin = new Vector2(
                PoleOn(_x) == Pole.A ? area.Position.X : center.X,
                PoleOn(_y) == Pole.B ? area.Position.Y : center.Y);
            var column = (int)bubble.Rank;
            var row = PoleOn(third) == Pole.A ? 0 : 1;
            var position = quarterOrigin + new Vector2((column + 0.5f) * slot.X, (row + 0.5f) * slot.Y);
            var radius = (float)(6 + (maxRadius - 6) * System.Math.Sqrt(maxMembers > 0 ? bubble.Members / maxMembers : 0));
            var color = UiTheme.PoleColor(new Orientation(third, PoleOn(third)));

            DrawCircle(position, radius, color with { A = 0.55f });
            DrawArc(position, radius, 0, Mathf.Tau, 48, bubble.Key == Selected ? UiTheme.Text : color, bubble.Key == Selected ? 2.5f : 1, true);
            if (IconRegistry.GetRankIcon(bubble.Rank, 10) is { } rankIcon && radius >= 12)
                DrawTextureRect(rankIcon, new Rect2(position - rankIcon.GetSize() / 2 - new Vector2(0, 6), rankIcon.GetSize()), false);
            var label = bubble.Members >= 1000 ? $"{bubble.Members / 1000:0.#} k" : $"{bubble.Members:N0}";
            var width = font.GetStringSize(label, HorizontalAlignment.Left, -1, size - 3).X;
            DrawString(font, position + new Vector2(-width / 2, radius >= 12 ? 14 : 4), label, HorizontalAlignment.Left, -1, size - 3, UiTheme.Text);
            _drawn.Add((bubble.Key, position, radius));
        }
    }
}
