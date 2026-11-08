using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;

namespace Demos.Carte;

/// <summary>
/// Gestionnaire de masque matriciel continu pour peindre et gommer
/// les biomes / milieux naturels (forêt, terre fertile, marais, plage, falaise, désert, glacier)
/// et les nappes de gisements miniers (pierre, charbon, fer)
/// avec un pinceau circulaire, tout en extrayant des contours vectoriels parfaits (Marching Squares).
/// </summary>
public sealed class ZoneMaskGrid
{
    public int Columns { get; }
    public int Rows { get; }
    public double WorldWidth { get; }
    public double WorldHeight { get; }

    public double CellWidth => WorldWidth / (Columns - 1);
    public double CellHeight => WorldHeight / (Rows - 1);

    public static readonly (string Key, string Label, string DefaultName)[] ResourceTypes =
    [
        ("pierre", "🪨 Pierre (Carrière)", "Carrière de pierre"),
        ("charbon", "⛏️ Charbon (Bassin houiller)", "Bassin houiller"),
        ("fer", "⛓️ Fer (Gisement ferrifère)", "Gisement de fer")
    ];

    public static readonly (string Key, string Label, string DefaultName, string Rule)[] BiomeTypes =
    [
        ("foret", "🌲 Forêt", "Bois des Chênes", "Terre émergée sous les glaciers (< 2 500 m)"),
        ("fertile", "🌾 Terre fertile", "Plaine fertile", "Terre émergée en plaine ou coteaux (< 600 m, pente tolérée < 60 %)"),
        ("marais", "🌾💧 Marais", "Grand Marais", "Près de l'eau (littoral bas ou le long d'un fleuve)"),
        ("plage", "🏖️ Plage", "Plage dorée", "Interface immédiate terre/mer (littoral ≤ 30 m)"),
        ("falaise", "🧗 Falaise", "Falaises rocheuses", "Forte pente (dénivelé abrupt ≥ 60 %) ou rivage à-pic"),
        ("desert", "🏜️ Désert", "Désert aride", "Plaine aride (< 250 m, pente douce)"),
        ("glacier", "❄️ Glacier", "Glacier éternel", "Haute altitude (≥ 2 500 m)")
    ];

    private readonly byte[,] _biome;    // 0 = rien, 1 = foret, 2 = fertile, 3 = marais, 4 = plage, 5 = falaise, 6 = desert, 7 = glacier
    private readonly byte[,] _resource; // 0 = rien, 1 = pierre, 2 = charbon, 3 = fer

    public ZoneMaskGrid(int columns, int rows, double worldWidth, double worldHeight)
    {
        Columns = columns;
        Rows = rows;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        _biome = new byte[columns, rows];
        _resource = new byte[columns, rows];
    }

    public double GetWorldX(int col) => col * CellWidth;
    public double GetWorldY(int row) => row * CellHeight;

    public ZoneMaskGrid Clone()
    {
        var clone = new ZoneMaskGrid(Columns, Rows, WorldWidth, WorldHeight);
        Array.Copy(_biome, clone._biome, _biome.Length);
        Array.Copy(_resource, clone._resource, _resource.Length);
        return clone;
    }

    public void SetCellForTesting(int i, int j, byte biomeId)
    {
        if (i >= 0 && i < Columns && j >= 0 && j < Rows)
        {
            _biome[i, j] = biomeId;
        }
    }

    public void ClearBiomes()
    {
        Array.Clear(_biome, 0, _biome.Length);
    }

    public void ClearBiome(string key)
    {
        var id = BiomeKeyToId(key);
        for (var i = 0; i < Columns; i++)
        {
            for (var j = 0; j < Rows; j++)
            {
                if (_biome[i, j] == id)
                    _biome[i, j] = 0;
            }
        }
    }

    public void ClearForest() => ClearBiome("foret");

    public void ClearResources()
    {
        Array.Clear(_resource, 0, _resource.Length);
    }

    public static byte ResourceKeyToId(string key) => key.ToLowerInvariant() switch
    {
        "pierre" => 1,
        "charbon" => 2,
        "fer" => 3,
        _ => 1
    };

    public static string ResourceIdToKey(byte id) => id switch
    {
        1 => "pierre",
        2 => "charbon",
        3 => "fer",
        _ => "pierre"
    };

    public static string ResourceIdToDefaultName(byte id) => id switch
    {
        1 => "Carrière de pierre",
        2 => "Bassin houiller",
        3 => "Gisement de fer",
        _ => "Gisement"
    };

