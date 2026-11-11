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

    /// <summary>Les biomes dans la vue Monde : teintes pastel (remplissage, contour).</summary>
    public static (Color Fill, Color Stroke) BiomeSoft(string kind) => kind.ToLowerInvariant() switch
    {
        "foret" or "forest" => (Forest, ForestStroke),
        "fertile" or "zone fertile" => (new Color("#EEE4B9"), new Color("#C8B982")),
        "marais" => (new Color("#C3DACD"), new Color("#87AFA0")),
        "plage" => (new Color("#F2E6BC"), new Color("#D7C391")),
        "falaise" => (new Color("#C8CACD"), new Color("#787D82")),
        "desert" => (new Color("#F5DEAA"), new Color("#D7AF73")),
        "glacier" => (new Color("#E1F0F8"), new Color("#8CB4D2")),
        _ => (Forest, ForestStroke),
    };

    /// <summary>Les biomes avec le filtre Biomes : une couleur nette par type (remplissage, contour).</summary>
    public static (Color Fill, Color Stroke) BiomeCrisp(string kind) => kind.ToLowerInvariant() switch
    {
        "foret" or "forest" => (new Color("#5E9E5A"), new Color("#2F6431")),
        "fertile" or "zone fertile" => (new Color("#E6C64F"), new Color("#9E8420")),
        "marais" => (new Color("#6FB09C"), new Color("#3A7A66")),
        "plage" => (new Color("#F3DE8A"), new Color("#C2A64A")),
        "falaise" => (new Color("#9AA0A8"), new Color("#565C64")),
        "desert" => (new Color("#E9A95B"), new Color("#A86A24")),
        "glacier" => (new Color("#CFEFFF"), new Color("#5C9CC8")),
        _ => (new Color("#5E9E5A"), new Color("#2F6431")),
    };

    // Légende du terrain coloré (vue Monde) : la teinte des prairies du shader terrain.gdshader
    public static readonly Color TerrainLegend = new(0.65f, 0.76f, 0.43f);

    // Vue Monde : les biomes ne sont qu'une teinte posée sur le terrain (opacité), sans contour
    public const float BiomeTint = 0.40f;

    // Les arbres (images arbre-feuillu.png, arbre-conifere.png) : un tous les tant de lieues dans les forêts, à peu
    // près de telle taille (lieues), en conifères au-dessus de telle altitude (m), avec leur ombre (décalage en lieues
    // vers le sud-est, opacité). Teinte des arbres sous les filtres Politique et Production et transport.
    public const float TreeSpacing = 9f;
    public const float TreeSize = 12f;
    public const float ConiferAltitude = 900f;
    public static readonly Vector2 TreeShadowOffset = new(2.2f, 2.6f);
    public const float TreeShadowStrength = 0.35f;
    public static readonly Color TreesFaded = new(1f, 1f, 1f, 0.55f);

    /// <summary>Les gisements (filtre Ressources minières et sols) : remplissage, contour et pictogramme.</summary>
    public static (Color Fill, Color Stroke, string Icon) Deposit(string kind) => kind.ToLowerInvariant() switch
    {
        "pierre" => (new Color(0.55f, 0.62f, 0.68f, 0.525f), new Color(0.40f, 0.48f, 0.55f, 1.0f), "🪨"),
        "charbon" => (new Color(0.20f, 0.20f, 0.22f, 0.55f), new Color(0.12f, 0.12f, 0.14f, 1.0f), "⛏️"),
        "fer" => (new Color(0.72f, 0.36f, 0.22f, 0.525f), new Color(0.55f, 0.25f, 0.15f, 1.0f), "⛓️"),
        "fertile" or "zone fertile" or "ble" or "lin" => (new Color(0.88f, 0.80f, 0.45f, 0.525f), new Color(0.70f, 0.60f, 0.28f, 1.0f), "🌾"),
        _ => (new Color(0.6f, 0.6f, 0.6f, 0.525f), new Color(0.4f, 0.4f, 0.4f, 1.0f), "💎"),
    };

    // Les toits des communes (images toit-1.png à toit-3.png) : une maison pour tant d'habitants, au plus tant ; taille
    // d'un toit (lieues), écart entre maisons (le rayon du bourg est cet écart × la racine du nombre de maisons), ombre.
    public const float ResidentsPerHouse = 60f;
    public const int MaxHouses = 400;
    public const float RoofSize = 6f;
    public const float HouseSpacing = 4.5f;
    public static readonly Vector2 RoofShadowOffset = new(1.2f, 1.5f);
    public const float RoofShadowStrength = 0.45f;

    // Les fumées : une cheminée qui fume pour tant de maisons, tant de bouffées par cheminée, leur taille (lieues).
    public const int HousesPerChimney = 10;
    public const int PuffsPerChimney = 4;
    public const float PuffSize = 4f;

    // Communes dans la vue Monde (disque bicolore sobre)
    public static readonly Color CommuneMark = new("#8B1E1E");
    public static readonly Color CommuneMarkInner = new("#FFF9EE");

    // Lien pointillé d'un bâtiment d'extraction à sa commune
    public static readonly Color ExtractionLink = new(0.25f, 0.22f, 0.18f, 0.45f);

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

    // Préfectures, Juridictions & Frontières départementales
    public static readonly Color PrefectureSeat = new("#6D2C4D");             // Pourpre civique noble
    public static readonly Color PrefectureBorder = new("#803657");           // Liseré frontière départementale
    public static readonly Color PrefectureWash = new(0.65f, 0.38f, 0.52f, 0.14f); // Lavis aquarelle pastel doux
    public static readonly Color AdministrativeLink = new(0.48f, 0.22f, 0.38f, 0.65f); // Pointillés liaison chef-lieu
    public static readonly Color CapitalGold = new("#E6B422");                // Or républicain capitale

    public static readonly Color[] PrefectureDepartmentWashes =
    [
        new Color(0.65f, 0.38f, 0.52f, 0.14f), // Pourpre / Lilas
        new Color(0.75f, 0.58f, 0.28f, 0.14f), // Ambre / Miel
        new Color(0.38f, 0.62f, 0.48f, 0.14f), // Sauge / Émeraude douce
        new Color(0.35f, 0.52f, 0.68f, 0.14f), // Bleu ardoise / Céleste
        new Color(0.72f, 0.42f, 0.35f, 0.14f), // Ocre / Terre cuite
        new Color(0.55f, 0.45f, 0.65f, 0.14f), // Lavande
    ];

    public static readonly Color[] PrefectureDepartmentBorders =
    [
        new Color(0.55f, 0.25f, 0.42f, 0.85f),
        new Color(0.65f, 0.45f, 0.18f, 0.85f),
        new Color(0.28f, 0.50f, 0.36f, 0.85f),
        new Color(0.25f, 0.42f, 0.58f, 0.85f),
        new Color(0.62f, 0.32f, 0.25f, 0.85f),
        new Color(0.45f, 0.35f, 0.55f, 0.85f),
    ];

    // Transit & Voies de communication
    // Les liaisons que le trafic crée : sentier (tirets), chemin, route (chaussée claire bordée de sombre)
    public static readonly Color RoadTrail = new(0.55f, 0.43f, 0.28f, 0.85f);
    public static readonly Color RoadPath = new("#9C7A4E");
    public static readonly Color RoadSurface = new("#E9DCB8");
    public static readonly Color RoadEdge = new("#6E5A40");
    // Un échange entre communes (filtre Production et transport)
    public static readonly Color TradeFlow = new(0.80f, 0.42f, 0.16f, 0.80f);
    public static readonly Color TradeRoad = new("#9C683B");                 // Voie marchande bistre
    public static readonly Color TradeRoadDashed = new(0.60f, 0.38f, 0.20f, 0.75f);
    public static readonly Color TradeRiverHighlight = new(0.25f, 0.55f, 0.75f, 0.35f); // Corridor fluvial navigable

    // Textes et cartouches
    public static readonly Color InkText = new("#3D2916");
    public static readonly Color LabelBackground = new(0.99f, 0.98f, 0.96f, 0.95f);
    public static readonly Color LabelBorder = new("#7A5636");
}
