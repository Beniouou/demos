using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
    private readonly StatusStrip _statusStrip = new() { BackColor = Color.FromArgb(238, 233, 222), Font = new Font("Segoe UI", 9f), SizingGrip = false };
    private readonly ToolStripStatusLabel _statusCoords = new() { Text = "Arpentage : X --- · Y ---", BorderSides = ToolStripStatusLabelBorderSides.Right, Margin = new Padding(3, 2, 3, 2) };
    private readonly ToolStripStatusLabel _statusAlt = new() { Text = "Alt. : --- m", BorderSides = ToolStripStatusLabelBorderSides.Right, Margin = new Padding(3, 2, 3, 2) };
    private readonly ToolStripStatusLabel _statusZoom = new() { Text = "Zoom : 100 %", BorderSides = ToolStripStatusLabelBorderSides.Right, Margin = new Padding(3, 2, 3, 2) };
    private readonly ToolStripStatusLabel _statusDimensions = new() { Text = "2 400 × 1 500", BorderSides = ToolStripStatusLabelBorderSides.Right, Margin = new Padding(3, 2, 3, 2) };
    private readonly ToolStripStatusLabel _statusTool = new() { Text = "Outil : Navigation", BorderSides = ToolStripStatusLabelBorderSides.Right, Margin = new Padding(3, 2, 3, 2) };
    private readonly ToolStripStatusLabel _statusFeature = new() { Text = "Prêt", Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _statusStats = new() { Text = "" };

    // Infobulles d'aide
    private readonly ToolTip _toolTip = new() { ShowAlways = true, InitialDelay = 300 };

    // Barre d'outils supérieure permanente (Ligne 1)
    private readonly FlowLayoutPanel _topToolbar = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = false,
        Padding = new Padding(6, 4, 6, 4),
        BackColor = Color.FromArgb(238, 233, 222)
    };
    private readonly Dictionary<EditorTool, Button> _toolButtons = new();

    // Contrôles permanents de la barre supérieure (Style, Calques & Actions globales)
    private readonly ComboBox _styleCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175, Margin = new Padding(2, 2, 4, 0) };
    private readonly CheckBox _chkMinimap = new() { Text = "Minimap", Checked = true, AutoSize = true, Margin = new Padding(4, 5, 6, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 35, 28) };
    private readonly Button _btnUndo = new();
    private readonly Button _btnRedo = new();
    private readonly Button _btnLoadExisting = new();
    private readonly Button _btnExportPng = new();
    private readonly Button _btnSave = new();

    // Bandeau contextuel dynamique au-dessus de la carte (Ligne 2)
    private readonly EditorContextBarManager _contextBar = new();

    // Raccourcis de compatibilité vers les contrôles gérés par le contextBar
    private TrackBar _sliderBrushRadius => _contextBar.SliderBrushRadius;
    private TrackBar _sliderBrushPower => _contextBar.SliderBrushPower;
    private TrackBar _sliderSmoothRadius => _contextBar.SliderSmoothRadius;
    private TrackBar _sliderSmoothPower => _contextBar.SliderSmoothPower;
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
    private NumericUpDown _numSeed => _contextBar.NumSeed;
    private ComboBox _comboMorphology => _contextBar.ComboMorphology;
    private ComboBox _comboMaxAltitude => _contextBar.ComboMaxAltitude;
    private ComboBox _comboBrushProfile => _contextBar.ComboBrushProfile;
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

        var bottomBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(210, 202, 190) };

        headerPanel.Controls.Add(bottomBorder);
        headerPanel.Controls.Add(_contextBar.Panel);
        headerPanel.Controls.Add(_topToolbar);
        _topToolbar.SendToBack();
        _contextBar.Panel.BringToFront();
        bottomBorder.BringToFront();

        _statusStrip.Items.AddRange([_statusCoords, _statusAlt, _statusZoom, _statusDimensions, _statusTool, _statusFeature, _statusStats]);

        BuildTopToolbar();

        Controls.Add(_canvas);
        Controls.Add(_statusStrip);
        Controls.Add(headerPanel);
    }

    private static Control CreateSep() => EditorContextBarManager.CreateSep();

    private void SetupActionButton(Button btn, string text, string tooltip, int? width = null, bool isPrimary = false)
    {
        btn.Text = text;
        btn.Height = 34;
        if (width.HasValue)
        {
            btn.AutoSize = false;
            btn.Width = width.Value;
        }
        else
        {
            btn.AutoSize = true;
            btn.Padding = new Padding(5, 0, 5, 0);
        }
        btn.Margin = new Padding(1, 0, 1, 0);
        btn.FlatStyle = FlatStyle.Flat;
        btn.Cursor = Cursors.Hand;
        btn.Font = new Font("Segoe UI", 9f, isPrimary ? FontStyle.Bold : FontStyle.Regular);

        if (isPrimary)
        {
            btn.BackColor = Color.FromArgb(46, 125, 50);
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderColor = Color.FromArgb(35, 95, 38);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 142, 60);
        }
        else
        {
            btn.BackColor = Color.FromArgb(248, 245, 238);
            btn.ForeColor = Color.FromArgb(45, 40, 32);
            btn.FlatAppearance.BorderColor = Color.FromArgb(215, 208, 195);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 226, 215);
        }

        _toolTip.SetToolTip(btn, tooltip);
    }

    private void BuildTopToolbar()
    {
        _topToolbar.Controls.Clear();

        var tools = new (EditorTool Tool, string Icon, string Text, string Tip, int Width)[]
        {
            (EditorTool.Navigate, "🔍", "Navigation", "Navigation panoramique et zoom libre", 122),
            (EditorTool.ElevateBrush, "⛰️", "Terrain", "Pinceau de relief (Surélever / Creuser)", 94),
            (EditorTool.SmoothBrush, "〰️", "Adoucir", "Adoucisseur de pente & aplanisseur en plateau", 94),
            (EditorTool.DrawRiver, "💧", "Fleuves", "Tracé spontané gravitaire des cours d'eau", 94),
            (EditorTool.DrawBiome, "🗺️", "Biomes", "Peinture des 7 milieux naturels (Forêts, Marais, Glaciers...)", 98),
            (EditorTool.PlaceResource, "⛏️", "Gisements", "Implantation des filons miniers et carrières", 114),
            (EditorTool.MoveCommune, "🏛️", "Communes", "Fondation et déplacement des communes", 122),
            (EditorTool.NamePlace, "📍", "Toponymie", "Toponymie et cotation altimétrique des lieux", 120)
        };

        foreach (var (tool, icon, text, tip, w) in tools)
        {
            var btn = new Button
            {
                Text = $"{icon} {text}",
                Height = 34,
                Width = w,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = Padding.Empty,
                Margin = new Padding(1, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(215, 208, 195);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 226, 215);

            var capturedTool = tool;
            btn.Click += (_, _) => SelectTool(capturedTool);
            _toolTip.SetToolTip(btn, tip);

            _toolButtons[tool] = btn;
            _topToolbar.Controls.Add(btn);
        }

        _topToolbar.Controls.Add(CreateSep());

        _styleCombo.Width = 160;
        _styleCombo.Font = new Font("Segoe UI", 8.8f);
        _styleCombo.Items.Clear();
        _styleCombo.Items.AddRange(["IGN Pastel Moderne", "Parchemin 1874"]);
        _styleCombo.SelectedIndex = 0;
        _styleCombo.Margin = new Padding(2, 5, 2, 0);
        _topToolbar.Controls.Add(_styleCombo);
        _topToolbar.Controls.Add(_chkMinimap);

        _topToolbar.Controls.Add(CreateSep());

        SetupActionButton(_btnUndo, "↶", "Annuler la dernière action (Ctrl+Z)", width: 28);
        SetupActionButton(_btnRedo, "↷", "Rétablir l'action annulée (Ctrl+Y)", width: 28);
        SetupActionButton(_btnLoadExisting, "📂", "Charger une carte existante (carte.json)", width: 32);
        SetupActionButton(_btnExportPng, "🖼", "Exporter la carte en image haute résolution PNG", width: 32);
        SetupActionButton(_btnSave, "💾 Enregistrer", "Enregistrer définitivement la carte vectorielle (carte.json)", width: 126, isPrimary: true);

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
                btn.FlatAppearance.BorderColor = Color.FromArgb(35, 95, 38);
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 142, 60);
                btn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.FromArgb(248, 245, 238);
                btn.ForeColor = Color.FromArgb(45, 40, 32);
                btn.FlatAppearance.BorderColor = Color.FromArgb(215, 208, 195);
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 226, 215);
                btn.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            }
        }

        _statusTool.Text = $"Outil : {_toolButtons[tool].Text.Split(' ')[1]}";
        _contextBar.Populate(tool, _currentMap?.AllRivers.Count ?? 0);
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

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
        _statusFeature.Text = message;
    }
}