    public static byte BiomeKeyToId(string key) => key.ToLowerInvariant() switch
    {
        "foret" or "forest" => 1,
        "fertile" or "zone fertile" or "ble" or "lin" => 2,
        "marais" => 3,
        "plage" => 4,
        "falaise" => 5,
        "desert" => 6,
        "glacier" => 7,
        _ => 1
    };

    public static string BiomeIdToKey(byte id) => id switch
    {
        1 => "foret",
        2 => "fertile",
        3 => "marais",
        4 => "plage",
        5 => "falaise",
        6 => "desert",
        7 => "glacier",
        _ => "foret"
    };

    public static string BiomeIdToDefaultName(byte id) => id switch
    {
        1 => "Bois des Chênes",
        2 => "Plaine fertile",
        3 => "Grand Marais",
        4 => "Plage dorée",
        5 => "Falaises rocheuses",
        6 => "Désert aride",
        7 => "Glacier éternel",
        _ => "Massif naturel"
    };

    /// <summary>
    /// Peint ou gomme un biome avec validation en direct des contraintes géographiques.
    /// </summary>
    public bool ApplyBiomeBrush(
        double worldX,
        double worldY,
        double radius,
        string biomeKey,
        bool isAdding,
        Heightmap heightmap,
        IReadOnlyList<RiverData>? rivers,
        out string? validationError)
    {
        validationError = null;
        if (radius <= 0) return false;

        var key = biomeKey.ToLowerInvariant();
        var id = BiomeKeyToId(key);

        var minI = Math.Clamp((int)Math.Floor((worldX - radius) / CellWidth), 0, Columns - 1);
        var maxI = Math.Clamp((int)Math.Ceiling((worldX + radius) / CellWidth), 0, Columns - 1);
        var minJ = Math.Clamp((int)Math.Floor((worldY - radius) / CellHeight), 0, Rows - 1);
        var maxJ = Math.Clamp((int)Math.Ceiling((worldY + radius) / CellHeight), 0, Rows - 1);

        var modifiedAny = false;
        var validCellsCount = 0;
        var totalInRadius = 0;

        for (var i = minI; i <= maxI; i++)
        {
            var gx = GetWorldX(i);
            for (var j = minJ; j <= maxJ; j++)
            {
                var gy = GetWorldY(j);
                var dx = gx - worldX;
                var dy = gy - worldY;
                if ((dx * dx + dy * dy) > (radius * radius)) continue;

                totalInRadius++;

                if (!isAdding)
                {
                    if (_biome[i, j] != 0)
                    {
                        _biome[i, j] = 0;
                        modifiedAny = true;
                    }
                    continue;
                }

                var alt = heightmap.SampleElevation(gx, gy);
                if (alt <= 0.0) continue; // Tous les biomes terrestres nécessitent une terre émergée

                var isValid = false;
                switch (key)
                {
                    case "foret" or "forest":
                        isValid = alt < 2500.0;
                        break;

                    case "glacier":
                        isValid = alt >= 2500.0;
                        break;

                    case "desert":
                        var slopeD = ComputeSlope(gx, gy, heightmap);
                        isValid = alt < 250.0 && slopeD < 0.20;
                        break;

                    case "fertile" or "zone fertile":
                        var slopeF = ComputeSlope(gx, gy, heightmap);
                        isValid = alt < 600.0 && slopeF < 0.60;
                        break;

                    case "plage":
                        isValid = alt <= 30.0 && IsNearSea(gx, gy, heightmap, 22.0);
                        break;

                    case "marais":
                        isValid = alt < 400.0 && (IsNearSea(gx, gy, heightmap, 25.0) || IsNearRiver(gx, gy, rivers, 25.0));
                        break;

                    case "falaise":
                        var slopeC = ComputeSlope(gx, gy, heightmap);
                        var isCoastalCliff = IsNearSea(gx, gy, heightmap, 16.0) && alt >= 20.0 && slopeC >= 0.40;
                        isValid = slopeC >= 0.60 || isCoastalCliff;
                        break;
                }

                if (isValid)
                {
                    validCellsCount++;
                    if (_biome[i, j] != id)
                    {
                        _biome[i, j] = id;
                        modifiedAny = true;
                    }
                }
            }
        }

        if (isAdding && validCellsCount == 0 && totalInRadius > 0)
        {
            validationError = key switch
            {
                "glacier" => "❄️ Un glacier requiert une haute altitude (≥ 2 500 m).",
                "foret" or "forest" => "🌲 La forêt s'arrête sous la ligne des glaciers (< 2 500 m).",
                "plage" => "🏖️ Une plage doit border le littoral (interface terre/mer).",
                "marais" => "🌾💧 Un marais doit border un littoral bas ou un cours d'eau.",
                "falaise" => "🧗 Une falaise requiert une pente abrupte (≥ 60 %) ou un rivage à-pic (alt. ≥ 20 m).",
                "desert" => "🏜️ Un désert ne s'étend que dans les plaines (< 250 m, pente douce).",
                "fertile" or "zone fertile" => "🌾 Les terres fertiles requièrent un terrain cultivable (< 600 m, pente < 60 %).",
                _ => null
            };
        }

        return modifiedAny;
    }

