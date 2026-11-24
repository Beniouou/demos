using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Map;
using Demos.Infrastructure;

namespace Demos.Carte;

/// <summary>
/// L'outil « Départs » de l'éditeur (décidé le 23 novembre 2026) : les terres d'exil de la carte (1 à 3), la fiche de
/// pauvreté de chacune avec son verdict, et le contrôle du départ riche (la commune de base) : le départ préréglé
/// (<c>partie-depart.json</c>) y tient-il ? Les fiches s'affichent dans un panneau à droite de la carte.
/// </summary>
public sealed partial class MapEditorForm
{
    /// <summary>Terres d'exil qu'une carte porte au plus (décidé : 1 à 3).</summary>
    public const int MaxExileSites = 3;

    private readonly ExileThresholds _exileThresholds = new();

    private readonly TextBox _startsReport = new()
    {
        Dock = DockStyle.Right,
        Width = 430,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Color.FromArgb(250, 247, 240),
        ForeColor = Color.FromArgb(40, 35, 28),
        Font = new Font("Segoe UI", 9.5f),
        BorderStyle = BorderStyle.FixedSingle,
        Visible = false,
    };

    /// <summary>Les bâtiments du départ préréglé qui se posent sur une zone ; null tant qu'ils ne sont pas lus.</summary>
    private IReadOnlyList<ZoneDemand>? _presetDemands;
    private string? _presetError;

    private void BindStartEvents()
    {
        _contextBar.BtnStartRefresh.Click += (_, _) => RefreshStartReports();

        _canvas.ExileSitePlaced += (wx, wy) =>
        {
            var sites = _currentMap.AllExileSites.ToList();
            if (sites.Count >= MaxExileSites)
            {
                Log($"⚠️ Une carte porte {MaxExileSites} terres d'exil au plus : retirez-en une (clic droit) ou déplacez-la.");
                return;
            }

            PushUndo();
            var name = string.IsNullOrWhiteSpace(_contextBar.TxtStartName.Text) ? $"Terre d'exil {sites.Count + 1}" : _contextBar.TxtStartName.Text.Trim();
            if (sites.Any(s => s.Name == name))
                name = $"{name} {sites.Count + 1}";
            sites.Add(new ExileSite(name, new Point2D(Math.Round(wx, 1), Math.Round(wy, 1))));
            SetExileSites(sites);
            Log($"🏕️ Terre d'exil « {name} » posée en ({MapScale.ToKm(wx):0.0} km, {MapScale.ToKm(wy):0.0} km).");
        };

        _canvas.ExileSiteMoved += (index, wx, wy) =>
        {
            var sites = _currentMap.AllExileSites.ToList();
            if (index < 0 || index >= sites.Count) return;

            PushUndo();
            sites[index] = sites[index] with { Position = new Point2D(Math.Round(wx, 1), Math.Round(wy, 1)) };
            SetExileSites(sites);
            Log($"🏕️ Terre d'exil « {sites[index].Name} » déplacée en ({MapScale.ToKm(wx):0.0} km, {MapScale.ToKm(wy):0.0} km).");
        };

        _canvas.ExileSiteDeleted += (wx, wy) =>
        {
            var sites = _currentMap.AllExileSites.ToList();
            var nearest = sites
                .Select((s, i) => (Index: i, Distance: s.Position.DistanceTo(new Point2D(wx, wy))))
                .Where(s => s.Distance < 25.0)
                .OrderBy(s => s.Distance)
                .Select(s => (int?)s.Index)
                .FirstOrDefault();
            if (nearest is not { } index) return;

            PushUndo();
            var removed = sites[index];
            sites.RemoveAt(index);
            SetExileSites(sites);
            Log($"🗑 Terre d'exil « {removed.Name} » retirée ({sites.Count} restantes).");
        };
    }

    private void SetExileSites(List<ExileSite> sites)
    {
        _currentMap = _currentMap with { ExileSites = sites.Count == 0 ? null : sites };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        _contextBar.UpdateStartCount(sites.Count, MaxExileSites);
        RefreshStartReports();
    }

    /// <summary>Montre ou cache le panneau des fiches (outil Départs seulement), et le remplit quand il s'ouvre.</summary>
    private void ShowStartReports(bool visible)
    {
        _startsReport.Visible = visible;
        if (visible)
        {
            _contextBar.UpdateStartCount(_currentMap?.AllExileSites.Count ?? 0, MaxExileSites);
            RefreshStartReports();
        }
    }

    private void RefreshStartReportsIfShown()
    {
        if (_startsReport.Visible)
            RefreshStartReports();
        else
            _canvas.ExileVerdicts = [];
    }

