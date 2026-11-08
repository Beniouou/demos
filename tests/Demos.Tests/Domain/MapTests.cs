using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Xunit;

namespace Demos.Tests.Domain;

public class MapTests
{
    [Fact]
    public void Les_coordonnees_calculent_les_distances_et_les_voisins()
    {
        var a = new MapCoordinates(0, 0);
        var b = new MapCoordinates(3, 4);

        Assert.Equal(5.0, a.EuclideanDistanceTo(b));
        Assert.Equal(7, a.ManhattanDistanceTo(b));

        var orth = a.OrthogonalNeighbors().ToList();
        Assert.Equal(4, orth.Count);
        Assert.Contains(new MapCoordinates(1, 0), orth);

        var all = a.AllNeighbors().ToList();
        Assert.Equal(8, all.Count);
    }

    [Fact]
    public void Le_relief_augmente_le_cout_de_trajet_et_l_eau_est_infranchissable()
    {
        // Grille 5x5 : plaine partout sauf une montagne en (1, 0) et de l'eau en (0, 1)
        var map = new TopographicMap(5, 5, (x, y) =>
        {
            var coord = new MapCoordinates(x, y);
            if (x == 1 && y == 0)
                return new TerrainCell(coord, TerrainType.Mountains, 0.9);
            if (x == 0 && y == 1)
                return new TerrainCell(coord, TerrainType.Water, 0.0);
            return new TerrainCell(coord, TerrainType.Plains, 0.1);
        });

        // Du point (0,0) au point (2,0), le chemin direct passe par la montagne en (1,0) ou contourne par (1,1).
        // Contourner par la plaine (1,1) coûte ~ 1.414 + 1.414 = 2.828, alors que franchir la montagne coûte 5.5.
        var costPlainsContour = map.ComputeTravelCost(new(0, 0), new(2, 0));
        Assert.True(costPlainsContour < 3.5, $"Le coût devrait privilégier le contournement, obtenu: {costPlainsContour}");

        // Vers l'eau : infranchissable
        var costToWater = map.ComputeTravelCost(new(0, 0), new(0, 1));
        Assert.True(double.IsPositiveInfinity(costToWater));
    }

    [Fact]
    public void Un_fleuve_facilite_le_deplacement()
    {
        // Deux cartes identiques : une avec fleuve, l'autre sans.
        var mapWithoutRiver = new TopographicMap(10, 3, (x, y) =>
            new TerrainCell(new(x, y), TerrainType.Plains, 0.1, HasRiver: false));

        var mapWithRiver = new TopographicMap(10, 3, (x, y) =>
            new TerrainCell(new(x, y), TerrainType.Plains, 0.1, HasRiver: y == 1));

        var costDry = mapWithoutRiver.ComputeTravelCost(new(0, 1), new(9, 1));
        var costRiver = mapWithRiver.ComputeTravelCost(new(0, 1), new(9, 1));

        Assert.True(costRiver < costDry, $"Le fleuve ({costRiver}) doit être plus rapide que la terre ferme ({costDry}).");
    }

    [Fact]
    public void L_influence_s_etend_selon_la_puissance_et_butte_sur_la_montagne()
    {
        // Grille de 11x11, commune au centre (5, 5)
        // Montagnes sur toute la ligne x = 7 (à droite)
        var map = new TopographicMap(11, 11, (x, y) =>
        {
            var coord = new MapCoordinates(x, y);
            if (x == 7)
                return new TerrainCell(coord, TerrainType.Mountains, 0.9);
            return new TerrainCell(coord, TerrainType.Plains, 0.1);
        });

        var commune = new CommuneSite("c1", "Camp de base", new(5, 5), Population: 500);
        // Puissance ~ 15 + sqrt(500)*0.4 + 5 = ~28.9
        var field = InfluenceCalculator.Compute(map, [commune]);

        // La case de départ est à influence maximale
        var center = field[5, 5];
        Assert.True(center.IsControlled);
        Assert.Equal("c1", center.DominantCommuneId);
        Assert.Equal(1.0, center.Strength, 2);

        // Vers la gauche (plaine), l'influence s'étend plus loin que vers la droite (montagne)
        // À 3 cases à gauche : (2, 5)
        var leftCell = field[2, 5];
        // À 3 cases à droite : (8, 5) derrière le mur de montagne en x=7
        var rightCell = field[8, 5];

        Assert.True(leftCell.Strength > rightCell.Strength,
            $"L'influence à gauche en plaine ({leftCell.Strength}) doit être supérieure à celle derrière la montagne ({rightCell.Strength})");
    }

