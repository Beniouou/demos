using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Demos.Domain.Map;

namespace Demos.Carte;

public sealed partial class MapEditorForm
{
    private void PushUndo()
    {
        if (_currentMap is not null && _currentHeightmap is not null)
        {
            _history.Push(_currentMap, _currentHeightmap, _currentZoneGrid);
        }
    }

    private void Undo()
    {
        if (_history.Undo(_currentMap, _currentHeightmap, _currentZoneGrid, out var prev))
        {
            _currentMap = prev.Map;
            _currentHeightmap.RestoreGrid(prev.HeightGrid);
            if (prev.ZoneGrid is not null)
            {
                _currentZoneGrid = prev.ZoneGrid.Clone();
                _canvas.ZoneGrid = _currentZoneGrid;
            }
            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
            UpdateCommuneLabels();
            UpdateRiverContextButtons();
            Log("↶ Action annulée.");
        }
    }

    private void Redo()
    {
        if (_history.Redo(_currentMap, _currentHeightmap, _currentZoneGrid, out var next))
        {
            _currentMap = next.Map;
            _currentHeightmap.RestoreGrid(next.HeightGrid);
            if (next.ZoneGrid is not null)
            {
                _currentZoneGrid = next.ZoneGrid.Clone();
                _canvas.ZoneGrid = _currentZoneGrid;
            }
            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
            UpdateCommuneLabels();
            UpdateRiverContextButtons();
            Log("↷ Action rétablie.");
        }
    }

    private MapRenderStyle GetCurrentStyle() =>
        _styleCombo.SelectedIndex == 1 ? MapRenderStyle.VintageParchment1874 : MapRenderStyle.IgnPastel;

    private void ReextractContoursAfterElevationChange()
    {
        const double interval = 25.0;
        var levels = new List<double>();
        for (var alt = interval; alt <= _currentHeightmap.MaxElevation; alt += interval)
            levels.Add(alt);

        var contours = ContourExtractor.ExtractContours(_currentHeightmap, levels);
        var updatedWater = ContourExtractor.ExtractWaterBodies(_currentHeightmap);
        _currentMap = _currentMap with
        {
            Contours = contours,
            WaterBodies = updatedWater.Count > 0 ? updatedWater : (_currentHeightmap.MinElevation < 0 ? _currentMap.WaterBodies : [])
        };

        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        _statusStats.Text = $"Isohypses : {_currentMap.Contours.Count} | Sommets : {_currentMap.SpotElevations.Count} | Alt. max : {_currentHeightmap.MaxElevation:F0} m";
    }

    private (double Width, double Height) GetCurrentDimensions()
    {
        return _comboDimensions.SelectedIndex switch
        {
            1 => (3600.0, 2250.0),
            2 => (1200.0, 800.0),
            3 => (1000.0, 640.0),
            _ => (2400.0, 1500.0),
        };
    }

    /// <summary>
    /// Initialise ou réinitialise la carte en mode complètement vierge.
    /// </summary>
    public void CreateBlankMap(bool isInitial = false)
    {
        if (!isInitial) PushUndo();

        var (w, h) = GetCurrentDimensions();
        _currentMap = MapGenerator.CreateBlank(w, h, out _currentHeightmap);
        var zcols = Math.Clamp((int)Math.Round(w / 10.0), 100, 360);
        var zrows = Math.Clamp((int)Math.Round(h / 10.0), 64, 230);
        _currentZoneGrid = new ZoneMaskGrid(zcols, zrows, w, h);
        _currentZoneGrid.RasterizeFromMap(_currentMap);
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        _canvas.ResetView();
        UpdateCommuneLabels();
        UpdateRiverContextButtons();

        _statusDimensions.Text = $"{w:N0} × {h:N0} lieues";
        _statusStats.Text = "Carte vierge (toile blanche)";
        var msg = isInitial
            ? $"📄 Carte vierge initialisée par défaut ({w:N0} × {h:N0} lieues). Prête pour vos créations !"
            : $"📄 Carte réinitialisée en mode vierge ({w:N0} × {h:N0} lieues).";
        Log(msg);
    }

