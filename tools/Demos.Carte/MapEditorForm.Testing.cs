using System;
using System.IO;
using System.Linq;
using Demos.Infrastructure;
using Demos.Domain.Map;

namespace Demos.Carte;

public sealed partial class MapEditorForm
{
    public void AddRiverPointForTesting(double x, double y) => OnRiverSourcePlaced(x, y);

    public void ElevateBrushForTesting(double x, double y, double radius, double delta)
    {
        _currentHeightmap?.ApplyBrush(x, y, radius, delta);
        ReextractContoursAfterElevationChange();
    }

    public void SmoothBrushForTesting(double x, double y, double radius, double power)
    {
        _currentHeightmap?.ApplySmoothBrush(x, y, radius, power);
        ReextractContoursAfterElevationChange();
    }

    public void FlattenBrushForTesting(double x, double y, double radius, double targetAltitude, double power)
    {
        _currentHeightmap?.ApplyFlattenBrush(x, y, radius, targetAltitude, power);
        ReextractContoursAfterElevationChange();
    }

    public bool ApplyBiomeBrushForTesting(double x, double y, double radius, string biomeKey, bool isAdding, out string? error)
    {
        error = null;
        var res = _currentZoneGrid?.ApplyBiomeBrush(x, y, radius, biomeKey, isAdding, _currentHeightmap, _currentMap.AllRivers, out error) ?? false;
        var biomes = _currentZoneGrid?.ExtractBiomes(biomeKey, _txtBiomeName.Text) ?? [];
        var forests = _currentZoneGrid?.ExtractForests(_txtBiomeName.Text) ?? [];
        _currentMap = _currentMap with { Biomes = biomes, Forests = forests };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
        return res;
    }

    public void ApplyForestBrushForTesting(double x, double y, double radius, bool isAdding)
    {
        ApplyBiomeBrushForTesting(x, y, radius, "foret", isAdding, out _);
    }

    public void ApplyResourceBrushForTesting(double x, double y, double radius, string type, bool isAdding)
    {
        _currentZoneGrid?.ApplyResourceBrush(x, y, radius, type, isAdding);
        var (zones, deposits) = _currentZoneGrid?.ExtractResources(_txtResourceRegion.Text) ?? ([], []);
        _currentMap = _currentMap with { ResourceZones = zones, Resources = deposits };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
    }

    public void AddCommuneForTesting(double x, double y, string name, int pop = 100)
    {
        var id = $"commune-{Guid.NewGuid():N}"[..16];
        var newCommune = new CommuneData(id, name, new Point2D(x, y), pop);
        var list = _currentMap.AllCommunes.ToList();
        list.Add(newCommune);
        _currentMap = _currentMap with { Communes = list, DefaultCommune = list[0] };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        UpdateCommuneLabels();
    }

    public void MoveCommuneForTesting(int index, double x, double y)
    {
        var list = _currentMap.AllCommunes.ToList();
        if (index < 0 || index >= list.Count) return;
        var old = list[index];
        list[index] = old with { Position = new Point2D(x, y) };
        _currentMap = _currentMap with { Communes = list, DefaultCommune = list[0] };
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        UpdateCommuneLabels();
    }

    public void SetStyleForTesting(MapRenderStyle style)
    {
        _styleCombo.SelectedIndex = style == MapRenderStyle.IgnPastel ? 0 : 1;
        _canvas.SetStyle(style);
    }

    public void SetIslandParametersForTesting(int seed, IslandMorphology morphology, double maxAltitude)
    {
        _contextBar.NumSeed.Value = Math.Clamp(seed, 1, 999999);
        _contextBar.ComboMorphology.SelectedIndex = morphology switch
        {
            IslandMorphology.AlpineRidge => 1,
            IslandMorphology.Archipelago => 2,
            IslandMorphology.Fjords => 3,
            _ => 0
        };
        _contextBar.ComboMaxAltitude.SelectedIndex = maxAltitude switch
        {
            <= 1200 => 0,
            <= 2000 => 1,
            <= 2800 => 2,
            _ => 3
        };
    }