    [Fact]
    public void Deux_communes_se_partagent_le_territoire_organiquement()
    {
        var map = new TopographicMap(20, 5, (x, y) =>
            new TerrainCell(new(x, y), TerrainType.Plains, 0.1));

        var c1 = new CommuneSite("c1", "Ouest", new(2, 2), Population: 100);
        var c2 = new CommuneSite("c2", "Est", new(17, 2), Population: 100);

        var field = InfluenceCalculator.Compute(map, [c1, c2]);

        Assert.Equal("c1", field[2, 2].DominantCommuneId);
        Assert.Equal("c2", field[17, 2].DominantCommuneId);

        // Au milieu (x = 9 ou 10), la frontière se partage
        Assert.Equal("c1", field[8, 2].DominantCommuneId);
        Assert.Equal("c2", field[11, 2].DominantCommuneId);
    }

    [Fact]
    public void Le_calculateur_de_distance_evalue_la_charge_administrative_d_une_prefecture()
    {
        var map = new TopographicMap(30, 10, (x, y) =>
            new TerrainCell(new(x, y), TerrainType.Plains, 0.1));

        var chefLieu = new CommuneSite("chef-lieu", "Capitale provinciale", new(5, 5));
        var communeProche = new CommuneSite("proche", "Village voisin", new(8, 5));
        var communeLoin = new CommuneSite("loin", "Avant-poste lointain", new(25, 5));

        var calc = new MapDistanceCalculator(map, [chefLieu, communeProche, communeLoin]);

        var costProche = calc.TravelCostBetween("chef-lieu", "proche");
        var costLoin = calc.TravelCostBetween("chef-lieu", "loin");

        Assert.Equal(3.0, costProche, 1);
        Assert.Equal(20.0, costLoin, 1);

        // Charge administrative de la préfecture pour ses 2 communes
        var load = calc.ComputeAdministrativeDistanceLoad("chef-lieu", ["chef-lieu", "proche", "loin"], distanceNorm: 50.0);
        // Coût total = 3 + 20 = 23 -> load = 23 / 50 = 0.46
        Assert.Equal(0.46, load, 2);
    }

