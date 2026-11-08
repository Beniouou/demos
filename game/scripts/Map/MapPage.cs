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
        recenterButton.Pressed += () => _map.CenterOn(PrototypeMap.DefaultCommunePosition);
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
        if (_map.Field is null)
            return;

        var controlledCount = _map.Field.ControlledCells().Count();
        var totalCells = _map.Map.Width * _map.Map.Height;
        var percent = (double)controlledCount / totalCells * 100.0;

        _stats.Text = _mode switch
        {
            MapViewMode.Geography =>
                $"Dimensions : {_map.Map.Width} × {_map.Map.Height} lieues (quadrillage continu)\n" +
                $"Relief : Massif rocheux au nord-est, fleuve central, plaine alluviale et baie maritime.",

            MapViewMode.Geopolitics =>
                $"Souveraineté : {controlledCount} cases sous juridiction ({percent:F1} % du territoire connu).\n" +
                $"Communes établies : {_map.Communes.Count}\n" +
                $"L'influence s'arrête devant les massifs et progresse le long de l'eau.",

            MapViewMode.Production =>
                $"Unités rattachées à la Première commune :\n" +
                $"• Mine de charbon, carrière de pierre, camp de bûcherons.\n" +
                $"• Ferme céréalière, moulin, boulangerie, atelier de tissage.",

            MapViewMode.Transit =>
                $"Voie fluviale navigable principale : connecte les piémonts du nord à l'estuaire du sud-ouest.\n" +
                $"Friction réduite : gain de vitesse et d'extension de 30 % le long du fleuve.",

            MapViewMode.NaturalResources =>
                $"Gisements naturels identifiés dans le bassin de vie :\n" +
                $"• Pierre : massif nord-est · Charbon : piémont minier\n" +
                $"• Bois : grand massif forestier occidental · Blé & Lin : plaine alluviale.",

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
                box.AddChild(LegendItem(MapPalette.River, "Fleuve navigable (La Sereine)"));
                box.AddChild(LegendItem(MapPalette.ContourIndex, "Courbes de niveau maîtresses (100 m)"));
                box.AddChild(LegendItem(MapPalette.ContourStandard, "Courbes de niveau intermédiaires (25 m / 50 m)"));
                box.AddChild(LegendItem(MapPalette.Hillshade, "Estompage topographique (ombrage continu)"));
                box.AddChild(LegendItem(MapPalette.Forest, "Massif forestier (Bois des Chênes)"));
                break;

            case MapViewMode.Geopolitics:
                box.AddChild(LegendItem(MapPalette.BlasonRed, "Blason communal / Chef-lieu"));
                box.AddChild(LegendItem(MapPalette.InfluenceBorder, "Frontière d'arpentage (souveraineté)"));
                box.AddChild(LegendItem(MapPalette.TerraIncognitaHatch, "Terres sauvages inexplorées (Terra Incognita)"));
                break;

            case MapViewMode.Production:
                box.AddChild(LegendItem(MapPalette.RoofSlate, "Mairie & Entrepôt (toits d'ardoise)"));
                box.AddChild(LegendItem(MapPalette.RoofTile, "École, Moulin & Logements (toits de tuile)"));
                box.AddChild(LegendItem(MapPalette.ContourIndex, "Carrières de pierre & Mines de charbon"));
                box.AddChild(LegendItem(MapPalette.Forest, "Exploitations forestières d'État"));
                break;

            case MapViewMode.Transit:
                box.AddChild(LegendItem(MapPalette.River, "Corridor fluvial navigable (faible friction)"));
                box.AddChild(LegendItem(MapPalette.Street, "Réseau de ruelles et voies de communication"));
                box.AddChild(LegendItem(MapPalette.ContourIndex, "Passes de crêtes et reliefs"));
                break;

            case MapViewMode.NaturalResources:
                box.AddChild(LegendItem(MapPalette.RoofTile, "🌾 Blé & 🌿 Lin (plaine alluviale)"));
                box.AddChild(LegendItem(MapPalette.Forest, "🌲 Bois (massif forestier ouest)"));
                box.AddChild(LegendItem(MapPalette.ContourIndex, "⛏️ Pierre (massif rocheux nord)"));
                box.AddChild(LegendItem(MapPalette.SpotElevation, "⚒️ Charbon (piémont minier)"));
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
