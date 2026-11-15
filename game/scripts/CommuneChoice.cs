using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Un menu « Toutes les communes » (sauf si <c>allowAll</c> est faux) puis chaque commune, refait quand une commune est
/// fondée (le choix en cours gardé).
/// <see cref="SelectedId"/> est null pour toutes les communes (sans « Toutes », la première commune est choisie au départ).
/// </summary>
public sealed class CommuneChoice(bool allowAll = true)
{
    private readonly List<string> _ids = [];

    public OptionButton Button { get; } = new()
    {
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        FocusMode = Control.FocusModeEnum.None,
        FitToLongestItem = false,
        ClipText = true,
    };

    // Le décalage entre la ligne du menu et la commune : 1 avec « Toutes les communes » en tête.
    private int Offset => allowAll ? 1 : 0;

    public string? SelectedId => Button.Selected - Offset is var index && index >= 0 && index < _ids.Count ? _ids[index] : null;

    /// <summary>Une ligne « Commune [menu] » à mettre en haut d'une page.</summary>
    public HBoxContainer Row()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Commune", CustomMinimumSize = new Vector2(90, 0) });
        row.AddChild(Button);
        return row;
    }

    public void Fill(IReadOnlyList<CommuneView> communes)
    {
        if (communes.Select(c => c.Id).SequenceEqual(_ids))
            return;

        var selected = SelectedId;
        _ids.Clear();
        _ids.AddRange(communes.Select(c => c.Id));
        Button.Clear();
        if (allowAll)
            Button.AddItem("Toutes les communes");
        foreach (var commune in communes)
            Button.AddItem(commune.Name);
        Button.Select(selected is null ? 0 : System.Math.Max(0, _ids.IndexOf(selected) + Offset));
    }
}
