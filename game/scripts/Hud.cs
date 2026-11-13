using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Écran de jeu du joueur : barre du haut, menus à gauche, place de la carte au centre, et en bas le dernier message
/// et les constructions de démonstration. N'affiche que l'état de la partie ; les décisions passent par le bureau du joueur, les constructions de démonstration remontent à <see cref="Main"/>.
/// </summary>
public partial class Hud : Control
{
    public event System.Action? FoundCommunePressed;

    public TopBar TopBar { get; private set; } = null!;
    public MainMenu Menu { get; private set; } = null!;
    public Demos.Game.Map.MapCanvas Map { get; private set; } = null!;
    private Label _message = null!;

    public void Init(GameView view, PlayerDesk desk, string[] speedLabels)
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

        Map = new Demos.Game.Map.MapCanvas
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        Map.Init(view);

        Menu = new MainMenu();
        Menu.Init(view, desk, Map);
        Menu.Message += ShowMessage;
        middle.AddChild(Menu);
        middle.AddChild(Map);

        layout.AddChild(BuildBottomBar());
    }

    /// <summary>Le dernier message ; coupé s'il est trop long pour la barre, il se lit en entier au survol.</summary>
    public void ShowMessage(string message)
    {
        _message.Text = message;
        _message.TooltipText = message;
    }

    private Control BuildBottomBar()
    {
        var bar = new PanelContainer();
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        bar.AddChild(row);

        _message = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            // Pour la bulle du message entier au survol (un Label ignore la souris par défaut).
            MouseFilter = MouseFilterEnum.Pass,
        };
        row.AddChild(_message);

        row.AddChild(UiTheme.Caption("Démonstration :"));
        row.AddChild(ActionButton("Camp de base", "Touche C : fonde une nouvelle commune", () => FoundCommunePressed?.Invoke()));
        return bar;
    }

    private static Button ActionButton(string text, string tooltip, System.Action pressed)
    {
        var button = new Button { Text = text, TooltipText = tooltip, FocusMode = FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }
}
