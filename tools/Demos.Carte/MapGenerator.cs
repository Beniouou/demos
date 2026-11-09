using System;
using System.Collections.Generic;
using Demos.Domain.Map;

namespace Demos.Carte;

public static class MapGenerator
{
    public const double DefaultWidth = 2400.0;
    public const double DefaultHeight = 1500.0;

    public static VectorMapDefinition Generate(TerrainParameters? parameters = null)
    {
        return Generate(parameters, out _);
    }

    /// <summary>
    /// Crée une carte vectorielle complètement vierge (toile blanche sans relief accidenté, sans fleuve, sans forêt).
    /// </summary>
    public static VectorMapDefinition CreateBlank(double worldWidth, double worldHeight, out Heightmap heightmap)
    {
        var cols = Math.Clamp((int)Math.Round(worldWidth / 6.25), 100, 360);
        var rows = Math.Clamp((int)Math.Round(worldHeight / 6.25), 64, 230);

        // Carte altimétrique plane (plaine douce à 50 m)
        heightmap = Heightmap.CreateFlat(cols, rows, worldWidth, worldHeight, baseElevation: 50.0);

        var contours = new List<ContourLine>();
        var spotElevations = new List<SpotElevation>();
        var river = new RiverData("Fleuve", [], Width: 5.0);
        var forests = new List<PolygonArea>();
        var waterBodies = new List<PolygonArea>();
        var resources = new List<ResourceDeposit>();

        var centerX = Math.Round(worldWidth * 0.5, 1);
        var centerY = Math.Round(worldHeight * 0.5, 1);
        var commune = new CommuneData(
            "commune-1",
            "Première commune",
            new Point2D(centerX, centerY),
            Population: 5000,
            InitialBuildings: ["mairie"]
        );

        return new VectorMapDefinition(
            Name: "Nouvelle carte vierge",
            Width: worldWidth,
            Height: worldHeight,
            Contours: contours,
            SpotElevations: spotElevations,
            River: river,
            Forests: forests,
            WaterBodies: waterBodies,
            Resources: resources,
            DefaultCommune: commune,
            Communes: [commune]
        );
    }

    /// <summary>
    /// Crée une carte d'île naturelle avec des côtes découpées, un relief central, et un océan enveloppant.
    /// </summary>
    public static VectorMapDefinition CreateIsland(
        double worldWidth,
        double worldHeight,
        out Heightmap heightmap,
        int seed = 42,
        IslandMorphology morphology = IslandMorphology.Natural,
        double maxAltitude = 2800.0)
    {
        var cols = Math.Clamp((int)Math.Round(worldWidth / 6.25), 100, 360);
        var rows = Math.Clamp((int)Math.Round(worldHeight / 6.25), 64, 230);

        heightmap = Heightmap.CreateIsland(cols, rows, worldWidth, worldHeight, seed, morphology, maxAltitude);

        // Isohypses adaptatives positives sur l'île
        var interval = heightmap.MaxElevation > 1500.0 ? 50.0 : 25.0;
        var levels = new List<double>();
        for (var alt = interval; alt <= heightmap.MaxElevation; alt += interval)
        {
            levels.Add(alt);
        }
        var contours = ContourExtractor.ExtractContours(heightmap, levels);

        var cx = Math.Round(worldWidth * 0.5, 1);
        var cy = Math.Round(worldHeight * 0.5, 1);

        // Extraction vectorielle exacte à 0 m par Marching Squares (côtes lisses sans aucun cube)
        var waterBodies = ContourExtractor.ExtractWaterBodies(heightmap);

        var spotElevations = new List<SpotElevation>
        {
            new(Math.Round(heightmap.MaxElevation), "Pic de l'Île", new Point2D(cx, cy - 25), "sommet", true)
        };

        var river = new RiverData("Fleuve", [], Width: 5.0);
        var forests = new List<PolygonArea>();
        var resources = new List<ResourceDeposit>
        {
            new("bois", "Bois côtier", new Point2D(cx - 80, cy + 40)),
            new("pierre", "Carrière insulaire", new Point2D(cx + 90, cy - 30))
        };

        var commune = new CommuneData(
            "commune-1",
            "Commune Insulaire",
            new Point2D(cx, cy + 20),
            Population: 5000,
            InitialBuildings: ["mairie"]
        );

        return new VectorMapDefinition(
            Name: "Île Découpée — Démos",
            Width: worldWidth,
            Height: worldHeight,
            Contours: contours,
            SpotElevations: spotElevations,
            River: river,
            Forests: forests,
            WaterBodies: waterBodies,
            Resources: resources,
            DefaultCommune: commune,
            Communes: [commune]
        );
    }