    /// <summary>
    /// Wrapper de rétrocompatibilité pour peindre la forêt.
    /// </summary>
    public void ApplyForestBrush(double worldX, double worldY, double radius, bool isAdding)
    {
        var id = (byte)1;
        var minI = Math.Clamp((int)Math.Floor((worldX - radius) / CellWidth), 0, Columns - 1);
        var maxI = Math.Clamp((int)Math.Ceiling((worldX + radius) / CellWidth), 0, Columns - 1);
        var minJ = Math.Clamp((int)Math.Floor((worldY - radius) / CellHeight), 0, Rows - 1);
        var maxJ = Math.Clamp((int)Math.Ceiling((worldY + radius) / CellHeight), 0, Rows - 1);

        var r2 = radius * radius;
        for (var i = minI; i <= maxI; i++)
        {
            var gx = GetWorldX(i);
            for (var j = minJ; j <= maxJ; j++)
            {
                var gy = GetWorldY(j);
                var d2 = (gx - worldX) * (gx - worldX) + (gy - worldY) * (gy - worldY);
                if (d2 <= r2)
                {
                    _biome[i, j] = isAdding ? id : (byte)0;
                }
            }
        }
    }

    /// <summary>
    /// Peint ou gomme une zone de ressource minière spécifique au pinceau circulaire.
    /// </summary>
    public void ApplyResourceBrush(double worldX, double worldY, double radius, string resourceKey, bool isAdding)
    {
        if (radius <= 0) return;
        var resId = ResourceKeyToId(resourceKey);

        var minI = Math.Clamp((int)Math.Floor((worldX - radius) / CellWidth), 0, Columns - 1);
        var maxI = Math.Clamp((int)Math.Ceiling((worldX + radius) / CellWidth), 0, Columns - 1);
        var minJ = Math.Clamp((int)Math.Floor((worldY - radius) / CellHeight), 0, Rows - 1);
        var maxJ = Math.Clamp((int)Math.Ceiling((worldY + radius) / CellHeight), 0, Rows - 1);

        var r2 = radius * radius;
        for (var i = minI; i <= maxI; i++)
        {
            var gx = GetWorldX(i);
            for (var j = minJ; j <= maxJ; j++)
            {
                var gy = GetWorldY(j);
                var d2 = (gx - worldX) * (gx - worldX) + (gy - worldY) * (gy - worldY);
                if (d2 <= r2)
                {
                    _resource[i, j] = isAdding ? resId : (byte)0;
                }
            }
        }
    }

    /// <summary>
    /// Extrait les polygones vectoriels de tous les biomes peints par Marching Squares.
    /// </summary>
    public List<PolygonArea> ExtractBiomes(string? activeKey = null, string? preferredName = null)
    {
        var biomes = new List<PolygonArea>();

        for (byte id = 1; id <= 7; id++)
        {
            var key = BiomeIdToKey(id);
            var defaultName = BiomeIdToDefaultName(id);

            var binary = new double[Columns, Rows];
            var count = 0;
            for (var i = 0; i < Columns; i++)
            {
                for (var j = 0; j < Rows; j++)
                {
                    if (_biome[i, j] == id)
                    {
                        binary[i, j] = 1.0;
                        count++;
                    }
                }
            }

            if (count == 0) continue;

            var smoothed = SmoothGrid(binary);
            var structured = ExtractStructuredBiomeLoops(smoothed, 0.45);

            var zoneIdx = 1;
            foreach (var (outer, holes) in structured)
            {
                if (outer.Count < 3) continue;

                string name;
                if (!string.IsNullOrWhiteSpace(preferredName) && string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase))
                {
                    name = structured.Count == 1 ? preferredName : $"{preferredName} ({zoneIdx})";
                }
                else
                {
                    name = structured.Count == 1 ? defaultName : $"{defaultName} ({zoneIdx})";
                }

                biomes.Add(new PolygonArea(name, key, outer, holes.Count > 0 ? holes : null));
                zoneIdx++;
            }
        }