    public bool TestSlidersSync(out string report)
    {
        _sliderBrushRadius.Value = 250;
        if ((int)_canvas.BrushRadius != 250)
        {
            report = $"Failed: terrain radius 250 -> canvas={_canvas.BrushRadius}";
            return false;
        }

        _sliderBrushPower.Value = 70;
        if ((int)_canvas.BrushPower != 70)
        {
            report = $"Failed: terrain power 70 -> canvas={_canvas.BrushPower}";
            return false;
        }

        _sliderForestRadius.Value = 180;
        if ((int)_canvas.ForestBrushRadius != 180)
        {
            report = $"Failed: forest radius 180 -> canvas={_canvas.ForestBrushRadius}";
            return false;
        }

        _sliderResourceRadius.Value = 120;
        if ((int)_canvas.ResourceBrushRadius != 120)
        {
            report = $"Failed: resource radius 120 -> canvas={_canvas.ResourceBrushRadius}";
            return false;
        }

        _sliderSmoothRadius.Value = 160;
        if ((int)_canvas.SmoothBrushRadius != 160)
        {
            report = $"Failed: smooth radius 160 -> canvas={_canvas.SmoothBrushRadius}";
            return false;
        }

        _sliderSmoothPower.Value = 65;
        if ((int)_canvas.SmoothBrushPower != 65)
        {
            report = $"Failed: smooth power 65 -> canvas={_canvas.SmoothBrushPower}";
            return false;
        }

        // Test profil de pinceau
        _comboBrushProfile.SelectedIndex = 1; // ConeLinear
        if (_canvas.BrushProfile != BrushProfile.ConeLinear)
        {
            report = $"Failed: brush profile ConeLinear -> canvas={_canvas.BrushProfile}";
            return false;
        }
        _comboBrushProfile.SelectedIndex = 2; // PlateauFlat
        if (_canvas.BrushProfile != BrushProfile.PlateauFlat)
        {
            report = $"Failed: brush profile PlateauFlat -> canvas={_canvas.BrushProfile}";
            return false;
        }
        _comboBrushProfile.SelectedIndex = 0; // DomeCos
        if (_canvas.BrushProfile != BrushProfile.DomeCos)
        {
            report = $"Failed: brush profile DomeCos -> canvas={_canvas.BrushProfile}";
            return false;
        }

        // Test fonctionnel du pinceau Adoucisseur (lissage et plateau)
        _currentHeightmap.ApplyBrush(600, 600, 50, 400); // crée un pic aigu à (600,600)
        var peakBefore = _currentHeightmap.SampleElevation(600, 600);
        SmoothBrushForTesting(600, 600, 60, 0.6);
        var peakAfterSmooth = _currentHeightmap.SampleElevation(600, 600);
        if (peakAfterSmooth >= peakBefore)
        {
            report = $"Failed: smooth brush should round and lower the sharp peak (before={peakBefore:F1}, after={peakAfterSmooth:F1})";
            return false;
        }

        FlattenBrushForTesting(600, 600, 60, 150.0, 0.8);
        var peakAfterFlatten = _currentHeightmap.SampleElevation(600, 600);
        if (Math.Abs(peakAfterFlatten - 150.0) >= Math.Abs(peakAfterSmooth - 150.0))
        {
            report = $"Failed: flatten brush should pull elevation towards target 150m (was={peakAfterSmooth:F1}, now={peakAfterFlatten:F1})";
            return false;
        }

        if (!_canvas.TestBrushHoverSafety())
        {
            report = "Failed: Canvas hover safety test failed";
            return false;
        }

        // Test de simulation d'écoulement de rivière en plaine
        var riverPts = _currentHeightmap.SimulateSpontaneousRiver(1200, 750);
        if (riverPts.Count < 10)
        {
            report = $"Failed: river simulation generated only {riverPts.Count} points from (1200, 750)";
            return false;
        }

        // Test des fleuves qui ne remontent pas (24 novembre 2026) : sur des îles générées, chaque fleuve tracé puis creusé
        // descend de la source à l'embouchure (1 m de tolérance), y compris sur les pentes faibles.
        foreach (var (seed, morph) in new[] { (1, IslandMorphology.Natural), (42, IslandMorphology.Natural), (7, IslandMorphology.Archipelago), (99, IslandMorphology.Archipelago) })
        {
            var island = Heightmap.CreateIsland(360, 230, 3600, 2250, seed, morph, 2800);
            for (var sx = 250.0; sx < 3400; sx += 330)
            {
                for (var sy = 250.0; sy < 2050; sy += 330)
                {
                    if (island.SampleElevation(sx, sy) < 150) continue;
                    var pts = island.SimulateSpontaneousRiver(sx, sy);
                    if (pts.Count < 5) continue;
                    island.DigRiverThalweg(pts, 45);
                    var lowest = island.SampleElevation(pts[0].X, pts[0].Y);
                    foreach (var p in pts.Skip(1))
                    {
                        var z = island.SampleElevation(p.X, p.Y);
                        if (z > lowest + 1.0)
                        {
                            report = $"Failed: river from ({sx}, {sy}) climbs {z - lowest:F1} m after digging (seed {seed})";
                            return false;
                        }
                        lowest = Math.Min(lowest, z);
                    }
                }
            }
        }

        // Test des fleuves de plaine (24 novembre 2026) : sur une pente très faible, un fleuve tracé va jusqu'à la mer ;
        // creusé, effacé puis retracé de la même source, il suit son lit jusqu'à la mer sans remonter.
        if (!PlainRiversReachTheSea(out var plainProblem))
        {
            report = $"Failed: {plainProblem}";
            return false;
        }
        if (!LakesHoldTheirWater(out var lakeProblem))
        {
            report = $"Failed: {lakeProblem}";
            return false;
        }

        // Test de déplacement de la commune de base
        var origX = _currentMap.DefaultCommune.Position.X;
        var origY = _currentMap.DefaultCommune.Position.Y;
        MoveCommuneForTesting(0, origX + 75, origY + 45);
        if (Math.Abs(_currentMap.DefaultCommune.Position.X - (origX + 75)) > 0.1)
        {
            report = "Failed: commune moving test failed";
            return false;
        }
        MoveCommuneForTesting(0, origX, origY);

        // Test des règles de biomes (glacier, forêt, falaise, fertile, désert)
        _currentHeightmap.ApplyBrush(1500, 1500, 50, -500);
        var lowAlt = _currentHeightmap.SampleElevation(1500, 1500);
        if (lowAlt > 0 && lowAlt < 2500)
        {
            var glacierApplied = ApplyBiomeBrushForTesting(1500, 1500, 30, "glacier", true, out var glacierErr);
            if (glacierApplied || glacierErr == null)
            {
                report = "Failed: glacier brush should be rejected below 2500m";
                return false;
            }

            // Falaise sur plaine plate doit être rejetée (pente insuffisante)
            var cliffOnFlat = ApplyBiomeBrushForTesting(1500, 1500, 30, "falaise", true, out var cliffErr);
            if (cliffOnFlat || cliffErr == null)
            {
                report = "Failed: cliff brush should be rejected on flat plain";
                return false;
            }

            // Terre fertile en plaine doit réussir
            var fertileOnPlain = ApplyBiomeBrushForTesting(1500, 1500, 30, "fertile", true, out var fertileErr);
            if (!fertileOnPlain)
            {
                report = $"Failed: fertile brush should succeed on plain (< 600m) (err={fertileErr})";
                return false;
            }
        }

        for (int step = 0; step < 25; step++)
        {
            _currentHeightmap.ApplyBrush(1800, 1200, 100, 150);
        }
        var highAlt = _currentHeightmap.SampleElevation(1800, 1200);
        if (highAlt >= 2500)
        {
            var forestOnGlacier = ApplyBiomeBrushForTesting(1800, 1200, 40, "foret", true, out var forestErr);
            if (forestOnGlacier || forestErr == null)
            {
                report = $"Failed: forest brush should be rejected on glacier territory (alt={highAlt:F0}m)";
                return false;
            }

            var glacierHigh = ApplyBiomeBrushForTesting(1800, 1200, 40, "glacier", true, out var okErr);
            if (!glacierHigh)
            {
                report = $"Failed: glacier brush should succeed at altitude >= 2500m (alt={highAlt:F0}m, err={okErr})";
                return false;
            }

            // Falaise sur le flanc escarpé de la montagne doit réussir
            var cliffHigh = ApplyBiomeBrushForTesting(1800, 1200, 40, "falaise", true, out var cliffHighErr);
            if (!cliffHigh)
            {
                report = $"Failed: cliff brush should succeed on steep mountain slope (err={cliffHighErr})";
                return false;
            }

            // Marais sur le flanc escarpé de la montagne doit être rejeté (pente >= 30%)
            var swampOnSteep = ApplyBiomeBrushForTesting(1800, 1200, 40, "marais", true, out var swampErr);
            if (swampOnSteep || swampErr == null)
            {
                report = "Failed: swamp brush should be rejected on steep mountain slope (slope >= 30%)";
                return false;
            }
        }

        // Test d'inversion des falaises / sommet (anneaux et trous Marching Squares)
        var ringGrid = new ZoneMaskGrid(80, 80, 800, 800);
        for (var ci = 0; ci < ringGrid.Columns; ci++)
        {
            var gx = ringGrid.GetWorldX(ci);
            for (var cj = 0; cj < ringGrid.Rows; cj++)
            {
                var gy = ringGrid.GetWorldY(cj);
                var dist = Math.Sqrt((gx - 400) * (gx - 400) + (gy - 400) * (gy - 400));
                if (dist >= 80.0 && dist <= 200.0)
                {
                    ringGrid.SetCellForTesting(ci, cj, 5); // falaise id = 5
                }
            }
        }
        var extractedBiomes = ringGrid.ExtractBiomes();
        var cliffBiomes = extractedBiomes.Where(b => b.Kind == "falaise").ToList();
        if (cliffBiomes.Count != 1)
        {
            report = $"Failed: cliff ring should produce exactly 1 biome with hole, got {cliffBiomes.Count}";
            return false;
        }
        if (cliffBiomes[0].Holes is not { Count: > 0 })
        {
            report = "Failed: cliff ring should contain at least 1 hole for summit";
            return false;
        }

        // Test des biomes qui touchent le bord de la carte (24 novembre 2026) : un coin de forêt (deux bords) et une bande
        // de falaise le long du bord gauche (trois bords) ; leur contour doit se refermer le long du bord, sans inversion.
        var edgeGrid = new ZoneMaskGrid(80, 80, 800, 800);
        for (var ci = 0; ci < edgeGrid.Columns; ci++)
        {
            var gx = edgeGrid.GetWorldX(ci);
            for (var cj = 0; cj < edgeGrid.Rows; cj++)
            {
                var gy = edgeGrid.GetWorldY(cj);
                if (Math.Sqrt((gx - 800) * (gx - 800) + gy * gy) <= 300.0)
                    edgeGrid.SetCellForTesting(ci, cj, 1); // forêt id = 1, coin en haut à droite
                else if (gx <= 150.0)
                    edgeGrid.SetCellForTesting(ci, cj, 5); // falaise id = 5, bande à gauche
            }
        }
        var edgeBiomes = edgeGrid.ExtractBiomes();
        var edgeForest = edgeBiomes.Where(b => b.Kind == "foret").ToList();
        var edgeCliff = edgeBiomes.Where(b => b.Kind == "falaise").ToList();
        if (edgeForest.Count != 1 || edgeCliff.Count != 1)
        {
            report = $"Failed: edge biomes should give 1 forest and 1 cliff, got {edgeForest.Count} and {edgeCliff.Count}";
            return false;
        }
        bool InEdge(PolygonArea zone, double x, double y) => ExtractionSites.Contains(zone, new Point2D(x, y));
        if (!InEdge(edgeForest[0], 750, 50) || !InEdge(edgeForest[0], 650, 100) || InEdge(edgeForest[0], 400, 400) || InEdge(edgeForest[0], 300, 700)
            || !InEdge(edgeCliff[0], 50, 10) || !InEdge(edgeCliff[0], 60, 400) || !InEdge(edgeCliff[0], 40, 790) || InEdge(edgeCliff[0], 400, 400))
        {
            report = "Failed: edge biomes are inverted (a zone touching the map border must stay on its own side)";
            return false;
        }

        // Test de la pré-génération automatique des biomes naturels sur l'île
        CreateIslandMap();
        if (_currentMap.AllBiomes.Count == 0)
        {
            report = "Failed: CreateIslandMap should produce biomes via AutoGenerateBiomes";
            return false;
        }
        var hasBeaches = _currentMap.AllBiomes.Any(b => b.Kind == "plage");
        var hasFertile = _currentMap.AllBiomes.Any(b => b.Kind == "fertile");
        var hasForest = _currentMap.AllBiomes.Any(b => b.Kind == "foret");
        if (!hasBeaches || !hasFertile || !hasForest)
        {
            report = $"Failed: AutoGenerateBiomes should produce beaches, fertile lands and forests (beaches={hasBeaches}, fertile={hasFertile}, forest={hasForest})";
            return false;
        }

        // Outil Lacs (24 novembre 2026) : deux lacs posés sur l'île (un plein, avec sa sortie ; un fermé), puis tout effacé.
        LakeScenarioForTesting();
        if (_currentMap.AllLakes.Count != 2 || !_currentMap.AllRivers.Any(r => r.Name.StartsWith("Sortie du")))
        {
            report = $"Failed: lake tool placed {_currentMap.AllLakes.Count} lakes, outlet river: {_currentMap.AllRivers.Any(r => r.Name.StartsWith("Sortie du"))}";
            return false;
        }
        ClearAllLakes();
        if (_currentMap.AllLakes.Count != 0 || _currentMap.AllRivers.Any(r => r.Name.StartsWith("Sortie du")))
        {
            report = "Failed: clearing lakes left lakes or outlet rivers";
            return false;
        }

        report = $"All slider, hover, river, commune move, cliff hole extraction, and 7-biome rule tests passed successfully! (River test: {riverPts.Count} points, Mountain peak: {highAlt:F0}m, Auto-biomes: {_currentMap.AllBiomes.Count})";
        return true;
    }

