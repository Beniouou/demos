using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Demos.Carte.UI;
using Demos.Domain.Map;

namespace Demos.Carte;

public sealed partial class MapEditorForm : Form
{
    private readonly string _dataDir;
    private readonly string _targetJsonPath;
    private readonly MapCanvasControl _canvas = new() { Dock = DockStyle.Fill };

    // Bandeau d'état inférieur
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusCoords = new() { Text = "Arpentage : X --- · Y ---", BorderSides = ToolStripStatusLabelBorderSides.Right };
    private readonly ToolStripStatusLabel _statusAlt = new() { Text = "Alt. : --- m", BorderSides = ToolStripStatusLabelBorderSides.Right };
    private readonly ToolStripStatusLabel _statusZoom = new() { Text = "Zoom : 100 %", BorderSides = ToolStripStatusLabelBorderSides.Right };
    private readonly ToolStripStatusLabel _statusDimensions = new() { Text = "2 400 × 1 500", BorderSides = ToolStripStatusLabelBorderSides.Right };
    private readonly ToolStripStatusLabel _statusTool = new() { Text = "Outil : Navigation", BorderSides = ToolStripStatusLabelBorderSides.Right };
    private readonly ToolStripStatusLabel _statusFeature = new() { Text = "Prêt", Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _statusStats = new() { Text = "" };

    // Barre d'outils supérieure permanente (Ligne 1)
    private readonly FlowLayoutPanel _topToolbar = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(8, 6, 8, 5), BackColor = Color.FromArgb(238, 233, 222) };
    private readonly Dictionary<EditorTool, Button> _toolButtons = new();

