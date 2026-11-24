using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Demos.Domain.Map;

namespace Demos.Carte;

/// <summary>
/// Morphologies procédurales pour la création d'îles naturelles.
/// </summary>
public enum IslandMorphology
{
    Natural,     // Île naturelle équilibrée
    AlpineRidge, // Massif alpin & Crêtes escarpées
    Archipelago, // Archipel & Îlots côtiers
    Fjords       // Fjords & Baies profondes
}

/// <summary>
/// Profils d'atténuation radiale pour le pinceau de relief.
/// </summary>
public enum BrushProfile
{
    DomeCos,    // Dôme doux (décroissance cosinus)
    ConeLinear, // Cône acéré (décroissance linéaire)
    PlateauFlat // Plateau abrupt (sommet plat puis tombée franche)
}

/// <summary>
/// Modèle altimétrique continu (Heightmap) : calcule l'élévation Z(x, y) en tout point,
/// avec massifs rocheux paramétrables, crêtes continues, estuaire maritime et vallée fluviale encaissée (thalweg).
/// </summary>
public sealed partial class Heightmap
{
    public int Columns { get; }
    public int Rows { get; }
    public double WorldWidth { get; }
    public double WorldHeight { get; }
    public TerrainParameters Parameters { get; }

    public double MinElevation { get; private set; }
    public double MaxElevation { get; private set; }

    private readonly double[,] _grid;

    public Heightmap(int columns, int rows, double worldWidth, double worldHeight, TerrainParameters? parameters = null)
    {
        Columns = columns;
        Rows = rows;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        Parameters = parameters ?? TerrainParameters.Default;
        _grid = new double[columns, rows];

        BuildElevation();
    }

    /// <summary>
    /// Crée une carte d'élévation vierge et plane pour concevoir une carte personnalisée de zéro.
    /// </summary>
    public static Heightmap CreateFlat(int columns, int rows, double worldWidth, double worldHeight, double baseElevation = 50.0)
    {
        return new Heightmap(columns, rows, worldWidth, worldHeight, isFlat: true, baseElevation);
    }

    /// <summary>
    /// Crée la carte d'élévation d'une carte existante à partir de son relief (celui du jeu : <see cref="ReliefGrid"/>,
    /// calculé depuis les courbes de niveau), échantillonné aux nœuds de la grille de l'éditeur.
    /// </summary>
    public static Heightmap FromRelief(ReliefGrid relief, int columns, int rows, double worldWidth, double worldHeight)
    {
        var hm = new Heightmap(columns, rows, worldWidth, worldHeight, isFlat: true, 0);
        for (var i = 0; i < columns; i++)
            for (var j = 0; j < rows; j++)
            {
                var x = i * worldWidth / (columns - 1);
                var y = j * worldHeight / (rows - 1);
                hm._grid[i, j] = Math.Round(relief.Sample(x, y), 1);
            }
        hm.UpdateMinMax();
        return hm;
    }

    /// <summary>
    /// Crée une carte d'élévation insulaire naturelle avec côtes festonnées et bords découpés (caps, baies, péninsules).
    /// </summary>
    public static Heightmap CreateIsland(int columns, int rows, double worldWidth, double worldHeight) =>
        CreateIsland(columns, rows, worldWidth, worldHeight, seed: 42, IslandMorphology.Natural, maxAltitude: 2800.0);

