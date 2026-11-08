using Godot;

namespace Demos.Game.Map;

/// <summary>
/// Palette de couleurs cartographiques IGN en teintes pastels douces :
/// vélin clair, courbes de niveau bistre/sépia, bleu ardoise poudré et vert sauge.
/// </summary>
public static class MapPalette
{
    // Fond général papier vélin IGN doux
    public static readonly Color Parchment = new("#FAF6ED");
    public static readonly Color FrameBorder = new("#7A6852");
    public static readonly Color GridCoordinate = new(0.55f, 0.45f, 0.35f, 0.30f);

    // Eaux et hydrographie pastel
    public static readonly Color Water = new("#94B5C5");
    public static readonly Color WaterShallow = new("#ADC9D6");
    public static readonly Color River = new("#6890A0");
    public static readonly Color RiverCore = new("#90B5C4");
    public static readonly Color BathymetryLine = new(0.35f, 0.50f, 0.60f, 0.55f);

    // Relief IGN (Isohypses et points cotés)
    public static readonly Color ContourIndex = new("#75583B");       // Courbe maîtresse (tous les 100m)
    public static readonly Color ContourStandard = new(0.61f, 0.50f, 0.38f, 0.65f); // Courbe intermédiaire
    public static readonly Color SpotElevation = new("#4A3420");      // Point coté sommet
    public static readonly Color Hillshade = new(0.30f, 0.22f, 0.15f, 0.12f); // Estompage doux

    // Végétation pastel
    public static readonly Color Forest = new("#C5D6BF");             // Vert sauge doux
    public static readonly Color ForestStroke = new("#8EA487");
    public static readonly Color ForestTrees = new("#7F9976");

    // Cadastre urbain & Bâtiments
    public static readonly Color Street = new("#C2B29A");
    public static readonly Color BuildingWall = new("#EAE0D0");
    public static readonly Color BuildingStroke = new("#5A422D");
    public static readonly Color RoofTile = new("#B35F53");           // Tuiles terre cuite pastel
    public static readonly Color RoofSlate = new("#6A8090");          // Ardoises bleu-gris pastel
    public static readonly Color BellTower = new("#D4AF37");          // Clocheton doré

    // Héraldique & Souveraineté
    public static readonly Color BlasonRed = new("#9E352B");
    public static readonly Color BlasonGold = new("#D4AF37");
    public static readonly Color InfluenceBorder = new("#A84C42");
    public static readonly Color TerraIncognitaHatch = new(0.55f, 0.48f, 0.40f, 0.25f);

    // Textes et cartouches
    public static readonly Color InkText = new("#3D2916");
    public static readonly Color LabelBackground = new(0.99f, 0.98f, 0.96f, 0.95f);
    public static readonly Color LabelBorder = new("#7A5636");
}