    /// <summary>
    /// Les fleuves de plaine (24 novembre 2026) : sur des plaines en pente très faible (0,13 et 0,05 m par km, avec de petites
    /// ondulations), le fleuve tracé atteint la mer ; creusé, puis retracé depuis la même source (comme après la gomme,
    /// qui laisse la vallée), il atteint encore la mer, sans remonter de plus de 1 m.
    /// </summary>
    private static bool PlainRiversReachTheSea(out string problem)
    {
        foreach (var drop in new[] { 40.0, 15.0 })
        {
            var plain = Heightmap.CreateFlat(360, 230, 3600, 2250, 0);
            var grid = new double[360, 230];
            for (var i = 0; i < 360; i++)
                for (var j = 0; j < 230; j++)
                {
                    var (x, y) = (plain.GetWorldX(i), plain.GetWorldY(j));
                    grid[i, j] = x >= 3000 ? -20 : drop * (1 - x / 3000) + 0.4 * Math.Sin(x / 70) * Math.Cos(y / 90) + 1;
                }
            plain.RestoreGrid(grid);

            foreach (var sy in new[] { 600.0, 1100.0, 1600.0 })
            {
                var first = plain.SimulateSpontaneousRiver(300, sy);
                if (!ReachesOutlet(plain, first))
                {
                    problem = $"plain river from (300, {sy}) stops at ({first[^1].X}, {first[^1].Y}) before the sea (drop {drop} m)";
                    return false;
                }
                plain.DigRiverThalweg(first, 45);
                var again = plain.SimulateSpontaneousRiver(300, sy);
                if (!ReachesOutlet(plain, again))
                {
                    problem = $"retraced plain river from (300, {sy}) stops at ({again[^1].X}, {again[^1].Y}) before the sea (drop {drop} m)";
                    return false;
                }
                var lowest = plain.SampleElevation(again[0].X, again[0].Y);
                foreach (var p in again.Skip(1))
                {
                    var z = plain.SampleElevation(p.X, p.Y);
                    if (z > lowest + 1.0)
                    {
                        problem = $"retraced plain river from (300, {sy}) climbs {z - lowest:F1} m (drop {drop} m)";
                        return false;
                    }
                    lowest = Math.Min(lowest, z);
                }
            }
        }
        problem = "";
        return true;
    }

