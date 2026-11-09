using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Demos.Domain.Map;

namespace Demos.Carte.UI;

/// <summary>
/// Gestionnaire du bandeau contextuel dynamique (Ligne 2) de l'éditeur cartographique,
/// affichant les outils et paramètres spécifiques au mode actif.
/// </summary>
public sealed class EditorContextBarManager
{
    public Panel Panel { get; } = new()
    {
        Dock = DockStyle.Top,
        Height = 46,
        Padding = new Padding(10, 6, 10, 6),
        BackColor = Color.FromArgb(248, 244, 236)
    };

    public FlowLayoutPanel FlowContent { get; } = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false
    };

    // Gabarits et actions rapides (Navigation & Terrain)
    public Button BtnQuickIsland { get; } = new() { Text = "🏝️ Mode Île", Height = 32, AutoSize = true, BackColor = Color.FromArgb(235, 246, 244), ForeColor = Color.FromArgb(20, 90, 80), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 1, 4, 0), Cursor = Cursors.Hand };
    public Button BtnQuickBlank { get; } = new() { Text = "📄 Carte vierge", Height = 32, AutoSize = true, Margin = new Padding(0, 1, 4, 0), Cursor = Cursors.Hand };
    public Button BtnQuickNoise { get; } = new() { Text = "🎲 Relief procédural", Height = 32, AutoSize = true, Margin = new Padding(0, 1, 6, 0), Cursor = Cursors.Hand };
    public Button BtnZoomReset { get; } = new() { Text = "🎯 Recentrer vue", Height = 32, AutoSize = true, Margin = new Padding(0, 1, 6, 0), Cursor = Cursors.Hand };
    public ComboBox ComboDimensions { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210, Margin = new Padding(0, 3, 8, 0) };

    // Pinceau Terrain
    public Label LblBrushRadius { get; } = new() { Text = "Rayon :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TrackBar SliderBrushRadius { get; } = new() { Minimum = 15, Maximum = 400, Value = 80, TickFrequency = 25, Width = 110, Margin = new Padding(0, 2, 2, 0) };
    public Label LblBrushRadiusVal { get; } = new() { Text = "80 lieues", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
    public Label LblBrushPower { get; } = new() { Text = "Force :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TrackBar SliderBrushPower { get; } = new() { Minimum = 5, Maximum = 150, Value = 30, TickFrequency = 15, Width = 100, Margin = new Padding(0, 2, 2, 0) };
    public Label LblBrushPowerVal { get; } = new() { Text = "30 m", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    // Pinceau Adoucisseur & Aplanisseur
    public Label LblSmoothRadius { get; } = new() { Text = "Rayon :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TrackBar SliderSmoothRadius { get; } = new() { Minimum = 15, Maximum = 400, Value = 80, TickFrequency = 25, Width = 110, Margin = new Padding(0, 2, 2, 0) };
    public Label LblSmoothRadiusVal { get; } = new() { Text = "80 lieues", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
    public Label LblSmoothPower { get; } = new() { Text = "Intensité :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TrackBar SliderSmoothPower { get; } = new() { Minimum = 10, Maximum = 100, Value = 30, TickFrequency = 10, Width = 100, Margin = new Padding(0, 2, 2, 0) };
    public Label LblSmoothPowerVal { get; } = new() { Text = "30 %", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    // Fleuves & Réseau hydrographique
    public Label LblRiverStatus { get; } = new() { Text = "💧 0 fleuve", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
    public Label LblRiverName { get; } = new() { Text = "Nom :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TextBox TxtRiverName { get; } = new() { Text = "La Sereine", Width = 130, Margin = new Padding(0, 3, 8, 0) };
    public Label LblRiverDepth { get; } = new() { Text = "Thalweg :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public NumericUpDown NumRiverDepth { get; } = new() { Minimum = 0, Maximum = 120, Value = 35, Width = 60, Margin = new Padding(0, 3, 6, 0) };
    public CheckBox ChkDigThalweg { get; } = new() { Text = "Creuser vallée", Checked = true, AutoSize = true, Margin = new Padding(0, 6, 8, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    public Label LblRiverWidth { get; } = new() { Text = "Largeur :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TrackBar SliderRiverWidth { get; } = new() { Minimum = 2, Maximum = 15, Value = 5, TickFrequency = 1, Width = 90, Margin = new Padding(0, 2, 2, 0) };
    public Label LblRiverWidthVal { get; } = new() { Text = "5 lieues", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
    public Button BtnRiverDeleteLast { get; } = new() { Text = "⌫ Retirer", Height = 32, AutoSize = true, Margin = new Padding(0, 1, 4, 0), Cursor = Cursors.Hand };
    public Button BtnRiverClearAll { get; } = new() { Text = "🗑 Tout effacer", Height = 32, AutoSize = true, BackColor = Color.FromArgb(198, 40, 40), ForeColor = Color.White, Margin = new Padding(0, 1, 8, 0), Cursor = Cursors.Hand };

    private static readonly string[] SuggestedRiverNames =
    [
        "La Sereine", "L'Allier Sauvage", "Rivière d'Argent", "Le Torrent Noir",
        "Ruisseau Clair", "L'Onde Vive", "La Haute-Durance", "La Loue Souterraine", "Le Grand Bras"
    ];
    private int _nextRiverNameIndex = 0;

    public string GetNextSuggestedRiverName()
    {
        var name = SuggestedRiverNames[_nextRiverNameIndex % SuggestedRiverNames.Length];
        _nextRiverNameIndex++;
        return name;
    }

    // Biomes (Milieux naturels)
    public Label LblBiomeType { get; } = new() { Text = "Milieu :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public ComboBox ComboBiomeType { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 165, Margin = new Padding(0, 3, 8, 0) };
    public Label LblBiomeRadius { get; } = new() { Text = "Rayon :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TrackBar SliderBiomeRadius { get; } = new() { Minimum = 15, Maximum = 300, Value = 50, TickFrequency = 25, Width = 110, Margin = new Padding(0, 2, 2, 0) };
    public Label LblBiomeRadiusVal { get; } = new() { Text = "50 lieues", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
    public Label LblBiomeName { get; } = new() { Text = "Nom :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TextBox TxtBiomeName { get; } = new() { Text = "Bois des Chênes", Width = 140, Margin = new Padding(0, 3, 8, 0) };
    public Button BtnBiomeAutoGenerate { get; } = new() { Text = "🪄 Peupler biomes", Height = 32, AutoSize = true, BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 1, 4, 0), Cursor = Cursors.Hand };
    public Button BtnBiomeClearAll { get; } = new() { Text = "🗑 Effacer tous les biomes", Height = 32, AutoSize = true, BackColor = Color.FromArgb(198, 40, 40), ForeColor = Color.White, Margin = new Padding(0, 1, 8, 0), Cursor = Cursors.Hand };
    public Label LblBiomeRuleHint { get; } = new() { Text = "📌 Terre émergée sous les glaciers (< 2 500 m) | Clic GAUCHE = Peindre | Clic DROIT = Gommer", AutoSize = true, ForeColor = Color.FromArgb(30, 90, 40), Font = new Font("Segoe UI", 8.8f, FontStyle.Italic), Margin = new Padding(4, 7, 8, 0) };

    // Gisements miniers
    public static readonly (string Key, string Display, string DefaultName)[] ResourceTypeChoices =
    [
        ("pierre", "🪨 Pierre", "Carrière de pierre"),
        ("charbon", "⛏️ Charbon", "Bassin houiller"),
        ("fer", "⛓️ Fer", "Gisement de fer")
    ];

    public Label LblResType { get; } = new() { Text = "Ressource :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public ComboBox ComboResourceType { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 135, Margin = new Padding(0, 3, 8, 0) };
    public Label LblResRadius { get; } = new() { Text = "Rayon :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TrackBar SliderResourceRadius { get; } = new() { Minimum = 10, Maximum = 200, Value = 40, TickFrequency = 20, Width = 100, Margin = new Padding(0, 2, 2, 0) };
    public Label LblResourceRadiusVal { get; } = new() { Text = "40 lieues", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
    public Label LblResName { get; } = new() { Text = "Nom :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TextBox TxtResourceRegion { get; } = new() { Text = "Carrière de pierre", Width = 130, Margin = new Padding(0, 3, 8, 0) };
    public Button BtnResourceClearAll { get; } = new() { Text = "🗑 Effacer ressources", Height = 32, AutoSize = true, BackColor = Color.FromArgb(198, 40, 40), ForeColor = Color.White, Margin = new Padding(0, 1, 8, 0), Cursor = Cursors.Hand };

    // Communes & Localités
    private static readonly string[] HistoricalVillageNames =
    [
        "Val-d'Or", "Saint-Martin", "Castel-Giron", "Rochebrune", "Val-d'Argent",
        "Miremont", "Pont-l'Évêque", "Clairvaux", "Montdragon", "Sainte-Croix",
        "Beaulieu", "Hauteville", "Grandchamp", "Bellombre", "Châteauneuf",
        "Port-Brumant", "Fontevraud", "Marvejols", "Saint-Germain", "Roquefort",
        "Villefranche", "Montaigu", "Saint-Clément", "Bellevaux", "Fontvieille",
        "Champfleury", "Châtillon", "Villeneuve", "Bois-le-Roi", "Puy-Saint-Vincent",
        "Aubeterre", "Vaucluse", "Saint-Loup", "Bourg-la-Reine", "Montclar",
        "Beauregard", "Châteauroux", "Saint-Paul", "Montferrand", "La Cluse",
        "Fontenay", "Verneuil", "Saint-Médard", "Montbazon", "Castelnau",
        "Châteaumeillant", "Saint-Amand", "Salignac", "Pontarlier", "Saint-Véran",
        "Montségur", "Villeperdue", "Argenton", "Mondoubleau", "Saint-Hilaire"
    ];
    private int _nextCommuneNameIndex = 0;

    public string GetNextSuggestedCommuneName()
    {
        var name = HistoricalVillageNames[_nextCommuneNameIndex % HistoricalVillageNames.Length];
        _nextCommuneNameIndex++;
        return name;
    }

    public Label LblCommuneCount { get; } = new() { Text = "🏛️ 1 commune", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
    public Label LblCommuneName { get; } = new() { Text = "Nom :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TextBox TxtCommuneName { get; } = new() { Text = "Val-d'Or", Width = 130, Margin = new Padding(0, 3, 2, 0) };
    public Button BtnCommuneDice { get; } = new() { Text = "🎲", Width = 32, Height = 28, Margin = new Padding(0, 3, 8, 0), Cursor = Cursors.Hand };
    public Button BtnCommuneDeleteLast { get; } = new() { Text = "⌫ Retirer", Height = 32, AutoSize = true, Margin = new Padding(0, 1, 8, 0), Cursor = Cursors.Hand };

    // Nommer un lieu (Toponymie)
    public Label LblPlaceCat { get; } = new() { Text = "Type :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public ComboBox ComboPlaceCategory { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Margin = new Padding(0, 3, 8, 0) };
    public Label LblPlaceName { get; } = new() { Text = "Nom :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) };
    public TextBox TxtPlaceName { get; } = new() { Text = "Mont Aiguille", Width = 130, Margin = new Padding(0, 3, 8, 0) };
    public CheckBox ChkPlaceShowAlt { get; } = new() { Text = "Afficher alt.", Checked = true, AutoSize = true, Margin = new Padding(0, 6, 8, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    public Button BtnPlaceDeleteLast { get; } = new() { Text = "⌫ Retirer", Height = 32, AutoSize = true, Margin = new Padding(0, 1, 8, 0), Cursor = Cursors.Hand };

    public EditorContextBarManager()
    {
        Panel.Controls.Add(FlowContent);
        InitDimensions();
    }

    private void InitDimensions()
    {
        ComboDimensions.Items.Clear();
        ComboDimensions.Items.AddRange([
            "Petit Pays (2 400 × 1 500)",
            "Grand Royaume (3 600 × 2 250)",
            "Région / Duché (1 200 × 800)",
            "Standard (1 000 × 640)"
        ]);
        ComboDimensions.SelectedIndex = 0;
    }

    public static Control CreateSep() =>
        new Label { Text = "|", ForeColor = Color.FromArgb(190, 185, 175), AutoSize = true, Margin = new Padding(4, 6, 4, 0), Font = new Font("Segoe UI", 11f) };

    public static Control CreateHint(string text, Color? color = null) =>
        new Label { Text = text, Font = new Font("Segoe UI", 9f, FontStyle.Italic), ForeColor = color ?? Color.FromArgb(100, 90, 80), AutoSize = true, Margin = new Padding(4, 7, 0, 0) };

    public void Populate(EditorTool tool, int riverCount)
    {
        FlowContent.SuspendLayout();
        FlowContent.Controls.Clear();
        Panel.Visible = true;

        switch (tool)
        {
            case EditorTool.ElevateBrush:
                FlowContent.Controls.Add(LblBrushRadius);
                FlowContent.Controls.Add(SliderBrushRadius);
                FlowContent.Controls.Add(LblBrushRadiusVal);
                FlowContent.Controls.Add(LblBrushPower);
                FlowContent.Controls.Add(SliderBrushPower);
                FlowContent.Controls.Add(LblBrushPowerVal);
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(BtnQuickIsland);
                FlowContent.Controls.Add(BtnQuickBlank);
                FlowContent.Controls.Add(BtnQuickNoise);
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(CreateHint("👉 Clic GAUCHE = Surélever (+) | Clic DROIT = Creuser (−, mer sous 0 m)", Color.FromArgb(40, 80, 50)));
                break;

            case EditorTool.SmoothBrush:
                FlowContent.Controls.Add(LblSmoothRadius);
                FlowContent.Controls.Add(SliderSmoothRadius);
                FlowContent.Controls.Add(LblSmoothRadiusVal);
                FlowContent.Controls.Add(LblSmoothPower);
                FlowContent.Controls.Add(SliderSmoothPower);
                FlowContent.Controls.Add(LblSmoothPowerVal);
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(CreateHint("👉 Clic GAUCHE = Adoucir la pente (lissage gaussien) | Clic DROIT = Aplanir en plateau", Color.FromArgb(20, 100, 140)));
                break;

            case EditorTool.DrawRiver:
                FlowContent.Controls.Add(LblRiverStatus);
                FlowContent.Controls.Add(LblRiverName);
                FlowContent.Controls.Add(TxtRiverName);
                FlowContent.Controls.Add(LblRiverDepth);
                FlowContent.Controls.Add(NumRiverDepth);
                FlowContent.Controls.Add(ChkDigThalweg);
                FlowContent.Controls.Add(LblRiverWidth);
                FlowContent.Controls.Add(SliderRiverWidth);
                FlowContent.Controls.Add(LblRiverWidthVal);
                FlowContent.Controls.Add(BtnRiverDeleteLast);
                FlowContent.Controls.Add(BtnRiverClearAll);
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(CreateHint("👉 Clic GAUCHE = Poser source & cours spontané | Clic DROIT = Supprimer", Color.FromArgb(30, 70, 110)));
                UpdateRiverButtons(riverCount);
                break;

            case EditorTool.DrawBiome:
                InitBiomeTypes();
                FlowContent.Controls.Add(LblBiomeType);
                FlowContent.Controls.Add(ComboBiomeType);
                FlowContent.Controls.Add(LblBiomeRadius);
                FlowContent.Controls.Add(SliderBiomeRadius);
                FlowContent.Controls.Add(LblBiomeRadiusVal);
                FlowContent.Controls.Add(LblBiomeName);
                FlowContent.Controls.Add(TxtBiomeName);
                FlowContent.Controls.Add(BtnBiomeAutoGenerate);
                FlowContent.Controls.Add(BtnBiomeClearAll);
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(LblBiomeRuleHint);
                UpdateBiomeRuleHint();
                break;

            case EditorTool.PlaceResource:
                InitResourceTypes();
                FlowContent.Controls.Add(LblResType);
                FlowContent.Controls.Add(ComboResourceType);
                FlowContent.Controls.Add(LblResRadius);
                FlowContent.Controls.Add(SliderResourceRadius);
                FlowContent.Controls.Add(LblResourceRadiusVal);
                FlowContent.Controls.Add(LblResName);
                FlowContent.Controls.Add(TxtResourceRegion);
                FlowContent.Controls.Add(BtnResourceClearAll);
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(CreateHint("👉 Clic GAUCHE = Poser veine | Clic DROIT = Gommer", Color.FromArgb(120, 60, 20)));
                break;

            case EditorTool.MoveCommune:
                FlowContent.Controls.Add(LblCommuneCount);
                FlowContent.Controls.Add(LblCommuneName);
                FlowContent.Controls.Add(TxtCommuneName);
                FlowContent.Controls.Add(BtnCommuneDice);
                FlowContent.Controls.Add(BtnCommuneDeleteLast);
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(CreateHint("👉 Glisser-déposer = Déplacer commune | Clic GAUCHE = Poser | Clic DROIT = Supprimer", Color.FromArgb(130, 30, 20)));
                break;

            case EditorTool.NamePlace:
                InitPlaceCategories();
                FlowContent.Controls.Add(LblPlaceCat);
                FlowContent.Controls.Add(ComboPlaceCategory);
                FlowContent.Controls.Add(LblPlaceName);
                FlowContent.Controls.Add(TxtPlaceName);
                FlowContent.Controls.Add(ChkPlaceShowAlt);
                FlowContent.Controls.Add(BtnPlaceDeleteLast);
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(CreateHint("👉 Clic GAUCHE = Poser lieu nommé | Clic DROIT = Supprimer", Color.FromArgb(110, 50, 20)));
                break;

            default: // Navigate
                FlowContent.Controls.Add(CreateHint("🔍 Molette = Zoom | Clic Droit ou Molette maintenu = Déplacer vue | Survol = Altitude", Color.FromArgb(60, 60, 60)));
                FlowContent.Controls.Add(CreateSep());
                FlowContent.Controls.Add(new Label { Text = "Dimensions :", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
                FlowContent.Controls.Add(ComboDimensions);
                FlowContent.Controls.Add(BtnQuickIsland);
                FlowContent.Controls.Add(BtnQuickBlank);
                FlowContent.Controls.Add(BtnZoomReset);
                break;
        }

        FlowContent.ResumeLayout();
    }

    public void UpdateRiverButtons(int riverCount)
    {
        LblRiverStatus.Text = riverCount == 0
            ? "💧 0 fleuve"
            : $"💧 {riverCount} fleuve{(riverCount > 1 ? "s" : "")}";
        BtnRiverDeleteLast.Enabled = riverCount > 0;
        BtnRiverClearAll.Enabled = riverCount > 0;
    }

    public void UpdateCommuneCount(int communeCount)
    {
        LblCommuneCount.Text = $"🏛️ {communeCount} commune{(communeCount > 1 ? "s" : "")}";
        BtnCommuneDeleteLast.Enabled = communeCount > 1;
    }

    public void InitBiomeTypes()
    {
        if (ComboBiomeType.Items.Count > 0) return;

        foreach (var b in ZoneMaskGrid.BiomeTypes)
        {
            ComboBiomeType.Items.Add(b.Label);
        }
        ComboBiomeType.SelectedIndex = 0;
    }

    public void UpdateBiomeRuleHint()
    {
        var idx = Math.Clamp(ComboBiomeType.SelectedIndex, 0, ZoneMaskGrid.BiomeTypes.Length - 1);
        var biome = ZoneMaskGrid.BiomeTypes[idx];
        LblBiomeRuleHint.Text = $"📌 {biome.Rule} | Clic GAUCHE = Peindre | Clic DROIT = Gommer";
        LblBiomeRuleHint.ForeColor = biome.Key switch
        {
            "foret" => Color.FromArgb(30, 90, 40),
            "fertile" => Color.FromArgb(140, 100, 20),
            "marais" => Color.FromArgb(20, 100, 80),
            "plage" => Color.FromArgb(140, 110, 30),
            "falaise" => Color.FromArgb(70, 75, 85),
            "desert" => Color.FromArgb(150, 80, 20),
            "glacier" => Color.FromArgb(40, 100, 150),
            _ => Color.FromArgb(30, 90, 40)
        };
    }

    public void InitPlaceCategories()
    {
        if (ComboPlaceCategory.Items.Count > 0) return;

        foreach (var cat in PlaceCategories.All)
        {
            ComboPlaceCategory.Items.Add($"{cat.Icon} {cat.DisplayName}");
        }
        ComboPlaceCategory.SelectedIndex = 0;
    }

    public void InitResourceTypes()
    {
        if (ComboResourceType.Items.Count > 0) return;

        foreach (var choice in ResourceTypeChoices)
        {
            ComboResourceType.Items.Add(choice.Display);
        }
        ComboResourceType.SelectedIndex = 0;
    }
}
