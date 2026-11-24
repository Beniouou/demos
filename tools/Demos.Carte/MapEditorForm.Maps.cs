using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Demos.Domain.Map;
using Demos.Infrastructure;

namespace Demos.Carte;

/// <summary>
/// Plusieurs cartes (décidé le 24 novembre 2026) : chaque carte a son dossier dans <c>game/data/cartes/&lt;nom&gt;/</c>,
/// avec sa <c>carte.json</c> et ses images de relief, refaites à chaque enregistrement. « Enregistrer » écrit dans la
/// carte ouverte, « Enregistrer sous » crée une nouvelle carte, « Par défaut » en fait la carte du jeu quand on ne
/// choisit rien (<c>cartes/carte-par-defaut.txt</c>).
/// </summary>
public sealed partial class MapEditorForm
{
    private const string TitleBase = "Projet Démos — Éditeur de Carte & Arpentage Vectoriel";

    /// <summary>Le dossier de la carte ouverte dans <c>cartes/</c> ; null pour une carte neuve, pas encore enregistrée.</summary>
    private string? _currentMapName;

    private string MapsDir => Path.Combine(_dataDir, MapFiles.Folder);

    private string MapFolder(string name) => Path.Combine(MapsDir, name);

    private string DefaultMapName() =>
        MapFiles.DefaultName(file => File.ReadAllText(Path.Combine(_dataDir, file)));