    /// <summary>
    /// Les lacs (24 novembre 2026) : une cuvette sur une plaine qui descend vers la mer se remplit jusqu'à son seuil ; un
    /// fleuve tracé en amont s'arrête dans le lac ; le fleuve de sortie part du seuil et atteint la mer ; creuser les deux
    /// ne vide pas le lac (le seuil garde son niveau). Sur une plaine plate, le fleuve va à la mer plutôt qu'au bord
    /// pourtant plus proche (à pente identique, il vise la mer).
    /// </summary>
    private static bool LakesHoldTheirWater(out string problem)
    {
        var map = Heightmap.CreateFlat(360, 230, 3600, 2250, 0);
        var grid = new double[360, 230];
        for (var i = 0; i < 360; i++)
            for (var j = 0; j < 230; j++)
            {
                var (x, y) = (map.GetWorldX(i), map.GetWorldY(j));
                var r2 = ((x - 1500) * (x - 1500) + (y - 1100) * (y - 1100)) / (250.0 * 250.0);
                grid[i, j] = x >= 3000 ? -20 : 60 * (1 - x / 3000) + 2 - (r2 < 1 ? 25 * (1 - r2) : 0);
            }
        map.RestoreGrid(grid);

        var basin = map.FindBasin(1450, 1080);
        if (basin is null || basin.Spill - basin.Bottom < 5)
        {
            problem = $"lake basin not found (spill {basin?.Spill:F1}, bottom {basin?.Bottom:F1})";
            return false;
        }
        var shape = map.LakeShape(basin, basin.Spill).OrderByDescending(s => s.Outer.Count).FirstOrDefault();
        if (shape.Outer is null)
        {
            problem = "lake shape is empty";
            return false;
        }
        var lake = new LakeData("Essai", new Point2D(1450, 1080), basin.Spill, shape.Outer, shape.Holes.Count > 0 ? shape.Holes : null);
        LakeData[] lakes = [lake];
        if (!lake.Contains(new Point2D(1500, 1100)))
        {
            problem = "lake does not contain its centre";
            return false;
        }

        var inflow = map.SimulateSpontaneousRiver(700, 1100, null, lakes);
        if (inflow.Count < 2 || !lake.Contains(inflow[^1]))
        {
            problem = $"river upstream of the lake ends at ({inflow.LastOrDefault().X}, {inflow.LastOrDefault().Y}), not in the lake";
            return false;
        }

        var beyond = new Point2D(map.GetWorldX(basin.Outflow.I), map.GetWorldY(basin.Outflow.J));
        var outflow = map.SimulateSpontaneousRiver(beyond.X, beyond.Y, null, lakes);
        if (!ReachesOutlet(map, outflow))
        {
            problem = $"lake outlet stops at ({outflow.LastOrDefault().X}, {outflow.LastOrDefault().Y}) before the sea";
            return false;
        }

        var col = new Point2D(map.GetWorldX(basin.Col.I), map.GetWorldY(basin.Col.J));
        map.DigRiverThalweg([col, .. outflow], 45, lakes: lakes);
        map.DigRiverThalweg(inflow, 45, lakes: lakes);
        var after = map.FindBasin(1450, 1080);
        if (after is null || after.Spill < lake.Level - 0.05)
        {
            problem = $"digging drained the lake: spill {after?.Spill:F2} m, lake level {lake.Level:F2} m";
            return false;
        }

        // À pente identique, la mer : plaine plate à 50 m, mer à l'est (2 700 unités), bord ouest à 300 unités.
        var flat = Heightmap.CreateFlat(360, 230, 3600, 2250, 50);
        var flatGrid = new double[360, 230];
        for (var i = 0; i < 360; i++)
            for (var j = 0; j < 230; j++)
                flatGrid[i, j] = flat.GetWorldX(i) >= 3000 ? -20 : 50;
        flat.RestoreGrid(flatGrid);
        var toSea = flat.SimulateSpontaneousRiver(300, 1100);
        if (toSea.Count < 2 || flat.SampleElevation(toSea[^1].X, toSea[^1].Y) > 0.5)
        {
            problem = $"on a flat plain the river ends at ({toSea.LastOrDefault().X}, {toSea.LastOrDefault().Y}), not at the sea";
            return false;
        }

        problem = "";
        return true;
    }

