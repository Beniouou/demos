using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Demos.Carte.UI;
using Demos.Domain.Map;

namespace Demos.Carte;

public sealed partial class MapEditorForm
{
    private void UpdateRiverContextButtons()
    {
        _contextBar.UpdateRiverButtons(_currentMap?.AllRivers.Count ?? 0);
    }

    private void BindEvents()
    {
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape)
            {
                SelectTool(EditorTool.Navigate);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                if (_canvas.CurrentTool == EditorTool.DrawRiver) { DeleteLastRiver(); e.Handled = true; }
            }
        };

        // Actions globales
        _btnUndo.Click += (_, _) => Undo();
        _btnRedo.Click += (_, _) => Redo();
        _btnLoadExisting.Click += (_, _) => LoadExistingMap();
        _btnExportPng.Click += (_, _) => ExportPng();
        _btnSave.Click += (_, _) => SaveMap();

        _contextBar.BtnQuickIsland.Click += (_, _) => CreateIslandMap();
        _contextBar.BtnQuickBlank.Click += (_, _) => CreateBlankMap();
        _contextBar.BtnQuickNoise.Click += (_, _) => RecalculateMap();
        _contextBar.BtnZoomReset.Click += (_, _) => _canvas.ResetView();
        _contextBar.BtnRandomSeed.Click += (_, _) => _contextBar.RandomizeSeed();

        // Style et Minimap
        _styleCombo.SelectedIndexChanged += (_, _) =>
        {
            var style = GetCurrentStyle();
            _canvas.SetStyle(style);
            Log($"Mode d'affichage basculé sur : {_styleCombo.SelectedItem}");
        };

        _chkMinimap.CheckedChanged += (_, _) =>
        {
            _canvas.ShowMinimap = _chkMinimap.Checked;
            _canvas.Invalidate();
        };

        _canvas.ZoomChanged += zoom =>
        {
            var percent = (int)Math.Round(zoom * 100);
            _statusZoom.Text = $"Zoom : {percent} %";
        };

        _comboDimensions.SelectedIndexChanged += (_, _) => CreateBlankMap();

        // Pinceau Terrain
        _sliderBrushRadius.ValueChanged += (_, _) =>
        {
            _canvas.BrushRadius = _sliderBrushRadius.Value;
            _contextBar.LblBrushRadiusVal.Text = MapScale.Format(MapScale.ToKm(_sliderBrushRadius.Value));
            _canvas.Invalidate();
        };

        _sliderBrushPower.ValueChanged += (_, _) =>
        {
            _canvas.BrushPower = _sliderBrushPower.Value;
            _contextBar.LblBrushPowerVal.Text = $"{_sliderBrushPower.Value} m";
        };

        _contextBar.ComboBrushProfile.SelectedIndexChanged += (_, _) =>
        {
            _canvas.BrushProfile = _contextBar.GetSelectedBrushProfile();
        };

        // Pinceau Adoucisseur
        _sliderSmoothRadius.ValueChanged += (_, _) =>
        {
            _canvas.SmoothBrushRadius = _sliderSmoothRadius.Value;
            _contextBar.LblSmoothRadiusVal.Text = MapScale.Format(MapScale.ToKm(_sliderSmoothRadius.Value));
            _canvas.Invalidate();
        };

        _sliderSmoothPower.ValueChanged += (_, _) =>
        {
            _canvas.SmoothBrushPower = _sliderSmoothPower.Value;
            _contextBar.LblSmoothPowerVal.Text = $"{_sliderSmoothPower.Value} %";
        };

        // Fleuves
        _sliderRiverWidth.ValueChanged += (_, _) =>
        {
            _contextBar.LblRiverWidthVal.Text = MapScale.Format(MapScale.ToKm(_sliderRiverWidth.Value));
        };

        _contextBar.BtnRiverDeleteLast.Click += (_, _) => DeleteLastRiver();
        _contextBar.BtnRiverClearAll.Click += (_, _) => ClearAllRivers();

        // Biomes (Milieux naturels)
        _comboBiomeType.SelectedIndexChanged += (_, _) =>
        {
            var idx = Math.Clamp(_comboBiomeType.SelectedIndex, 0, ZoneMaskGrid.BiomeTypes.Length - 1);
            var choice = ZoneMaskGrid.BiomeTypes[idx];
            _canvas.BiomeBrushType = choice.Key;
            _txtBiomeName.Text = choice.DefaultName;
            _canvas.BiomeBrushName = choice.DefaultName;
            _contextBar.UpdateBiomeRuleHint();
            _canvas.Invalidate();
        };

        _sliderBiomeRadius.ValueChanged += (_, _) =>
        {
            _canvas.BiomeBrushRadius = _sliderBiomeRadius.Value;
            _contextBar.LblBiomeRadiusVal.Text = MapScale.Format(MapScale.ToKm(_sliderBiomeRadius.Value));
            _canvas.Invalidate();
        };

        _txtBiomeName.TextChanged += (_, _) =>
        {
            _canvas.BiomeBrushName = _txtBiomeName.Text;
        };

        _contextBar.BtnBiomeAutoGenerate.Click += (_, _) => AutoGenerateBiomes();
        _contextBar.BtnBiomeClearAll.Click += (_, _) => ClearAllBiomes();

        // Gisements
        _sliderResourceRadius.ValueChanged += (_, _) =>
        {
            _canvas.ResourceBrushRadius = _sliderResourceRadius.Value;
            _contextBar.LblResourceRadiusVal.Text = MapScale.Format(MapScale.ToKm(_sliderResourceRadius.Value));
            _canvas.Invalidate();
        };

        _comboResourceType.SelectedIndexChanged += (_, _) =>
        {
            var idx = Math.Clamp(_comboResourceType.SelectedIndex, 0, ResourceTypeChoices.Length - 1);
            var choice = ResourceTypeChoices[idx];
            _canvas.ResourceBrushType = choice.Key;
            _txtResourceRegion.Text = choice.DefaultName;
        };

        _txtResourceRegion.TextChanged += (_, _) =>
        {
            _canvas.ResourceBrushName = _txtResourceRegion.Text;
        };

        _contextBar.BtnResourceClearAll.Click += (_, _) => ClearAllResources();

        // Communes
        _contextBar.BtnCommuneDice.Click += (_, _) =>
        {
            _txtCommuneName.Text = _contextBar.GetNextSuggestedCommuneName();
        };

        _contextBar.BtnCommuneDeleteLast.Click += (_, _) => DeleteLastCommune();

        // Toponymie
        _comboPlaceCategory.SelectedIndexChanged += (_, _) => OnPlaceCategoryChanged(_comboPlaceCategory.SelectedIndex);

        _txtPlaceName.TextChanged += (_, _) =>
        {
            _canvas.PlaceLabelName = _txtPlaceName.Text;
            _canvas.Invalidate();
        };

        _chkPlaceShowAlt.CheckedChanged += (_, _) =>
        {
            _canvas.PlaceLabelShowAltitude = _chkPlaceShowAlt.Checked;
            _canvas.Invalidate();
        };

        _contextBar.BtnPlaceDeleteLast.Click += (_, _) => DeleteLastPlace();

        // Initialisation des valeurs des pinceaux pour le Canvas
        _canvas.BrushRadius = _sliderBrushRadius.Value;
        _canvas.BrushPower = _sliderBrushPower.Value;
        _canvas.SmoothBrushRadius = _sliderSmoothRadius.Value;
        _canvas.SmoothBrushPower = _sliderSmoothPower.Value;
        _canvas.BiomeBrushRadius = _sliderBiomeRadius.Value;
        _canvas.BiomeBrushType = ZoneMaskGrid.BiomeTypes[0].Key;
        _canvas.BiomeBrushName = _txtBiomeName.Text;
        _canvas.ResourceBrushRadius = _sliderResourceRadius.Value;
        _canvas.ResourceBrushType = ResourceTypeChoices[0].Key;
        _canvas.ResourceBrushName = _txtResourceRegion.Text;

        // Canvas events
        _canvas.ElevationModified += () =>
        {
            PushUndo();
            ReextractContoursAfterElevationChange();
            Log("Altitude modifiée au pinceau.");
        };

        _canvas.BiomeBrushApplied += () =>
        {
            PushUndo();
            Log($"🗺️ Milieu naturel modifié au pinceau ({_currentMap.AllBiomes.Count} zones de biomes).");
        };

        _canvas.BiomeValidationWarning += warn =>
        {
            Log($"⚠️ {warn}");
        };

        _canvas.ResourceBrushApplied += () =>
        {
            PushUndo();
            Log($"⛏️ Zone de ressource modifiée au pinceau ({_currentMap.AllResourceZones.Count} zones).");
        };

        _canvas.RiverSourcePlaced += OnRiverSourcePlaced;
        _canvas.RiverDeleted += OnRiverDeleted;

        _canvas.CommuneMoved += (index, wx, wy) =>
        {
            var list = _currentMap.AllCommunes.ToList();
            if (index < 0 || index >= list.Count) return;

            PushUndo();
            var old = list[index];
            var updated = old with { Position = new Point2D(Math.Round(wx, 1), Math.Round(wy, 1)) };
            list[index] = updated;
            _currentMap = _currentMap with { Communes = list, DefaultCommune = list[0] };

            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
            UpdateCommuneLabels();
            var roleStr = index == 0 ? "Chef-lieu (commune de base)" : "Commune";
            Log($"📍 {roleStr} \"{updated.Name}\" déplacée en ({wx:F0}, {wy:F0}).");
        };

        _canvas.CommunePlaced += (wx, wy) =>
        {
            PushUndo();
            var name = string.IsNullOrWhiteSpace(_txtCommuneName.Text) ? _contextBar.GetNextSuggestedCommuneName() : _txtCommuneName.Text.Trim();
            var id = $"commune-{Guid.NewGuid():N}"[..16];
            var newCommune = new CommuneData(id, name, new Point2D(Math.Round(wx, 1), Math.Round(wy, 1)));
            var list = _currentMap.AllCommunes.ToList();
            list.Add(newCommune);
            _currentMap = _currentMap with { Communes = list, DefaultCommune = list[0] };

            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
            UpdateCommuneLabels();
            Log($"🏛️ Commune \"{name}\" posée en ({wx:F0}, {wy:F0}). Total : {list.Count} communes.");
            _txtCommuneName.Text = _contextBar.GetNextSuggestedCommuneName();
        };

        _canvas.CommuneDeleted += (wx, wy) =>
        {
            var list = _currentMap.AllCommunes.ToList();
            if (list.Count == 0) return;

            CommuneData? toDelete = null;
            var bestDist = 30.0;
            foreach (var c in list)
            {
                var d = Math.Sqrt(Math.Pow(c.Position.X - wx, 2) + Math.Pow(c.Position.Y - wy, 2));
                if (d < bestDist)
                {
                    bestDist = d;
                    toDelete = c;
                }
            }

            if (toDelete is not null)
            {
                PushUndo();
                list.Remove(toDelete);
                _currentMap = _currentMap with { Communes = list, DefaultCommune = list.FirstOrDefault() ?? new CommuneData("commune-1", "Village", new Point2D(0, 0)) };
                _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
                UpdateCommuneLabels();
                Log($"🗑 Commune \"{toDelete.Name}\" supprimée ({list.Count} restantes).");
            }
        };

        _canvas.PlaceNamed += (wx, wy) =>
        {
            PushUndo();
            var alt = Math.Round(_currentHeightmap.SampleElevation(wx, wy), 1);
            var cat = PlaceCategories.Get(_canvas.PlaceLabelCategory);
            var placeName = string.IsNullOrWhiteSpace(_txtPlaceName.Text) ? cat.DefaultName : _txtPlaceName.Text.Trim();
            var showAlt = _chkPlaceShowAlt.Checked;

            var newSpots = _currentMap.SpotElevations.ToList();
            newSpots.Add(new SpotElevation(alt, placeName, new Point2D(Math.Round(wx, 1), Math.Round(wy, 1)), cat.Id, showAlt));
            _currentMap = _currentMap with { SpotElevations = newSpots };

            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
            Log($"✓ Lieu nommé \"{placeName}\" ({cat.DisplayName}, Alt. {alt:F0} m) posé en ({MapScale.ToKm(wx):0.0} km, {MapScale.ToKm(wy):0.0} km).");
        };

        _canvas.PlaceDeleted += (wx, wy) =>
        {
            if (_currentMap.SpotElevations.Count == 0) return;
            var clicked = _currentMap.SpotElevations
                .Select(s => (Spot: s, Dist: Math.Sqrt(Math.Pow(s.Position.X - wx, 2) + Math.Pow(s.Position.Y - wy, 2))))
                .Where(x => x.Dist < 25.0)
                .OrderBy(x => x.Dist)
                .Select(x => x.Spot)
                .FirstOrDefault();

            if (clicked is not null)
            {
                PushUndo();
                var newSpots = _currentMap.SpotElevations.ToList();
                newSpots.Remove(clicked);
                _currentMap = _currentMap with { SpotElevations = newSpots };
                _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
                Log($"⌫ Lieu \"{clicked.Name}\" supprimé.");
            }
        };

        _canvas.HoverChanged += OnHoverChanged;
    }

    private void OnRiverSourcePlaced(double wx, double wy)
    {
        if (_canvas.CurrentTool != EditorTool.DrawRiver) return;

        var pts = _currentHeightmap.SimulateSpontaneousRiver(wx, wy, _currentMap.AllRivers);
        if (pts.Count < 2)
        {
            Log("⚠️ Source trop proche de la mer ou d'une dépression fermée pour former un cours d'eau.");
            return;
        }

        PushUndo();
        var name = string.IsNullOrWhiteSpace(_txtRiverName.Text) ? _contextBar.GetNextSuggestedRiverName() : _txtRiverName.Text.Trim();
        var width = (double)_sliderRiverWidth.Value;
        var depth = (double)_numRiverDepth.Value;

        var newRiver = new RiverData(name, pts, Width: width);
        var rivers = _currentMap.AllRivers.ToList();
        rivers.Add(newRiver);
        _currentMap = _currentMap with { Rivers = rivers, River = rivers[0] };

        if (_chkDigThalweg.Checked && depth > 0)
        {
            _currentHeightmap.DigRiverThalweg(pts, maxDepth: depth);
            ReextractContoursAfterElevationChange();
            Log($"✓ Cours d'eau \"{name}\" généré ({pts.Count} points) avec thalweg creusé à {depth:F0} m.");
        }
        else
        {
            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
            Log($"✓ Cours d'eau \"{name}\" généré ({pts.Count} points) sans modification du relief.");
        }

        _txtRiverName.Text = _contextBar.GetNextSuggestedRiverName();
        UpdateRiverContextButtons();
    }

    private void OnRiverDeleted(double wx, double wy)
    {
        if (_canvas.CurrentTool != EditorTool.DrawRiver) return;

        var rivers = _currentMap.AllRivers.ToList();
        if (rivers.Count == 0) return;

        RiverData? toDelete = null;
        var bestDist = 25.0;

        foreach (var r in rivers)
        {
            for (var i = 0; i < r.Points.Count - 1; i++)
            {
                var p1 = r.Points[i];
                var p2 = r.Points[i + 1];
                var (d, _, _) = Heightmap.DistanceAndProjectionPointToSegment(wx, wy, p1.X, p1.Y, p2.X, p2.Y);
                if (d < bestDist)
                {
                    bestDist = d;
                    toDelete = r;
                }
            }
        }

        if (toDelete is not null)
        {
            PushUndo();
            rivers.Remove(toDelete);
            _currentMap = _currentMap with { Rivers = rivers, River = rivers.FirstOrDefault() ?? new RiverData("", []) };
            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
            UpdateRiverContextButtons();
            Log($"🗑 Cours d'eau \"{toDelete.Name}\" supprimé.");
        }
    }

    private void DeleteLastRiver()
    {
        var rivers = _currentMap.AllRivers.ToList();
        if (rivers.Count == 0) return;

        PushUndo();
        var last = rivers[^1];
        rivers.RemoveAt(rivers.Count - 1);
        _currentMap = _currentMap with { Rivers = rivers, River = rivers.FirstOrDefault() ?? new RiverData("", []) };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        UpdateRiverContextButtons();
        Log($"🗑 Cours d'eau \"{last.Name}\" retiré ({rivers.Count} restants).");
    }

    private void ClearAllRivers()
    {
        if (_currentMap.AllRivers.Count == 0) return;

        PushUndo();
        _currentMap = _currentMap with { Rivers = Array.Empty<RiverData>(), River = new RiverData("", []) };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        UpdateRiverContextButtons();
        Log("🗑 Tout le réseau hydrographique a été effacé.");
    }

    private void ClearAllBiomes()
    {
        if (_currentMap.AllBiomes.Count == 0) return;
        PushUndo();
        _currentZoneGrid?.ClearBiomes();
        _currentMap = _currentMap with { Biomes = [], Forests = [] };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        Log("🗑 Tous les milieux naturels ont été effacés.");
    }

    public void AutoGenerateBiomes()
    {
        if (_currentHeightmap is null || _currentZoneGrid is null || _currentMap is null) return;

        PushUndo();
        _currentZoneGrid.AutoGenerateBiomes(_currentHeightmap, _currentMap.AllRivers);
        var biomes = _currentZoneGrid.ExtractBiomes(_canvas.BiomeBrushType, _txtBiomeName.Text);
        var forests = _currentZoneGrid.ExtractForests(_txtBiomeName.Text);
        _currentMap = _currentMap with { Biomes = biomes, Forests = forests };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);

        var stats = string.Join(", ", _currentMap.AllBiomes.GroupBy(b => b.Kind).Select(g => $"{g.Count()} {g.Key}"));
        Log($"🪄 Biomes naturels pré-générés automatiquement selon le relief ({_currentMap.AllBiomes.Count} zones : {stats}).");
    }

    private void ClearAllForests() => ClearAllBiomes();

    private void OnPlaceCategoryChanged(int index)
    {
        if (index < 0 || index >= PlaceCategories.All.Count) return;
        var cat = PlaceCategories.All[index];

        if (_comboPlaceCategory.SelectedIndex != index) _comboPlaceCategory.SelectedIndex = index;

        _canvas.PlaceLabelCategory = cat.Id;
        _chkPlaceShowAlt.Checked = cat.DefaultShowAltitude;
        _canvas.PlaceLabelShowAltitude = cat.DefaultShowAltitude;

        var currentText = _txtPlaceName.Text.Trim();
        if (string.IsNullOrEmpty(currentText) || PlaceCategories.All.Any(c => c.DefaultName == currentText))
        {
            _txtPlaceName.Text = cat.DefaultName;
            _canvas.PlaceLabelName = cat.DefaultName;
        }
        _canvas.Invalidate();
    }

    private void DeleteLastPlace()
    {
        if (_currentMap.SpotElevations.Count == 0) return;
        PushUndo();
        var last = _currentMap.SpotElevations[^1];
        var newSpots = _currentMap.SpotElevations.Take(_currentMap.SpotElevations.Count - 1).ToList();
        _currentMap = _currentMap with { SpotElevations = newSpots };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        Log($"⌫ Lieu retiré : \"{last.Name}\".");
    }

    private void ClearAllResources()
    {
        if (_currentMap.Resources.Count == 0 && (_currentMap.ResourceZones == null || _currentMap.ResourceZones.Count == 0)) return;
        PushUndo();
        _currentZoneGrid?.ClearResources();
        _currentMap = _currentMap with { Resources = [], ResourceZones = [] };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        Log("🗑 Tous les gisements et zones fertiles ont été effacés.");
    }

    private void DeleteLastCommune()
    {
        var list = _currentMap.AllCommunes.ToList();
        if (list.Count <= 1)
        {
            Log("⚠️ Impossible de supprimer l'unique commune restante.");
            return;
        }

        PushUndo();
        var last = list[^1];
        list.RemoveAt(list.Count - 1);
        _currentMap = _currentMap with { Communes = list, DefaultCommune = list[0] };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        UpdateCommuneLabels();
        Log($"🗑 Commune \"{last.Name}\" retirée ({list.Count} restantes).");
    }

    private void UpdateCommuneLabels()
    {
        _contextBar.UpdateCommuneCount(_currentMap?.AllCommunes.Count ?? 0);
    }
}
