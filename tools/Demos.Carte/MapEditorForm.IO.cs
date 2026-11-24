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
            RefreshStartReportsIfShown();
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
            RefreshStartReportsIfShown();
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
        var size = MapSizes.All[Math.Clamp(_comboDimensions.SelectedIndex, 0, MapSizes.All.Length - 1)];
        return (size.Width, size.Height);
    }

    /// <summary>
    /// Initialise ou réinitialise la carte en mode complètement vierge.
    /// </summary>
    public void CreateBlankMap(bool isInitial = false)
    {
        if (!isInitial) PushUndo();
        StartNewMap();

        var (w, h) = GetCurrentDimensions();
        _currentMap = MapGenerator.CreateBlank(w, h, out _currentHeightmap);
        var (zcols, zrows) = MapSizes.ZoneGrid(w, h);
        _currentZoneGrid = new ZoneMaskGrid(zcols, zrows, w, h);
        _currentZoneGrid.RasterizeFromMap(_currentMap);
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        _canvas.ResetView();
        UpdateCommuneLabels();
        UpdateRiverContextButtons();

        _statusDimensions.Text = $"{MapScale.ToKm(w):N0} × {MapScale.ToKm(h):N0} km";
        _statusStats.Text = "Carte vierge (toile blanche)";
        var msg = isInitial
            ? $"📄 Carte vierge initialisée par défaut ({MapScale.ToKm(w):N0} × {MapScale.ToKm(h):N0} km). Prête pour vos créations !"
            : $"📄 Carte réinitialisée en mode vierge ({MapScale.ToKm(w):N0} × {MapScale.ToKm(h):N0} km).";
        Log(msg);
    }

    /// <summary>
    /// Initialise la carte en Mode Île avec des côtes découpées naturelles et un océan enveloppant.
    /// </summary>
    public void CreateIslandMap()
    {
        PushUndo();
        StartNewMap();

        var (w, h) = GetCurrentDimensions();
        var seed = (int)_contextBar.NumSeed.Value;
        var morph = _contextBar.GetSelectedMorphology();
        var maxAlt = _contextBar.GetSelectedMaxAltitude();

        _currentMap = MapGenerator.CreateIsland(w, h, out _currentHeightmap, seed, morph, maxAlt);
        var (zcols, zrows) = MapSizes.ZoneGrid(w, h);
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
        _statusDimensions.Text = $"{MapScale.ToKm(w):N0} × {MapScale.ToKm(h):N0} km";
        _statusStats.Text = $"{morphDesc} ({_currentMap.Contours.Count} courbes, alt. max {_currentHeightmap.MaxElevation:F0} m, {_currentMap.AllBiomes.Count} biomes)";
        Log($"🏝️ Mode Île initialisé ({morphDesc}, graine {seed}, plafond {maxAlt:F0} m, {MapScale.ToKm(w):N0} × {MapScale.ToKm(h):N0} km).");
    }

    private void RecalculateMap(bool isInitial = false)
    {
        PushUndo();
        StartNewMap();
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

        var (zcols, zrows) = MapSizes.ZoneGrid(w, h);
        _currentZoneGrid = new ZoneMaskGrid(zcols, zrows, w, h);
        _currentZoneGrid.RasterizeFromMap(_currentMap);
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        UpdateCommuneLabels();
        UpdateRiverContextButtons();

        _statusDimensions.Text = $"{MapScale.ToKm(w):N0} × {MapScale.ToKm(h):N0} km";
        _statusStats.Text = $"Isohypses : {_currentMap.Contours.Count} | Sommets : {_currentMap.SpotElevations.Count} | Alt. max : {_currentHeightmap.MaxElevation:F0} m";

        var msg = isInitial
            ? $"✓ Carte initialisée : {w:N0} × {h:N0} ({_currentMap.Contours.Count} isohypses en {sw.ElapsedMilliseconds} ms)."
            : $"✓ Relief recalculé en {sw.ElapsedMilliseconds} ms : {w:N0} × {h:N0} ({_currentMap.Contours.Count} isohypses, alt. max {_currentHeightmap.MaxElevation:F0} m).";
        Log(msg);
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
