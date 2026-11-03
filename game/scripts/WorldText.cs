using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Shared;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Textes de l'interface tirés de la vue de la partie : noms des pôles et des saisons, fiches des communes et des stocks.
/// En attendant des écrans dédiés, les menus les affichent tels quels.
/// </summary>
public static class WorldText
{
    public static string PoleName(Orientation pole) => pole.Code switch
    {
        "EG" => "égalitariste",
        "SE" => "ségrégationniste",
        "PO" => "positiviste",
        "EC" => "écologiste",
        "PR" => "protectionniste",
        _ => "libéral",
    };

    public static string SeasonName(Season season) => season switch
    {
        Season.Winter => "Hiver",
        Season.Spring => "Printemps",
        Season.Summer => "Été",
        _ => "Automne",
    };

    /// <summary>Une commune : habitants, emplois, logements, pollution, caisse, puis ses groupes locaux (une classe par ligne).</summary>
    public static string Commune(CommuneView commune)
    {
        var text = new StringBuilder();
        if (commune.IsGhostTown)
            text.AppendLine("VILLE FANTÔME (bâtiments en ruine)");
        text.AppendLine($"Habitants {commune.Residents:N0}   logements {commune.HousingPlaces:N0} places");
        text.AppendLine($"Emplois de production pourvus {commune.JobsFilled:N0} / {commune.Jobs:N0}");
        text.AppendLine(
            $"Pollution {commune.PollutionPerResident:F3} par habitant " +
            $"(seuil {Demos.Domain.Economy.Pollution.PollutedThreshold})");
        text.AppendLine($"Caisse {commune.Fund:N0}   impôt communal {commune.TaxRate:P2} par jour");
        text.AppendLine();

        foreach (var socialClass in commune.Classes)
        {
            text.AppendLine(socialClass.Name);
            text.AppendLine(
                $"    population {socialClass.Population:N0}   richesse {socialClass.Wealth:N0}   adhésion {socialClass.Adhesion:P0}");
            text.AppendLine(
                $"    besoins couverts {socialClass.NeedsSatisfaction:P0} (logés {socialClass.HousingCoverage:P0})   " +
                $"éducation {socialClass.EducationPoints:N0} (avancée {socialClass.AdvancedEducationPoints:N0})");
        }

        return Clean(text);
    }

    /// <summary>Le stockage de chaque commune : remplissage, contenu et part de l'État.</summary>
    public static string Stocks(GameView view)
    {
        var text = new StringBuilder();
        text.AppendLine($"Trésor de l'État {view.TreasuryBalance:N0}   impôt d'État {view.StateTaxRate:P2} par jour");
        text.AppendLine();

        foreach (var commune in view.Communes())
        {
            text.AppendLine(commune.Name.ToUpperInvariant());
            text.AppendLine($"  Stockage {commune.Stored:N0} / {commune.StorageCapacity:N0}");
            if (commune.Stock.Count == 0)
                text.AppendLine("  Vide");
            foreach (var item in commune.Stock.OrderBy(i => i.Key))
                text.AppendLine($"  {item.Key} {item.Value:N0}   (dont l'État {commune.StateStock.GetValueOrDefault(item.Key):N0})");
            text.AppendLine();
        }

        return Clean(text);
    }

    // Sous Windows, AppendLine écrit « \r\n », que Godot affiche comme deux retours à la ligne.
    private static string Clean(StringBuilder text) => text.ToString().TrimEnd().Replace("\r\n", "\n");
}
