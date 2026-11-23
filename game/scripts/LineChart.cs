using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Demos.Game;

/// <summary>
/// Un graphique en courbes, dessiné par Godot : une courbe par série, l'axe vertical part de 0, une légende en haut.
/// Au survol, une ligne verticale montre le relevé le plus proche et une boîte donne sa date et la valeur de chaque
/// série. Une valeur manquante (NaN) coupe la courbe : une commune fondée en cours de partie commence plus tard.
/// </summary>
public partial class LineChart : Control
{
    /// <summary>Une courbe : son nom (dans la légende), sa couleur et une valeur par relevé.</summary>
    public sealed record Series(string Name, Color Color, IReadOnlyList<double> Values);

    private const float Left = 64;
    private const float Right = 12;
    private const float Top = 28;
    private const float Bottom = 24;

    private IReadOnlyList<string> _labels = [];
    private IReadOnlyList<Series> _series = [];
    private System.Func<double, string> _format = v => $"{v:N0}";
    private double? _fixedMax;
    private int _hover = -1;

    public LineChart()
    {
        CustomMinimumSize = new Vector2(0, 240);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Pass;
        MouseExited += () => SetHover(-1);
    }

    /// <summary>
    /// Change les données : les dates des relevés (axe horizontal), les courbes, la mise en forme d'une valeur, et le
    /// haut de l'axe vertical s'il est fixe (1 pour une part : de 0 à 100 %), sinon le plus haut relevé.
    /// </summary>
    public void SetData(IReadOnlyList<string> labels, IReadOnlyList<Series> series, System.Func<double, string> format, double? fixedMax = null)
    {
        _labels = labels;
        _series = series;
        _format = format;
        _fixedMax = fixedMax;
        _hover = System.Math.Min(_hover, labels.Count - 1);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
            SetHover(IndexAt(motion.Position.X));
    }

    private void SetHover(int index)
    {
        if (index == _hover)
            return;
        _hover = index;
        QueueRedraw();
    }

    private Rect2 Plot => new(Left, Top, System.Math.Max(1, Size.X - Left - Right), System.Math.Max(1, Size.Y - Top - Bottom));

    private int IndexAt(float x)
    {
        if (_labels.Count == 0)
            return -1;
        if (_labels.Count == 1)
            return 0;
        var plot = Plot;
        var index = (int)System.Math.Round((x - plot.Position.X) / plot.Size.X * (_labels.Count - 1));
        return System.Math.Clamp(index, 0, _labels.Count - 1);
    }

    private float XAt(int index)
    {
        var plot = Plot;
        return _labels.Count <= 1 ? plot.Position.X + plot.Size.X / 2 : plot.Position.X + plot.Size.X * index / (_labels.Count - 1);
    }

