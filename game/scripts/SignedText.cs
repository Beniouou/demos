using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game;

/// <summary>
/// Le texte signé de La Promesse, sur papier (décidé le 25 novembre 2026, « Le texte qui s'efface ») : il montre l'état de
/// La Promesse sans chiffre. Sa lisibilité suit la jauge par paliers (propositions) : net jusqu'à 0,7, pâli jusqu'à 0,5,
/// des mots qui disparaissent jusqu'à 0,3, presque illisible dessous. Chaque article a sa propre encre, qui pâlit avec
/// les trahisons de son engagement. Les lois de l'An I s'écrivent dessous, sans encre propre.
/// </summary>
public static class SignedText
{
    public static readonly Color PaperColor = new("#efe6d2");
    public static readonly Color InkColor = new("#2b2418");
    public static readonly Color FadedInk = new("#b7ab92");

    /// <summary>Un texte sur papier, à l'encre fraîche (aperçu de l'écran de La Promesse, serment).</summary>
    public static PanelContainer Paper(string text, float width)
    {
        var panel = PaperPanel(width);
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", InkColor);
        label.AddThemeFontSizeOverride("font_size", UiTheme.FontSize + 1);
        panel.AddChild(label);
        return panel;
    }

    /// <summary>Le panneau de papier, sans contenu.</summary>
    public static PanelContainer PaperPanel(float width)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = PaperColor,
            ContentMarginLeft = 26,
            ContentMarginRight = 26,
            ContentMarginTop = 22,
            ContentMarginBottom = 22,
        });
        return panel;
    }

    /// <summary>L'encre du texte selon la jauge : son opacité et la part des mots effacés (paliers, propositions).</summary>
    public static (float Alpha, double Erased) Legibility(double gauge) => gauge switch
    {
        >= 0.7 => (1f, 0),
        >= 0.5 => (0.6f, 0),
        >= 0.3 => (0.45f, (0.5 - gauge) / 0.2 * 0.35),
        _ => (0.22f, 0.6),
    };

    /// <summary>Le texte signé, tel que l'état de La Promesse le laisse lire.</summary>
    public static PanelContainer Of(PromiseView promise, float width)
    {
        var panel = PaperPanel(width);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);
        var (alpha, erased) = Legibility(promise.Gauge);

        var title = Line(promise.Name.ToUpperInvariant(), FadedInk, UiTheme.FontSize - 2);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);
        var seed = 0;
        foreach (var sentence in new[] { promise.Oath.First, promise.Oath.Middle, promise.Oath.Last })
            column.AddChild(Line(Erase(sentence, erased, ref seed), Ink(alpha), UiTheme.FontSize + 1));
        column.AddChild(Line(promise.Oath.Signed, Ink(alpha * 0.85f), UiTheme.FontSize));

        column.AddChild(new HSeparator());
        for (var i = 0; i < promise.Engagements.Count; i++)
        {
            var article = promise.Engagements[i];
            // L'article s'efface plus vite que le reste : son encre propre s'ajoute à celle du texte.
            var ink = alpha * (float)(0.15 + 0.85 * article.Ink);
            column.AddChild(Line(Erase($"Article {i + 1} — {article.Text}.", erased + (1 - article.Ink) * 0.4, ref seed), Ink(ink), UiTheme.FontSize));
        }

        if (promise.FoundingLaws.Count > 0)
            column.AddChild(Line($"Lois de l'An I : {string.Join(", ", promise.FoundingLaws)}.", Ink(alpha * 0.7f), UiTheme.FontSize - 2));

        if (promise.Signature.Count > 0)
            column.AddChild(new SignatureView(promise.Signature, Ink(alpha)) { CustomMinimumSize = new Vector2(240, 70) });
        return panel;
    }

    private static Color Ink(float alpha) => new(InkColor, Math.Clamp(alpha, 0.08f, 1f));

    private static Label Line(string text, Color color, int size)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    /// <summary>
    /// Efface une part des mots, toujours les mêmes pour un texte donné (tirés de leur rang) : ils réapparaissent quand la
    /// jauge remonte.
    /// </summary>
    private static string Erase(string text, double share, ref int seed)
    {
        if (share <= 0)
            return text;

        var words = text.Split(' ');
        var result = new StringBuilder();
        foreach (var word in words)
        {
            var draw = (uint)(++seed * 2654435761u) % 1000 / 1000.0;
            result.Append(draw < share ? new string(' ', word.Length) : word).Append(' ');
        }

        return result.ToString().TrimEnd();
    }
}

/// <summary>La signature tracée au serment, redessinée à l'encre du texte (points de 0 à 1 dans le cadre).</summary>
public partial class SignatureView : Control
{
    private readonly IReadOnlyList<IReadOnlyList<Point2D>> _strokes = [];
    private readonly Color _ink;

    public SignatureView() { }

    public SignatureView(IReadOnlyList<IReadOnlyList<Point2D>> strokes, Color ink)
    {
        _strokes = strokes;
        _ink = ink;
    }

    public override void _Draw()
    {
        foreach (var stroke in _strokes.Where(s => s.Count > 1))
            DrawPolyline(stroke.Select(p => new Vector2((float)p.X * Size.X, (float)p.Y * Size.Y)).ToArray(), _ink, 2f, antialiased: true);
    }
}