    /// <summary>
    /// Pour la capture de l'outil Lacs : une cuvette creusée dans la plaine de l'île (vers 100 m), remplie jusqu'à son
    /// seuil (avec son fleuve de sortie), une seconde plus petite baissée à 60 % (lac fermé), et un fleuve qui arrive au lac.
    /// </summary>
    public void LakeScenarioForTesting()
    {
        var spot = FindLandForTesting(90, 140, (1000, 800));
        DigBowlForTesting(spot, 140, 30);
        PlaceLake(spot.X, spot.Y, 1.0);

        var second = FindLandForTesting(40, 100, (spot.X + 600, spot.Y + 300));
        DigBowlForTesting(second, 90, 20);
        PlaceLake(second.X, second.Y, 0.6);

        var source = FindLandForTesting(250, 400, (spot.X + 150, spot.Y - 450));
        SelectTool(EditorTool.DrawRiver);
        OnRiverSourcePlaced(source.X, source.Y);
        SelectTool(EditorTool.DrawLake);
    }

    /// <summary>Creuse une cuvette d'à peu près cette profondeur, sans passer sous la mer.</summary>
    private void DigBowlForTesting(Point2D at, double radius, double depth)
    {
        // Le bord de la cuvette est au niveau du point le plus bas du cercle : elle est fermée jusque-là.
        var rim = Enumerable.Range(0, 72).Min(a => _currentHeightmap.SampleElevation(at.X + radius * Math.Cos(a * Math.PI / 36), at.Y + radius * Math.Sin(a * Math.PI / 36)));
        var grid = _currentHeightmap.CloneGrid();
        for (var i = 0; i < _currentHeightmap.Columns; i++)
            for (var j = 0; j < _currentHeightmap.Rows; j++)
            {
                var (dx, dy) = (_currentHeightmap.GetWorldX(i) - at.X, _currentHeightmap.GetWorldY(j) - at.Y);
                var r2 = (dx * dx + dy * dy) / (radius * radius);
                if (r2 < 1)
                    grid[i, j] = Math.Min(grid[i, j], Math.Max(rim - depth * (1 - r2), 1));
            }
        _currentHeightmap.RestoreGrid(grid);
        ReextractContoursAfterElevationChange();
    }

