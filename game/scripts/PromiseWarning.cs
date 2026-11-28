using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// L'avertissement avant un acte qui trahit La Promesse (décidé le 21 novembre 2026, maquette du 27 novembre 2026) :
/// jamais bloquant. Le jeu dit quel article l'acte rompt et ce que la trahison coûterait ; « Revenir » est le bouton mis
/// en avant, l'acte reste possible. Sans trahison (ou en bac à sable), l'acte se fait tout de suite.
/// </summary>
public static class PromiseWarning
{
    /// <summary>Fait l'acte, après avertissement s'il trahit un engagement.</summary>
    /// <param name="act">Le verbe de l'acte (« Voter », « Vendre », « Fonder »…), pour le bouton « … quand même ».</param>
    /// <param name="what">Ce que fait l'acte (« voter cette loi »), dans la phrase de l'avertissement.</param>
    public static void Check(Node anchor, IReadOnlyList<PromiseBreachView> breaches, string act, string what, Action proceed)
    {
        if (breaches.Count == 0)
        {
            proceed();
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Theme = UiTheme.Create(),
            Title = "La Promesse",
            OkButtonText = $"{act} quand même",
            CancelButtonText = "Revenir",
            DialogAutowrap = true,
            DialogText = Text(breaches, what),
            MinSize = new Vector2I(560, 0),
        };
        anchor.GetTree().Root.AddChild(dialog);
        dialog.Confirmed += proceed;
        dialog.VisibilityChanged += () =>
        {
            if (!dialog.Visible)
                dialog.QueueFree();
        };
        dialog.PopupCentered();
        dialog.GetCancelButton().CallDeferred(Control.MethodName.GrabFocus);
    }

    public static string Text(IReadOnlyList<PromiseBreachView> breaches, string what)
    {
        var text = new StringBuilder();
        var articles = string.Join(" et ", breaches.Select(b => $"l'article {b.Article} : « {b.Text} »"));
        text.AppendLine($"{Capitalized(what)} rompt {articles}.");
        text.AppendLine();
        foreach (var breach in breaches)
        {
            text.AppendLine($"Jauge de la promesse : −{breach.Wound:0.00} (dont {breach.Permanent:0.00} pour toujours ; le reste guérit en {breach.HealingMonths} mois).");
            text.AppendLine($"Classes {WorldText.PoleName(breach.Pole).ToLowerInvariant()}s ({breach.ClassesHit}) : −{breach.AdhesionLoss:0.0#} d'adhésion, qui revient peu à peu.");
            text.AppendLine($"L'encre de l'article {breach.Article} pâlit dans le texte signé.");
        }

        text.AppendLine();
        text.Append("L'acte n'est pas interdit : vous seul décidez.");
        return text.ToString();
    }

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
