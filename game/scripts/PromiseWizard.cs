using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>Ce que le joueur a choisi dans <see cref="PromiseWizard"/> : son peuple, La Promesse, ses engagements, ses lois de l'An I.</summary>
public sealed record PromiseChoice(
    string PeopleName,
    PromiseDefinition Promise,
    IReadOnlyList<EngagementDefinition> Engagements,
    IReadOnlyList<ScenarioLaw> Laws);

/// <summary>
/// Les écrans de La Promesse avant le serment (décidés et maquettés le 27 novembre 2026, canevas « Démos — début de
/// partie et Promesse ») : le nom du peuple, La Promesse axe par axe (le nom suit les axes et se renomme), un engagement
/// par axe avec sa difficulté, puis les trois lois de l'An I (l'acte fondateur), une loi qui romprait un engagement
/// étant grisée avec la raison. Sert aussi à la refondation, sans les lois et sans redemander le nom du peuple.
/// </summary>
public partial class PromiseWizard : Control
{
    /// <summary>Le nombre de lois de l'An I (décidé le 27 novembre 2026, proposition).</summary>
    public const int FoundingLawCount = 3;

    private static readonly string[] StepNames = ["Mode", "Peuple", "Promesse", "Engagements", "Lois de l'An I", "Serment"];

    private static readonly IdeologicalAxis[] Axes = [IdeologicalAxis.Hierarchy, IdeologicalAxis.Production, IdeologicalAxis.Property];

    private static readonly Dictionary<IdeologicalAxis, (string Name, string Question)> AxisTexts = new()
    {
        [IdeologicalAxis.Hierarchy] = ("Hiérarchie", "les gens ont-ils les mêmes droits et les mêmes conditions ?"),
        [IdeologicalAxis.Production] = ("Production", "faut-il produire plus, ou ménager la terre ?"),
        [IdeologicalAxis.Property] = ("Propriété", "à qui appartiennent les moyens de produire, et qui les gère ?"),
    };

    // Le pôle de chaque rang au départ d'exil, sur les axes libres (le départ du bac à sable, décidé le 27 novembre 2026).
    private static readonly (Rank Rank, string Name, Ideology Ideology)[] Ranks =
    [
        (Rank.Workers, "Ouvriers", new Ideology(Pole.A, Pole.A, Pole.A)),
        (Rank.Intermediate, "Intermédiaires", new Ideology(Pole.A, Pole.A, Pole.B)),
        (Rank.Executives, "Cadres", new Ideology(Pole.B, Pole.A, Pole.B)),
    ];

    /// <summary>Le joueur a fini : reste le serment.</summary>
    public event Action<PromiseChoice>? Finished;

    /// <summary>Le joueur revient avant le premier écran.</summary>
    public event Action? Cancelled;

    private readonly IReadOnlyList<PromiseDefinition> _promises = [];
    private readonly IReadOnlyList<EngagementDefinition> _engagements = [];
    private readonly LawCatalogue _laws = null!;
    private readonly bool _withLaws;
    private readonly bool _askPeople;

    private readonly Dictionary<IdeologicalAxis, Pole?> _poles = Axes.ToDictionary(a => a, _ => (Pole?)null);
    private readonly Dictionary<IdeologicalAxis, EngagementDefinition?> _chosenEngagements = [];
    private readonly HashSet<string> _chosenLaws = [];
    private string _peopleName = "";
    private string? _renamed;
    private int _step;
    private VBoxContainer _body = null!;
    private Control _stepsBar = null!;
    private Button _next = null!;

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PromiseWizard() { }

