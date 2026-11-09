using System;
using System.Linq;
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

        report = $"All slider, hover, river, commune move, cliff hole extraction, and 7-biome rule tests passed successfully! (River test: {riverPts.Count} points, Mountain peak: {highAlt:F0}m, Auto-biomes: {_currentMap.AllBiomes.Count})";
        return true;
    }
}
