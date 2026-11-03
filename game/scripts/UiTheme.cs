using System.Collections.Generic;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

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