    /// <param name="withLaws">Vrai à la création de la partie (les lois de l'An I), faux pour une refondation.</param>
    /// <param name="peopleName">Le nom du peuple déjà connu (refondation) : l'écran du peuple est alors passé.</param>
    public PromiseWizard(
        IReadOnlyList<PromiseDefinition> promises,
        IReadOnlyList<EngagementDefinition> engagements,
        LawCatalogue laws,
        bool withLaws,
        string? peopleName = null)
    {
        _promises = promises;
        _engagements = engagements.Where(e => e.Kept).ToList();
        _laws = laws;
        _withLaws = withLaws;
        _askPeople = peopleName is null;
        _peopleName = peopleName ?? "";
        _step = _askPeople ? 1 : 2;

        Theme = UiTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = UiTheme.Background, AnchorRight = 1, AnchorBottom = 1 });
        var margin = UiTheme.Margin(32);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 24);
        column.AddChild(header);
        header.AddChild(UiTheme.Title(withLaws ? "Nouvelle partie" : "Refonder La Promesse"));
        _stepsBar = new HBoxContainer();
        header.AddChild(_stepsBar);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_body);

        var buttons = new HBoxContainer();
        column.AddChild(buttons);
        var back = new Button { Text = "Retour", CustomMinimumSize = new Vector2(140, 0) };
        back.Pressed += Back;
        buttons.AddChild(back);
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _next = new Button { Text = "Suivant", CustomMinimumSize = new Vector2(160, 0) };
        _next.Pressed += Forward;
        buttons.AddChild(_next);

        ShowStep();
    }

    /// <summary>La barre des étapes, l'étape en cours en couleur (aussi pour l'écran du mode).</summary>
    public static HBoxContainer Steps(int current, bool withLaws = true)
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 8);
        for (var i = 0; i < StepNames.Length; i++)
        {
            if (!withLaws && (i == 0 || i == 4))
                continue;
            if (bar.GetChildCount() > 0)
                bar.AddChild(UiTheme.Caption("›"));
            var label = i == current ? new Label { Text = StepNames[i] } : UiTheme.Caption(StepNames[i]);
            if (i == current)
                label.AddThemeColorOverride("font_color", UiTheme.Accent);
            bar.AddChild(label);
        }

        return bar;
    }

    /// <summary>La Promesse des pôles choisis, renommée si le joueur l'a fait ; null sans axe choisi.</summary>
    private PromiseDefinition? CurrentPromise()
    {
        var poles = _poles.Where(p => p.Value is not null).Select(p => new Orientation(p.Key, p.Value!.Value)).ToList();
        if (poles.Count == 0)
            return null;
        var promise = _promises.FirstOrDefault(p => p.Id == PromiseDefinition.IdOf(poles));
        return promise is not null && !string.IsNullOrWhiteSpace(_renamed) ? promise with { Name = _renamed.Trim() } : promise;
    }

    private IReadOnlyList<EngagementDefinition> CurrentEngagements() =>
        CurrentPromise() is { } promise
            ? promise.Orientations.Select(p => _chosenEngagements.GetValueOrDefault(p.Axis) is { } e && e.Orientation == p ? e : null).OfType<EngagementDefinition>().ToList()
            : [];

    private void ShowStep()
    {
        foreach (var child in _body.GetChildren())
            child.QueueFree();
        // La barre des étapes est refaite dans son support (ReplaceBy y déplacerait aussi les anciennes étiquettes).
        foreach (var child in _stepsBar.GetChildren())
            child.QueueFree();
        _stepsBar.AddChild(Steps(_step, _withLaws));

        switch (_step)
        {
            case 1: BuildPeople(); break;
            case 2: BuildPromise(); break;
            case 3: BuildEngagements(); break;
            default: BuildLaws(); break;
        }

        _next.Text = _step == LastStep ? "Prêter serment" : "Suivant";
        UpdateNext();
    }

    private int LastStep => _withLaws ? 4 : 3;

    private void UpdateNext() => _next.Disabled = _step switch
    {
        1 => string.IsNullOrWhiteSpace(_peopleName),
        2 => CurrentPromise() is null,
        3 => CurrentPromise() is not { } promise || CurrentEngagements().Count != promise.Orientations.Count,
        _ => _chosenLaws.Count > FoundingLawCount,
    };

    private void Back()
    {
        if (_step <= (_askPeople ? 1 : 2))
        {
            Cancelled?.Invoke();
            return;
        }

        _step--;
        ShowStep();
    }

    private void Forward()
    {
        if (_step < LastStep)
        {
            _step++;
            ShowStep();
            return;
        }

        if (CurrentPromise() is { } promise)
            Finished?.Invoke(new PromiseChoice(_peopleName.Trim(), promise, CurrentEngagements(),
                _chosenLaws.Select(id => new ScenarioLaw(id)).ToList()));
    }

    // ---- 2. Le peuple ----------------------------------------------------------------------------------------------

    private void BuildPeople()
    {
        _body.AddChild(Big("Qui sont les exilés ?"));
        _body.AddChild(UiTheme.Section("Nom du peuple"));
        var name = new LineEdit { Text = _peopleName, PlaceholderText = "le nom de votre peuple", CustomMinimumSize = new Vector2(420, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        name.AddThemeFontSizeOverride("font_size", UiTheme.TitleSize);
        name.TextChanged += text =>
        {
            _peopleName = text;
            UpdateNext();
        };
        name.TextSubmitted += _ =>
        {
            if (!_next.Disabled)
                Forward();
        };
        _body.AddChild(name);
        name.CallDeferred(Control.MethodName.GrabFocus);
        _body.AddChild(Wrapped("Il paraît dans le serment, les titres et l'épilogue. Il se change plus tard dans la page La Promesse."));
        _body.AddChild(UiTheme.Section("Ce peuple, à l'arrivée"));
        _body.AddChild(new Label { Text = "900 ouvriers  ·  90 professions intermédiaires  ·  10 cadres" });
        _body.AddChild(Wrapped("Leur idéologie se fixe à l'écran suivant : tous partagent La Promesse."));
    }

    // ---- 3. La Promesse --------------------------------------------------------------------------------------------

    /// <summary>
    /// Les axes, puis les exilés à l'arrivée, puis La Promesse en dessous (nom, première phrase, difficulté) : une seule
    /// colonne de largeur bornée, pour que rien ne déborde ni ne cache La Promesse (retour du 28 novembre 2026).
    /// </summary>
    private void BuildPromise()
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(PromiseColumnWidth, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        column.AddThemeConstantOverride("separation", 12);
        _body.AddChild(column);

        column.AddChild(UiTheme.Section("Ce que promettent les exilés, axe par axe"));
        foreach (var axis in Axes)
        {
            var (axisName, question) = AxisTexts[axis];
            column.AddChild(new Label { Text = $"{axisName}  ·  {question}" });
            var choices = new HBoxContainer();
            choices.AddThemeConstantOverride("separation", 8);
            column.AddChild(choices);
            var group = new ButtonGroup();
            foreach (var pole in new Pole?[] { Pole.A, null, Pole.B })
            {
                var text = pole is { } p ? Capitalized(WorldText.PoleName(new Orientation(axis, p))) : "Libre";
                var button = new Button { Text = text, ToggleMode = true, ButtonGroup = group, SizeFlagsHorizontal = SizeFlags.ExpandFill, ButtonPressed = _poles[axis] == pole };
                if (pole is { } colored)
                    button.AddThemeColorOverride("font_pressed_color", UiTheme.PoleColor(new Orientation(axis, colored)));
                button.Pressed += () =>
                {
                    _poles[axis] = pole;
                    _renamed = null;
                    ShowStep();
                };
                choices.AddChild(button);
            }
        }

        var promise = CurrentPromise();
        column.AddChild(UiTheme.Section("Vos exilés à l'arrivée"));
        column.AddChild(Wrapped(string.Join("   ·   ", Ranks.Select(r => $"{r.Name} {IdeologyText(r.Ideology, promise)}"))));
        column.AddChild(Wrapped("Sur les axes de La Promesse, tous sont au pôle promis ; sur un axe libre, chaque rang garde son pôle."));

        column.AddChild(new HSeparator());
        column.AddChild(UiTheme.Section("La Promesse"));
        if (promise is null)
        {
            column.AddChild(Wrapped("Choisissez un pôle sur un axe au moins."));
            return;
        }

        var name = new LineEdit { Text = promise.Name, CustomMinimumSize = new Vector2(420, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        name.AddThemeFontSizeOverride("font_size", UiTheme.TitleSize);
        name.TooltipText = "Le nom suit les axes choisis ; vous pouvez le changer.";
        name.TextChanged += text => _renamed = text;
        column.AddChild(name);
        var oath = PromiseOath.Of(promise, string.IsNullOrWhiteSpace(_peopleName) ? "…" : _peopleName.Trim());
        var paper = SignedText.Paper($"« {oath.First} »\n\n{oath.Middle}", PromiseColumnWidth);
        column.AddChild(paper);
        var axes = promise.Orientations.Count;
        column.AddChild(Wrapped($"{axes} axe{(axes > 1 ? "s" : "")} : difficulté {(axes switch { 1 => "faible", 2 => "moyenne", _ => "forte" })}. "
            + "Moins d'axes, c'est une promesse plus facile à tenir. La jauge suit l'axe le plus faible."));
    }

    /// <summary>La largeur de la colonne de l'écran de La Promesse, en pixels.</summary>
    private const float PromiseColumnWidth = 760;

    /// <summary>Essais (« --assistant lois ») : l'écran des lois de l'An I, avec deux axes et deux engagements concrets.</summary>
    public void ShowLawsStepForTests()
    {
        ShowPromiseStepForTests();
        _chosenEngagements[IdeologicalAxis.Hierarchy] = _engagements.First(e => e.Id == "eg-logement");
        _chosenEngagements[IdeologicalAxis.Production] = _engagements.First(e => e.Id == "ec-defrichement");
        _step = 4;
        ShowStep();
    }

    /// <summary>Essais (« --assistant promesse ») : un peuple nommé, l'écran de La Promesse avec deux axes choisis.</summary>
    public void ShowPromiseStepForTests()
    {
        _peopleName = "Essai";
        _poles[IdeologicalAxis.Hierarchy] = Pole.A;
        _poles[IdeologicalAxis.Production] = Pole.B;
        _step = 2;
        ShowStep();
    }

    /// <summary>« EG · EC · CO », les pôles de La Promesse en premier, comme ils seront à l'arrivée.</summary>
    private static string IdeologyText(Ideology ideology, PromiseDefinition? promise)
    {
        if (promise is not null)
            foreach (var pole in promise.Orientations)
                ideology = ideology.With(pole.Axis, pole.Pole);
        return string.Join(" · ", Axes.Select(a => new Orientation(a, ideology[a]).Code));
    }

    // ---- 4. Les engagements ----------------------------------------------------------------------------------------

    private void BuildEngagements()
    {
        if (CurrentPromise() is not { } promise)
            return;

        _body.AddChild(Wrapped("Un engagement par axe de La Promesse. Il porte sur un acte : le jeu vous préviendra avant, sans "
            + "jamais l'interdire. Le rompre, c'est trahir."));
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 28);
        _body.AddChild(columns);
        foreach (var pole in promise.Orientations)
        {
            var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", 6);
            columns.AddChild(column);
            var title = new Label { Text = Capitalized(WorldText.PoleName(pole)) };
            title.AddThemeColorOverride("font_color", UiTheme.PoleColor(pole));
            title.AddThemeFontSizeOverride("font_size", UiTheme.TitleSize);
            column.AddChild(title);
            var group = new ButtonGroup();
            foreach (var kind in new[] { EngagementKind.Principle, EngagementKind.Concrete })
            {
                column.AddChild(UiTheme.Section(kind == EngagementKind.Principle ? "De principe" : "Concrets"));
                foreach (var engagement in _engagements.Where(e => e.Orientation == pole && e.Kind == kind))
                {
                    var button = new Button
                    {
                        Text = $"{engagement.Text}   {Dots(engagement.Difficulty)}",
                        ToggleMode = true,
                        ButtonGroup = group,
                        Alignment = HorizontalAlignment.Left,
                        ButtonPressed = _chosenEngagements.GetValueOrDefault(pole.Axis)?.Id == engagement.Id,
                        TooltipText = $"Rompu si : {engagement.Breach}",
                    };
                    button.Pressed += () =>
                    {
                        _chosenEngagements[pole.Axis] = engagement;
                        // Une loi de l'An I qui romprait le nouvel engagement n'est plus permise.
                        _chosenLaws.RemoveWhere(id => Problem(id) is not null);
                        ShowStep();
                    };
                    column.AddChild(button);
                    if (button.ButtonPressed)
                        column.AddChild(Wrapped($"Rompu si : {engagement.Breach}"));
                }
            }
        }

        var chosen = CurrentEngagements();
        _body.AddChild(Wrapped(chosen.Count == promise.Orientations.Count
            ? $"Difficulté de La Promesse : {chosen.Sum(e => e.Difficulty)} sur {3 * chosen.Count}  (● facile à tenir, ●●● exigeant)."
            : "Choisissez un engagement sur chaque axe."));
    }

    private static string Dots(int difficulty) => new string('●', Math.Clamp(difficulty, 0, 3)) + new string('○', 3 - Math.Clamp(difficulty, 0, 3));

    // ---- 5. Les lois de l'An I -------------------------------------------------------------------------------------

    /// <summary>Pourquoi une loi de l'An I est grisée (contraire à La Promesse, ou rompt un engagement), ou null.</summary>
    private string? Problem(string templateId) =>
        CurrentPromise() is { } promise
            ? PrototypeScenario.FoundingLawProblem(_laws, new ScenarioLaw(templateId), promise, CurrentEngagements())
            : null;

    private void BuildLaws()
    {
        // Une loi cochée qui serait devenue contraire (Promesse ou engagement changés) est retirée.
        _chosenLaws.RemoveWhere(id => Problem(id) is not null);
        var header = new HBoxContainer();
        _body.AddChild(header);
        header.AddChild(Wrapped($"L'acte fondateur : {FoundingLawCount} lois de l'État, votées sans coût, le jour du serment. "
            + "Une loi contraire à La Promesse, ou qui romprait un engagement, est grisée."));
        header.AddChild(new Label { Text = $"{_chosenLaws.Count} sur {FoundingLawCount}", SizeFlagsHorizontal = SizeFlags.ShrinkEnd });

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        _body.AddChild(grid);
        foreach (var template in _laws.Templates)
        {
            var problem = Problem(template.Id);
            var domain = _laws.Domains.FirstOrDefault(d => d.Id == template.Domain)?.Name ?? template.Domain;
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
            var check = new CheckBox
            {
                Text = template.Name,
                ButtonPressed = _chosenLaws.Contains(template.Id),
                Disabled = problem is not null || (!_chosenLaws.Contains(template.Id) && _chosenLaws.Count >= FoundingLawCount),
                TooltipText = template.Description ?? "",
            };
            check.Toggled += on =>
            {
                if (on)
                    _chosenLaws.Add(template.Id);
                else
                    _chosenLaws.Remove(template.Id);
                ShowStep();
            };
            box.AddChild(check);
            var note = problem is not null ? UiTheme.Caption(char.ToUpperInvariant(problem[0]) + problem[1..]) : UiTheme.Caption(domain);
            if (problem is not null)
                note.AddThemeColorOverride("font_color", UiTheme.Accent);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(note);
            grid.AddChild(box);
        }

        if (!_chosenLaws.Contains("loi-extension"))
            _body.AddChild(Wrapped("Sans loi d'extension : pas de nouvelle commune, ni de bâtiment d'extraction au-delà de "
                + "8 km. Elle pourra être votée en jeu, à crédit de légitimité."));
    }

    // ---- Petits éléments -------------------------------------------------------------------------------------------

    private static Label Big(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 26);
        return label;
    }

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static Label Wrapped(string text)
    {
        var label = UiTheme.Caption(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }
}
