using Godot;

namespace Demos.Game;

/// <summary>
/// Le texte d'une fiche (RichTextLabel en BBCode) dont les bulles d'aide (balise [hint]) vont à la ligne : la bulle
/// de Godot tiendrait sur une seule ligne, aussi large que l'écran.
/// </summary>
public partial class HintText : RichTextLabel
{
    public override Control? _MakeCustomTooltip(string forText) => UiTheme.Tooltip(forText);
}