    /// <summary>
    /// Crée une carte d'élévation insulaire paramétrée avec graine procédurale, morphologie de relief et altitude max.
    /// </summary>
    public static Heightmap CreateIsland(
        int columns,
        int rows,
        double worldWidth,
        double worldHeight,
        int seed,
        IslandMorphology morphology,
        double maxAltitude)
    {
        var hm = new Heightmap(columns, rows, worldWidth, worldHeight, isFlat: true, baseElevation: -20.0);
        var cx = worldWidth * 0.5;
        var cy = worldHeight * 0.5;
        var rx = worldWidth * 0.38;
        var ry = worldHeight * 0.38;

        var rng = new Random(seed);
        var phi1 = rng.NextDouble() * Math.PI * 2.0;
        var phi2 = rng.NextDouble() * Math.PI * 2.0;
        var phi3 = rng.NextDouble() * Math.PI * 2.0;
        var phi4 = rng.NextDouble() * Math.PI * 2.0;

        // Décalage du centre orographique / massif central (±10% de rx, ry)
        var mcx = cx + (rng.NextDouble() - 0.5) * rx * 0.25;
        var mcy = cy + (rng.NextDouble() - 0.5) * ry * 0.25;

        // Angle d'orientation de la crête pour les morphologies avec échine montagneuse
        var ridgeAngle = rng.NextDouble() * Math.PI;
        var cosRidge = Math.Cos(ridgeAngle);
        var sinRidge = Math.Sin(ridgeAngle);

        // Centres des îlots satellites pour l'archipel
        var sat1X = cx + rx * 0.88 * Math.Cos(phi1);
        var sat1Y = cy + ry * 0.88 * Math.Sin(phi1);
        var sat2X = cx + rx * 0.92 * Math.Cos(phi2);
        var sat2Y = cy + ry * 0.92 * Math.Sin(phi2);
        var satRadius = rx * 0.20;

        for (var i = 0; i < columns; i++)
        {
            var x = hm.GetWorldX(i);
            var dx = (x - cx) / rx;
            for (var j = 0; j < rows; j++)
            {
                var y = hm.GetWorldY(j);
                var dy = (y - cy) / ry;

                var angle = Math.Atan2(dy, dx);

                // Modulateur de rayon de la côte selon la morphologie et la graine
                double radiusMod;
                switch (morphology)
                {
                    case IslandMorphology.Archipelago:
                        radiusMod = 0.92
                            + 0.16 * Math.Sin(3.0 * angle + phi1)
                            + 0.14 * Math.Cos(6.0 * angle + phi2)
                            + 0.10 * Math.Sin(10.0 * angle + phi3)
                            + 0.08 * Math.Cos(15.0 * angle + phi4);
                        break;

                    case IslandMorphology.AlpineRidge:
                        var alongRidge = dx * cosRidge + dy * sinRidge;
                        var perpRidge = -dx * sinRidge + dy * cosRidge;
                        var ridgeDist = Math.Sqrt(alongRidge * alongRidge * 0.65 + perpRidge * perpRidge * 1.55);
                        radiusMod = 1.05 / Math.Max(0.5, ridgeDist / Math.Max(0.1, Math.Sqrt(dx * dx + dy * dy)))
                            + 0.12 * Math.Sin(4.0 * angle + phi1)
                            + 0.08 * Math.Cos(7.0 * angle + phi2);
                        radiusMod = Math.Clamp(radiusMod, 0.75, 1.35);
                        break;

                    case IslandMorphology.Fjords:
                        radiusMod = 0.96
                            + 0.26 * Math.Sin(4.0 * angle + phi1)
                            + 0.18 * Math.Cos(8.0 * angle + phi2)
                            + 0.12 * Math.Sin(12.0 * angle + phi3)
                            + 0.06 * Math.Cos(17.0 * angle + phi4);
                        break;

                    default: // Natural
                        radiusMod = 1.0
                            + 0.18 * Math.Sin(3.0 * angle + phi1)
                            + 0.12 * Math.Cos(5.0 * angle + phi2)
                            + 0.08 * Math.Sin(8.0 * angle + phi3)
                            + 0.05 * Math.Cos(13.0 * angle + phi4);
                        break;
                }

                var normalizedDist = Math.Sqrt(dx * dx + dy * dy) / Math.Max(0.1, radiusMod);

                // Pour l'archipel, vérifier la proximité d'un îlot satellite
                if (morphology == IslandMorphology.Archipelago && normalizedDist >= 1.0)
                {
                    var dSat1 = Math.Sqrt((x - sat1X) * (x - sat1X) + (y - sat1Y) * (y - sat1Y)) / satRadius;
                    var dSat2 = Math.Sqrt((x - sat2X) * (x - sat2X) + (y - sat2Y) * (y - sat2Y)) / satRadius;
                    var minSat = Math.Min(dSat1, dSat2);
                    if (minSat < 1.0)
                    {
                        normalizedDist = minSat;
                    }
                }

                double elev;
                if (normalizedDist < 0.28)
                {
                    // Massif central culminant vers maxAltitude
                    var inner = 1.0 - (normalizedDist / 0.28);

                    double peakProfile;
                    if (morphology == IslandMorphology.AlpineRidge)
                    {
                        var dAlong = (x - mcx) * cosRidge + (y - mcy) * sinRidge;
                        var dPerp = -(x - mcx) * sinRidge + (y - mcy) * cosRidge;
                        var spineDist = Math.Abs(dPerp) / (rx * 0.16);
                        var alongDist = Math.Abs(dAlong) / (rx * 0.40);
                        peakProfile = Math.Max(0.0, 1.0 - spineDist) * Math.Max(0.0, 1.0 - alongDist);
                    }
                    else
                    {
                        peakProfile = Math.Pow(inner, 1.4);
                    }

                    var noise = Math.Sin((x + phi1 * 100) * 0.02) * Math.Cos((y + phi2 * 100) * 0.02);
                    var baseMassif = 80.0 + (maxAltitude - 80.0) * peakProfile;
                    elev = baseMassif + 25.0 * noise;
                }
                else if (normalizedDist < 1.0)
                {
                    // Versant côtier descendant vers la plage (0 m)
                    var t = (1.0 - normalizedDist) / 0.72;
                    var noise = Math.Sin((x + phi3 * 100) * 0.035) * Math.Cos((y + phi4 * 100) * 0.035);
                    elev = 80.0 * Math.Pow(t, 1.25) + 12.0 * noise * t;
                }
                else
                {
                    // Plateforme marine et océan profond
                    var t = Math.Min(1.0, (normalizedDist - 1.0) / 0.35);
                    elev = -4.0 - 24.0 * t;
                }

                hm._grid[i, j] = Math.Round(elev, 1);
            }
        }

        hm.UpdateMinMax();
        return hm;
    }

