using System.Collections.Generic;
using Demos.Application;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Barre du haut, toujours visible : date et saison, pause et vitesses, trésor de l'État et légitimité des six pôles.
/// </summary>
public partial class TopBar : PanelContainer
{
    public event System.Action? PauseToggled;
    public event System.Action<int>? SpeedChosen;

    private Button _pause = null!;
    private readonly List<Button> _speeds = [];
    private Label _date = null!;
    private Label _season = null!;
    private Label _treasury = null!;
    private readonly Dictionary<Orientation, Label> _legitimacy = [];

    public void Init(IReadOnlyList<string> speedLabels)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        AddChild(row);

        _pause = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 0), TooltipText = "Espace" };
        _pause.Pressed += () => PauseToggled?.Invoke();
        row.AddChild(_pause);

        // Les boutons de vitesse forment un groupe : un seul enfoncé à la fois.
        var group = new ButtonGroup();
        for (var index = 0; index < speedLabels.Count; index++)
        {
            var speed = index;
            var button = new Button
            {
                Text = speedLabels[index],
                ToggleMode = true,
                ButtonGroup = group,
                FocusMode = FocusModeEnum.None,
                TooltipText = $"Touche {index + 1}",
            };
            button.Pressed += () => SpeedChosen?.Invoke(speed);
            _speeds.Add(button);
            row.AddChild(button);
        }

        row.AddChild(new VSeparator());
        _date = UiTheme.Title("");
        row.AddChild(_date);
        _season = UiTheme.Caption("");
        row.AddChild(_season);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        row.AddChild(UiTheme.Caption("Trésor"));
        _treasury = UiTheme.Title("");
        row.AddChild(_treasury);

        row.AddChild(new VSeparator());
        row.AddChild(UiTheme.Caption("Légitimité"));
        foreach (var pole in Orientation.All)
            row.AddChild(LegitimacyChip(pole));
    }

    public void Refresh(Simulation simulation, bool paused, int speedIndex)
    {
        var world = simulation.World;
        _pause.Text = paused ? "Reprendre" : "Pause";
        _date.Text = $"{simulation.Clock.Today:dd/MM/yyyy}";
        _season.Text = WorldText.SeasonName(simulation.Clock.Season);
        _treasury.Text = $"{world.Treasury.Balance:N0}";
        _treasury.AddThemeColorOverride("font_color", world.Treasury.Balance < 0 ? UiTheme.Danger : UiTheme.Text);

        for (var index = 0; index < _speeds.Count; index++)
            _speeds[index].SetPressedNoSignal(index == speedIndex);

        foreach (var (pole, label) in _legitimacy)
            label.Text = $"{pole.Code} {world.Legitimacy[pole]:F0}";
    }

    /// <summary>Une pastille de la couleur du pôle et sa légitimité ; le nom complet s'affiche au survol.</summary>
    private Control LegitimacyChip(Orientation pole)
    {
        var chip = new HBoxContainer { TooltipText = $"Légitimité {WorldText.PoleName(pole)}", MouseFilter = MouseFilterEnum.Pass };
        chip.AddThemeConstantOverride("separation", 4);
        chip.AddChild(new ColorRect
        {
            Color = UiTheme.PoleColor(pole),
            CustomMinimumSize = new Vector2(10, 10),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        var label = new Label { CustomMinimumSize = new Vector2(52, 0), MouseFilter = MouseFilterEnum.Ignore };
        chip.AddChild(label);
        _legitimacy[pole] = label;
        return chip;
    }
}