    // Contrôles permanents de la barre supérieure (Style, Calques & Actions globales)
    private readonly ComboBox _styleCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(2, 3, 4, 0) };
    private readonly CheckBox _chkMinimap = new() { Text = "Minimap", Checked = true, AutoSize = true, Margin = new Padding(4, 6, 6, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly Button _btnUndo = new() { Text = "↶ Annuler", Height = 32, AutoSize = true, Margin = new Padding(2, 1, 2, 0), Cursor = Cursors.Hand };
    private readonly Button _btnRedo = new() { Text = "↷ Rétablir", Height = 32, AutoSize = true, Margin = new Padding(2, 1, 2, 0), Cursor = Cursors.Hand };
    private readonly Button _btnLoadExisting = new() { Text = "📂 Charger", Height = 32, AutoSize = true, Margin = new Padding(2, 1, 2, 0), Cursor = Cursors.Hand };
    private readonly Button _btnExportPng = new() { Text = "🖼 Export PNG", Height = 32, AutoSize = true, Margin = new Padding(2, 1, 4, 0), Cursor = Cursors.Hand };
    private readonly Button _btnSave = new() { Text = "💾 Enregistrer", Height = 34, AutoSize = true, BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Margin = new Padding(4, 0, 4, 0), Cursor = Cursors.Hand };

    // Bandeau contextuel dynamique au-dessus de la carte (Ligne 2)
    private readonly EditorContextBarManager _contextBar = new();

    // Raccourcis de compatibilité vers les contrôles gérés par le contextBar
    private TrackBar _sliderBrushRadius => _contextBar.SliderBrushRadius;
    private TrackBar _sliderBrushPower => _contextBar.SliderBrushPower;
    private TrackBar _sliderBiomeRadius => _contextBar.SliderBiomeRadius;
    private TrackBar _sliderForestRadius => _contextBar.SliderBiomeRadius;
    private TrackBar _sliderResourceRadius => _contextBar.SliderResourceRadius;
    private TextBox _txtBiomeName => _contextBar.TxtBiomeName;
    private TextBox _txtForestName => _contextBar.TxtBiomeName;
    private TextBox _txtResourceRegion => _contextBar.TxtResourceRegion;
    private TextBox _txtRiverName => _contextBar.TxtRiverName;
    private TextBox _txtCommuneName => _contextBar.TxtCommuneName;
    private TextBox _txtPlaceName => _contextBar.TxtPlaceName;
    private NumericUpDown _numRiverDepth => _contextBar.NumRiverDepth;
    private CheckBox _chkDigThalweg => _contextBar.ChkDigThalweg;
    private TrackBar _sliderRiverWidth => _contextBar.SliderRiverWidth;
    private CheckBox _chkPlaceShowAlt => _contextBar.ChkPlaceShowAlt;
    private ComboBox _comboDimensions => _contextBar.ComboDimensions;
    private ComboBox _comboBiomeType => _contextBar.ComboBiomeType;
    private ComboBox _comboResourceType => _contextBar.ComboResourceType;
    private ComboBox _comboPlaceCategory => _contextBar.ComboPlaceCategory;
    public static (string Key, string Display, string DefaultName)[] ResourceTypeChoices => EditorContextBarManager.ResourceTypeChoices;

    // Journal en mémoire
    private readonly TextBox _logBox = new() { Multiline = true, ReadOnly = true };

    // Historique Undo / Redo
    private readonly EditorHistory _history = new();

    // Données actives
    private VectorMapDefinition _currentMap = null!;
    private Heightmap _currentHeightmap = null!;
    private ZoneMaskGrid _currentZoneGrid = null!;

    public MapEditorForm(string dataDir)
    {
        _dataDir = dataDir;
        _targetJsonPath = Path.Combine(dataDir, "carte.json");

        Text = "Projet Démos — Éditeur de Carte & Arpentage Vectoriel";
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(1400, 860);
        MinimumSize = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;

        BuildLayout();
        BindEvents();

        Log("Dossier de données : " + _dataDir);
        CreateBlankMap(isInitial: true);
        SelectTool(EditorTool.Navigate);
    }

    private void BuildLayout()
    {
        var headerPanel = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        _topToolbar.Dock = DockStyle.Top;
        _contextBar.Panel.Dock = DockStyle.Top;

        headerPanel.Controls.Add(_topToolbar);
        headerPanel.Controls.Add(_contextBar.Panel);
        _topToolbar.SendToBack();
        _contextBar.Panel.BringToFront();

        _statusStrip.Items.AddRange([_statusCoords, _statusAlt, _statusZoom, _statusDimensions, _statusTool, _statusFeature, _statusStats]);

        BuildTopToolbar();

        Controls.Add(_canvas);
        Controls.Add(_statusStrip);
        Controls.Add(headerPanel);
    }

    private static Control CreateSep() => EditorContextBarManager.CreateSep();

    private void BuildTopToolbar()
    {
        _topToolbar.Controls.Clear();

        var tools = new (EditorTool Tool, string Icon, string Text)[]
        {
            (EditorTool.Navigate, "🔍", "Navigation"),
            (EditorTool.ElevateBrush, "⛰️", "Terrain"),
            (EditorTool.DrawRiver, "💧", "Fleuves"),
            (EditorTool.DrawBiome, "🗺️", "Biomes"),
            (EditorTool.PlaceResource, "⛏️", "Gisement"),
            (EditorTool.MoveCommune, "🏛️", "Communes"),
            (EditorTool.NamePlace, "📍", "Toponymie")
        };

        foreach (var (tool, icon, text) in tools)
        {
            var btn = new Button
            {
                Text = $"{icon} {text}",
                Height = 34,
                AutoSize = true,
                Margin = new Padding(2, 0, 3, 0),
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };

            var capturedTool = tool;
            btn.Click += (_, _) => SelectTool(capturedTool);

            _toolButtons[tool] = btn;
            _topToolbar.Controls.Add(btn);
        }

        _topToolbar.Controls.Add(CreateSep());

        _topToolbar.Controls.Add(new Label { Text = "Style :", AutoSize = true, Margin = new Padding(2, 7, 2, 0) });
        _styleCombo.Items.Clear();
        _styleCombo.Items.AddRange(["IGN Pastel Moderne", "Parchemin 1874"]);
        _styleCombo.SelectedIndex = 0;
        _topToolbar.Controls.Add(_styleCombo);
        _topToolbar.Controls.Add(_chkMinimap);

        _topToolbar.Controls.Add(CreateSep());

        _topToolbar.Controls.Add(_btnUndo);
        _topToolbar.Controls.Add(_btnRedo);
        _topToolbar.Controls.Add(_btnLoadExisting);
        _topToolbar.Controls.Add(_btnExportPng);
        _topToolbar.Controls.Add(_btnSave);
    }

    public void SelectTool(EditorTool tool)
    {
        _canvas.CurrentTool = tool;

        foreach (var (t, btn) in _toolButtons)
        {
            if (t == tool)
            {
                btn.BackColor = Color.FromArgb(46, 125, 50);
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.FromArgb(248, 245, 238);
                btn.ForeColor = Color.FromArgb(40, 30, 20);
                btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            }
        }

        _statusTool.Text = $"Outil : {_toolButtons[tool].Text.Split(' ')[1]}";
        _contextBar.Populate(tool, _currentMap?.AllRivers.Count ?? 0);
    }

    public void AddRiverPointForTesting(double x, double y) => OnRiverSourcePlaced(x, y);

    public void ElevateBrushForTesting(double x, double y, double radius, double delta)
    {
        _currentHeightmap?.ApplyBrush(x, y, radius, delta);
        ReextractContoursAfterElevationChange();
    }

    public bool ApplyBiomeBrushForTesting(double x, double y, double radius, string biomeKey, bool isAdding, out string? error)
    {
        error = null;
        var res = _currentZoneGrid?.ApplyBiomeBrush(x, y, radius, biomeKey, isAdding, _currentHeightmap, _currentMap.AllRivers, out error) ?? false;
        var biomes = _currentZoneGrid?.ExtractBiomes(biomeKey, _txtBiomeName.Text) ?? [];
        var forests = _currentZoneGrid?.ExtractForests(_txtBiomeName.Text) ?? [];
        _currentMap = _currentMap with { Biomes = biomes, Forests = forests };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        return res;
    }

    public void ApplyForestBrushForTesting(double x, double y, double radius, bool isAdding)
    {
        ApplyBiomeBrushForTesting(x, y, radius, "foret", isAdding, out _);
    }

    public void ApplyResourceBrushForTesting(double x, double y, double radius, string type, bool isAdding)
    {
        _currentZoneGrid?.ApplyResourceBrush(x, y, radius, type, isAdding);
        var (zones, deposits) = _currentZoneGrid?.ExtractResources(_txtResourceRegion.Text) ?? ([], []);
        _currentMap = _currentMap with { ResourceZones = zones, Resources = deposits };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
    }

    public void AddCommuneForTesting(double x, double y, string name, int pop = 100)
    {
        var id = $"commune-{Guid.NewGuid():N}"[..16];
        var newCommune = new CommuneData(id, name, new Point2D(x, y), pop);
        var list = _currentMap.AllCommunes.ToList();
        list.Add(newCommune);
        _currentMap = _currentMap with { Communes = list, DefaultCommune = list[0] };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        UpdateCommuneLabels();
    }

    public void MoveCommuneForTesting(int index, double x, double y)
    {
        var list = _currentMap.AllCommunes.ToList();
        if (index < 0 || index >= list.Count) return;
        var old = list[index];
        list[index] = old with { Position = new Point2D(x, y) };
        _currentMap = _currentMap with { Communes = list, DefaultCommune = list[0] };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        UpdateCommuneLabels();
    }

    public void SetStyleForTesting(MapRenderStyle style)
    {
        _styleCombo.SelectedIndex = style == MapRenderStyle.IgnPastel ? 0 : 1;
        _canvas.SetStyle(style);
    }

    public bool TestSlidersSync(out string report)
    {
        _sliderBrushRadius.Value = 250;
        if ((int)_canvas.BrushRadius != 250)
        {
            report = $"Failed: terrain radius 250 -> canvas={_canvas.BrushRadius}";
            return false;
        }

        _sliderBrushPower.Value = 70;
        if ((int)_canvas.BrushPower != 70)
        {
            report = $"Failed: terrain power 70 -> canvas={_canvas.BrushPower}";
            return false;
        }

        _sliderForestRadius.Value = 180;
        if ((int)_canvas.ForestBrushRadius != 180)
        {
            report = $"Failed: forest radius 180 -> canvas={_canvas.ForestBrushRadius}";
            return false;
        }

        _sliderResourceRadius.Value = 120;
        if ((int)_canvas.ResourceBrushRadius != 120)
        {
            report = $"Failed: resource radius 120 -> canvas={_canvas.ResourceBrushRadius}";
            return false;
        }

        if (!_canvas.TestBrushHoverSafety())
        {
            report = "Failed: Canvas hover safety test failed";
            return false;
        }

        // Test de simulation d'écoulement de rivière en plaine
        var riverPts = _currentHeightmap.SimulateSpontaneousRiver(1200, 750);
        if (riverPts.Count < 10)
        {
            report = $"Failed: river simulation generated only {riverPts.Count} points from (1200, 750)";
            return false;
        }

        // Test de déplacement de la commune de base
        var origX = _currentMap.DefaultCommune.Position.X;
        var origY = _currentMap.DefaultCommune.Position.Y;
        MoveCommuneForTesting(0, origX + 75, origY + 45);
        if (Math.Abs(_currentMap.DefaultCommune.Position.X - (origX + 75)) > 0.1)
        {
            report = "Failed: commune moving test failed";
            return false;
        }
        MoveCommuneForTesting(0, origX, origY);

        // Test des règles de biomes (glacier, forêt, falaise, fertile, désert)
        _currentHeightmap.ApplyBrush(1500, 1500, 50, -500);
        var lowAlt = _currentHeightmap.SampleElevation(1500, 1500);
        if (lowAlt > 0 && lowAlt < 2500)
        {
            var glacierApplied = ApplyBiomeBrushForTesting(1500, 1500, 30, "glacier", true, out var glacierErr);
            if (glacierApplied || glacierErr == null)
            {
                report = "Failed: glacier brush should be rejected below 2500m";
                return false;
            }

            // Falaise sur plaine plate doit être rejetée (pente insuffisante)
            var cliffOnFlat = ApplyBiomeBrushForTesting(1500, 1500, 30, "falaise", true, out var cliffErr);
            if (cliffOnFlat || cliffErr == null)
            {
                report = "Failed: cliff brush should be rejected on flat plain";
                return false;
            }

            // Terre fertile en plaine doit réussir
            var fertileOnPlain = ApplyBiomeBrushForTesting(1500, 1500, 30, "fertile", true, out var fertileErr);
            if (!fertileOnPlain)
            {
                report = $"Failed: fertile brush should succeed on plain (< 600m) (err={fertileErr})";
                return false;
            }
        }

        for (int step = 0; step < 25; step++)
        {
            _currentHeightmap.ApplyBrush(1800, 1200, 100, 150);
        }
        var highAlt = _currentHeightmap.SampleElevation(1800, 1200);
        if (highAlt >= 2500)
        {
            var forestOnGlacier = ApplyBiomeBrushForTesting(1800, 1200, 40, "foret", true, out var forestErr);
            if (forestOnGlacier || forestErr == null)
            {
                report = $"Failed: forest brush should be rejected on glacier territory (alt={highAlt:F0}m)";
                return false;
            }

            var glacierHigh = ApplyBiomeBrushForTesting(1800, 1200, 40, "glacier", true, out var okErr);
            if (!glacierHigh)
            {
                report = $"Failed: glacier brush should succeed at altitude >= 2500m (alt={highAlt:F0}m, err={okErr})";
                return false;
            }

            // Falaise sur le flanc escarpé de la montagne doit réussir
            var cliffHigh = ApplyBiomeBrushForTesting(1800, 1200, 40, "falaise", true, out var cliffHighErr);
            if (!cliffHigh)
            {
                report = $"Failed: cliff brush should succeed on steep mountain slope (err={cliffHighErr})";
                return false;
            }
        }

        // Test d'inversion des falaises / sommet (anneaux et trous Marching Squares)
        var ringGrid = new ZoneMaskGrid(80, 80, 800, 800);
        for (var ci = 0; ci < ringGrid.Columns; ci++)
        {
            var gx = ringGrid.GetWorldX(ci);
            for (var cj = 0; cj < ringGrid.Rows; cj++)
            {
                var gy = ringGrid.GetWorldY(cj);
                var dist = Math.Sqrt((gx - 400) * (gx - 400) + (gy - 400) * (gy - 400));
                if (dist >= 80.0 && dist <= 200.0)
                {
                    ringGrid.SetCellForTesting(ci, cj, 5); // falaise id = 5
                }
            }
        }
        var extractedBiomes = ringGrid.ExtractBiomes();
        var cliffBiomes = extractedBiomes.Where(b => b.Kind == "falaise").ToList();
        if (cliffBiomes.Count != 1)
        {
            report = $"Failed: cliff ring should produce exactly 1 biome with hole, got {cliffBiomes.Count}";
            return false;
        }
        if (cliffBiomes[0].Holes is not { Count: > 0 })
        {
            report = "Failed: cliff ring should contain at least 1 hole for summit";
            return false;
        }

        report = $"All slider, hover, river, commune move, cliff hole extraction, and 7-biome rule tests passed successfully! (River test: {riverPts.Count} points, Mountain peak: {highAlt:F0}m)";
        return true;
    }

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
            _contextBar.LblBrushRadiusVal.Text = $"{_sliderBrushRadius.Value} lieues";
            _canvas.Invalidate();
        };

        _sliderBrushPower.ValueChanged += (_, _) =>
        {
            _canvas.BrushPower = _sliderBrushPower.Value;
            _contextBar.LblBrushPowerVal.Text = $"{_sliderBrushPower.Value} m";
        };

        // Fleuves
        _sliderRiverWidth.ValueChanged += (_, _) =>
        {
            _contextBar.LblRiverWidthVal.Text = $"{_sliderRiverWidth.Value} lieues";
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
            _contextBar.LblBiomeRadiusVal.Text = $"{_sliderBiomeRadius.Value} lieues";
            _canvas.Invalidate();
        };

        _txtBiomeName.TextChanged += (_, _) =>
        {
            _canvas.BiomeBrushName = _txtBiomeName.Text;
        };

        _contextBar.BtnBiomeClearAll.Click += (_, _) => ClearAllBiomes();

        // Gisements
        _sliderResourceRadius.ValueChanged += (_, _) =>
        {
            _canvas.ResourceBrushRadius = _sliderResourceRadius.Value;
            _contextBar.LblResourceRadiusVal.Text = $"{_sliderResourceRadius.Value} lieues";
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
            Log($"✓ Lieu nommé \"{placeName}\" ({cat.DisplayName}, Alt. {alt:F0} m) posé en ({wx:F0}, {wy:F0}).");
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

    private void OnHoverChanged(HoverInfo info)
    {
        if (info.IsInMap)
        {
            _statusCoords.Text = $"Arpentage : X {info.WorldX:F0} · Y {info.WorldY:F0}";
            _statusAlt.Text = $"Alt. : {info.Altitude:F0} m";
            _statusFeature.Text = info.FeatureName;
        }
        else
        {
            _statusCoords.Text = "Arpentage : Hors limites";
            _statusAlt.Text = "Alt. : ---";
            _statusFeature.Text = "";
        }
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
        _currentMap = MapGenerator.CreateIsland(w, h, out _currentHeightmap);
        var zcols = Math.Clamp((int)Math.Round(w / 10.0), 100, 360);
        var zrows = Math.Clamp((int)Math.Round(h / 10.0), 64, 230);
        _currentZoneGrid = new ZoneMaskGrid(zcols, zrows, w, h);
        _currentZoneGrid.RasterizeFromMap(_currentMap);
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        _canvas.ResetView();
        UpdateCommuneLabels();
        UpdateRiverContextButtons();

        _statusDimensions.Text = $"{w:N0} × {h:N0} lieues";
        _statusStats.Text = $"Île découpée créée ({_currentMap.Contours.Count} courbes, {_currentMap.WaterBodies.Count} mers)";
        Log($"🏝️ Mode Île initialisé ({w:N0} × {h:N0} lieues, côtes découpées et océan enveloppant).");
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

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
        _statusFeature.Text = message;
    }
}
