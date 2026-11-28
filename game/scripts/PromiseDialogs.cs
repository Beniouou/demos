using System;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// La fenêtre du pacte déchiré (décidé le 21 novembre 2026, maquette du 27 novembre 2026) : le texte signé, déchiré, et
/// trois choix — terminer la partie (avec un bilan), continuer sans promesse (bac à sable), refonder (une nouvelle
/// Promesse, au prix d'une crise et d'une cicatrice). Le jeu est en pause tant qu'elle est ouverte.
/// </summary>
public partial class PactTornScreen : Control
{
    public event Action? Ended;
    public event Action? Continued;
    public event Action? Refounding;

    public PactTornScreen() { }

    public PactTornScreen(GameView view)
    {
        Theme = UiTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = new Color(0.04f, 0.05f, 0.06f, 0.94f), AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 48);
        center.AddChild(row);

        if (view.Promise() is { } promise)
        {
            // Le papier en deux morceaux, l'encre presque partie.
            var pieces = new HBoxContainer();
            pieces.AddThemeConstantOverride("separation", 14);
            row.AddChild(pieces);
            var words = $"{promise.Oath.First} {promise.Oath.Middle} {promise.Oath.Last}".Split(' ');
            var half = words.Length / 2;
            pieces.AddChild(Piece(string.Join(' ', words.Take(half)), -4));
            pieces.AddChild(Piece(string.Join(' ', words.Skip(half)), 5));
        }

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        column.AddThemeConstantOverride("separation", 12);
        row.AddChild(column);
        var when = UiTheme.Caption($"An {view.Today.Year - 1899} de l'exil");
        when.AddThemeColorOverride("font_color", UiTheme.Danger);
        column.AddChild(when);
        var title = new Label { Text = "Le pacte est déchiré." };
        title.AddThemeFontSizeOverride("font_size", 30);
        column.AddChild(title);
        if (view.Promise() is { } torn)
        {
            var weakest = torn.Axes.OrderBy(a => a.Alignment).First();
            column.AddChild(Wrapped($"Depuis {torn.RuptureMonths} mois, presque plus personne ne vit selon {torn.Name}. "
                + $"{char.ToUpperInvariant(WorldText.PoleName(weakest.Pole)[0])}{WorldText.PoleName(weakest.Pole)[1..]} : {weakest.Alignment:P0} des habitants."));
        }

        column.AddChild(Choice("Terminer la partie", "Bilan de la nation, de l'An I à aujourd'hui.", () => Ended?.Invoke()));
        column.AddChild(Choice("Continuer sans promesse", "La nation vit sans sa promesse : bac à sable, seule la révolution reste une fin.",
            () => Continued?.Invoke()));
        var settings = Demos.Domain.Economy.Balance.Current;
        column.AddChild(Choice("Refonder", "Une nouvelle Promesse et un nouveau serment, signé sous l'ancien. Crise : "
            + $"−{settings.RefoundingAdhesionLoss:0.0#} d'adhésion pour toutes les classes ; cicatrice : {settings.RefoundingScar:0.00} de blessure pour toujours.",
            () => Refounding?.Invoke()));
    }

    private static Control Piece(string text, float degrees)
    {
        var paper = SignedText.PaperPanel(220);
        paper.RotationDegrees = degrees;
        paper.CustomMinimumSize = new Vector2(220, 420);
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color(SignedText.InkColor, 0.25f));
        paper.AddChild(label);
        return paper;
    }

    private static Button Choice(string title, string text, Action pressed)
    {
        var button = new Button { Text = $"{title}\n{text}", Alignment = HorizontalAlignment.Left, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(500, 64) };
        button.Pressed += pressed;
        return button;
    }

    private static Label Wrapped(string text)
    {
        var label = UiTheme.Caption(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }
}

/// <summary>
/// L'arrivée en exil (maquette du 27 novembre 2026) : la date, la terre d'exil, ce que les exilés ont et ce qui leur
/// manque, sans conseil (le jeu pose les problèmes, le joueur les résout).
/// </summary>
public static class ArrivalText
{
    public static string Of(GameView view, string communeId)
    {
        var people = view.Promise()?.PeopleName is { Length: > 0 } name ? $"Le peuple de {name}" : "Les exilés";
        var buildings = view.BuildingsOf(communeId);
        return $"1er janvier, An I de l'exil.\n\n{people} arrive à {view.NameOf(communeId)} : {view.TotalResidents:N0} exilés "
            + "dressent leur camp de base, en plein hiver.\n\n"
            + $"Ce qu'ils ont : {string.Join(", ", buildings)} ; {view.TreasuryBalance:N0} au trésor, des charrettes, "
            + "la moitié des outils, des vêtements et des meubles qu'il faudrait, et quelques jours de farine.\n\n"
            + "Ce qui manque : être administré, être protégé, se distraire ; le fer et les outils neufs ; la pierre, loin d'ici. "
            + (view.AllowsExtension(communeId) ? "" : "Aucune loi d'extension : rien au-delà de 8 km.");
    }
}