    /// <summary>Recalcule la fiche de chaque départ, la montre dans le panneau et colore les départs sur la carte.</summary>
    private void RefreshStartReports()
    {
        if (_currentMap is null || _currentHeightmap is null)
            return;

        var sw = Stopwatch.StartNew();
        Cursor = Cursors.WaitCursor;
        try
        {
            var demands = PresetDemands();
            var zones = (IReadOnlyList<PolygonArea>)[.. _currentMap.AllBiomes, .. _currentMap.AllResourceZones];
            var rivers = _currentMap.AllRivers;
            Func<Point2D, double> elevation = p => _currentHeightmap.SampleElevation(p.X, p.Y);
            _canvas.StartRadii = (Balance.Current.CommuneJurisdictionRadius, Balance.Current.ExtractionMaxDistance);

            var text = new StringBuilder();
            text.AppendLine("FICHES DES DÉPARTS");
            text.AppendLine();

            // Le départ riche : la commune de base de la carte.
            var rich = _currentMap.DefaultCommune;
            var richReport = StartSites.Report(rich.Position, zones, rivers, elevation, _exileThresholds);
            text.AppendLine($"★ Départ riche (bac à sable) : {rich.Name}");
            if (demands is null)
            {
                text.AppendLine($"   Départ préréglé non vérifié : {_presetError}");
                _canvas.RichStartHolds = null;
            }
            else
            {
                var problems = StartSites.PresetProblems(demands, rich.Position, zones);
                _canvas.RichStartHolds = problems.Count == 0;
                if (problems.Count == 0)
                    text.AppendLine($"   ✔ Le départ préréglé tient ({demands.Count} bâtiments posés sur une zone).");
                else
                {
                    text.AppendLine("   ✖ Le départ préréglé ne tient pas :");
                    foreach (var problem in problems)
                        text.AppendLine($"      · {problem}");
                }
            }
            AppendReport(text, richReport);
            text.AppendLine();

            // Les terres d'exil.
            var verdicts = new List<ExileVerdict?>();
            var sites = _currentMap.AllExileSites;
            if (sites.Count == 0)
                text.AppendLine("Aucune terre d'exil : posez-en 1 à 3 au clic gauche.");
            for (var i = 0; i < sites.Count; i++)
            {
                var report = StartSites.Report(sites[i].Position, zones, rivers, elevation, _exileThresholds);
                var (verdict, reasons) = StartSites.Judge(report, _exileThresholds);
                verdicts.Add(verdict);
                text.AppendLine($"{i + 1}. {sites[i].Name} : {VerdictText(verdict)}");
                foreach (var reason in reasons)
                    text.AppendLine($"      · {reason}");
                AppendReport(text, report);
                text.AppendLine();
            }
            _canvas.ExileVerdicts = verdicts;

            var t = _exileThresholds;
            text.AppendLine("Seuils d'une terre d'exil (propositions, à régler par la simulation) :");
            text.AppendLine($"   · au moins {t.MinFarmJobs:N0} et au plus {t.MaxFarmJobs:N0} emplois de ferme dans le ressort ;");
            text.AppendLine($"   · fer au-delà de {MapScale.Format(t.IronMinKm)} et à moins de {MapScale.Format(t.IronMaxKm)} ;");
            text.AppendLine($"   · une forêt et de la pierre à moins de {MapScale.Format(t.WoodAndStoneMaxKm)}.");
            text.AppendLine("Pas encore vérifié : le passage par la terre (pente de 12 % au plus) vers les gisements.");
            text.AppendLine();
            text.AppendLine($"Calculé en {sw.ElapsedMilliseconds} ms. Après une retouche des biomes ou des gisements, « Recalculer les fiches ».");

            _startsReport.Text = text.ToString();
            _canvas.Invalidate();
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private static void AppendReport(StringBuilder text, StartSiteReport r)
    {
        string Km(double? km) => km is { } v ? MapScale.Format(v) : "aucun";
        text.AppendLine($"   Terre fertile : {r.FertileNearKm2:N0} km² à {r.NearKm:N0} km · {r.FertileFarKm2:N0} km² à {r.FarKm:N0} km");
        text.AppendLine($"   Forêt : {r.ForestNearKm2:N0} km² à {r.NearKm:N0} km · {r.ForestFarKm2:N0} km² à {r.FarKm:N0} km");
        text.AppendLine($"   Emplois possibles dans le ressort : ferme {r.FarmJobs:N0} · bûcheron {r.ForestJobs:N0}");
        text.AppendLine($"   Gisement le plus proche : pierre {Km(r.KmTo("pierre"))} · charbon {Km(r.KmTo("charbon"))} · fer {Km(r.KmTo("fer"))}");
        var altitude = r.Altitude is { } a ? $"{a:N0} m" : "?";
        text.AppendLine($"   Côte : {(r.OnCoast ? "oui" : "non")} · Fleuve : {(r.NearRiver ? "oui" : "non")} · Altitude : {altitude}");
    }

    private static string VerdictText(ExileVerdict verdict) => verdict switch
    {
        ExileVerdict.Playable => "JOUABLE",
        ExileVerdict.Unplayable => "INJOUABLE",
        ExileVerdict.TooEasy => "TROP FACILE",
        _ => verdict.ToString(),
    };

    /// <summary>
    /// Les bâtiments du départ préréglé qui se posent sur une zone (nom, zones, emplois), lus une fois dans les données
    /// du jeu (la partie de départ, sans carte). Charge aussi les réglages d'équilibrage du jeu.
    /// </summary>
    private IReadOnlyList<ZoneDemand>? PresetDemands()
    {
        if (_presetDemands is not null || _presetError is not null)
            return _presetDemands;

        try
        {
            var content = new JsonContentSource(file => file == "carte.json" || file.StartsWith(MapFiles.Folder + "/") ? "" : File.ReadAllText(Path.Combine(_dataDir, file)));
            var world = PrototypeScenario.Create(content).World;
            _presetDemands = world.Structures.Values
                .Select(s => (Structure: s, Kinds: ExtractionSites.ZonesFor(s.TypeId, s.Production?.ProductId)))
                .Where(s => s.Kinds.Count > 0)
                .Select(s => new ZoneDemand(world.DisplayNameOf(s.Structure), s.Kinds, s.Structure.Jobs))
                .ToList();
        }
        catch (Exception ex)
        {
            _presetError = $"partie de départ illisible ({ex.Message})";
            Log($"⚠️ {_presetError}");
        }

        return _presetDemands;
    }
}
