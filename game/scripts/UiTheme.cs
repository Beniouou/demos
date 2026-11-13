using System.Collections.Generic;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;
using Ownership = Demos.Domain.Politics.Ownership;

namespace Demos.Game;

/// <summary>
/// Thème commun de l'interface du joueur : couleurs, tailles et styles des boutons, panneaux et onglets.
/// Tous les écrans le partagent, pour que chaque menu ait le même aspect.
/// </summary>
public static class UiTheme
{
    public static readonly Color Background = new("#14171c");
    public static readonly Color Surface = new("#1d2128");
    public static readonly Color Raised = new("#262b34");
    public static readonly Color Hover = new("#303744");
    public static readonly Color Border = new("#363d4a");
    public static readonly Color Text = new("#e6e8ec");
    public static readonly Color Muted = new("#9aa1ad");
    public static readonly Color Accent = new("#d9a441");
    public static readonly Color Danger = new("#e5484d");

    // Couleurs des pôles idéologiques, reprises du schéma de fonctionnement.
    private static readonly Dictionary<string, Color> PoleColors = new()
    {
        ["EG"] = new("#e5484d"),
        ["SE"] = new("#4c8df6"),
        ["PO"] = new("#9a6ce0"),
        ["EC"] = new("#30a46c"),
        ["PR"] = new("#3dd6b5"),
        ["LI"] = new("#f5c542"),
    };

    public const int FontSize = 15;
    public const int TitleSize = 19;

    public static Color PoleColor(Orientation pole) => PoleColors.GetValueOrDefault(pole.Code, Muted);

    // Couleurs du propriétaire d'un bâtiment : rouge pour l'État, jaune pour une SCOP, bleu pour le privé, dans des
    // teintes plus sombres que celles des pôles (EG, LI, SE) pour ne pas les confondre.
    private static readonly Color StateOwned = new("#a33a2c");
    private static readonly Color CooperativeOwned = new("#c09a2a");
    private static readonly Color PrivatelyOwned = new("#3a5fa8");

    public static Color OwnershipColor(Ownership ownership) => ownership switch
    {
        Ownership.Public => StateOwned,
        Ownership.Cooperative => CooperativeOwned,
        _ => PrivatelyOwned,
    };

    /// <summary>La légende des propriétaires : « ■ État  ■ SCOP  ■ Privé ».</summary>
    public static HBoxContainer OwnershipLegend()
    {
        var legend = new HBoxContainer();
        legend.AddThemeConstantOverride("separation", 6);
        foreach (var (ownership, name) in new[] { (Ownership.Public, "État"), (Ownership.Cooperative, "SCOP"), (Ownership.Private, "Privé") })
        {
            legend.AddChild(new TextureRect { Texture = Square(OwnershipColor(ownership), 12, 12), StretchMode = TextureRect.StretchModeEnum.KeepCentered });
            legend.AddChild(Caption(name));
            legend.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) });
        }
        return legend;
    }

    private static readonly Dictionary<(Color, int, int), Texture2D> Squares = [];

    /// <summary>Un rectangle plein d'une couleur, à mettre en icône (menus déroulants, schémas) ; gardé en mémoire.</summary>
    public static Texture2D Square(Color color, int width, int height)
    {
        var key = (color, width, height);
        if (Squares.TryGetValue(key, out var texture))
            return texture;

        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        return Squares[key] = ImageTexture.CreateFromImage(image);
    }

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = FontSize };

        theme.SetStylebox("panel", "PanelContainer", Box(Surface, border: true));
        theme.SetStylebox("panel", "Panel", Box(Surface, border: true));
        theme.SetColor("font_color", "Label", Text);

        theme.SetStylebox("normal", "Button", Box(Raised));
        theme.SetStylebox("hover", "Button", Box(Hover));
        theme.SetStylebox("pressed", "Button", Box(Accent.Darkened(0.55f), accentBorder: true));
        theme.SetStylebox("hover_pressed", "Button", Box(Accent.Darkened(0.45f), accentBorder: true));
        theme.SetStylebox("disabled", "Button", Box(Surface));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_color", "Button", Text);
        theme.SetColor("font_hover_color", "Button", Text);
        theme.SetColor("font_pressed_color", "Button", Accent);
        theme.SetColor("font_hover_pressed_color", "Button", Accent);
        theme.SetColor("font_disabled_color", "Button", Muted.Darkened(0.3f));

        theme.SetStylebox("panel", "TabContainer", Box(Surface));
        theme.SetStylebox("tab_selected", "TabContainer", Tab(Surface, Accent));
        theme.SetStylebox("tab_unselected", "TabContainer", Tab(Background, Background));
        theme.SetStylebox("tab_hovered", "TabContainer", Tab(Raised, Raised));
        theme.SetStylebox("tab_focus", "TabContainer", new StyleBoxEmpty());
        theme.SetColor("font_selected_color", "TabContainer", Text);
        theme.SetColor("font_unselected_color", "TabContainer", Muted);
        theme.SetColor("font_hovered_color", "TabContainer", Text);

        theme.SetStylebox("separator", "HSeparator", Line(vertical: false));
        theme.SetStylebox("separator", "VSeparator", Line(vertical: true));
        theme.SetColor("font_color", "TooltipLabel", Text);
        theme.SetStylebox("panel", "TooltipPanel", Box(Raised, border: true));

        return theme;
    }

    /// <summary>Un texte secondaire (légende, intitulé) : plus discret que le texte courant.</summary>
    public static Label Caption(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", Muted);
        return label;
    }

    /// <summary>Largeur maximale d'une bulle d'aide, en pixels : au-delà, le texte va à la ligne.</summary>
    public const int TooltipWidth = 420;

    /// <summary>
    /// Le contenu d'une bulle d'aide qui va à la ligne (Godot la place dans son panneau de bulle). Un texte court garde
    /// sa largeur ; un long texte est coupé à <see cref="TooltipWidth"/>.
    /// </summary>
    public static Control? Tooltip(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", Text);
        label.AddThemeFontSizeOverride("font_size", FontSize - 1);
        var width = label.GetThemeDefaultFont().GetStringSize(text, fontSize: FontSize - 1).X;
        if (width > TooltipWidth)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(TooltipWidth, 0);
        }
        return label;
    }

    /// <summary>Une marge égale sur les quatre côtés.</summary>
    public static MarginContainer Margin(int size)
    {
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", size);
        return margin;
    }

    /// <summary>Un titre de section dans une page : en majuscules discrètes, comme sur une fiche.</summary>
    public static Label Section(string text)
    {
        var label = Caption(text.ToUpperInvariant());
        label.AddThemeFontSizeOverride("font_size", FontSize - 2);
        return label;
    }

    public static Label Title(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", TitleSize);
        return label;
    }

    private static StyleBoxFlat Box(Color color, bool border = false, bool accentBorder = false)
    {
        var box = new StyleBoxFlat
        {
            BgColor = color,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
        box.SetCornerRadiusAll(4);
        if (border || accentBorder)
        {
            box.SetBorderWidthAll(1);
            box.BorderColor = accentBorder ? Accent : Border;
        }
        return box;
    }

    // Onglet : un trait coloré en bas marque l'onglet choisi.
    private static StyleBoxFlat Tab(Color color, Color underline)
    {
        var box = new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = underline,
            BorderWidthBottom = 2,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        box.CornerRadiusTopLeft = box.CornerRadiusTopRight = 4;
        return box;
    }

    private static StyleBoxLine Line(bool vertical) =>
        new() { Color = Border, Thickness = 1, Vertical = vertical };
}