    /// <summary>Le point de terre le plus proche de <paramref name="near"/> dont l'altitude est entre les deux bornes.</summary>
    private Point2D FindLandForTesting(double low, double high, (double X, double Y) near)
    {
        var best = new Point2D(near.X, near.Y);
        var bestDist = double.MaxValue;
        for (var x = 50.0; x < _currentMap.Width - 50; x += 20)
            for (var y = 50.0; y < _currentMap.Height - 50; y += 20)
            {
                var z = _currentHeightmap.SampleElevation(x, y);
                var d = (x - near.X) * (x - near.X) + (y - near.Y) * (y - near.Y);
                if (z >= low && z <= high && d < bestDist)
                    (best, bestDist) = (new Point2D(x, y), d);
            }
        return best;
    }

    /// <summary>
    /// Mesure de l'éditeur pour chaque taille de carte (24 novembre 2026, <c>--mesurer-tailles</c>) : carte vierge, île,
    /// dix coups de pinceau et les courbes, un fleuve (réseau d'écoulement calculé, puis gardé), un dessin complet.
    /// </summary>
    public string MeasureSizesForTesting(string? saveLastTo = null)
    {
        var lines = new System.Collections.Generic.List<string>();
        for (var index = 0; index < MapSizes.All.Length; index++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _comboDimensions.SelectedIndex = index;
            var blank = sw.ElapsedMilliseconds;

            var (sw0, sh0) = GetCurrentDimensions();
            sw.Restart();
            var probe = MapGenerator.CreateIsland(sw0, sh0, out var probeHeights, 42, IslandMorphology.Natural, 2800);
            var tRelief = sw.ElapsedMilliseconds;
            var (pc, pr) = MapSizes.ZoneGrid(sw0, sh0);
            var probeZones = new ZoneMaskGrid(pc, pr, sw0, sh0);
            sw.Restart();
            probeZones.AutoGenerateBiomes(probeHeights, probe.AllRivers);
            var tAuto = sw.ElapsedMilliseconds;
            sw.Restart();
            probeZones.ExtractBiomes();
            var tExtract = sw.ElapsedMilliseconds;
            sw.Restart();
            probeZones.ExtractForests();
            var tForests = sw.ElapsedMilliseconds;
            lines.Add($"  île en détail : relief et courbes {tRelief} ms, biomes {tAuto} ms, contours des biomes {tExtract} ms, forêts {tForests} ms");

            sw.Restart();
            CreateIslandMap();
            var island = sw.ElapsedMilliseconds;

            var (w, h) = (_currentMap.Width, _currentMap.Height);
            sw.Restart();
            for (var step = 0; step < 10; step++)
                _currentHeightmap.ApplyBrush(w * 0.4, h * 0.4, w * 0.03, 20);
            var brushOnly = sw.ElapsedMilliseconds;
            var levelsProbe = new System.Collections.Generic.List<double>();
            for (var alt = 25.0; alt <= _currentHeightmap.MaxElevation; alt += 25.0) levelsProbe.Add(alt);
            sw.Restart();
            ContourExtractor.ExtractContours(_currentHeightmap, levelsProbe);
            var tContours = sw.ElapsedMilliseconds;
            sw.Restart();
            ContourExtractor.ExtractWaterBodies(_currentHeightmap);
            var tWater = sw.ElapsedMilliseconds;
            lines.Add($"  courbes seules {tContours} ms, côte {tWater} ms");
            sw.Restart();
            ReextractContoursAfterElevationChange();
            var brush = sw.ElapsedMilliseconds;

            var source = FindLandForTesting(300, 2500, (w * 0.5, h * 0.45));
            sw.Restart();
            var river = _currentHeightmap.SimulateSpontaneousRiver(source.X, source.Y, _currentMap.AllRivers, _currentMap.AllLakes);
            var firstRiver = sw.ElapsedMilliseconds;
            sw.Restart();
            _currentHeightmap.SimulateSpontaneousRiver(source.X + 30, source.Y, _currentMap.AllRivers, _currentMap.AllLakes);
            var nextRiver = sw.ElapsedMilliseconds;

            using var image = new System.Drawing.Bitmap(Math.Max(1, _canvas.Width), Math.Max(1, _canvas.Height));
            _canvas.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height));
            sw.Restart();
            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle(), _currentZoneGrid);
            _canvas.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height));
            var draw = sw.ElapsedMilliseconds;

            lines.Add($"{MapSizes.All[index].Label} : relief {_currentHeightmap.Columns} × {_currentHeightmap.Rows} cases, " +
                $"zones {_currentZoneGrid.Columns} × {_currentZoneGrid.Rows} ; vierge {blank} ms, île {island} ms, " +
                $"10 coups de pinceau {brushOnly} ms + courbes {brush} ms, fleuve {firstRiver} ms puis {nextRiver} ms ({river.Count} points), " +
                $"dessin complet {draw} ms ; {_currentMap.Contours.Count} courbes, {_currentMap.AllBiomes.Count} biomes");
        }

        // La dernière carte (le petit continent), enregistrée pour l'essayer dans le jeu, avec son relief.
        if (saveLastTo is not null)
        {
            Directory.CreateDirectory(saveLastTo);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
            File.WriteAllText(Path.Combine(saveLastTo, MapFiles.MapFile), System.Text.Json.JsonSerializer.Serialize(_currentMap with { Name = "Essai continent (provisoire)" }, options));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ReliefExport.Run(saveLastTo, previewPath: null);
            lines.Add($"Enregistrée dans {saveLastTo} ; relief en {sw.ElapsedMilliseconds} ms");
        }
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Le tracé finit dans la mer (ou à moins de 1 m au-dessus), au bord de la carte, ou sur un autre fleuve.</summary>
    private static bool ReachesOutlet(Heightmap map, System.Collections.Generic.IReadOnlyList<Point2D> points)
    {
        if (points.Count < 2) return false;
        var end = points[^1];
        return map.SampleElevation(end.X, end.Y) <= 1.0
            || end.X <= 5 || end.Y <= 5 || end.X >= map.WorldWidth - 5 || end.Y >= map.WorldHeight - 5;
    }
}