    /// <summary>
    /// Initialise la carte en Mode Île avec des côtes découpées naturelles et un océan enveloppant.
    /// </summary>
    public void CreateIslandMap()
    {
        PushUndo();

        var (w, h) = GetCurrentDimensions();
        var seed = (int)_contextBar.NumSeed.Value;
        var morph = _contextBar.GetSelectedMorphology();
        var maxAlt = _contextBar.GetSelectedMaxAltitude();

        _currentMap = MapGenerator.CreateIsland(w, h, out _currentHeightmap, seed, morph, maxAlt);
        var zcols = Math.Clamp((int)Math.Round(w / 10.0), 100, 360);
        var zrows = Math.Clamp((int)Math.Round(h / 10.0), 64, 230);
        _currentZoneGrid = new ZoneMaskGrid(zcols, zrows, w, h);
        _currentZoneGrid.AutoGenerateBiomes(_currentHeightmap, _currentMap.AllRivers);
        var biomes = _currentZoneGrid.ExtractBiomes();
        var forests = _currentZoneGrid.ExtractForests();
        _currentMap = _currentMap with { Biomes = biomes, Forests = forests };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        _canvas.ResetView();
        UpdateCommuneLabels();
        UpdateRiverContextButtons();

        var morphDesc = morph switch
        {
            IslandMorphology.AlpineRidge => "Crêtes alpines",
            IslandMorphology.Archipelago => "Archipel d'îlots",
            IslandMorphology.Fjords => "Fjords & Baies",
            _ => "Île naturelle"
        };
        _statusDimensions.Text = $"{w:N0} × {h:N0} lieues";
        _statusStats.Text = $"{morphDesc} ({_currentMap.Contours.Count} courbes, alt. max {_currentHeightmap.MaxElevation:F0} m, {_currentMap.AllBiomes.Count} biomes)";
        Log($"🏝️ Mode Île initialisé ({morphDesc}, graine {seed}, plafond {maxAlt:F0} m, {w:N0} × {h:N0} lieues).");
    }