    [Fact]
    public void La_carte_pilote_handcrafted_contient_tous_les_biomes_et_ressources_de_base()
    {
        var map = PrototypeMap.CreateHandcrafted();

        Assert.Equal(60, map.Width);
        Assert.Equal(40, map.Height);

        // Commune de départ
        var startCoord = PrototypeMap.DefaultCommunePosition;
        Assert.True(map.IsInBounds(startCoord));
        var startCell = map[startCoord];
        Assert.Equal(TerrainType.Plains, startCell.Terrain);

        // Vérification de la présence des différents biomes
        var hasWater = false;
        var hasMountains = false;
        var hasForest = false;
        var hasRiver = false;
        var foundResources = new HashSet<string>();

        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                var cell = map[x, y];
                if (cell.Terrain == TerrainType.Water) hasWater = true;
                if (cell.Terrain == TerrainType.Mountains) hasMountains = true;
                if (cell.Terrain == TerrainType.Forest) hasForest = true;
                if (cell.HasRiver) hasRiver = true;
                if (cell.NaturalResource is not null) foundResources.Add(cell.NaturalResource);
            }
        }

        Assert.True(hasWater, "La carte doit avoir un plan d'eau / mer");
        Assert.True(hasMountains, "La carte doit comporter des montagnes");
        Assert.True(hasForest, "La carte doit comporter une forêt");
        Assert.True(hasRiver, "La carte doit comporter un cours d'eau");
        Assert.Contains("pierre", foundResources);
        Assert.Contains("charbon", foundResources);
        Assert.Contains("bois", foundResources);
        Assert.Contains("ble", foundResources);
        Assert.Contains("lin", foundResources);
    }

    [Fact]
    public void Les_structures_vectorielles_de_la_carte_sont_valides()
    {
        var contours = new List<ContourLine>
        {
            new(100, IsIndex: true, IsClosed: true, [new(10, 10), new(20, 10), new(20, 20), new(10, 20), new(10, 10)]),
            new(50, IsIndex: false, IsClosed: false, [new(0, 30), new(50, 30), new(100, 30)]),
        };
        var spots = new List<SpotElevation> { new(542, "Pic du Nord", new(820, 36)) };
        var river = new RiverData("La Sereine", [new(10, 10), new(50, 50)]);
        var forests = new List<PolygonArea> { new("Bois", "Forest", [new(0, 0), new(10, 0), new(10, 10), new(0, 10)]) };
        var water = new List<PolygonArea> { new("Mer", "Sea", [new(0, 50), new(100, 50), new(100, 100), new(0, 100)]) };
        var res = new List<ResourceDeposit> { new("charbon", "Mine", new(790, 200)) };
        var commune = new CommuneData("c1", "Première commune", new(470, 310), 5000, ["mairie", "moulin"]);

        var map = new VectorMapDefinition("Test", 1000, 640, contours, spots, river, forests, water, res, commune);

        Assert.Equal("Test", map.Name);
        Assert.Equal(2, map.Contours.Count);
        Assert.True(map.Contours[0].IsIndex);
        Assert.True(map.Contours[0].IsClosed);
        Assert.Equal(542, map.SpotElevations[0].Altitude);
        Assert.Equal("Première commune", map.DefaultCommune.Name);
    }

    [Fact]
    public void Le_fichier_carte_json_est_conforme_au_modele_vectoriel()
    {
        var path = Path.Combine(GameData.Folder, "carte.json");
        Assert.True(File.Exists(path), "Le fichier game/data/carte.json doit exister.");

        var json = File.ReadAllText(path);
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var map = System.Text.Json.JsonSerializer.Deserialize<VectorMapDefinition>(json, options);

        Assert.NotNull(map);
        Assert.True(map.Width >= 1000, "La largeur de la carte doit être d'au moins 1000 lieues.");
        Assert.True(map.Height >= 640, "La hauteur de la carte doit être d'au moins 640 lieues.");
        Assert.NotEmpty(map.Contours);
        Assert.NotEmpty(map.SpotElevations);
        Assert.Equal("La Sereine", map.River.Name);
        Assert.NotEmpty(map.River.Points);
        Assert.NotEmpty(map.Forests);
        Assert.NotEmpty(map.WaterBodies);
        Assert.NotEmpty(map.Resources);
        Assert.Equal("Première commune", map.DefaultCommune.Name);
        Assert.NotEmpty(map.AllRivers);
    }

    [Fact]
    public void VectorMapDefinition_Supporte_Multiples_Fleuves_Et_Retrocompatibilite()
    {
        var r1 = new RiverData("Fleuve Nord", [new(100, 100), new(200, 200)], Width: 4.0);
        var r2 = new RiverData("Affluent Sud", [new(300, 300), new(400, 400)], Width: 3.0);
        var commune = new CommuneData("c1", "Commune", new(100, 100), 1000, []);

        // Cas 1 : multi-fleuves
        var mapMulti = new VectorMapDefinition("Multi", 1000, 640, [], [], r1, [], [], [], commune, Rivers: [r1, r2]);
        Assert.Equal(2, mapMulti.AllRivers.Count);
        Assert.Equal("Fleuve Nord", mapMulti.AllRivers[0].Name);
        Assert.Equal("Affluent Sud", mapMulti.AllRivers[1].Name);

        // Cas 2 : rétrocompatibilité legacy (Rivers = null)
        var mapLegacy = new VectorMapDefinition("Legacy", 1000, 640, [], [], r1, [], [], [], commune);
        Assert.Single(mapLegacy.AllRivers);
        Assert.Equal("Fleuve Nord", mapLegacy.AllRivers[0].Name);

        // Sérialisation JSON
        var json = System.Text.Json.JsonSerializer.Serialize(mapMulti);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<VectorMapDefinition>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.AllRivers.Count);
        Assert.Equal("Fleuve Nord", deserialized.AllRivers[0].Name);
        Assert.Equal("Affluent Sud", deserialized.AllRivers[1].Name);
    }

    [Fact]
    public void SpotElevation_Supporte_Category_Et_ShowAltitude()
    {
        var spot = new SpotElevation(540, "Mont Sombre", new(100, 200), "Hill", false);
        Assert.Equal("Hill", spot.Category);
        Assert.False(spot.ShowAltitude);

        var spotDefault = new SpotElevation(600, "Pic Blanc", new(100, 200));
        Assert.Equal("Peak", spotDefault.Category);
        Assert.True(spotDefault.ShowAltitude);
    }

    [Fact]
    public void PolygonArea_Avec_Trous_Serialise_Et_Deserialise_Fidelement()
    {
        var outer = new List<Point2D> { new(0, 0), new(100, 0), new(100, 100), new(0, 100), new(0, 0) };
        var hole = new List<Point2D> { new(20, 20), new(80, 20), new(80, 80), new(20, 80), new(20, 20) };
        var poly = new PolygonArea("Falaises du Pic", "falaise", outer, Holes: [hole]);

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var json = System.Text.Json.JsonSerializer.Serialize(poly, options);
        Assert.Contains("\"holes\"", json);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<PolygonArea>(json, options);
        Assert.NotNull(deserialized);
        Assert.Equal("Falaises du Pic", deserialized.Name);
        Assert.Equal("falaise", deserialized.Kind);
        Assert.Equal(5, deserialized.Boundary.Count);
        Assert.NotNull(deserialized.Holes);
        Assert.Single(deserialized.Holes);
        Assert.Equal(5, deserialized.Holes[0].Count);
    }

    [Fact]
    public void PolygonArea_Sans_Trous_Ne_Serialise_Pas_De_Propriete_Holes()
    {
        var outer = new List<Point2D> { new(0, 0), new(50, 0), new(50, 50), new(0, 50), new(0, 0) };
        var poly = new PolygonArea("Bois Simple", "foret", outer);

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var json = System.Text.Json.JsonSerializer.Serialize(poly, options);
        Assert.DoesNotContain("\"holes\"", json);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<PolygonArea>(json, options);
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Holes);
    }
}
