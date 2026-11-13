using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Les messages des événements de la simulation pour la barre du bas : chantier terminé, entreprises nationalisées par
/// la loi, nouvelle saison, bilan de l'année ; et la charte de fondation d'une commune.
/// </summary>
public static class EventText
{
    /// <summary>« Chantier terminé : Scierie (Première commune), en service demain. »</summary>
    public static string BuildingCompleted(GameView view, Structure building)
    {
        var commune = view.CommuneOf(building.Id) is { } communeId ? $" ({view.NameOf(communeId)})" : "";
        return $"Chantier terminé : {building.Name}{commune}, en service demain.";
    }

    /// <summary>Les entreprises privées que les lois interdisent, reprises par l'État sans indemnité.</summary>
    public static string NationalizedByLaw(IReadOnlyList<Structure> buildings) =>
        $"Les lois interdisent ces entreprises privées : l'État les reprend sans indemnité ({string.Join(", ", buildings.Select(b => b.Name))}).";

    public static string SeasonStarted(GameView view) =>
        $"{WorldText.SeasonName(view.Season)} {view.Today.Year} : nouvelle saison (migrations et évolutions lentes).";

    /// <summary>Le bilan d'une année : trésor et habitants, et leur variation depuis le 1er janvier.</summary>
    public static string YearEnded(GameView view, int year, double treasuryAtStart, double residentsAtStart) =>
        $"Bilan de {year} : trésor {view.TreasuryBalance:N0} ({view.TreasuryBalance - treasuryAtStart:+#,0;−#,0;0}), "
        + $"{view.TotalResidents:N0} habitants ({view.TotalResidents - residentsAtStart:+#,0;−#,0;0}).";

    /// <summary>La ligne de la charte d'une commune dans sa fiche (BBCode), avec sa bulle d'aide.</summary>
    public static string Charter(Orientation? charter)
    {
        if (charter is not { } pole)
            return $"Charte de fondation : {SheetText.Muted("aucune (commune de départ)", "Seule une commune fondée par un camp de base a une charte : le pôle payé à sa fondation.")}";

        var opposite = pole.Opposite;
        var hint = $"Le pôle payé à la fondation de la commune. Dans les migrations, les classes {pole.Code} ({WorldText.PoleName(pole)}) "
            + $"comptent ses emplois libres ×{Migration.SamePoleAttraction:0.##} : elles y viennent plus volontiers ; les classes "
            + $"{opposite.Code} ({WorldText.PoleName(opposite)}) les comptent ×{Migration.OppositePoleAttraction:0.##}.";
        return $"{SheetText.Hint("Charte de fondation", hint)} : {SheetText.Chip(pole)} {pole.Code} — {WorldText.PoleName(pole)}";
    }
}
