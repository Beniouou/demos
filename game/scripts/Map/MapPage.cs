using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// La page du menu Carte : la vue de base (Monde), une case à cocher par filtre d'information (on peut les superposer),
/// une légende qui suit les filtres cochés, et quelques chiffres sur ce que montrent ces filtres. Les filtres restent
/// cochés sur la carte quand le menu se ferme.
/// </summary>
public partial class MapPage : ScrollContainer, IMenuPage
{
    private readonly MapCanvas _map;
    private readonly VBoxContainer _legend;
    private readonly Label _stats;

    // Le nom des biomes et des gisements pour le joueur.
    private static readonly Dictionary<string, string> KindNames = new()
    {
        ["foret"] = "Forêt",
        ["fertile"] = "Terres fertiles",
        ["marais"] = "Marais",
        ["plage"] = "Plage",
        ["falaise"] = "Falaise",
        ["desert"] = "Désert",
        ["glacier"] = "Glacier",
        ["pierre"] = "Pierre (carrières)",
        ["charbon"] = "Charbon (bassins houillers)",
        ["fer"] = "Fer (minerai)",
    };

    public MapPage(MapCanvas map)
    {
        _map = map;
        HorizontalScrollMode = ScrollMode.Disabled;

        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 10);
        AddChild(content);