    private Heightmap(int columns, int rows, double worldWidth, double worldHeight, bool isFlat, double baseElevation)
    {
        Columns = columns;
        Rows = rows;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        Parameters = TerrainParameters.Default;
        _grid = new double[columns, rows];

        for (var i = 0; i < columns; i++)
        {
            for (var j = 0; j < rows; j++)
            {
                _grid[i, j] = baseElevation;
            }
        }
        MinElevation = baseElevation;
        MaxElevation = baseElevation;
    }

    public double this[int i, int j] => _grid[i, j];

    public double GetWorldX(int i) => (double)i / (Columns - 1) * WorldWidth;
    public double GetWorldY(int j) => (double)j / (Rows - 1) * WorldHeight;

    /// <summary>
    /// Clone la matrice d'altitude pour l'historique d'annulation (Undo/Redo).
    /// </summary>
    public double[,] CloneGrid()
    {
        var clone = new double[Columns, Rows];
        Array.Copy(_grid, clone, _grid.Length);
        return clone;
    }

    /// <summary>
    /// Restaure la matrice d'altitude depuis un clone et recalcule les min/max.
    /// </summary>
    public void RestoreGrid(double[,] savedGrid)
    {
        Array.Copy(savedGrid, _grid, _grid.Length);
        UpdateMinMax();
    }

    /// <summary>
    /// Applique le pinceau de terrain continu selon un profil d'atténuation (dôme cosinus, cône ou plateau) :
    /// - deltaElevation > 0 : surélève le terrain (+ altitude, fait émerger de la terre si sous l'eau).
    /// - deltaElevation < 0 : creuse le terrain (- altitude, crée de la mer dès qu'on passe sous 0 m).
    /// </summary>
    public void ApplyBrush(double worldX, double worldY, double radius, double deltaElevation, BrushProfile profile = BrushProfile.DomeCos)
    {
        if (radius <= 0) return;

        var sx = WorldWidth / (Columns - 1);
        var sy = WorldHeight / (Rows - 1);

        var minI = Math.Clamp((int)Math.Floor((worldX - radius) / sx), 0, Columns - 1);
        var maxI = Math.Clamp((int)Math.Ceiling((worldX + radius) / sx), 0, Columns - 1);
        var minJ = Math.Clamp((int)Math.Floor((worldY - radius) / sy), 0, Rows - 1);
        var maxJ = Math.Clamp((int)Math.Ceiling((worldY + radius) / sy), 0, Rows - 1);

        for (var i = minI; i <= maxI; i++)
        {
            var gx = GetWorldX(i);
            for (var j = minJ; j <= maxJ; j++)
            {
                var gy = GetWorldY(j);
                var dist = Math.Sqrt((gx - worldX) * (gx - worldX) + (gy - worldY) * (gy - worldY));
                if (dist < radius)
                {
                    var t = dist / radius;
                    var falloff = profile switch
                    {
                        BrushProfile.ConeLinear => 1.0 - t,
                        BrushProfile.PlateauFlat => t < 0.65 ? 1.0 : (1.0 - (t - 0.65) / 0.35),
                        _ => Math.Cos(t * (Math.PI * 0.5))
                    };
                    var newVal = Math.Clamp(_grid[i, j] + deltaElevation * falloff, -40.0, 5000.0);
                    _grid[i, j] = Math.Round(newVal, 1);
                }
            }
        }

        UpdateMinMax();
    }

