using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Map;

namespace Demos.Game;

/// <summary>
/// Les mots des routes : le nom d'un niveau, une liaison, et les messages du bilan (sentier ouvert, chemin, route,
/// entretien payé ou impayé), pour la barre du bas et la page Production → Transport.
/// </summary>
public static class RoadText
{
    public static string LevelName(RoadLevel level) => level switch
    {
        RoadLevel.Trail => "sentier",
        RoadLevel.Path => "chemin",
        RoadLevel.Road => "route",
        _ => "rien",
    };

    /// <summary>« le sentier », « le chemin », « la route ».</summary>
    private static string WithArticle(RoadLevel level) => level == RoadLevel.Road ? "la route" : $"le {LevelName(level)}";

    public static string Link(GameView view, RoadLink link) => $"{view.NameOf(link.A)} – {view.NameOf(link.B)}";

    /// <summary>La part de la perte de transport que retire la liaison (un chemin 25 %, une route 50 %).</summary>
    public static double LossCut(RoadLevel level) => 1 - Roads.LossFactor(level, Balance.Current);

    /// <summary>Une ligne par changement : construction, entretien impayé ; l'entretien payé, en une ligne pour toutes les liaisons.</summary>
    public static IReadOnlyList<string> Lines(GameView view, IReadOnlyList<RoadChange> changes)
    {
        var lines = new List<string>();
        foreach (var change in changes.Where(c => c.Reason != "entretien"))
        {
            var link = change.Link;
            var payer = view.NameOf(link.PayerId);
            lines.Add(change.Reason switch
            {
                "trafic" when change.Before == RoadLevel.None =>
                    $"Sentier ouvert entre {view.NameOf(link.A)} et {view.NameOf(link.B)} ({link.Length:0.#} km, payé par {payer} : {change.Cost:N0}).",
                "trafic" =>
                    $"{Capitalize(WithArticle(change.Before))} {Link(view, link)} devient {(link.Level == RoadLevel.Road ? "une" : "un")} {LevelName(link.Level)} (payé par {payer} : {change.Cost:N0}).",
                _ when link.Level == RoadLevel.None =>
                    $"Entretien impayé par {payer} : {WithArticle(change.Before)} {Link(view, link)} disparaît.",
                _ =>
                    $"Entretien impayé par {payer} : {WithArticle(change.Before)} {Link(view, link)} redevient {(link.Level == RoadLevel.Road ? "une" : "un")} {LevelName(link.Level)}.",
            });
        }

        var upkeep = changes.Where(c => c.Reason == "entretien").ToList();
        if (upkeep.Count > 0)
            lines.Add($"Entretien des routes payé : {upkeep.Count} liaison(s), {upkeep.Sum(c => c.Cost):N0} au total.");
        return lines;
    }

    /// <summary>Le message de la barre du bas : tous les changements du bilan, sur une ligne.</summary>
    public static string Message(GameView view, IReadOnlyList<RoadChange> changes) =>
        "Routes : " + string.Join(" ", Lines(view, changes));

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
