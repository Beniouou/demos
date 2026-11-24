using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Infrastructure;
using Godot;

namespace Demos.Game;

/// <summary>
/// L'écran « Nouvelle partie », avant la partie (24 novembre 2026) : le joueur choisit la carte parmi celles de
/// <c>res://data/cartes/</c> ; la carte par défaut (<c>cartes/carte-par-defaut.txt</c>) est choisie d'avance et marquée ★.
/// Le choix du mode (avec Promesse ou bac à sable) et du départ viendra avec le départ d'exil.
/// </summary>
public partial class NewGameScreen : Control
{
    private const string DataFolder = "res://data/";

    /// <summary>Le joueur a choisi sa carte : le nom de son dossier (null s'il n'y a aucune carte rangée par dossier).</summary>
    public event Action<string?>? Started;

    private readonly List<string> _maps = [];
    private ItemList _list = null!;

    public void Init()
    {
        Theme = UiTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = UiTheme.Background, AnchorRight = 1, AnchorBottom = 1 });

        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
        center.AddChild(panel);
        var margin = UiTheme.Margin(20);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        column.AddChild(UiTheme.Title("Projet Démos"));
        column.AddChild(UiTheme.Section("Nouvelle partie"));
        column.AddChild(UiTheme.Caption("Carte :"));

        _list = new ItemList { CustomMinimumSize = new Vector2(0, 180), SelectMode = ItemList.SelectModeEnum.Single };
        column.AddChild(_list);

        var defaultMap = MapFiles.DefaultName(file => FileAccess.GetFileAsString(DataFolder + file));
        foreach (var folder in SavedMaps())
        {
            var name = MapName(folder);
            var label = string.IsNullOrEmpty(name) ? folder : $"{name}  ({folder})";
            if (folder == defaultMap)
                label += "  ★ carte par défaut";
            _maps.Add(folder);
            _list.AddItem(label);
        }

        var selected = Math.Max(0, _maps.IndexOf(defaultMap));
        if (_maps.Count > 0)
            _list.Select(selected);
        _list.ItemActivated += index => Start((int)index);

        var note = UiTheme.Caption("La partie commence avec le départ préréglé, sur le départ riche de la carte. " +
            "Le départ d'exil et le choix du mode viendront plus tard.");
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(note);

        var start = new Button { Text = "Commencer", CustomMinimumSize = new Vector2(160, 0), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        start.Pressed += () => Start(_list.GetSelectedItems() is { Length: > 0 } items ? items[0] : selected);
        column.AddChild(start);
        start.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Start(int index)
    {
        var map = index >= 0 && index < _maps.Count ? _maps[index] : null;
        Started?.Invoke(map);
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
