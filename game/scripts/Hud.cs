using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Écran de jeu du joueur : barre du haut, menus à gauche, place de la carte au centre, et en bas le dernier message
/// et les constructions de démonstration. N'affiche que l'état de la partie ; les actions remontent à <see cref="Main"/>.
/// </summary>
public partial class Hud : Control
{
    public event System.Action? BuildHousingPressed;
    public event System.Action? FoundCommunePressed;
    public event System.Action? AdminPressed;

    public TopBar TopBar { get; private set; } = null!;
    public MainMenu Menu { get; private set; } = null!;
    private Label _message = null!;

    public void Init(Simulation simulation, string[] speedLabels)
    {
        Theme = UiTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        AddChild(new ColorRect { Color = UiTheme.Background, MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });

        var layout = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 0);
        AddChild(layout);

        TopBar = new TopBar();
        TopBar.Init(speedLabels);
        layout.AddChild(TopBar);

        var middle = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        middle.AddThemeConstantOverride("separation", 0);
        layout.AddChild(middle);

        Menu = new MainMenu();
        Menu.Init(simulation.World);
        middle.AddChild(Menu);

        // La carte viendra ici ; en attendant, la place reste vide.
        var map = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        map.AddChild(UiTheme.Caption("Carte en régions — à venir"));
        middle.AddChild(map);

        layout.AddChild(BuildBottomBar());
    }

    public void ShowMessage(string message) => _message.Text = message;

    private Control BuildBottomBar()
    {
        var bar = new PanelContainer();
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        bar.AddChild(row);

        _message = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        row.AddChild(_message);

        row.AddChild(UiTheme.Caption("Démonstration :"));
        row.AddChild(ActionButton("Logements d'État", "Touche B : 500 places pour 200 pierre et 100 bois", () => BuildHousingPressed?.Invoke()));
        row.AddChild(ActionButton("Camp de base", "Touche C : fonde une nouvelle commune", () => FoundCommunePressed?.Invoke()));
        row.AddChild(ActionButton("Administration", "Touche A : outil de test des bâtiments", () => AdminPressed?.Invoke()));
        return bar;
    }

    private static Button ActionButton(string text, string tooltip, System.Action pressed)
    {
        var button = new Button { Text = text, TooltipText = tooltip, FocusMode = FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }
}