    public static VectorMapDefinition Generate(TerrainParameters? parameters, out Heightmap heightmap)
    {
        var p = parameters ?? TerrainParameters.Default;
        var worldWidth = p.WorldWidth > 0 ? p.WorldWidth : DefaultWidth;
        var worldHeight = p.WorldHeight > 0 ? p.WorldHeight : DefaultHeight;

        var sx = worldWidth / 1000.0;
        var sy = worldHeight / 640.0;
        Point2D S(double x, double y) => new(Math.Round(x * sx, 1), Math.Round(y * sy, 1));

        // 1. Grille altimétrique adaptée aux dimensions (160x102 pour 1000x640, monte proportionnellement)
        var cols = Math.Clamp((int)Math.Round(worldWidth / 6.25), 100, 360);
        var rows = Math.Clamp((int)Math.Round(worldHeight / 6.25), 64, 230);
        heightmap = new Heightmap(cols, rows, worldWidth, worldHeight, p);

        // 2. Extraction des courbes de niveau IGN (isohypses régulières selon le pas paramétré)
        var levels = new List<double>();
        var interval = Math.Max(10.0, p.ContourInterval);
        for (var alt = interval; alt <= heightmap.MaxElevation; alt += interval)
        {
            levels.Add(alt);
        }
        var contours = ContourExtractor.ExtractContours(heightmap, levels);

        // 3. Points cotés d'altitude (croix IGN au sommet des crêtes, altitude mesurée en direct sur la heightmap)
        var spotElevations = new List<SpotElevation>
        {
            new(Math.Round(heightmap.SampleElevation(820 * sx, 36 * sy)), "Pic du Nord", S(820, 36)),
            new(Math.Round(heightmap.SampleElevation(680 * sx, 58 * sy)), "Crête occidentale", S(680, 58)),
            new(Math.Round(heightmap.SampleElevation(890 * sx, 78 * sy)), "Chaînon oriental", S(890, 78)),
            new(Math.Round(heightmap.SampleElevation(820 * sx, 395 * sy)), "Plateau des Châtaigniers", S(820, 395)),
            new(Math.Round(heightmap.SampleElevation(450 * sx, 310 * sy)), "Bassin de la Sereine", S(450, 310)),
        };

        // 4. Hydrographie continue (Fleuve La Sereine navigable)
        var riverPoints = new List<Point2D>
        {
            S(760, 90), S(730, 120), S(700, 150), S(660, 180),
            S(630, 200), S(580, 240), S(520, 300), S(470, 340),
            S(430, 380), S(360, 430), S(300, 470), S(250, 495),
            S(210, 510)
        };
        var riverWidth = Math.Max(5.0, Math.Round(5.0 * Math.Sqrt(Math.Min(sx, sy)), 1));
        var river = new RiverData("La Sereine", riverPoints, Width: riverWidth);

        // 5. Massif forestier ouest (Bois des Chênes)
        var forestBoundary = new List<Point2D>
        {
            S(60, 180), S(120, 130), S(200, 160), S(250, 210),
            S(290, 260), S(270, 330), S(220, 360), S(160, 390),
            S(90, 360), S(60, 300)
        };
        var forests = new List<PolygonArea>
        {
            new("Bois des Chênes", "Forest", forestBoundary)
        };

        // 6. Étendue maritime (Baie méridionale)
        var bayBoundary = new List<Point2D>
        {
            S(0, 440), S(140, 460), S(210, 510), S(310, 640),
            S(0, 640)
        };
        var waterBodies = new List<PolygonArea>
        {
            new("Baie Méridionale", "Sea", bayBoundary)
        };

        // 7. Gisements naturels identifiés
        var resources = new List<ResourceDeposit>
        {
            new("pierre", "Massif rocheux du Nord", S(680, 140)),
            new("charbon", "Piémont minier", S(790, 200)),
            new("bois", "Bois des Chênes", S(160, 250)),
            new("ble", "Plaine alluviale", S(360, 260)),
            new("lin", "Bord du fleuve", S(410, 330)),
        };

        // 8. Commune de départ (Première commune)
        var initialBuildings = new List<string>
        {
            "mairie", "ecole", "moulin", "boulangerie", "logements-etat",
            "entrepot", "carriere", "mine", "ferme", "champ-de-lin", "camp-forestier"
        };
        var defaultCommune = new CommuneData(
            "commune-1",
            "Première commune",
            S(470, 310),
            Population: 5000,
            initialBuildings);

        return new VectorMapDefinition(
            "Carte Topographique IGN — Démos",
            worldWidth,
            worldHeight,
            contours,
            spotElevations,
            river,
            forests,
            waterBodies,
            resources,
            defaultCommune,
            Communes: [defaultCommune]);
    }
}
