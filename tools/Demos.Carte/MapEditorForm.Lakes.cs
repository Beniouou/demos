using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;

namespace Demos.Carte;

/// <summary>
/// L'outil « Lacs » (décidé le 24 novembre 2026) : un clic dans une cuvette la remplit jusqu'au niveau du curseur. Au seuil
/// (100 %), le lac déborde : un fleuve de sortie part du seuil, par le chemin qui creuse le moins, et il est creusé comme
/// dans l'outil Fleuves ; plus bas, le lac est fermé. Un clic droit dans un lac le retire, avec son fleuve de sortie.
/// Les lacs se posent avant les fleuves : un fleuve qui arrive à un lac s'y arrête.
/// </summary>
public sealed partial class MapEditorForm
{
    /// <summary>Une cuvette moins profonde que cela (m) n'est pas un lac.</summary>
    private const double MinLakeDepth = 0.5;

    private void BindLakeEvents()
    {
        _contextBar.SliderLakeLevel.ValueChanged += (_, _) => _contextBar.UpdateLakeLevelText();
        _contextBar.BtnLakeClearAll.Click += (_, _) => ClearAllLakes();
        _canvas.LakePlaced += (wx, wy) => PlaceLake(wx, wy, _contextBar.SliderLakeLevel.Value / 100.0);
        _canvas.LakeDeleted += (wx, wy) =>
        {
            var lake = _currentMap.AllLakes.FirstOrDefault(l => l.Contains(new Point2D(wx, wy)));
            if (lake is null)
            {
                Log("Aucun lac ici.");
                return;
            }
            PushUndo();
            RemoveLake(lake);
            _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
            Log($"⌫ Lac « {lake.Name} » retiré{(lake.Outlet is null ? "" : $", avec son fleuve de sortie « {lake.Outlet} »")} (la vallée creusée reste).");
            UpdateLakeContext();
        };
    }

    /// <summary>
    /// Remplit la cuvette du point (x, y) : <paramref name="share"/> est la part de la hauteur entre le fond et le seuil
    /// (1 = au seuil, avec un fleuve de sortie). Un clic dans un lac existant le refait au nouveau niveau.
    /// </summary>
    public LakeData? PlaceLake(double x, double y, double share)
    {
        var replaced = _currentMap.AllLakes.FirstOrDefault(l => l.Contains(new Point2D(x, y)));
        var seed = replaced?.Seed ?? new Point2D(Math.Round(x, 1), Math.Round(y, 1));

        // La cuvette, calculée sans le lac qu'on refait ni son fleuve de sortie.
        var others = _currentMap.AllLakes.Where(l => !ReferenceEquals(l, replaced)).ToList();
        var basin = _currentHeightmap.FindBasin(seed.X, seed.Y, others);
        if (basin is null)
        {
            Log("⚠️ C'est la mer : un lac se pose dans une cuvette au-dessus de la mer.");
            return null;
        }
        if (basin.Spill - basin.Bottom < MinLakeDepth)
        {
            Log("⚠️ Pas de cuvette ici : l'eau s'écoulerait. Creusez d'abord une cuvette avec le pinceau de relief.");
            return null;
        }

        var full = share >= 0.999;
        var level = full ? basin.Spill : basin.Bottom + (basin.Spill - basin.Bottom) * Math.Clamp(share, 0.1, 1.0);
        var shapes = _currentHeightmap.LakeShape(basin, level);
        var bottom = new Point2D(_currentHeightmap.GetWorldX(basin.BottomCell.I), _currentHeightmap.GetWorldY(basin.BottomCell.J));
        var (outer, holes) = shapes
            .Where(s => s.Outer.Count >= 3)
            .Select(s => (s.Outer, s.Holes, Contains: ExtractionSites.Contains(new PolygonArea("", "lac", s.Outer, s.Holes.Count > 0 ? s.Holes : null), bottom)))
            .OrderByDescending(s => s.Contains)
            .ThenByDescending(s => s.Outer.Count)
            .Select(s => (s.Outer, s.Holes))
            .FirstOrDefault();
        if (outer is null)
        {
            Log("⚠️ Le lac serait trop petit pour être dessiné : montez le niveau ou creusez la cuvette.");
            return null;
        }

        PushUndo();
        var name = replaced?.Name
            ?? (string.IsNullOrWhiteSpace(_contextBar.TxtLakeName.Text) ? $"Lac {_currentMap.AllLakes.Count + 1}" : _contextBar.TxtLakeName.Text.Trim());
        if (replaced is not null)
            RemoveLake(replaced);
        if (_currentMap.AllLakes.Any(l => l.Name == name))
            name = $"{name} {_currentMap.AllLakes.Count + 1}";

        var lake = new LakeData(name, seed, Math.Round(level, 2), outer, holes.Count > 0 ? holes : null);
        var lakes = _currentMap.AllLakes.Append(lake).ToList();
        _currentMap = _currentMap with { Lakes = lakes };

        var message = $"🏞️ Lac « {name} » : niveau {level:F1} m, {LakeDepthText(basin, level)}";
        if (full)
        {
            var outlet = OutletRiver(lake, basin, lakes);
            if (outlet is not null)
            {
                lake = lake with { Outlet = outlet.Name };
                lakes[^1] = lake;
                var rivers = _currentMap.AllRivers.Append(outlet).ToList();
                _currentMap = _currentMap with { Lakes = lakes, Rivers = rivers, River = rivers[0] };
                if (_chkDigThalweg.Checked && _numRiverDepth.Value > 0)
                    _currentHeightmap.DigRiverThalweg(outlet.Points, maxDepth: (double)_numRiverDepth.Value, lakes: lakes);
                message += $" ; il déborde par son seuil : fleuve de sortie « {outlet.Name} » ({outlet.Points.Count} points).";
            }
            else
            {
                message += " ; au seuil, mais l'eau tombe aussitôt dans la mer ou hors de la carte : pas de fleuve de sortie.";
            }
        }
        else
        {
            message += " ; sous son seuil : lac fermé, sans sortie.";
        }

        ReextractContoursAfterElevationChange();
        Log(message);
        UpdateLakeContext();
        return lake;
    }