    /// <summary>
    /// Adoucit les pentes et élimine le bruit du relief sous un pinceau circulaire (flou gaussien local).
    /// </summary>
    public void ApplySmoothBrush(double worldX, double worldY, double radius, double power)
    {
        if (radius <= 0 || power <= 0) return;

        var sx = WorldWidth / (Columns - 1);
        var sy = WorldHeight / (Rows - 1);

        var minI = Math.Clamp((int)Math.Floor((worldX - radius) / sx), 0, Columns - 1);
        var maxI = Math.Clamp((int)Math.Ceiling((worldX + radius) / sx), 0, Columns - 1);
        var minJ = Math.Clamp((int)Math.Floor((worldY - radius) / sy), 0, Rows - 1);
        var maxJ = Math.Clamp((int)Math.Ceiling((worldY + radius) / sy), 0, Rows - 1);

        var countI = maxI - minI + 1;
        var countJ = maxJ - minJ + 1;
        var buffer = new double[countI, countJ];

        for (var i = minI; i <= maxI; i++)
        {
            var gx = GetWorldX(i);
            var i0 = Math.Max(0, i - 1);
            var i1 = Math.Min(Columns - 1, i + 1);

            for (var j = minJ; j <= maxJ; j++)
            {
                var gy = GetWorldY(j);
                var j0 = Math.Max(0, j - 1);
                var j1 = Math.Min(Rows - 1, j + 1);

                var cur = _grid[i, j];
                var dist = Math.Sqrt((gx - worldX) * (gx - worldX) + (gy - worldY) * (gy - worldY));
                if (dist < radius)
                {
                    var sum = cur * 4.0
                        + _grid[i0, j] * 2.0 + _grid[i1, j] * 2.0 + _grid[i, j0] * 2.0 + _grid[i, j1] * 2.0
                        + _grid[i0, j0] + _grid[i1, j0] + _grid[i0, j1] + _grid[i1, j1];
                    var blurred = sum / 16.0;

                    var falloff = Math.Cos((dist / radius) * (Math.PI * 0.5));
                    var factor = Math.Clamp(power * falloff, 0.0, 1.0);
                    buffer[i - minI, j - minJ] = Math.Clamp(cur + (blurred - cur) * factor, -40.0, 5000.0);
                }
                else
                {
                    buffer[i - minI, j - minJ] = cur;
                }
            }
        }

        for (var i = minI; i <= maxI; i++)
        {
            for (var j = minJ; j <= maxJ; j++)
            {
                _grid[i, j] = Math.Round(buffer[i - minI, j - minJ], 1);
            }
        }

        UpdateMinMax();
    }

    /// <summary>
    /// Aplanit le relief en plateau sous un pinceau circulaire vers une altitude de référence cible.
    /// </summary>
    public void ApplyFlattenBrush(double worldX, double worldY, double radius, double targetAltitude, double power)
    {
        if (radius <= 0 || power <= 0) return;

        var sx = WorldWidth / (Columns - 1);
        var sy = WorldHeight / (Rows - 1);

        var minI = Math.Clamp((int)Math.Floor((worldX - radius) / sx), 0, Columns - 1);
        var maxI = Math.Clamp((int)Math.Ceiling((worldX + radius) / sx), 0, Columns - 1);
        var minJ = Math.Clamp((int)Math.Floor((worldY - radius) / sy), 0, Rows - 1);
        var maxJ = Math.Clamp((int)Math.Ceiling((worldY + radius) / sy), 0, Rows - 1);

        for (var i = minI; i <= maxI; i++)
        {
            var gx = GetWorldX(i);
            for (var j = minJ; j <= maxJ; j++)
            {
                var gy = GetWorldY(j);
                var dist = Math.Sqrt((gx - worldX) * (gx - worldX) + (gy - worldY) * (gy - worldY));
                if (dist < radius)
                {
                    var cur = _grid[i, j];
                    var falloff = Math.Cos((dist / radius) * (Math.PI * 0.5));
                    var factor = Math.Clamp(power * falloff, 0.0, 1.0);
                    var newVal = Math.Clamp(cur + (targetAltitude - cur) * factor, -40.0, 5000.0);
                    _grid[i, j] = Math.Round(newVal, 1);
                }
            }
        }

        UpdateMinMax();
    }

    /// <summary>Le lit d'un fleuve ne descend pas sous cette altitude avant la mer (m).</summary>
    private const double RiverBedFloor = 0.5;

    /// <summary>
    /// Pente minimale du lit creusé, en m par unité de carte (0,01 = 0,1 m par km, proposition du 24 novembre 2026) : un
    /// lit creusé n'est jamais plat, et un fleuve retracé dedans le suit jusqu'au bout.
    /// </summary>
    private const double RiverBedMinSlope = 0.01;