    /// <summary>Les cartes enregistrées : leur dossier et le nom de la carte écrit dans sa carte.json.</summary>
    private List<(string Folder, string Name)> SavedMaps()
    {
        if (!Directory.Exists(MapsDir))
            return [];
        return Directory.GetDirectories(MapsDir)
            .Where(d => File.Exists(Path.Combine(d, MapFiles.MapFile)))
            .Select(d => (Folder: Path.GetFileName(d), Name: ReadMapName(Path.Combine(d, MapFiles.MapFile))))
            .OrderBy(m => m.Folder, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Le nom écrit en tête d'une carte.json, sans lire tout le fichier.</summary>
    private static string ReadMapName(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[4096];
            var read = stream.Read(buffer, 0, buffer.Length);
            return MapFiles.NameFromStart(buffer.AsSpan(0, read));
        }
        catch (IOException)
        {
        }
        return "";
    }

    /// <summary>Une carte neuve (vierge, île, générée) : pas encore enregistrée.</summary>
    private void StartNewMap()
    {
        _currentMapName = null;
        UpdateMapTitle();
    }

    private void UpdateMapTitle()
    {
        var isDefault = _currentMapName is not null && _currentMapName == DefaultMapName();
        Text = _currentMapName is null
            ? $"{TitleBase} — carte neuve (pas encore enregistrée)"
            : $"{TitleBase} — {_currentMapName}{(isDefault ? " ★ carte par défaut" : "")}";
    }

    /// <summary>Bouton « Ouvrir » : choisir une carte enregistrée.</summary>
    public void LoadExistingMap()
    {
        var maps = SavedMaps();
        if (maps.Count == 0)
        {
            MessageBox.Show($"Aucune carte enregistrée dans {MapsDir}.", "Ouvrir une carte", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var defaultName = DefaultMapName();
        var labels = maps.Select(m => $"{m.Folder}{(string.IsNullOrEmpty(m.Name) ? "" : $" — {m.Name}")}{(m.Folder == defaultName ? "  ★ par défaut" : "")}").ToList();
        var index = maps.FindIndex(m => m.Folder == (_currentMapName ?? defaultName));
        if (MapDialogs.Choose(this, "Ouvrir une carte", "Cartes enregistrées dans game/data/cartes :", labels, Math.Max(0, index)) is { } chosen)
            LoadMapFrom(maps[chosen].Folder);
    }

    /// <summary>Ouvre la carte de ce dossier (dans <c>cartes/</c>).</summary>
    public void LoadMapFrom(string name)
    {
        var folder = MapFolder(name);
        var mapPath = Path.Combine(folder, MapFiles.MapFile);
        if (!File.Exists(mapPath))
        {
            MessageBox.Show($"Aucune carte dans {folder}.", "Carte introuvable", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            PushUndo();
            var json = File.ReadAllText(mapPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var loaded = JsonSerializer.Deserialize<VectorMapDefinition>(json, options);
            if (loaded is null) throw new InvalidOperationException("Format JSON invalide.");

            var (w, h) = (loaded.Width, loaded.Height);
            var (cols, rows) = MapSizes.ReliefGrid(w, h);
            // Le relief de la carte elle-même (ses images), pas un relief recalculé au hasard.
            var (relief, reliefSource) = LoadMapRelief(loaded, folder);
            _currentHeightmap = Heightmap.FromRelief(relief, cols, rows, w, h);

            var sanitizedWater = loaded.WaterBodies
                .Where(wb => !wb.IsErroneousGlobalSea(w, h))
                .ToList();

            _currentMap = loaded with { WaterBodies = sanitizedWater };
            var (zcols, zrows) = MapSizes.ZoneGrid(w, h);
            _currentZoneGrid = new ZoneMaskGrid(zcols, zrows, w, h);
            _currentZoneGrid.RasterizeFromMap(_currentMap);
            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
            _canvas.ResetView();
            UpdateCommuneLabels();
            UpdateRiverContextButtons();
            _currentMapName = name;
            UpdateMapTitle();
            RefreshStartReportsIfShown();

            _statusDimensions.Text = $"{MapScale.ToKm(w):N0} × {MapScale.ToKm(h):N0} km";
            _statusStats.Text = $"Chargée : {_currentMap.Contours.Count} courbes, {_currentMap.Forests.Count} forêts";
            Log($"📂 Carte « {name} » chargée ({_currentMap.Contours.Count} courbes, {_currentMap.Forests.Count} forêts, {_currentMap.SpotElevations.Count} sommets).");
            Log($"⛰️ Relief : {reliefSource}, altitude de {_currentHeightmap.MinElevation:F0} à {_currentHeightmap.MaxElevation:F0} m.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur lors de la lecture de la carte « {name} » :\n{ex.Message}", "Erreur de chargement", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Le relief d'une carte chargée : ses images (relief.json, relief-altitude.png) si elles sont plus récentes que sa
    /// carte.json, sinon recalculé depuis les courbes de niveau (quelques secondes), comme le fait <c>--relief</c>.
    /// </summary>
    private static (ReliefGrid Relief, string Source) LoadMapRelief(VectorMapDefinition map, string folder)
    {
        var mapPath = Path.Combine(folder, MapFiles.MapFile);
        var reliefJson = Path.Combine(folder, ReliefExport.InfoFile);
        var reliefPng = Path.Combine(folder, ReliefExport.AltitudeFile);
        if (File.Exists(reliefJson) && File.Exists(reliefPng)
            && File.GetLastWriteTimeUtc(reliefPng) >= File.GetLastWriteTimeUtc(mapPath))
        {
            var content = new JsonContentSource(
                file => File.ReadAllText(Path.Combine(folder, file)), file => File.ReadAllBytes(Path.Combine(folder, file)));
            if (content.LoadRelief() is { } saved)
                return (saved, "images de la carte (relief-altitude.png)");
        }

        var sw = Stopwatch.StartNew();
        var built = ReliefBuilder.Build(map, ReliefSettings.For(map.Width, map.Height));
        return (built, $"recalculé depuis les courbes en {sw.ElapsedMilliseconds} ms (refait à l'enregistrement)");
    }

    /// <summary>Bouton « Enregistrer » : dans la carte ouverte ; une carte neuve demande d'abord un nom.</summary>
    private void SaveMap()
    {
        if (_currentMapName is null)
        {
            SaveMapAs();
            return;
        }
        WriteMap(_currentMapName);
    }

    /// <summary>Bouton « Enregistrer sous » : une nouvelle carte, dans un nouveau dossier tiré de son nom.</summary>
    private void SaveMapAs()
    {
        var suggested = _currentMapName is null ? _currentMap.Name : $"{_currentMap.Name} (copie)";
        if (MapDialogs.AskText(this, "Enregistrer sous", "Nom de la nouvelle carte :", suggested) is not { } displayName)
            return;

        var folderName = MapFiles.FolderNameOf(displayName);
        if (Directory.Exists(MapFolder(folderName))
            && MessageBox.Show($"La carte « {folderName} » existe déjà. La remplacer ?", "Enregistrer sous",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _currentMap = _currentMap with { Name = displayName };
        WriteMap(folderName);
    }

    /// <summary>Écrit la carte dans son dossier, puis refait ses images de relief.</summary>
    private void WriteMap(string name)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
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

            var folder = MapFolder(name);
            Directory.CreateDirectory(folder);
            var mapPath = Path.Combine(folder, MapFiles.MapFile);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            File.WriteAllText(mapPath, JsonSerializer.Serialize(_currentMap, options));
            var sizeKb = Math.Round(new FileInfo(mapPath).Length / 1024.0, 1);

            var sw = Stopwatch.StartNew();
            ReliefExport.Run(folder, previewPath: null);
            var reliefMs = sw.ElapsedMilliseconds;

            _currentMapName = name;
            UpdateMapTitle();
            Log($"★ Carte « {name} » enregistrée ({sizeKb} Ko) ; relief refait en {reliefMs} ms.");

            var isDefault = name == DefaultMapName();
            MessageBox.Show(
                $"La carte « {_currentMap.Name} » a été enregistrée.\n\n" +
                $"Dossier : {folder}\n" +
                $"Dimensions : {MapScale.ToKm(_currentMap.Width):N0} × {MapScale.ToKm(_currentMap.Height):N0} km\n" +
                $"Courbes de niveau : {_currentMap.Contours.Count} · Cours d'eau : {_currentMap.AllRivers.Count}\n" +
                $"Biomes : {_currentMap.AllBiomes.Count} zones · Gisements : {_currentMap.AllResourceZones.Count} zones\n" +
                $"Terres d'exil : {_currentMap.AllExileSites.Count} · Communes : {_currentMap.AllCommunes.Count}\n" +
                $"Taille : {sizeKb} Ko · relief refait\n\n" +
                (isDefault
                    ? "C'est la carte par défaut : le jeu la chargera."
                    : $"Ce n'est pas la carte par défaut (« {DefaultMapName()} ») : le jeu ne la chargera pas tant qu'elle n'est pas choisie."),
                "Enregistrement réussi — Démos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"ERREUR lors de l'enregistrement : {ex.Message}");
            MessageBox.Show($"Une erreur est survenue lors de l'enregistrement de la carte :\n{ex.Message}",
                "Erreur d'enregistrement", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>Bouton « Par défaut » : la carte ouverte devient celle que le jeu charge quand on ne choisit rien.</summary>
    private void SetAsDefaultMap()
    {
        if (_currentMapName is null)
        {
            MessageBox.Show("Enregistrez d'abord la carte (« Enregistrer sous »).", "Carte par défaut", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var previous = DefaultMapName();
        if (previous == _currentMapName)
        {
            Log($"★ « {_currentMapName} » est déjà la carte par défaut.");
            return;
        }
        if (MessageBox.Show($"Faire de « {_currentMapName} » la carte par défaut du jeu, à la place de « {previous} » ?",
                "Carte par défaut", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        File.WriteAllText(Path.Combine(_dataDir, MapFiles.DefaultFile), _currentMapName + Environment.NewLine);
        UpdateMapTitle();
        Log($"★ « {_currentMapName} » est maintenant la carte par défaut (avant : « {previous} »).");
    }
}

/// <summary>Les petites fenêtres de l'éditeur pour les cartes : choisir dans une liste, taper un nom.</summary>
internal static class MapDialogs
{
    public static int? Choose(IWin32Window owner, string title, string prompt, IReadOnlyList<string> items, int selected)
    {
        using var form = NewDialog(title, 520, 360);
        var label = new Label { Text = prompt, Dock = DockStyle.Top, Height = 28, Padding = new Padding(4, 8, 4, 0) };
        var list = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f), IntegralHeight = false };
        list.Items.AddRange(items.Cast<object>().ToArray());
        if (items.Count > 0) list.SelectedIndex = Math.Clamp(selected, 0, items.Count - 1);
        list.DoubleClick += (_, _) => { if (list.SelectedIndex >= 0) form.DialogResult = DialogResult.OK; };
        form.Controls.Add(list);
        form.Controls.Add(label);
        form.Controls.Add(Buttons(form, "Ouvrir"));
        return form.ShowDialog(owner) == DialogResult.OK && list.SelectedIndex >= 0 ? list.SelectedIndex : null;
    }

    public static string? AskText(IWin32Window owner, string title, string prompt, string initial)
    {
        using var form = NewDialog(title, 460, 150);
        var label = new Label { Text = prompt, Dock = DockStyle.Top, Height = 28, Padding = new Padding(4, 8, 4, 0) };
        var box = new TextBox { Text = initial, Dock = DockStyle.Top, Font = new Font("Segoe UI", 10f) };
        form.Controls.Add(box);
        form.Controls.Add(label);
        form.Controls.Add(Buttons(form, "Enregistrer"));
        form.Shown += (_, _) => { box.SelectAll(); box.Focus(); };
        return form.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : null;
    }

    private static Form NewDialog(string title, int width, int height) => new()
    {
        Text = title,
        ClientSize = new Size(width, height),
        FormBorderStyle = FormBorderStyle.FixedDialog,
        StartPosition = FormStartPosition.CenterParent,
        MinimizeBox = false,
        MaximizeBox = false,
        Font = new Font("Segoe UI", 9.5f),
        Padding = new Padding(8),
    };

    private static Control Buttons(Form form, string okText)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 0, 0) };
        var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = okText, DialogResult = DialogResult.OK, AutoSize = true };
        panel.Controls.Add(cancel);
        panel.Controls.Add(ok);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return panel;
    }
}
