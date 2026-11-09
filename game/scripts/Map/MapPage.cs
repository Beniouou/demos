using System.Linq;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// Panneau d'un onglet du menu Carte : descriptif, légende cartographique, actions de cadrage et état du territoire.
/// Dès qu'il est affiché, il bascule la vue correspondante sur le canevas de carte.
/// </summary>
public partial class MapPage : VBoxContainer, IMenuPage
{
    private readonly MapCanvas _map;
    private readonly MapViewMode _mode;
    private readonly Label _stats;

    public MapPage(MapCanvas map, MapViewMode mode, string title, string description)
    {
        _map = map;
        _mode = mode;

        AddThemeConstantOverride("separation", 12);

        AddChild(UiTheme.Title(title));
        AddChild(new Label { Text = description });

        // Bouton de recentrage rapide sur la capitale / première commune
        var recenterButton = new Button
        {
            Text = "Recadrer la carte sur la commune",
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(220, 32),
        };
        recenterButton.Pressed += () => _map.CenterOnCapital();
        AddChild(recenterButton);

        AddChild(new HSeparator());
        AddChild(UiTheme.Caption("Légende cartographique :"));
        AddChild(BuildLegend(mode));

        AddChild(new HSeparator());
        _stats = new Label();
        AddChild(_stats);

        UpdateStats();
    }

    public override void _Ready()
    {
        _map.SetViewMode(_mode);
        UpdateStats();
    }

    public void Refresh() => UpdateStats();

    private void UpdateStats()
    {
        var vMap = _map.VectorMap;
        var view = _map.GameView;

        var communesCount = view?.Communes().Count ?? vMap?.AllCommunes.Count ?? _map.Communes.Count;
        var prefecturesCount = view?.Prefectures().Count ?? 0;
        var totalPop = view?.Communes().Sum(c => c.Residents) ?? (communesCount * 5000);

        _stats.Text = _mode switch
        {
            MapViewMode.Geography =>
                $"Dimensions : {(vMap != null ? $"{vMap.Width:F0} × {vMap.Height:F0}" : "1 000 × 640")} lieues (IGN continue)\n" +
                $"Relief : {(vMap?.Contours.Count ?? 0)} isohypses Marching Squares, {(vMap?.SpotElevations.Count ?? 0)} points cotés.\n" +
                $"Hydrographie : {(vMap?.AllRivers.Count ?? 1)} cours d'eau thalweg avec modélisation d'écoulement.",

            MapViewMode.Geopolitics =>
                $"Communes établies : {communesCount} · Population administrée : {totalPop:N0} citoyens\n" +
                $"Préfectures & départements : {prefecturesCount} chef(s)-lieu(x) et cantons rattachés\n" +
                $"Les enveloppes de juridiction s'étendent le long des plaines et s'arrêtent aux crêtes.",

            MapViewMode.Production =>
                $"Unités et bassins d'extraction répertoriés : {(vMap?.AllResourceZones.Count ?? 4)} gisements\n" +
                $"• Carrières de pierre, mines de charbon, gisements de fer.\n" +
                $"• Terres arables fertiles, moulins et manufactures communales.",

            MapViewMode.Transit =>
                $"Réseau logistique & transport : {(vMap?.AllRivers.Count ?? 1)} corridors fluviaux navigables\n" +
                $"Friction de transport : perte de 5 % à 60 % calibrée sur la distance réelle en lieues.\n" +
                $"Les voies marchandes inter-communes assurent les transferts de produits.",

            MapViewMode.NaturalResources =>
                $"Gisements naturels identifiés dans le cadastre : {(vMap?.AllResourceZones.Count ?? 0)} zones\n" +
                $"• Pierre : bancs de calcaire et falaises · Charbon : filons sédimentaires\n" +
                $"• Fer : veines de magnétite · Terres fertiles : plaines alluviales.",

            _ => "",
        };
    }

    private static Control BuildLegend(MapViewMode mode)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);

        switch (mode)
        {
            case MapViewMode.Geography:
                box.AddChild(LegendItem(MapPalette.Water, "Étendue d'eau / Baie maritime pastel"));
                box.AddChild(LegendItem(MapPalette.River, "Cours d'eau thalweg avec débit"));
                box.AddChild(LegendItem(MapPalette.ContourIndex, "Courbes de niveau maîtresses (100 m)"));
                box.AddChild(LegendItem(MapPalette.ContourStandard, "Courbes de niveau intermédiaires (25 m / 50 m)"));
                box.AddChild(LegendItem(MapPalette.SpotElevation, "Points cotés d'arpentage (alt. IGN en mètres)"));
                box.AddChild(LegendItem(MapPalette.Forest, "Biomes naturels (forêts, marais, glaciers, arables)"));
                break;

            case MapViewMode.Geopolitics:
                box.AddChild(LegendItem(MapPalette.BlasonGold, "Capitale nationale (blason or et carmin)"));
                box.AddChild(LegendItem(MapPalette.PrefectureSeat, "Chef-lieu de préfecture (sceau pourpre civique)"));
                box.AddChild(LegendItem(MapPalette.PrefectureBorder, "Frontière départementale & enveloppe de juridiction"));
                box.AddChild(LegendItem(MapPalette.AdministrativeLink, "Liaison administrative chef-lieu / membre"));
                box.AddChild(LegendItem(MapPalette.InfluenceBorder, "Limite d'arpentage communal"));
                break;

            case MapViewMode.Production:
                box.AddChild(LegendItem(MapPalette.RoofSlate, "Mairie & Entrepôt (toits d'ardoise)"));
                box.AddChild(LegendItem(MapPalette.RoofTile, "Ateliers, Moulins & Logements (toits de tuile)"));
                box.AddChild(LegendItem(MapPalette.ContourIndex, "Carrières de pierre & Mines de charbon"));
                box.AddChild(LegendItem(MapPalette.Forest, "Exploitations forestières d'État"));
                break;

            case MapViewMode.Transit:
                box.AddChild(LegendItem(MapPalette.TradeRiverHighlight, "Corridor fluvial navigable (friction réduite)"));
                box.AddChild(LegendItem(MapPalette.TradeRoad, "Voie marchande & axe logistique inter-communes"));
                box.AddChild(LegendItem(MapPalette.Street, "Réseau viaire et ruelles urbaines"));
                break;

            case MapViewMode.NaturalResources:
                box.AddChild(LegendItem(new Color(0.88f, 0.80f, 0.45f), "🌾 Plaine alluviale & terres fertiles"));
                box.AddChild(LegendItem(MapPalette.Forest, "🌲 Massif forestier (bois d'œuvre)"));
                box.AddChild(LegendItem(new Color(0.55f, 0.62f, 0.68f), "🪨 Bancs de calcaire (pierre de taille)"));
                box.AddChild(LegendItem(new Color(0.20f, 0.20f, 0.22f), "⛏️ Filon de charbon sédimentaire"));
                box.AddChild(LegendItem(new Color(0.72f, 0.36f, 0.22f), "⛓️ Veine de fer (minerai)"));
                break;
        }

        return box;
    }

    private static Control LegendItem(Color color, string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var patch = new ColorRect
        {
            Color = color,
            CustomMinimumSize = new Vector2(16, 16),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        row.AddChild(patch);

        var label = UiTheme.Caption(text);
        row.AddChild(label);

        return row;
    }
}
