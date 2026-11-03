using Godot;

namespace Demos.Game;

/// <summary>Une page de texte qui défile : son texte suit la partie, ou reste fixe (page à venir, en gris).</summary>
public partial class TextPage : ScrollContainer, IMenuPage
{
    private readonly System.Func<string>? _describe;
    private readonly Label _text;

    public TextPage(System.Func<string>? describe, string fixedText = "")
    {
        _describe = describe;
        HorizontalScrollMode = ScrollMode.Disabled;
        var margin = UiTheme.Margin(8);
        AddChild(margin);

        _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, Text = fixedText };
        if (describe is null)
            _text.AddThemeColorOverride("font_color", UiTheme.Muted);
        margin.AddChild(_text);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public TextPage() : this(null) { }

    public void Refresh()
    {
        if (_describe is not null)
            _text.Text = _describe();
    }
}