        content.AddChild(UiTheme.Title("Vue de base : Monde"));
        content.AddChild(new Label
        {
            Text = "Le terrain, l'eau, les forêts, les bâtiments et le nom des communes. Cochez des filtres pour y superposer des informations.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var recenterButton = new Button
        {
            Text = "Recadrer la carte sur la capitale",
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(220, 32),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        recenterButton.Pressed += () => _map.CenterOnCapital();
        content.AddChild(recenterButton);

        content.AddChild(new HSeparator());
        content.AddChild(UiTheme.Caption("Filtres d'information :"));
        foreach (var (filter, name, description) in MapFilterCatalog.All)
        {
            var box = new CheckBox
            {
                Text = name,
                TooltipText = description,
                ButtonPressed = _map.HasFilter(filter),
                FocusMode = FocusModeEnum.None,
            };
            box.Toggled += on =>
            {
                _map.SetFilter(filter, on);
                BuildLegend();
                UpdateStats();
            };
            content.AddChild(box);
        }

        content.AddChild(new HSeparator());
        content.AddChild(UiTheme.Caption("Légende :"));
        _legend = new VBoxContainer();
        _legend.AddThemeConstantOverride("separation", 6);
        content.AddChild(_legend);

        content.AddChild(new HSeparator());
        _stats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(_stats);

        BuildLegend();
        UpdateStats();
    }

    public void Refresh() => UpdateStats();

    private void UpdateStats()
    {
        var vMap = _map.VectorMap;
        var view = _map.GameView;
        var communesCount = view?.Communes().Count ?? vMap?.AllCommunes.Count ?? _map.Communes.Count;

        var lines = new List<string>
        {
            $"Monde : {(vMap != null ? $"{vMap.Width:F0} × {vMap.Height:F0}" : "1 000 × 640")} lieues, {communesCount} commune(s).",
        };
        if (_map.HasFilter(MapFilters.Physical))
            lines.Add($"Géographie physique : {vMap?.Contours.Count ?? 0} courbes de niveau, {vMap?.SpotElevations.Count ?? 0} points cotés, {vMap?.AllRivers.Count ?? 0} cours d'eau.");
        if (_map.HasFilter(MapFilters.Biomes))
            lines.Add($"Biomes : {vMap?.AllBiomes.Count ?? 0} zones naturelles.");
        if (_map.HasFilter(MapFilters.Deposits))
            lines.Add($"Ressources minières et sols : {vMap?.AllResourceZones.Count ?? 0} gisements.");
        if (_map.HasFilter(MapFilters.Politics))
            lines.Add($"Politique : {view?.Prefectures().Count ?? 0} préfecture(s), {view?.Communes().Sum(c => c.Residents) ?? 0:N0} habitants.");
        if (_map.HasFilter(MapFilters.ProductionTransport))
            lines.Add($"Production et transport : {view?.BuildingPositions().Count ?? 0} bâtiment(s) posé(s) sur la carte, {view?.Roads().Count ?? 0} liaison(s) (sentiers, chemins, routes), {vMap?.AllRivers.Count ?? 0} voies navigables ; perte de transport de 5 % à 60 % selon la distance, moins un quart sur un chemin et la moitié sur une route.");
        _stats.Text = string.Join("\n", lines);
    }

    /// <summary>La légende : celle de la vue Monde, puis celle de chaque filtre coché.</summary>
    private void BuildLegend()
    {
        foreach (var child in _legend.GetChildren())
        {
            _legend.RemoveChild(child);
            child.QueueFree();
        }

        var vMap = _map.VectorMap;
        Section("Monde");
        Item(MapPalette.Water, "Mer et lacs");
        Item(MapPalette.River, "Fleuves et rivières");
        Item(MapPalette.BiomeSoft("foret").Fill, "Forêts et leurs bosquets");
        Item(MapPalette.TerrainLegend, "Terrain coloré selon l'altitude, avec les ombres du relief");
        Item(MapPalette.CommuneMark, "Commune et son nom");
        Item(Layers.ProductionLayer.ZoneColor(null), "Bâtiment d'extraction, de la couleur de sa zone (nom au survol)");
        Item(MapPalette.RoadSurface, "Sentier, chemin, route : le trafic entre communes les crée");

        if (_map.HasFilter(MapFilters.Physical))
        {
            Section("Géographie physique");
            Item(MapPalette.ContourIndex, "Courbe maîtresse (tous les 100 m), avec sa cote");
            Item(MapPalette.ContourStandard, "Courbe intermédiaire");
            Item(MapPalette.SpotElevation, "Point coté (sommet, col, lieu-dit)");
            Item(MapPalette.River, "Nom des cours d'eau, de près");
        }

        if (_map.HasFilter(MapFilters.Biomes))
        {
            Section("Biomes");
            foreach (var kind in Kinds(vMap?.AllBiomes.Select(b => b.Kind)))
                Item(MapPalette.BiomeCrisp(kind).Fill, KindName(kind));
        }

        if (_map.HasFilter(MapFilters.Deposits))
        {
            Section("Ressources minières et sols");
            foreach (var kind in Kinds(vMap?.AllResourceZones.Select(z => z.Kind)))
            {
                var (_, stroke, icon) = MapPalette.Deposit(kind);
                Item(stroke, $"{icon} {KindName(kind)}");
            }
        }

        if (_map.HasFilter(MapFilters.Politics))
        {
            Section("Politique");
            Item(MapPalette.BlasonGold, "Capitale (sceau or et carmin)");
            Item(MapPalette.PrefectureSeat, "Chef-lieu de préfecture (sceau pourpre)");
            Item(MapPalette.PrefectureBorder, "Frontière d'une préfecture");
            Item(MapPalette.AdministrativeLink, "Liaison entre le chef-lieu et une commune membre");
            Item(MapPalette.InfluenceBorder, "Ressort d'une commune sans préfecture");
        }

        if (_map.HasFilter(MapFilters.ProductionTransport))
        {
            Section("Production et transport");
            Item(MapPalette.ExtractionLink, "Lien d'un bâtiment d'extraction à sa commune (noms de près)");
            Item(MapPalette.TradeRiverHighlight, "Voie navigable");
            Item(MapPalette.TradeFlow, "Échange entre communes : ce qui circule en un mois, et la perte en route");
            Item(MapPalette.RoadPath, "Un chemin retire un quart de la perte, une route la moitié");
        }
    }

    private static IEnumerable<string> Kinds(IEnumerable<string>? kinds) =>
        (kinds ?? []).Select(k => k.ToLowerInvariant()).Distinct().OrderBy(KindName);

    private static string KindName(string kind) => KindNames.TryGetValue(kind, out var name) ? name : kind;

    private void Section(string title) => _legend.AddChild(UiTheme.Caption(title));

    private void Item(Color color, string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new ColorRect
        {
            Color = color,
            CustomMinimumSize = new Vector2(16, 16),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        row.AddChild(new Label { Text = text });
        _legend.AddChild(row);
    }
}