    /// <summary>
    /// Le fleuve de sortie d'un lac plein : du seuil, puis de la première case au-delà, par le chemin qui creuse le moins
    /// vers la mer, le bord ou un autre lac. Null si l'eau tombe aussitôt dans la mer ou hors de la carte.
    /// </summary>
    private RiverData? OutletRiver(LakeData lake, Heightmap.Basin basin, IReadOnlyList<LakeData> lakes)
    {
        var col = new Point2D(_currentHeightmap.GetWorldX(basin.Col.I), _currentHeightmap.GetWorldY(basin.Col.J));
        var beyond = new Point2D(_currentHeightmap.GetWorldX(basin.Outflow.I), _currentHeightmap.GetWorldY(basin.Outflow.J));
        var path = _currentHeightmap.SimulateSpontaneousRiver(beyond.X, beyond.Y, _currentMap.AllRivers, lakes);
        var points = new List<Point2D> { col };
        points.AddRange(path.Count > 0 ? path : [beyond]);
        if (points.Count < 3)
            return null;
        return new RiverData($"Sortie du {lake.Name}", points, Width: (double)_sliderRiverWidth.Value);
    }

    /// <summary>Retire le lac et son fleuve de sortie de la carte (le relief creusé reste, comme pour un fleuve effacé).</summary>
    private void RemoveLake(LakeData lake)
    {
        var lakes = _currentMap.AllLakes.Where(l => !ReferenceEquals(l, lake)).ToList();
        var rivers = _currentMap.AllRivers.Where(r => lake.Outlet is null || r.Name != lake.Outlet).ToList();
        _currentMap = _currentMap with
        {
            Lakes = lakes.Count > 0 ? lakes : null,
            Rivers = rivers,
            River = rivers.Count > 0 ? rivers[0] : new RiverData("", []),
        };
    }

    /// <summary>Efface tous les lacs et leurs fleuves de sortie (annulable ; le relief creusé reste).</summary>
    public void ClearAllLakes()
    {
        var lakes = _currentMap.AllLakes;
        if (lakes.Count == 0)
            return;
        PushUndo();
        foreach (var lake in lakes)
            RemoveLake(lake);
        _canvas.SetMap(_currentMap, _currentHeightmap, GetCurrentStyle());
        Log($"🗑 {lakes.Count} lac{(lakes.Count > 1 ? "s" : "")} effacé{(lakes.Count > 1 ? "s" : "")}, avec leurs fleuves de sortie (Ctrl+Z pour annuler ; la vallée creusée reste).");
        UpdateLakeContext();
    }

    private static string LakeDepthText(Heightmap.Basin basin, double level) =>
        $"profondeur {level - basin.Bottom:F1} m (seuil à {basin.Spill:F1} m)";

    private void UpdateLakeContext()
    {
        _contextBar.UpdateLakeCount(_currentMap.AllLakes.Count);
        _contextBar.UpdateRiverButtons(_currentMap.AllRivers.Count);
    }
}
