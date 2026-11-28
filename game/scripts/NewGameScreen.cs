using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Infrastructure;
using Godot;

namespace Demos.Game;

/// <summary>
/// L'écran « Nouvelle partie », avant la partie (24 novembre 2026, mode ajouté le 27 novembre 2026) : la carte parmi
/// celles de <c>res://data/cartes/</c> (la carte par défaut choisie d'avance et marquée ★), puis le mode. Avec La
/// Promesse, la suite se fait dans <see cref="PromiseWizard"/> (peuple, Promesse, engagements, lois de l'An I) puis
/// <see cref="OathScreen"/> (recopier, signer) ; la terre d'exil est tirée au hasard. En bac à sable, le joueur prend
/// le départ riche (départ préréglé) ou une terre d'exil (départ d'exil, sans Promesse).
/// </summary>
public partial class NewGameScreen : Control
{
    private const string DataFolder = "res://data/";

    /// <summary>Le joueur a tout choisi : la partie peut commencer.</summary>
    public event Action<NewGameChoice>? Started;

    private readonly List<string> _maps = [];
    private ItemList _list = null!;
    private Control _modeScreen = null!;
    private Button _withPromise = null!;
    private CheckBox _richStart = null!;
    private PromiseWizard? _wizard;

    public void Init()
    {
        Theme = UiTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = UiTheme.Background, AnchorRight = 1, AnchorBottom = 1 });

        _modeScreen = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(_modeScreen);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(860, 0) };
        _modeScreen.AddChild(panel);
        var margin = UiTheme.Margin(24);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);

        column.AddChild(UiTheme.Title("Projet Démos — Nouvelle partie"));
        column.AddChild(PromiseWizard.Steps(0));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 24);
        column.AddChild(row);

        var maps = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        row.AddChild(maps);
        maps.AddChild(UiTheme.Section("Carte"));
        _list = new ItemList { CustomMinimumSize = new Vector2(0, 220), SelectMode = ItemList.SelectModeEnum.Single };
        maps.AddChild(_list);
        var defaultMap = MapFiles.DefaultName(file => FileAccess.GetFileAsString(DataFolder + file));
        foreach (var folder in SavedMaps())
        {
            var name = MapName(folder);
            var label = string.IsNullOrEmpty(name) ? folder : $"{name}  ({folder})";
            if (folder == defaultMap)
                label = "★ " + label;
            _maps.Add(folder);
            _list.AddItem(label);
        }

        if (_maps.Count > 0)
            _list.Select(Math.Max(0, _maps.IndexOf(defaultMap)));

        var modes = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        modes.AddThemeConstantOverride("separation", 10);
        row.AddChild(modes);
        modes.AddChild(UiTheme.Section("Mode"));
        var group = new ButtonGroup();
        _withPromise = ModeButton(modes, group, "Avec La Promesse",
            "Vous nommez votre peuple, choisissez sa Promesse et ses engagements, puis prêtez serment. Départ d'exil : "
            + "1 000 exilés, un campement de survie. La terre d'exil est tirée au hasard parmi celles de la carte : on ne "
            + "choisit pas où l'on échoue.");
        _withPromise.ButtonPressed = true;
        var sandbox = ModeButton(modes, group, "Bac à sable", "Sans Promesse, sans fin, sauf la révolution.");
        _richStart = new CheckBox { Text = "Départ riche (départ préréglé) ; décoché : une terre d'exil, départ d'exil", ButtonPressed = true };
        modes.AddChild(_richStart);
        _richStart.Disabled = true;
        sandbox.Toggled += on => _richStart.Disabled = !on;

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        column.AddChild(buttons);
        var next = new Button { Text = "Suivant", CustomMinimumSize = new Vector2(160, 0) };
        next.Pressed += Next;
        buttons.AddChild(next);
        _list.ItemActivated += _ => Next();
        next.CallDeferred(Control.MethodName.GrabFocus);
    }

    private static Button ModeButton(VBoxContainer parent, ButtonGroup group, string title, string text)
    {
        var button = new Button { Text = title, ToggleMode = true, ButtonGroup = group, Alignment = HorizontalAlignment.Left };
        button.AddThemeFontSizeOverride("font_size", UiTheme.TitleSize);
        parent.AddChild(button);
        var note = UiTheme.Caption(text);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        parent.AddChild(note);
        return button;
    }

    private string? SelectedMap() =>
        _list.GetSelectedItems() is { Length: > 0 } items && items[0] < _maps.Count ? _maps[items[0]] : _maps.FirstOrDefault();

    /// <summary>Essais : passe l'écran du mode, avec La Promesse.</summary>
    public void SkipToPromise()
    {
        _withPromise.ButtonPressed = true;
        Next();
        // « --assistant promesse » : droit à l'écran de La Promesse, avec un peuple et deux axes choisis.
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "promesse") >= 0)
            _wizard?.ShowPromiseStepForTests();
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "lois") >= 0)
            _wizard?.ShowLawsStepForTests();
    }

    private void Next()
    {
        var map = SelectedMap();
        if (!_withPromise.ButtonPressed)
        {
            Started?.Invoke(_richStart.ButtonPressed
                ? new NewGameChoice(map, null, null)
                : new NewGameChoice(map, new ExileStart("", null, [], (int)Time.GetTicksMsec()), StartingLaws.None));
            return;
        }

        // Avec La Promesse : le peuple, La Promesse, les engagements et les lois de l'An I, puis le serment.
        _modeScreen.Visible = false;
        var content = new JsonContentSource(file => FileAccess.GetFileAsString(DataFolder + file));
        var laws = content.LoadLawCatalogue();
        var wizard = new PromiseWizard(content.LoadPromises(), content.LoadEngagements(), laws, withLaws: true);
        AddChild(wizard);
        _wizard = wizard;
        wizard.Cancelled += () =>
        {
            wizard.QueueFree();
            _modeScreen.Visible = true;
        };
        wizard.Finished += choice =>
        {
            wizard.QueueFree();
            var lawNames = choice.Laws.Select(l => laws.TemplateOf(l.Template)?.Name ?? l.Template).ToList();
            var oath = new OathScreen(choice, lawNames);
            AddChild(oath);
            oath.Signed += signature => Started?.Invoke(new NewGameChoice(
                map,
                new ExileStart(choice.PeopleName, choice.Promise, choice.Engagements, (int)Time.GetTicksMsec(), signature),
                new StartingLaws(StartingLawsMode.Chosen, choice.Laws)));
        };
    }

    /// <summary>Les dossiers de <c>res://data/cartes/</c> qui contiennent une carte, dans l'ordre alphabétique.</summary>
    private static IEnumerable<string> SavedMaps() =>
        DirAccess.GetDirectoriesAt(DataFolder + MapFiles.Folder)
            .Where(folder => FileAccess.FileExists(DataFolder + MapFiles.PathOf(folder, MapFiles.MapFile)))
            .OrderBy(folder => folder, StringComparer.Ordinal);

    /// <summary>Le nom écrit en tête de la carte, sans lire tout le fichier.</summary>
    private static string MapName(string folder)
    {
        using var file = FileAccess.Open(DataFolder + MapFiles.PathOf(folder, MapFiles.MapFile), FileAccess.ModeFlags.Read);
        return file is null ? "" : MapFiles.NameFromStart(file.GetBuffer(4096));
    }
}
