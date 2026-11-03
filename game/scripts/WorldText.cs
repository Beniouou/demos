using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Shared;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Textes de l'interface tirés de l'état de la partie : noms des pôles et des saisons, fiches des communes et des stocks.
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

    /// <summary>Chaque commune : habitants, emplois, logements, pollution, caisse, puis ses groupes locaux (une classe par ligne).</summary>
    public static string Communes(World world)
    {
        var text = new StringBuilder();
        foreach (var commune in world.Communes)
        {
            var jobs = world.Structures.Values
                .Where(s => s.Jobs > 0 && Demos.Domain.Politics.Communes.CommuneOf(s, world.Structures) == commune.Id)
                .ToList();
            var filled = jobs.Sum(s => world.Workers.GetValueOrDefault(s.Id));

            text.AppendLine(commune.Name.ToUpperInvariant() + (world.IsGhostTown(commune.Id) ? "   — VILLE FANTÔME (bâtiments en ruine)" : ""));
            text.AppendLine($"  Habitants {world.Residents(commune.Id):N0}   logements {world.HousingPlaces(commune.Id):N0} places");
            text.AppendLine($"  Emplois de production pourvus {filled:N0} / {jobs.Sum(s => s.Jobs):N0}");
            text.AppendLine(
                $"  Pollution {world.PollutionByCommune.GetValueOrDefault(commune.Id):F3} par habitant " +
                $"(seuil {Demos.Domain.Economy.Pollution.PollutedThreshold})");
            text.AppendLine($"  Caisse {world.CommuneFundOf(commune.Id).Balance:N0}   impôt communal {world.CommuneTaxRateOf(commune.Id):P2} par jour");
            text.AppendLine();

            foreach (var socialClass in world.Classes.Where(c => c.CommuneId == commune.Id && c.Population > 0))
            {
                text.AppendLine($"  {socialClass.Name}");
                text.AppendLine(
                    $"      population {socialClass.Population:N0}   richesse {socialClass.Wealth:N0}   adhésion {socialClass.Adhesion:P0}");
                text.AppendLine(
                    $"      besoins couverts {socialClass.NeedsSatisfaction:P0} (logés {socialClass.HousingCoverage:P0})   " +
                    $"éducation {socialClass.EducationPoints:N0} (avancée {socialClass.AdvancedEducationPoints:N0})");
            }
            text.AppendLine();
        }

        return Clean(text);
    }

    /// <summary>Le stockage de chaque commune : remplissage, contenu et part de l'État.</summary>
    public static string Stocks(World world)
    {
        var text = new StringBuilder();
        text.AppendLine($"Trésor de l'État {world.Treasury.Balance:N0}   impôt d'État {world.StateTaxRate:P2} par jour");
        text.AppendLine();

        foreach (var commune in world.Communes)
        {
            var market = world.MarketOf(commune.Id);
            var state = world.StateHoldingsOf(commune.Id);
            text.AppendLine(commune.Name.ToUpperInvariant());
            text.AppendLine($"  Stockage {world.StorageOf(commune.Id).Stored.Values.Sum():N0} / {world.StorageCapacity(commune.Id):N0}");
            if (market.Contents.Count == 0)
                text.AppendLine("  Vide");
            foreach (var item in market.Contents.OrderBy(i => i.Key))
                text.AppendLine($"  {item.Key} {item.Value:N0}   (dont l'État {state.GetValueOrDefault(item.Key):N0})");
            text.AppendLine();
        }

        return Clean(text);
    }

    // Sous Windows, AppendLine écrit « \r\n », que Godot affiche comme deux retours à la ligne.
    private static string Clean(StringBuilder text) => text.ToString().TrimEnd().Replace("\r\n", "\n");
}