    /// <summary>
    /// L'axe vertical : de 0 au haut fixe pour une part ; sinon serré autour des relevés (des valeurs rondes autour du
    /// plus bas et du plus haut), pour qu'une variation de quelques dizaines sur 5 000 se voie.
    /// </summary>
    private (double Low, double High, double Step) Axis()
    {
        if (_fixedMax is { } max)
            return (0, max, max / 4);
        var values = _series.SelectMany(s => s.Values).Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).ToList();
        var (lowest, highest) = (values.Min(), values.Max());
        var range = highest - lowest;
        if (range <= 0)
            range = System.Math.Max(1, System.Math.Abs(highest) * 0.1);
        var step = NiceCeiling(range / 4);
        var low = System.Math.Floor(lowest / step) * step;
        var high = System.Math.Max(low + step, System.Math.Ceiling(highest / step) * step);
        if (lowest == highest)
            (low, high) = (low - step, high + step);
        // L'axe part de 0 au plus bas, sauf pour des valeurs négatives (un pôle de légitimité en dette).
        return (lowest >= 0 ? System.Math.Max(0, low) : low, high, step);
    }

    // Un haut d'axe rond (1, 2, 2,5 ou 5 × une puissance de 10) au-dessus du plus haut relevé.
    private static double NiceCeiling(double value)
    {
        var power = System.Math.Pow(10, System.Math.Floor(System.Math.Log10(value)));
        foreach (var step in new[] { 1, 2, 2.5, 5, 10 })
            if (step * power >= value)
                return step * power;
        return 10 * power;
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var size = UiTheme.FontSize - 2;
        var plot = Plot;
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.Surface);

        // La légende, en haut : un carré de la couleur de chaque courbe et son nom.
        var x = Left;
        foreach (var series in _series)
        {
            DrawRect(new Rect2(x, 9, 10, 10), series.Color);
            DrawString(font, new Vector2(x + 14, 18), series.Name, fontSize: size, modulate: UiTheme.Text);
            x += 14 + font.GetStringSize(series.Name, fontSize: size).X + 16;
        }

        if (_labels.Count == 0)
        {
            DrawString(font, new Vector2(plot.Position.X + 8, plot.Position.Y + plot.Size.Y / 2), "Aucun relevé encore.", fontSize: size, modulate: UiTheme.Muted);
            return;
        }

        // L'axe vertical : une ligne de repère par pas, du bas au haut de l'axe.
        var (low, high, axisStep) = Axis();
        var lines = (int)System.Math.Round((high - low) / axisStep);
        for (var line = 0; line <= lines; line++)
        {
            var y = plot.End.Y - plot.Size.Y * line / lines;
            DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), UiTheme.Border);
            var text = _format(low + axisStep * line);
            var width = font.GetStringSize(text, fontSize: size).X;
            DrawString(font, new Vector2(Left - 6 - width, y + 5), text, fontSize: size, modulate: UiTheme.Muted);
        }

        // L'axe horizontal : au plus huit dates, régulièrement espacées.
        var step = System.Math.Max(1, (int)System.Math.Ceiling(_labels.Count / 8.0));
        for (var index = 0; index < _labels.Count; index += step)
        {
            var width = font.GetStringSize(_labels[index], fontSize: size).X;
            var left = System.Math.Clamp(XAt(index) - width / 2, 0, Size.X - width);
            DrawString(font, new Vector2(left, Size.Y - 6), _labels[index], fontSize: size, modulate: UiTheme.Muted);
        }

        foreach (var series in _series)
        {
            var points = new List<Vector2>();
            for (var index = 0; index <= series.Values.Count; index++)
            {
                var value = index < series.Values.Count ? series.Values[index] : double.NaN;
                if (double.IsNaN(value))
                {
                    DrawSegment(points, series.Color);
                    points.Clear();
                    continue;
                }
                var height = System.Math.Clamp((value - low) / (high - low), 0, 1);
                points.Add(new Vector2(XAt(index), plot.End.Y - (float)(plot.Size.Y * height)));
            }
        }

        if (_hover >= 0)
            DrawHover(font, size, plot);
    }

    private void DrawSegment(List<Vector2> points, Color color)
    {
        if (points.Count >= 2)
            DrawPolyline(points.ToArray(), color, 2, antialiased: true);
        foreach (var point in points)
            DrawCircle(point, 3, color);
    }

    /// <summary>La ligne verticale du relevé survolé et sa boîte : la date, puis la valeur de chaque courbe.</summary>
    private void DrawHover(Font font, int size, Rect2 plot)
    {
        var x = XAt(_hover);
        DrawLine(new Vector2(x, plot.Position.Y), new Vector2(x, plot.End.Y), UiTheme.Muted);

        var lines = new List<(string Text, Color Color)> { (_labels[_hover], UiTheme.Text) };
        foreach (var series in _series)
            if (_hover < series.Values.Count && !double.IsNaN(series.Values[_hover]))
                lines.Add(($"{series.Name} : {_format(series.Values[_hover])}", series.Color));

        var lineHeight = size + 6;
        var width = lines.Max(l => font.GetStringSize(l.Text, fontSize: size).X) + 16;
        var height = lines.Count * lineHeight + 8;
        var left = x + 10 + width > Size.X ? x - 10 - width : x + 10;
        var box = new Rect2(left, plot.Position.Y + 4, width, height);
        DrawRect(box, UiTheme.Raised);
        DrawRect(box, UiTheme.Border, filled: false);
        for (var index = 0; index < lines.Count; index++)
            DrawString(font, new Vector2(box.Position.X + 8, box.Position.Y + 4 + (index + 1) * lineHeight - 6), lines[index].Text,
                fontSize: size, modulate: lines[index].Color);
    }
}