        return biomes;
    }

    /// <summary>
    /// Surcharge rétrocompatible d'extraction des biomes.
    /// </summary>
    public List<PolygonArea> ExtractBiomes(string? preferredName) => ExtractBiomes(null, preferredName);

    /// <summary>
    /// Extrait les forêts (rétrocompatibilité avec l'ancien modèle).
    /// </summary>
    public List<PolygonArea> ExtractForests(string forestName = "Bois des Chênes")
    {
        var allBiomes = ExtractBiomes("foret", forestName);
        return allBiomes.Where(b => b.Kind is "foret" or "Forest").ToList();
    }

    /// <summary>
    /// Extrait les zones vectorielles de gisements miniers (pierre, charbon, fer) et leurs dépôts.
    /// </summary>
    public (List<PolygonArea> Zones, List<ResourceDeposit> Deposits) ExtractResources(string? preferredName = null)
    {
        var zones = new List<PolygonArea>();
        var deposits = new List<ResourceDeposit>();

        for (byte id = 1; id <= 3; id++)
        {
            var key = ResourceIdToKey(id);
            var defaultName = ResourceIdToDefaultName(id);

            var binary = new double[Columns, Rows];
            var count = 0;
            for (var i = 0; i < Columns; i++)
            {
                for (var j = 0; j < Rows; j++)
                {
                    if (_resource[i, j] == id)
                    {
                        binary[i, j] = 1.0;
                        count++;
                    }
                }
            }

            if (count == 0) continue;

            var smoothed = SmoothGrid(binary);
            var structured = ExtractStructuredBiomeLoops(smoothed, 0.45);

            var zoneIdx = 1;
            foreach (var (outer, holes) in structured)
            {
                if (outer.Count < 3) continue;

                var name = !string.IsNullOrWhiteSpace(preferredName) && zones.Count == 0
                    ? preferredName
                    : (structured.Count == 1 ? defaultName : $"{defaultName} {zoneIdx}");

                zones.Add(new PolygonArea(name, key, outer, holes.Count > 0 ? holes : null));

                var depositPt = FindInternalPoint(outer, holes) ?? new Point2D(outer.Average(p => p.X), outer.Average(p => p.Y));
                deposits.Add(new ResourceDeposit(key, name, new Point2D(Math.Round(depositPt.X, 1), Math.Round(depositPt.Y, 1))));

                zoneIdx++;
            }
        }

        return (zones, deposits);
    }

    /// <summary>
    /// Remplissage du masque à partir des données vectorielles existantes d'une carte (chargement de carte.json).
    /// </summary>
    public void RasterizeFromMap(VectorMapDefinition map)
    {
        Array.Clear(_biome, 0, _biome.Length);
        Array.Clear(_resource, 0, _resource.Length);

        // 1. Biomes existants ou forêts rétrocompatibles
        var sourceBiomes = map.AllBiomes;
        foreach (var biome in sourceBiomes)
        {
            if (biome.Boundary.Count < 3) continue;
            var biomeId = BiomeKeyToId(biome.Kind);
            RasterizePolygonIntoBiome(biome.Boundary, biomeId);
            if (biome.Holes is { Count: > 0 } holes)
            {
                foreach (var hole in holes)
                {
                    if (hole.Count >= 3)
                        RasterizePolygonIntoBiome(hole, 0);
                }
            }
        }

        // 2. Zones de gisements existantes (pierre, charbon, fer)
        if (map.ResourceZones is { Count: > 0 } zones)
        {
            foreach (var zone in zones)
            {
                if (zone.Boundary.Count < 3) continue;
                // Si l'ancienne carte avait une zone fertile dans les gisements, la migrer vers les biomes
                if (zone.Kind.Equals("fertile", StringComparison.OrdinalIgnoreCase) || zone.Kind.Equals("zone fertile", StringComparison.OrdinalIgnoreCase))
                {
                    RasterizePolygonIntoBiome(zone.Boundary, 2);
                    if (zone.Holes is { Count: > 0 } holes)
                    {
                        foreach (var hole in holes)
                        {
                            if (hole.Count >= 3)
                                RasterizePolygonIntoBiome(hole, 0);
                        }
                    }
                }
                else
                {
                    var resId = ResourceKeyToId(zone.Kind);
                    RasterizePolygonIntoResource(zone.Boundary, resId);
                    if (zone.Holes is { Count: > 0 } holes)
                    {
                        foreach (var hole in holes)
                        {
                            if (hole.Count >= 3)
                                RasterizePolygonIntoResource(hole, 0);
                        }
                    }
                }
            }
        }
        else if (map.Resources.Count > 0)
        {
            foreach (var res in map.Resources)
            {
                if (res.Resource.Equals("fertile", StringComparison.OrdinalIgnoreCase))
                {
                    // Migrer vers biome fertile
                    var r2 = 35.0 * 35.0;
                    var minI = Math.Clamp((int)Math.Floor((res.Position.X - 35.0) / CellWidth), 0, Columns - 1);
                    var maxI = Math.Clamp((int)Math.Ceiling((res.Position.X + 35.0) / CellWidth), 0, Columns - 1);
                    var minJ = Math.Clamp((int)Math.Floor((res.Position.Y - 35.0) / CellHeight), 0, Rows - 1);
                    var maxJ = Math.Clamp((int)Math.Ceiling((res.Position.Y + 35.0) / CellHeight), 0, Rows - 1);
                    for (var i = minI; i <= maxI; i++)
                    {
                        var gx = GetWorldX(i);
                        for (var j = minJ; j <= maxJ; j++)
                        {
                            var gy = GetWorldY(j);
                            var d2 = (gx - res.Position.X) * (gx - res.Position.X) + (gy - res.Position.Y) * (gy - res.Position.Y);
                            if (d2 <= r2) _biome[i, j] = 2;
                        }
                    }
                    continue;
                }
                ApplyResourceBrush(res.Position.X, res.Position.Y, 35.0, res.Resource, true);
            }
        }
    }

    private void RasterizePolygonIntoBiome(IReadOnlyList<Point2D> poly, byte biomeId)
    {
        var minX = poly.Min(p => p.X);
        var maxX = poly.Max(p => p.X);
        var minY = poly.Min(p => p.Y);
        var maxY = poly.Max(p => p.Y);

        var minI = Math.Clamp((int)Math.Floor(minX / CellWidth), 0, Columns - 1);
        var maxI = Math.Clamp((int)Math.Ceiling(maxX / CellWidth), 0, Columns - 1);
        var minJ = Math.Clamp((int)Math.Floor(minY / CellHeight), 0, Rows - 1);
        var maxJ = Math.Clamp((int)Math.Ceiling(maxY / CellHeight), 0, Rows - 1);

        for (var i = minI; i <= maxI; i++)
        {
            var gx = GetWorldX(i);
            for (var j = minJ; j <= maxJ; j++)
            {
                var gy = GetWorldY(j);
                if (IsPointInPolygon(gx, gy, poly))
                {
                    _biome[i, j] = biomeId;
                }
            }
        }
    }

    private void RasterizePolygonIntoResource(IReadOnlyList<Point2D> poly, byte resId)
    {
        var minX = poly.Min(p => p.X);
        var maxX = poly.Max(p => p.X);
        var minY = poly.Min(p => p.Y);
        var maxY = poly.Max(p => p.Y);

        var minI = Math.Clamp((int)Math.Floor(minX / CellWidth), 0, Columns - 1);
        var maxI = Math.Clamp((int)Math.Ceiling(maxX / CellWidth), 0, Columns - 1);
        var minJ = Math.Clamp((int)Math.Floor(minY / CellHeight), 0, Rows - 1);
        var maxJ = Math.Clamp((int)Math.Ceiling(maxY / CellHeight), 0, Rows - 1);

        for (var i = minI; i <= maxI; i++)
        {
            var gx = GetWorldX(i);
            for (var j = minJ; j <= maxJ; j++)
            {
                var gy = GetWorldY(j);
                if (IsPointInPolygon(gx, gy, poly))
                {
                    _resource[i, j] = resId;
                }
            }
        }
    }

    private static double ComputeSlope(double wx, double wy, Heightmap hm)
    {
        const double delta = 6.0;
        var dx = (hm.SampleElevation(wx + delta, wy) - hm.SampleElevation(wx - delta, wy)) / (2.0 * delta);
        var dy = (hm.SampleElevation(wx, wy + delta) - hm.SampleElevation(wx, wy - delta)) / (2.0 * delta);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool IsNearSea(double wx, double wy, Heightmap hm, double maxDist)
    {
        if (hm.SampleElevation(wx, wy) <= 0.0) return true;
        for (var a = 0; a < 8; a++)
        {
            var angle = a * (Math.PI * 2.0 / 8.0);
            var px = Math.Clamp(wx + Math.Cos(angle) * maxDist, 0, hm.WorldWidth);
            var py = Math.Clamp(wy + Math.Sin(angle) * maxDist, 0, hm.WorldHeight);
            if (hm.SampleElevation(px, py) <= 0.0) return true;
        }
        return false;
    }

    private static bool IsNearRiver(double wx, double wy, IReadOnlyList<RiverData>? rivers, double maxDist)
    {
        if (rivers is null || rivers.Count == 0) return false;
        foreach (var river in rivers)
        {
            if (river.Points.Count < 2) continue;
            for (var i = 0; i < river.Points.Count - 1; i++)
            {
                var p1 = river.Points[i];
                var p2 = river.Points[i + 1];
                var (dist, _, _) = Heightmap.DistanceAndProjectionPointToSegment(wx, wy, p1.X, p1.Y, p2.X, p2.Y);
                if (dist <= maxDist) return true;
            }
        }
        return false;
    }

    private static bool IsPointInPolygon(double x, double y, IReadOnlyList<Point2D> poly)
    {
        var inside = false;
        var count = poly.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            if (((poly[i].Y > y) != (poly[j].Y > y)) &&
                (x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private double[,] SmoothGrid(double[,] src)
    {
        var dst = new double[Columns, Rows];
        for (var i = 0; i < Columns; i++)
        {
            var i0 = Math.Max(0, i - 1);
            var i1 = Math.Min(Columns - 1, i + 1);
            for (var j = 0; j < Rows; j++)
            {
                var j0 = Math.Max(0, j - 1);
                var j1 = Math.Min(Rows - 1, j + 1);

                var sum = src[i, j] * 4.0
                    + src[i0, j] * 2.0 + src[i1, j] * 2.0 + src[i, j0] * 2.0 + src[i, j1] * 2.0
                    + src[i0, j0] + src[i1, j0] + src[i0, j1] + src[i1, j1];

                dst[i, j] = sum / 16.0;
            }
        }
        return dst;
    }

    private List<List<Point2D>> ExtractMarchingSquaresLoops(double[,] grid, double threshold)
    {
        var segments = new List<ContourExtractor.Segment>();

        for (var i = 0; i < Columns - 1; i++)
        {
            var x0 = GetWorldX(i);
            var x1 = GetWorldX(i + 1);

            for (var j = 0; j < Rows - 1; j++)
            {
                var y0 = GetWorldY(j);
                var y1 = GetWorldY(j + 1);

                var zTL = grid[i, j];
                var zTR = grid[i + 1, j];
                var zBR = grid[i + 1, j + 1];
                var zBL = grid[i, j + 1];

                var config = (zTL >= threshold ? 8 : 0) |
                             (zTR >= threshold ? 4 : 0) |
                             (zBR >= threshold ? 2 : 0) |
                             (zBL >= threshold ? 1 : 0);

                if (config is 0 or 15) continue;

                Point2D? ptTop = (config & 12) is 4 or 8 ? Interp(x0, y0, zTL, x1, y0, zTR, threshold) : null;
                Point2D? ptRight = (config & 6) is 2 or 4 ? Interp(x1, y0, zTR, x1, y1, zBR, threshold) : null;
                Point2D? ptBottom = (config & 3) is 1 or 2 ? Interp(x0, y1, zBL, x1, y1, zBR, threshold) : null;
                Point2D? ptLeft = (config & 9) is 1 or 8 ? Interp(x0, y0, zTL, x0, y1, zBL, threshold) : null;

                switch (config)
                {
                    case 1:
                    case 14:
                        if (ptBottom.HasValue && ptLeft.HasValue) segments.Add(new(ptBottom.Value, ptLeft.Value));
                        break;
                    case 2:
                    case 13:
                        if (ptRight.HasValue && ptBottom.HasValue) segments.Add(new(ptRight.Value, ptBottom.Value));
                        break;
                    case 3:
                    case 12:
                        if (ptLeft.HasValue && ptRight.HasValue) segments.Add(new(ptLeft.Value, ptRight.Value));
                        break;
                    case 4:
                    case 11:
                        if (ptTop.HasValue && ptRight.HasValue) segments.Add(new(ptTop.Value, ptRight.Value));
                        break;
                    case 6:
                    case 9:
                        if (ptTop.HasValue && ptBottom.HasValue) segments.Add(new(ptTop.Value, ptBottom.Value));
                        break;
                    case 7:
                    case 8:
                        if (ptLeft.HasValue && ptTop.HasValue) segments.Add(new(ptLeft.Value, ptTop.Value));
                        break;
                    case 5:
                        if (ptTop.HasValue && ptRight.HasValue) segments.Add(new(ptTop.Value, ptRight.Value));
                        if (ptLeft.HasValue && ptBottom.HasValue) segments.Add(new(ptLeft.Value, ptBottom.Value));
                        break;
                    case 10:
                        if (ptLeft.HasValue && ptTop.HasValue) segments.Add(new(ptLeft.Value, ptTop.Value));
                        if (ptBottom.HasValue && ptRight.HasValue) segments.Add(new(ptBottom.Value, ptRight.Value));
                        break;
                }
            }
        }

        var chained = ContourExtractor.ChainSegments(segments);
        var loops = new List<List<Point2D>>();

        foreach (var poly in chained)
        {
            if (poly.Count < 3) continue;

            var pts = poly.ToList();
            if (Distance(pts[0], pts[^1]) > 1.0)
            {
                pts.Add(pts[0]);
            }

            var area = 0.0;
            for (var k = 0; k < pts.Count - 1; k++)
            {
                area += pts[k].X * pts[k + 1].Y - pts[k + 1].X * pts[k].Y;
            }
            if (Math.Abs(area * 0.5) >= 100.0)
            {
                loops.Add(pts);
            }
        }

        return loops;
    }

    private static Point2D Interp(double x1, double y1, double z1, double x2, double y2, double z2, double level)
    {
        var denom = z2 - z1;
        var t = Math.Abs(denom) < 1e-6 ? 0.5 : Math.Clamp((level - z1) / denom, 0.0, 1.0);
        return new Point2D(
            Math.Round(x1 + t * (x2 - x1), 1),
            Math.Round(y1 + t * (y2 - y1), 1));
    }

    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Échantillonne la grille matricielle continue par interpolation bilinéaire.
    /// </summary>
    private double SampleGrid(double[,] grid, double wx, double wy)
    {
        var u = (wx / CellWidth) - 0.5;
        var v = (wy / CellHeight) - 0.5;
        var i0 = Math.Clamp((int)Math.Floor(u), 0, Columns - 1);
        var i1 = Math.Clamp(i0 + 1, 0, Columns - 1);
        var j0 = Math.Clamp((int)Math.Floor(v), 0, Rows - 1);
        var j1 = Math.Clamp(j0 + 1, 0, Rows - 1);
        var fx = Math.Clamp(u - i0, 0.0, 1.0);
        var fy = Math.Clamp(v - j0, 0.0, 1.0);

        var z00 = grid[i0, j0];
        var z10 = grid[i1, j0];
        var z01 = grid[i0, j1];
        var z11 = grid[i1, j1];

        var top = z00 + fx * (z10 - z00);
        var bot = z01 + fx * (z11 - z01);
        return top + fy * (bot - top);
    }

    /// <summary>
    /// Détermine si une boucle Marching Squares est un trou intérieur (vide/sommet où grid &lt; threshold)
    /// ou un contour extérieur (biome où grid &gt;= threshold).
    /// </summary>
    private bool IsHoleLoop(IReadOnlyList<Point2D> loop, double[,] grid, double threshold)
    {
        if (loop.Count < 3) return false;

        var votesBiome = 0;
        var votesVoid = 0;

        var step = Math.Max(1, loop.Count / 16);
        for (var i = 0; i < loop.Count - 1; i += step)
        {
            var p1 = loop[i];
            var p2 = loop[i + 1];
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4) continue;

            var mx = (p1.X + p2.X) * 0.5;
            var my = (p1.Y + p2.Y) * 0.5;
            var nx = -dy / len;
            var ny = dx / len;

            for (var dist = 2.0; dist <= 8.0; dist += 2.0)
            {
                var s1 = new Point2D(mx + nx * dist, my + ny * dist);
                var s2 = new Point2D(mx - nx * dist, my - ny * dist);

                var in1 = IsPointInPolygon(s1.X, s1.Y, loop);
                var in2 = IsPointInPolygon(s2.X, s2.Y, loop);

                if (in1 != in2)
                {
                    var insidePt = in1 ? s1 : s2;
                    var val = SampleGrid(grid, insidePt.X, insidePt.Y);
                    if (val >= threshold)
                        votesBiome++;
                    else
                        votesVoid++;
                }
            }
        }

        if (votesBiome + votesVoid > 0)
        {
            return votesVoid > votesBiome;
        }

        var minX = loop.Min(p => p.X);
        var maxX = loop.Max(p => p.X);
        var minY = loop.Min(p => p.Y);
        var maxY = loop.Max(p => p.Y);

        var sx = Math.Max(CellWidth, (maxX - minX) / 8.0);
        var sy = Math.Max(CellHeight, (maxY - minY) / 8.0);

        for (var x = minX + sx * 0.5; x < maxX; x += sx)
        {
            for (var y = minY + sy * 0.5; y < maxY; y += sy)
            {
                if (IsPointInPolygon(x, y, loop))
                {
                    var val = SampleGrid(grid, x, y);
                    if (val >= threshold) votesBiome++;
                    else votesVoid++;
                }
            }
        }

        if (votesBiome + votesVoid > 0)
        {
            return votesVoid > votesBiome;
        }

        var avgX = loop.Average(p => p.X);
        var avgY = loop.Average(p => p.Y);
        return SampleGrid(grid, avgX, avgY) < threshold;
    }

    private static double ComputePolygonArea(IReadOnlyList<Point2D> pts)
    {
        var area = 0.0;
        for (var k = 0; k < pts.Count - 1; k++)
        {
            area += pts[k].X * pts[k + 1].Y - pts[k + 1].X * pts[k].Y;
        }
        return Math.Abs(area * 0.5);
    }

    private static bool IsHoleInsideOuter(IReadOnlyList<Point2D> hole, IReadOnlyList<Point2D> outer)
    {
        if (hole.Count < 3 || outer.Count < 3) return false;

        var step = Math.Max(1, hole.Count / 5);
        var insideCount = 0;
        var totalTested = 0;

        for (var i = 0; i < hole.Count - 1 && totalTested < 5; i += step)
        {
            totalTested++;
            if (IsPointInPolygon(hole[i].X, hole[i].Y, outer))
            {
                insideCount++;
            }
        }

        return insideCount > totalTested / 2;
    }

    private static Point2D? FindInternalPoint(IReadOnlyList<Point2D> outer, IReadOnlyList<List<Point2D>> holes)
    {
        var avgX = outer.Average(p => p.X);
        var avgY = outer.Average(p => p.Y);
        if (IsPointInPolygon(avgX, avgY, outer) && !holes.Any(h => IsPointInPolygon(avgX, avgY, h)))
        {
            return new Point2D(avgX, avgY);
        }

        for (var i = 0; i < outer.Count - 1; i++)
        {
            var p1 = outer[i];
            var p2 = outer[i + 1];
            var mx = (p1.X + p2.X) * 0.5;
            var my = (p1.Y + p2.Y) * 0.5;
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4) continue;
            var nx = -dy / len;
            var ny = dx / len;

            foreach (var d in new[] { 5.0, 10.0, 15.0 })
            {
                var s1 = new Point2D(mx + nx * d, my + ny * d);
                if (IsPointInPolygon(s1.X, s1.Y, outer) && !holes.Any(h => IsPointInPolygon(s1.X, s1.Y, h)))
                    return s1;

                var s2 = new Point2D(mx - nx * d, my - ny * d);
                if (IsPointInPolygon(s2.X, s2.Y, outer) && !holes.Any(h => IsPointInPolygon(s2.X, s2.Y, h)))
                    return s2;
            }
        }
        return null;
    }

    /// <summary>
    /// Extrait les boucles Marching Squares pour une grille binaire lissée,
    /// puis classe et hiérarchise les boucles en contours extérieurs (positives)
    /// et trous intérieurs (négatifs), en associant chaque trou à son contour parent.
    /// Élimine définitivement le bug d'inversion des falaises / sommets.
    /// </summary>
    private List<(List<Point2D> Outer, List<List<Point2D>> Holes)> ExtractStructuredBiomeLoops(double[,] grid, double threshold)
    {
        var allLoops = ExtractMarchingSquaresLoops(grid, threshold);
        if (allLoops.Count == 0) return [];

        var outerLoops = new List<List<Point2D>>();
        var holeLoops = new List<List<Point2D>>();

        foreach (var loop in allLoops)
        {
            if (loop.Count < 3) continue;

            if (IsHoleLoop(loop, grid, threshold))
            {
                holeLoops.Add(loop);
            }
            else
            {
                outerLoops.Add(loop);
            }
        }

        if (outerLoops.Count == 0) return [];

        var structured = outerLoops.Select(outer => (Outer: outer, Holes: new List<List<Point2D>>())).ToList();

        foreach (var hole in holeLoops)
        {
            var bestIdx = -1;
            var bestArea = double.MaxValue;

            for (var i = 0; i < structured.Count; i++)
            {
                var outer = structured[i].Outer;
                if (IsHoleInsideOuter(hole, outer))
                {
                    var area = ComputePolygonArea(outer);
                    if (area < bestArea)
                    {
                        bestArea = area;
                        bestIdx = i;
                    }
                }
            }

            if (bestIdx >= 0)
            {
                structured[bestIdx].Holes.Add(hole);
            }
        }

        return structured;
    }
}