    private static double Distance(Point2D a, Point2D b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// Creuse la vallée fluviale (thalweg) le long d'un tracé de fleuve. Le lit descend toujours de la source vers
    /// l'embouchure (corrigé le 24 novembre 2026 : l'ancien creusement, proportionnel à l'altitude du lieu, laissait des
    /// bosses, et le fleuve remontait par endroits, surtout sur les pentes faibles) : le lit visé est l'altitude du
    /// terrain moins la profondeur de la vallée, jamais plus haut qu'en amont ; une bosse est entaillée jusqu'à lui.
    /// Autour, la vallée rejoint le terrain en douceur. Le terrain n'est jamais relevé.
    /// Lacs (24 novembre 2026) : un fleuve qui finit dans un lac a son lit au-dessus du niveau du lac ; la rive d'un lac
    /// (ses cases et celles qui les touchent) n'est jamais creusée sous son niveau, pour que le lac ne se vide pas.
    /// </summary>
    public void DigRiverThalweg(IReadOnlyList<Point2D> riverPoints, double maxDepth = 45.0, double influenceRadius = 60.0,
        IReadOnlyList<LakeData>? lakes = null)
    {
        if (riverPoints.Count < 2) return;
        var shore = ShoreLevels(lakes ?? []);
        var end = riverPoints[^1];
        var baseLevel = (lakes ?? []).FirstOrDefault(l => l.Contains(end))?.Level ?? 0.0;

        // 1. Le lit visé à chaque point du tracé, de la source vers l'aval : toujours un peu plus bas qu'en amont, d'au
        //    moins RiverBedMinSlope (24 novembre 2026 : un lit plat par paliers égarait le fleuve retracé dedans). Le
        //    plancher monte avec la distance à l'embouchure, de la même pente, pour que le lit descende jusqu'au bout.
        var toEnd = new double[riverPoints.Count];
        for (var k = riverPoints.Count - 2; k >= 0; k--)
            toEnd[k] = toEnd[k + 1] + Distance(riverPoints[k], riverPoints[k + 1]);

        // Sur une plaine plus plate que cette pente, le lit descend de ce que le terrain permet entre la source et la mer.
        var sourceGround = SampleElevation(riverPoints[0].X, riverPoints[0].Y);
        var slope = toEnd[0] <= 0 ? 0.0 : Math.Clamp((sourceGround - baseLevel - RiverBedFloor) / toEnd[0], 0.0, RiverBedMinSlope);

        var bed = new double[riverPoints.Count];
        var upstream = double.PositiveInfinity;
        for (var k = 0; k < riverPoints.Count; k++)
        {
            var ground = SampleElevation(riverPoints[k].X, riverPoints[k].Y);
            var fall = k == 0 ? 0.0 : slope * Distance(riverPoints[k - 1], riverPoints[k]);
            if (ground <= 0.0)
            {
                // La mer : rien à creuser, le lit y finit.
                bed[k] = Math.Min(ground, upstream - fall);
                upstream = bed[k];
                continue;
            }
            var target = ground - Math.Min(maxDepth, ground * 0.45);
            var floor = baseLevel + RiverBedFloor + slope * toEnd[k];
            bed[k] = Math.Max(floor, Math.Min(target, upstream - fall));
            upstream = bed[k];
        }

        // 2. Les cases autour du tracé : au fond du lit tout près (moins d'une case), puis la vallée remonte vers le
        //    terrain (gaussienne). Seulement dans le cadre du tracé élargi de la vallée.
        var cell = Math.Max(WorldWidth / (Columns - 1), WorldHeight / (Rows - 1));
        var core = cell * 0.75;
        var width = influenceRadius * 0.4;
        var minX = riverPoints.Min(p => p.X) - influenceRadius;
        var maxX = riverPoints.Max(p => p.X) + influenceRadius;
        var minY = riverPoints.Min(p => p.Y) - influenceRadius;
        var maxY = riverPoints.Max(p => p.Y) + influenceRadius;
        var i0 = Math.Clamp((int)Math.Floor(minX / (WorldWidth / (Columns - 1))), 0, Columns - 1);
        var i1 = Math.Clamp((int)Math.Ceiling(maxX / (WorldWidth / (Columns - 1))), 0, Columns - 1);
        var j0 = Math.Clamp((int)Math.Floor(minY / (WorldHeight / (Rows - 1))), 0, Rows - 1);
        var j1 = Math.Clamp((int)Math.Ceiling(maxY / (WorldHeight / (Rows - 1))), 0, Rows - 1);

        for (var i = i0; i <= i1; i++)
        {
            var wx = GetWorldX(i);
            for (var j = j0; j <= j1; j++)
            {
                var elev = _grid[i, j];
                if (elev <= 0.0)
                    continue;
                var wy = GetWorldY(j);
                var (dist, bedHere) = NearestBed(wx, wy, riverPoints, bed);
                if (dist >= influenceRadius || bedHere >= elev)
                    continue;
                var rise = dist <= core ? 0.0 : 1.0 - Math.Exp(-Math.Pow((dist - core) / width, 2));
                var lowered = bedHere + (elev - bedHere) * rise;
                if (shore is not null && !double.IsNaN(shore[i, j]))
                    lowered = Math.Max(lowered, shore[i, j]);
                // Au centimètre : arrondi au décimètre, la pente minimale du lit (10 cm par case, souvent moins sur une plaine très plate) disparaîtrait.
                _grid[i, j] = Math.Round(Math.Min(elev, lowered), 2);
            }
        }

        UpdateMinMax();
        InvalidateHillshadeCache();
    }

    /// <summary>La distance au tracé et l'altitude du lit au point du tracé le plus proche (interpolée sur le segment).</summary>
    private static (double Dist, double Bed) NearestBed(double x, double y, IReadOnlyList<Point2D> points, double[] bed)
    {
        var best = double.MaxValue;
        var bestBed = bed[0];
        for (var k = 0; k < points.Count - 1; k++)
        {
            var a = points[k];
            var b = points[k + 1];
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var lenSq = dx * dx + dy * dy;
            var t = lenSq < 1e-6 ? 0.0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / lenSq, 0.0, 1.0);
            var px = a.X + t * dx - x;
            var py = a.Y + t * dy - y;
            var d = Math.Sqrt(px * px + py * py);
            if (d < best)
            {
                best = d;
                bestBed = bed[k] + (bed[k + 1] - bed[k]) * t;
            }
        }
        return (best, bestBed);
    }

