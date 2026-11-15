using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Demos.Game;

/// <summary>
/// Une barre horizontale découpée en parts (les rangs, les deux pôles d'un axe…), chacune de sa couleur, avec son nom
/// et sa part écrits dedans quand la place suffit. Au survol, une bulle donne toutes les parts.
/// </summary>
public partial class StackedBar : Control
{
    /// <summary>Une part : son nom, sa valeur (les parts sont rapportées à leur total) et sa couleur.</summary>
    public sealed record Part(string Name, double Value, Color Color);

    private IReadOnlyList<Part> _parts = [];
    private System.Func<Part, double, string> _describe = (part, share) => $"{part.Name} {share:P0}";

    public StackedBar()
    {
        CustomMinimumSize = new Vector2(0, 24);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>Change les parts ; <paramref name="describe"/> écrit une part et sa part du total (« EG 96 % »).</summary>
    public void SetParts(IReadOnlyList<Part> parts, System.Func<Part, double, string>? describe = null)
    {
        _parts = parts;
        if (describe is not null)
            _describe = describe;
        var total = Total;
        TooltipText = string.Join(" · ", parts.Select(p => _describe(p, total > 0 ? p.Value / total : 0)));
        QueueRedraw();
    }

    private double Total => _parts.Sum(p => p.Value);

    public override Control? _MakeCustomTooltip(string forText) => UiTheme.Tooltip(forText);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.Surface);
        var total = Total;
        if (total <= 0)
            return;

        var font = GetThemeDefaultFont();
        var size = UiTheme.FontSize - 2;
        float x = 0;
        foreach (var part in _parts)
        {
            var share = part.Value / total;
            var width = (float)(Size.X * share);
            if (width <= 0)
                continue;
            DrawRect(new Rect2(x, 0, width, Size.Y), part.Color);
            // Le texte, sombre sur la couleur, seulement s'il tient dans sa part.
            var text = _describe(part, share);
            var textWidth = font.GetStringSize(text, fontSize: size).X;
            if (textWidth + 12 <= width)
                DrawString(font, new Vector2(x + 6, Size.Y / 2 + size / 2f - 2), text, fontSize: size, modulate: UiTheme.Background);
            x += width;
        }
    }
}