    /// <summary>
    /// Charge la carte existante enregistrée dans game/data/carte.json.
    /// </summary>
    public void LoadExistingMap()
    {
        if (!File.Exists(_targetJsonPath))
        {
            MessageBox.Show("Aucun fichier carte.json trouvé dans le dossier de données.", "Fichier introuvable", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            PushUndo();
            var json = File.ReadAllText(_targetJsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var loaded = JsonSerializer.Deserialize<VectorMapDefinition>(json, options);
            if (loaded is null) throw new InvalidOperationException("Format JSON invalide.");

            var (w, h) = (loaded.Width, loaded.Height);
            var cols = Math.Clamp((int)Math.Round(w / 6.25), 100, 360);
            var rows = Math.Clamp((int)Math.Round(h / 6.25), 64, 230);
            _currentHeightmap = new Heightmap(cols, rows, w, h);

            var sanitizedWater = loaded.WaterBodies
                .Where(wb => !wb.IsErroneousGlobalSea(w, h))
                .ToList();

            _currentMap = loaded with { WaterBodies = sanitizedWater };
            var zcols = Math.Clamp((int)Math.Round(w / 10.0), 100, 360);
            var zrows = Math.Clamp((int)Math.Round(h / 10.0), 64, 230);
            _currentZoneGrid = new ZoneMaskGrid(zcols, zrows, w, h);
            _currentZoneGrid.RasterizeFromMap(_currentMap);
            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
            _canvas.ResetView();
            UpdateCommuneLabels();
            UpdateRiverContextButtons();

            _statusDimensions.Text = $"{w:N0} × {h:N0} lieues";
            _statusStats.Text = $"Chargée : {_currentMap.Contours.Count} courbes, {_currentMap.Forests.Count} forêts";
            Log($"📂 Carte chargée depuis {_targetJsonPath} ({_currentMap.Contours.Count} courbes, {_currentMap.Forests.Count} forêts, {_currentMap.SpotElevations.Count} sommets).");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur lors de la lecture de carte.json :\n{ex.Message}", "Erreur de chargement", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RecalculateMap(bool isInitial = false)
    {
        PushUndo();
        var sw = Stopwatch.StartNew();
        var (w, h) = GetCurrentDimensions();

        var parameters = new TerrainParameters
        {
            WorldWidth = w,
            WorldHeight = h,
            MountainMaxAltitude = 540,
            RiverValleyDepth = 45,
            ContourInterval = 25.0,
            SeaDrop = 140,
            HillAltitude = 120
        };

        _currentMap = MapGenerator.Generate(parameters, out _currentHeightmap);
        sw.Stop();

        var zcols = Math.Clamp((int)Math.Round(w / 10.0), 100, 360);
        var zrows = Math.Clamp((int)Math.Round(h / 10.0), 64, 230);
        _currentZoneGrid = new ZoneMaskGrid(zcols, zrows, w, h);
        _currentZoneGrid.RasterizeFromMap(_currentMap);
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        UpdateCommuneLabels();
        UpdateRiverContextButtons();

        _statusDimensions.Text = $"{w:N0} × {h:N0} lieues";
        _statusStats.Text = $"Isohypses : {_currentMap.Contours.Count} | Sommets : {_currentMap.SpotElevations.Count} | Alt. max : {_currentHeightmap.MaxElevation:F0} m";

        var msg = isInitial
            ? $"✓ Carte initialisée : {w:N0} × {h:N0} ({_currentMap.Contours.Count} isohypses en {sw.ElapsedMilliseconds} ms)."
            : $"✓ Relief recalculé en {sw.ElapsedMilliseconds} ms : {w:N0} × {h:N0} ({_currentMap.Contours.Count} isohypses, alt. max {_currentHeightmap.MaxElevation:F0} m).";
        Log(msg);
    }

    private void SaveMap()
    {
        try
        {
            if (_currentZoneGrid != null)
            {
                var biomes = _currentZoneGrid.ExtractBiomes(_canvas.BiomeBrushType, _txtBiomeName.Text);
                var forests = _currentZoneGrid.ExtractForests(_txtBiomeName.Text);
                var (zones, deposits) = _currentZoneGrid.ExtractResources(_txtResourceRegion.Text);
                _currentMap = _currentMap with
                {
                    Biomes = biomes,
                    Forests = forests,
                    ResourceZones = zones,
                    Resources = deposits
                };
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            var json = JsonSerializer.Serialize(_currentMap, options);
            File.WriteAllText(_targetJsonPath, json);

            var fileInfo = new FileInfo(_targetJsonPath);
            var sizeKb = Math.Round((double)fileInfo.Length / 1024.0, 1);

            Log($"★ Carte enregistrée dans game/data/carte.json ({sizeKb} Ko)");

            MessageBox.Show(
                $"La carte a été enregistrée avec succès !\n\n" +
                $"Fichier : {_targetJsonPath}\n" +
                $"Dimensions : {_currentMap.Width:N0} × {_currentMap.Height:N0} lieues\n" +
                $"Courbes de niveau : {_currentMap.Contours.Count} polylignes\n" +
                $"Communes & localités : {_currentMap.AllCommunes.Count}\n" +
                $"Réseau hydrographique : {_currentMap.AllRivers.Count} cours d'eau\n" +
                $"Points cotés : {_currentMap.SpotElevations.Count} sommets\n" +
                $"Biomes naturels : {_currentMap.AllBiomes.Count} zones\n" +
                $"Gisements miniers : {_currentMap.AllResourceZones.Count} zones\n" +
                $"Taille du fichier : {sizeKb} Ko\n\n" +
                $"Le jeu Démos chargera automatiquement cette version.",
                "Enregistrement réussi — Démos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"ERREUR lors de l'enregistrement : {ex.Message}");
            MessageBox.Show(
                $"Une erreur est survenue lors de l'enregistrement de la carte :\n{ex.Message}",
                "Erreur d'enregistrement",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ExportPng()
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Exporter la carte géographique en haute résolution (PNG)",
            Filter = "Image PNG (*.png)|*.png",
            FileName = $"carte-demos-{_currentMap.Width}x{_currentMap.Height}.png"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _canvas.ExportToPng(sfd.FileName);
                Log($"Image haute résolution exportée : {sfd.FileName}");
                MessageBox.Show($"Carte exportée avec succès !\nRésolution : {Math.Max(2000, _currentMap.Width * 2)} × {Math.Max(1280, _currentMap.Height * 2)} px", "Exportation réussie", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"Erreur export PNG : {ex.Message}");
                MessageBox.Show($"Erreur lors de l'export : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
