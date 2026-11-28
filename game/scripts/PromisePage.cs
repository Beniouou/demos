using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// La page La Promesse (maquette du 27 novembre 2026) : à gauche le texte signé, qui pâlit avec la jauge et dont chaque
/// article a son encre ; à droite la jauge et son seuil de rupture, l'alignement du peuple axe par axe, les blessures
/// datées et la méfiance des classes trahies. Le nom du peuple se change ici. En bac à sable : pas de Promesse.
/// </summary>
public partial class PromisePage : HBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private string _shape = "";

    public PromisePage() { }

    public PromisePage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 24);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        Refresh();
    }

    public void Refresh()
    {
        var promise = _view.Promise();
        // On redessine quand la page change à l'œil : jauge au centième, blessures, encre des articles, nom.
        var shape = promise is null ? "aucune"
            : $"{promise.Name}|{promise.PeopleName}|{promise.Gauge:0.00}|{promise.Wounds.Count}|{promise.MonthsBelowThreshold}|{promise.Torn}|"
              + string.Join(",", promise.Engagements.Select(e => e.Ink.ToString("0.00")))
              + string.Join(",", promise.Axes.Select(a => a.Alignment.ToString("0.00")));
        if (shape == _shape)
            return;
        _shape = shape;
        foreach (var child in GetChildren())
            child.QueueFree();

        if (promise is null)
        {
            var none = UiTheme.Caption("Pas de Promesse : la partie se joue en bac à sable (ou la nation continue sans sa promesse). "
                + "Seule la révolution reste une fin.");
            none.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            none.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(none);
            return;
        }

        var left = new ScrollContainer { CustomMinimumSize = new Vector2(470, 0), SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(left);
        left.AddChild(SignedText.Of(promise, 440));

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 12);
        AddChild(right);
        BuildGauge(right, promise);
        BuildAxes(right, promise);
        BuildWounds(right, promise);
        BuildPeople(right, promise);
    }

    private static void BuildGauge(VBoxContainer parent, PromiseView promise)
    {
        var header = new HBoxContainer();
        parent.AddChild(header);
        header.AddChild(new Label { Text = "Jauge de la promesse", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        header.AddChild(new Label { Text = promise.Gauge.ToString("0.00") });
        parent.AddChild(new GaugeBar(promise.Gauge, promise.RuptureThreshold, UiTheme.Accent) { CustomMinimumSize = new Vector2(0, 14) });
        parent.AddChild(Note(promise.Torn
            ? "Le pacte est déchiré."
            : $"Le pacte se déchire après {promise.RuptureMonths} mois de suite sous {promise.RuptureThreshold:0.0#} (trait rouge). "
              + $"Mois sous le seuil : {promise.MonthsBelowThreshold}."));
    }

    private static void BuildAxes(VBoxContainer parent, PromiseView promise)
    {
        parent.AddChild(UiTheme.Section("Alignement du peuple, axe par axe"));
        foreach (var axis in promise.Axes)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            parent.AddChild(row);
            var name = new Label { Text = Capitalized(WorldText.PoleName(axis.Pole)), CustomMinimumSize = new Vector2(140, 0) };
            name.AddThemeColorOverride("font_color", UiTheme.PoleColor(axis.Pole));
            row.AddChild(name);
            row.AddChild(new GaugeBar(axis.Alignment, null, UiTheme.PoleColor(axis.Pole)) { CustomMinimumSize = new Vector2(0, 10), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter });
            row.AddChild(new Label { Text = axis.Alignment.ToString("0.00"), CustomMinimumSize = new Vector2(44, 0) });
        }

        var weakest = promise.Axes.OrderBy(a => a.Alignment).FirstOrDefault();
        if (weakest is not null)
            parent.AddChild(Note($"La part des habitants au pôle promis, chacun comptant pour un. La jauge suit l'axe le plus faible "
                + $"({WorldText.PoleName(weakest.Pole)}), moins les blessures."));
    }

    private static void BuildWounds(VBoxContainer parent, PromiseView promise)
    {
        var depth = promise.Wounds.Sum(w => w.Healing + w.Permanent) + promise.ContraryCharterShare * Demos.Domain.Economy.Balance.Current.PromiseCharterPenalty;
        parent.AddChild(UiTheme.Section($"Blessures : −{depth:0.00}"));
        if (promise.Wounds.Count == 0 && promise.ContraryCharterShare <= 0)
            parent.AddChild(Note("Aucune : La Promesse n'a jamais été trahie."));
        foreach (var wound in promise.Wounds.OrderByDescending(w => w.Date))
        {
            var article = promise.Engagements.ToList().FindIndex(e => e.Id == wound.EngagementId);
            var row = new HBoxContainer();
            parent.AddChild(row);
            var cause = new Label
            {
                Text = $"{wound.Date:dd/MM/yyyy} · {wound.Cause}{(article >= 0 ? $" (article {article + 1})" : "")}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            row.AddChild(cause);
            row.AddChild(UiTheme.Caption($"−{wound.Healing + wound.Permanent:0.00}, dont {wound.Permanent:0.00} pour toujours"));
        }

        if (promise.ContraryCharterShare > 0)
            parent.AddChild(Note($"Communes à charte contraire : {promise.ContraryCharterShare:P0} des habitants."));
    }

    private void BuildPeople(VBoxContainer parent, PromiseView promise)
    {
        parent.AddChild(UiTheme.Section("Le peuple"));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);
        var name = new LineEdit { Text = promise.PeopleName, CustomMinimumSize = new Vector2(240, 0) };
        row.AddChild(name);
        var rename = new Button { Text = "Renommer" };
        var message = UiTheme.Caption("");
        rename.Pressed += () => message.Text = _desk.NamePeople(name.Text);
        row.AddChild(rename);
        parent.AddChild(message);
    }

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static Label Note(string text)
    {
        var label = UiTheme.Caption(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }
}

/// <summary>Une barre de 0 à 1, avec au besoin un trait rouge au seuil.</summary>
public partial class GaugeBar : Control
{
    private readonly double _value;
    private readonly double? _threshold;
    private readonly Color _color;

    public GaugeBar() { }

    public GaugeBar(double value, double? threshold, Color color)
    {
        _value = System.Math.Clamp(value, 0, 1);
        _threshold = threshold;
        _color = color;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.Raised);
        DrawRect(new Rect2(Vector2.Zero, new Vector2((float)(_value * Size.X), Size.Y)), _color);
        if (_threshold is { } threshold)
        {
            var x = (float)(threshold * Size.X);
            DrawLine(new Vector2(x, -3), new Vector2(x, Size.Y + 3), UiTheme.Danger, 2);
        }
    }
}