    private Bitmap? _cachedHillshadeIgn;
    private Bitmap? _cachedHillshadeVintage;

    public void InvalidateHillshadeCache()
    {
        _cachedHillshadeIgn?.Dispose();
        _cachedHillshadeIgn = null;
        _cachedHillshadeVintage?.Dispose();
        _cachedHillshadeVintage = null;
    }

    /// <summary>
    /// Génère ou retourne l'image d'estompage topographique (Hillshading continu 3D)
    /// calculée par illumination physique Nord-Ouest (azimut 315°, zénith 45°).
    /// </summary>
    public Bitmap GetOrCreateHillshadeBitmap(MapRenderStyle style)
    {
        if (style == MapRenderStyle.IgnPastel && _cachedHillshadeIgn is not null)
            return _cachedHillshadeIgn;
        if (style == MapRenderStyle.VintageParchment1874 && _cachedHillshadeVintage is not null)
            return _cachedHillshadeVintage;

        var bmp = ComputeHillshadeBitmap(style);
        if (style == MapRenderStyle.IgnPastel)
            _cachedHillshadeIgn = bmp;
        else
            _cachedHillshadeVintage = bmp;

        return bmp;
    }

    private Bitmap ComputeHillshadeBitmap(MapRenderStyle style)
    {
        var bmp = new Bitmap(Columns, Rows, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, Columns, Rows);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            var dx = WorldWidth / Math.Max(1, Columns - 1);
            var dy = WorldHeight / Math.Max(1, Rows - 1);

            // Facteur de relief équilibré pour un estompage naturel et harmonieux
            const double zFactor = 0.75;

            var isVintage = style == MapRenderStyle.VintageParchment1874;

            // Palette d'ombre et de lumière
            var shadowR = isVintage ? (byte)55 : (byte)40;
            var shadowG = isVintage ? (byte)38 : (byte)35;
            var shadowB = isVintage ? (byte)24 : (byte)50;
            var maxShadowAlpha = isVintage ? 100 : 85;

            var sunR = (byte)255;
            var sunG = isVintage ? (byte)250 : (byte)255;
            var sunB = isVintage ? (byte)230 : (byte)245;
            var maxSunAlpha = isVintage ? 55 : 45;

            // Illumination Nord-Ouest (Azimut 315°, Zénith 45°)
            const double sunZenithRad = 45.0 * Math.PI / 180.0;
            const double sunAzimuthRad = 315.0 * Math.PI / 180.0; // NW

            var cosZenith = Math.Cos(sunZenithRad);
            var sinZenith = Math.Sin(sunZenithRad);

            var pixels = new int[Columns * Rows];

            for (var j = 0; j < Rows; j++)
            {
                var j0 = Math.Max(0, j - 1);
                var j1 = Math.Min(Rows - 1, j + 1);
                var stepY = (j1 - j0) * dy;
                if (stepY <= 0) stepY = 1.0;

                var rowOffset = j * Columns;

                for (var i = 0; i < Columns; i++)
                {
                    var elev = _grid[i, j];

                    // Pas d'estompage sur l'eau (altitude <= 0)
                    if (elev <= 0)
                    {
                        pixels[rowOffset + i] = 0;
                        continue;
                    }

                    var i0 = Math.Max(0, i - 1);
                    var i1 = Math.Min(Columns - 1, i + 1);
                    var stepX = (i1 - i0) * dx;
                    if (stepX <= 0) stepX = 1.0;

                    // Dérivées partielles
                    var dzdx = ((_grid[i1, j] - _grid[i0, j]) / stepX) * zFactor;
                    // Y écran descend vers le bas, donc Y mathématique (Nord) est inversé
                    var dzdy = -((_grid[i, j1] - _grid[i, j0]) / stepY) * zFactor;

                    var slopeRad = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy));
                    var aspectRad = Math.Atan2(dzdy, -dzdx);
                    if (aspectRad < 0) aspectRad += 2.0 * Math.PI;

                    // Formule standard de Hillshade Lambertian (valeur entre 0 et 1)
                    var hillshade = (cosZenith * Math.Cos(slopeRad)) + (sinZenith * Math.Sin(slopeRad) * Math.Cos(sunAzimuthRad - aspectRad));
                    hillshade = Math.Clamp(hillshade, 0.0, 1.0);

                    // Valeur sur terrain plat (slope = 0) : hillshade = cosZenith * 1 = 0.7071
                    var diff = hillshade - cosZenith;

                    if (diff > 0.01)
                    {
                        // Versant éclairé (Adret)
                        var intensity = Math.Min(1.0, diff / (1.0 - cosZenith));
                        var alpha = (int)(intensity * maxSunAlpha);
                        pixels[rowOffset + i] = (alpha << 24) | (sunR << 16) | (sunG << 8) | sunB;
                    }
                    else if (diff < -0.01)
                    {
                        // Versant à l'ombre (Ubac)
                        var intensity = Math.Min(1.0, -diff / cosZenith);
                        var alpha = (int)(intensity * maxShadowAlpha);
                        pixels[rowOffset + i] = (alpha << 24) | (shadowR << 16) | (shadowG << 8) | shadowB;
                    }
                    else
                    {
                        pixels[rowOffset + i] = 0;
                    }
                }
            }

            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        return bmp;
    }

    private void UpdateMinMax()
    {
        InvalidateHillshadeCache();
        var min = double.MaxValue;
        var max = double.MinValue;
        for (var i = 0; i < Columns; i++)
        {
            for (var j = 0; j < Rows; j++)
            {
                var v = _grid[i, j];
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }
        MinElevation = min;
        MaxElevation = max;
    }

    /// <summary>
    /// Échantillonne l'altitude continue à n'importe quelles coordonnées réelles (x, y)
    /// par interpolation bilinéaire sur la grille.
    /// </summary>
    public double SampleElevation(double x, double y)
    {
        if (x < 0 || x > WorldWidth || y < 0 || y > WorldHeight)
            return 0.0;

        var gx = (x / WorldWidth) * (Columns - 1);
        var gy = (y / WorldHeight) * (Rows - 1);

        var i0 = Math.Clamp((int)Math.Floor(gx), 0, Columns - 1);
        var j0 = Math.Clamp((int)Math.Floor(gy), 0, Rows - 1);
        var i1 = Math.Min(i0 + 1, Columns - 1);
        var j1 = Math.Min(j0 + 1, Rows - 1);

        var fx = gx - i0;
        var fy = gy - j0;

        var z00 = _grid[i0, j0];
        var z10 = _grid[i1, j0];
        var z01 = _grid[i0, j1];
        var z11 = _grid[i1, j1];

        var z0 = z00 + fx * (z10 - z00);
        var z1 = z01 + fx * (z11 - z01);

        return Math.Round(z0 + fy * (z1 - z0), 1);
    }

    private void BuildElevation()
    {
        var sx = WorldWidth / 1000.0;
        var sy = WorldHeight / 640.0;

        var riverSpline = new List<Point2D>
        {
            new(760 * sx, 90 * sy), new(700 * sx, 150 * sy), new(630 * sx, 200 * sy),
            new(520 * sx, 300 * sy), new(430 * sx, 380 * sy), new(300 * sx, 470 * sy), new(210 * sx, 510 * sy)
        };

        var min = double.MaxValue;
        var max = double.MinValue;

        for (var i = 0; i < Columns; i++)
        {
            var wx = GetWorldX(i);
            for (var j = 0; j < Rows; j++)
            {
                var wy = GetWorldY(j);
                var elev = ComputeElevationAt(wx, wy, riverSpline);
                _grid[i, j] = elev;

                if (elev < min) min = elev;
                if (elev > max) max = elev;
            }
        }

        MinElevation = min;
        MaxElevation = max;
    }

    private double ComputeElevationAt(double x, double y, List<Point2D> riverSpline)
    {
        var sx = WorldWidth / 1000.0;
        var sy = WorldHeight / 640.0;
        var sScale = (sx + sy) * 0.5;

        // 1. Pente générale du Nord-Est vers le Sud-Ouest
        var gradient = (120.0 * (x / WorldWidth) + 80.0 * (1.0 - y / WorldHeight)) * Parameters.GradientFactor;

        // Facteur d'étalonnage pour que le sommet culminant (Pic du Nord) atteigne exactement MountainMaxAltitude (542 m par défaut)
        var mountainScale = Math.Max(0.1, (Parameters.MountainMaxAltitude - 174.0) / (542.0 - 174.0));

        // 2. Massif montagneux Nord-Est
        // Sommet 1 (Pic du Nord) : (820, 40)
        var d1 = Math.Sqrt(Math.Pow((x - 820 * sx) / (1.1 * sx), 2) + Math.Pow((y - 40 * sy) * 1.6 / sy, 2));
        var peak1 = 230.0 * mountainScale * Math.Exp(-Math.Pow(d1 / (110.0 * sScale), 2));

        // Sommet 2 (Crête occidentale) : (680, 60)
        var d2 = Math.Sqrt(Math.Pow((x - 680 * sx) / sx, 2) + Math.Pow((y - 60 * sy) * 1.4 / sy, 2));
        var peak2 = 200.0 * mountainScale * Math.Exp(-Math.Pow(d2 / (90.0 * sScale), 2));

        // Sommet 3 (Chaînon oriental) : (890, 80)
        var d3 = Math.Sqrt(Math.Pow((x - 890 * sx) * 1.2 / sx, 2) + Math.Pow((y - 80 * sy) / sy, 2));
        var peak3 = 205.0 * mountainScale * Math.Exp(-Math.Pow(d3 / (85.0 * sScale), 2));

        // Crête continue reliant les sommets
        var ridgeDist = Math.Abs(y - (30.0 * sy + 0.15 * (x - 650 * sx)));
        var ridgeMask = (x >= 650 * sx && x <= 920 * sx && y <= 110 * sy)
            ? Math.Exp(-Math.Pow(ridgeDist / (40.0 * sScale), 2)) * 85.0 * mountainScale
            : 0;

        // 3. Colline / plateau au Sud-Est : (820, 395)
        var dColline = Math.Sqrt(Math.Pow((x - 820 * sx) / (1.3 * sx), 2) + Math.Pow((y - 395 * sy) / (1.1 * sy), 2));
        var hill = Parameters.HillAltitude * Math.Exp(-Math.Pow(dColline / (75.0 * sScale), 2));

        // 4. Dépression maritime (Golfe / Mer au Sud-Ouest)
        var seaDist = Math.Sqrt(Math.Pow(x - 0, 2) + Math.Pow(y - WorldHeight, 2));
        var seaDrop = 0.0;
        var maxSeaDist = 420.0 * sScale;
        if (seaDist < maxSeaDist)
        {
            var factor = Math.Clamp((maxSeaDist - seaDist) / maxSeaDist, 0, 1);
            seaDrop = Parameters.SeaDrop * Math.Pow(factor, 1.8);
        }

        // Somme des composantes topographiques
        var elevation = gradient + peak1 + peak2 + peak3 + ridgeMask + hill - seaDrop;

        // 5. Creusement du thalweg (la vallée alluviale de la Sereine)
        var distToRiver = DistanceToSpline(x, y, riverSpline);
        if (distToRiver < 100.0 * sScale && elevation > 5.0)
        {
            var valleyDepth = Math.Min(Parameters.RiverValleyDepth, elevation * 0.50);
            var valleyEffect = valleyDepth * Math.Exp(-Math.Pow(distToRiver / (32.0 * sScale), 2));
            elevation -= valleyEffect;
        }

        return Math.Round(elevation, 1);
    }

    private static double DistanceToSpline(double x, double y, List<Point2D> spline)
    {
        var minDist = double.MaxValue;
        for (var i = 0; i < spline.Count - 1; i++)
        {
            var p1 = spline[i];
            var p2 = spline[i + 1];
            var d = DistancePointToSegment(x, y, p1.X, p1.Y, p2.X, p2.Y);
            if (d < minDist)
                minDist = d;
        }
        return minDist;
    }

    private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        var (d, _, _) = DistanceAndProjectionPointToSegment(px, py, x1, y1, x2, y2);
        return d;
    }

    public static (double dist, double projX, double projY) DistanceAndProjectionPointToSegment(
        double px, double py, double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 0.0001)
        {
            var d = Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
            return (d, x1, y1);
        }

        var t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / lenSq, 0.0, 1.0);
        var projX = x1 + t * dx;
        var projY = y1 + t * dy;
        var dist = Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
        return (dist, projX, projY);
    }

}
