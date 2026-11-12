using System;
using System.Collections.Generic;
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
public sealed class Heightmap
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

    /// <summary>
    /// Creuse la vallée fluviale (thalweg) le long d'un tracé de fleuve personnalisé.
    /// </summary>
    public void DigRiverThalweg(IReadOnlyList<Point2D> riverPoints, double maxDepth = 45.0, double influenceRadius = 60.0)
    {
        if (riverPoints.Count < 2) return;
        var riverList = riverPoints.ToList();

        for (var i = 0; i < Columns; i++)
        {
            var wx = GetWorldX(i);
            for (var j = 0; j < Rows; j++)
            {
                var wy = GetWorldY(j);
                var elev = _grid[i, j];
                if (elev > 5.0)
                {
                    var dist = DistanceToSpline(wx, wy, riverList);
                    if (dist < influenceRadius)
                    {
                        var depth = Math.Min(maxDepth, elev * 0.45);
                        var effect = depth * Math.Exp(-Math.Pow(dist / (influenceRadius * 0.4), 2));
                        _grid[i, j] = Math.Round(Math.Max(2.0, elev - effect), 1);
                    }
                }
            }
        }

        UpdateMinMax();
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

    /// <summary>
    /// Simule l'écoulement gravitaire spontané d'un cours d'eau depuis une source (x, y)
    /// suivant la ligne de plus grande pente (-∇h) jusqu'à la mer, un lac ou un autre fleuve (confluence).
    /// <summary>
    /// Simule l'écoulement naturel d'un fleuve selon la gravité (-∇h).
    /// Gère la traversée prolongée des plaines avec inertie hydraulique, méandres et attraction marine,
    /// et s'arrête lorsqu'il atteint la mer (alt &lt;= 0), une confluence ou le bord de la carte.
    /// </summary>
    public List<Point2D> SimulateSpontaneousRiver(
        double sourceX,
        double sourceY,
        IReadOnlyList<RiverData>? existingRivers = null,
        double stepSize = 10.0,
        int maxSteps = 500)
    {
        var points = new List<Point2D>();
        var curX = Math.Clamp(sourceX, 0, WorldWidth);
        var curY = Math.Clamp(sourceY, 0, WorldHeight);

        // Si la source est déjà sous l'eau (mer), pas d'écoulement
        if (SampleElevation(curX, curY) <= 0.0)
            return points;

        points.Add(new Point2D(Math.Round(curX, 1), Math.Round(curY, 1)));

        double vx = 0.0, vy = 0.0;
        const double delta = 4.0;
        var flatSteps = 0;

        for (var step = 0; step < maxSteps; step++)
        {
            // 1. Condition d'arrêt : niveau marin atteint (embouchure)
            var currentElev = SampleElevation(curX, curY);
            if (currentElev <= 0.0)
            {
                break;
            }

            // 2. Condition d'arrêt : bordure de carte
            if (curX <= 3.0 || curX >= WorldWidth - 3.0 || curY <= 3.0 || curY >= WorldHeight - 3.0)
            {
                break;
            }

            // 3. Confluence naturelle : si on approche à moins de 12 unités d'un cours d'eau existant
            if (existingRivers is not null && points.Count >= 3)
            {
                Point2D? confluencePoint = null;
                foreach (var river in existingRivers)
                {
                    if (river.Points.Count < 2) continue;
                    for (var i = 0; i < river.Points.Count - 1; i++)
                    {
                        var p1 = river.Points[i];
                        var p2 = river.Points[i + 1];
                        var (dist, projX, projY) = DistanceAndProjectionPointToSegment(curX, curY, p1.X, p1.Y, p2.X, p2.Y);
                        if (dist < 12.0)
                        {
                            confluencePoint = new Point2D(Math.Round(projX, 1), Math.Round(projY, 1));
                            break;
                        }
                    }
                    if (confluencePoint.HasValue) break;
                }

                if (confluencePoint.HasValue)
                {
                    points.Add(confluencePoint.Value);
                    break;
                }
            }

            // 4. Calcul du gradient d'altitude -∇h
            var dxElev = (SampleElevation(curX + delta, curY) - SampleElevation(curX - delta, curY)) / (2.0 * delta);
            var dyElev = (SampleElevation(curX, curY + delta) - SampleElevation(curX, curY - delta)) / (2.0 * delta);

            var gx = -dxElev;
            var gy = -dyElev;
            var gLen = Math.Sqrt(gx * gx + gy * gy);
            var isFlat = gLen < 0.01;

            if (isFlat)
            {
                // Zone quasi-plate, cuvette ou plaine : recherche multi-échelle du dénivelé descendant
                double bestElev = currentElev;
                double bestDx = 0, bestDy = 0;
                var foundDescent = false;

                // Sonde à 18, 36 puis 72 unités pour trouver le cheminement vers la mer ou la vallée
                double[] probeDistances = [18.0, 36.0, 72.0];
                foreach (var probeDist in probeDistances)
                {
                    for (var a = 0; a < 16; a++)
                    {
                        var angle = a * (Math.PI * 2.0 / 16.0);
                        var probeX = Math.Clamp(curX + Math.Cos(angle) * probeDist, 0, WorldWidth);
                        var probeY = Math.Clamp(curY + Math.Sin(angle) * probeDist, 0, WorldHeight);
                        var probeElev = SampleElevation(probeX, probeY);

                        // Si la mer est détectée, foncer vers l'embouchure
                        if (probeElev <= 0.0)
                        {
                            bestElev = probeElev;
                            bestDx = Math.Cos(angle);
                            bestDy = Math.Sin(angle);
                            foundDescent = true;
                            break;
                        }

                        if (probeElev < bestElev - 0.0001)
                        {
                            bestElev = probeElev;
                            bestDx = Math.Cos(angle);
                            bestDy = Math.Sin(angle);
                            foundDescent = true;
                        }
                    }

                    if (foundDescent && bestElev <= 0.0)
                    {
                        break;
                    }
                }

                if (foundDescent)
                {
                    flatSteps = 0;
                    gx = bestDx;
                    gy = bestDy;
                    gLen = 1.0;
                }
                else
                {
                    // Aucun dénivelé descendant direct trouvé à 72 unités.
                    if (step == 0 || (vx * vx + vy * vy < 0.0001))
                    {
                        // Premier pas en terrain plat : orienter vers l'extérieur (direction du littoral/bordure)
                        var toCenterX = curX - (WorldWidth * 0.5);
                        var toCenterY = curY - (WorldHeight * 0.5);
                        var cLen = Math.Sqrt(toCenterX * toCenterX + toCenterY * toCenterY);
                        if (cLen > 1.0)
                        {
                            gx = toCenterX / cLen;
                            gy = toCenterY / cLen;
                        }
                        else
                        {
                            gx = 1.0;
                            gy = 0.0;
                        }
                        gLen = 1.0;
                    }
                    else
                    {
                        // L'eau est déjà en mouvement : vérifier si le relief devant nous bloque le passage
                        var probeAheadX = Math.Clamp(curX + vx * 25.0, 0, WorldWidth);
                        var probeAheadY = Math.Clamp(curY + vy * 25.0, 0, WorldHeight);
                        var elevAhead = SampleElevation(probeAheadX, probeAheadY);

                        if (elevAhead > currentElev + 0.5)
                        {
                            // Mur ou colline montante devant nous, et aucune issue descendante trouvée
                            flatSteps++;
                            if (flatSteps > 30)
                            {
                                // Véritable cuvette fermée / dépression endoréique
                                break;
                            }
                        }
                        else
                        {
                            // Plaine continue plate : prolonger le tracé sans interruption
                            flatSteps = 0;
                        }

                        gx = vx;
                        gy = vy;
                        gLen = Math.Sqrt(gx * gx + gy * gy);
                        if (gLen < 0.0001)
                        {
                            gx = 1.0;
                            gy = 0.0;
                            gLen = 1.0;
                        }
                    }
                }
            }
            else
            {
                flatSteps = 0;
            }

            // Normaliser le gradient cible
            var targetVx = gx / gLen;
            var targetVy = gy / gLen;

            // En terrain plat ou basse altitude, ajouter de doux méandres sinueux naturels
            if (isFlat || currentElev < 30.0)
            {
                var meanderAngle = step * 0.28 + (sourceX + sourceY) * 0.005;
                var meanderAmount = isFlat ? 0.32 : 0.16;
                var perpX = -targetVy;
                var perpY = targetVx;
                targetVx += perpX * Math.Sin(meanderAngle) * meanderAmount;
                targetVy += perpY * Math.Sin(meanderAngle) * meanderAmount;
                var tLen = Math.Sqrt(targetVx * targetVx + targetVy * targetVy);
                if (tLen > 0.0001)
                {
                    targetVx /= tLen;
                    targetVy /= tLen;
                }
            }

            // 5. Inertie / momentum de l'eau
            if (step == 0)
            {
                vx = targetVx;
                vy = targetVy;
            }
            else
            {
                var inertia = isFlat ? 0.72 : 0.58;
                vx = vx * inertia + targetVx * (1.0 - inertia);
                vy = vy * inertia + targetVy * (1.0 - inertia);
                var vLen = Math.Sqrt(vx * vx + vy * vy);
                if (vLen > 0.0001)
                {
                    vx /= vLen;
                    vy /= vLen;
                }
            }

            // 6. Avancer le point d'écoulement
            curX = Math.Clamp(curX + vx * stepSize, 0, WorldWidth);
            curY = Math.Clamp(curY + vy * stepSize, 0, WorldHeight);

            // Vérifier boucle / auto-intersection (en ignorant les pas immédiats pour laisser méandrer)
            var tooCloseToPrevious = false;
            for (var k = 0; k < points.Count - 4; k++)
            {
                var p = points[k];
                var dSq = (curX - p.X) * (curX - p.X) + (curY - p.Y) * (curY - p.Y);
                if (dSq < 64.0) // < 8 unités
                {
                    tooCloseToPrevious = true;
                    break;
                }
            }
            if (tooCloseToPrevious)
            {
                break;
            }

            points.Add(new Point2D(Math.Round(curX, 1), Math.Round(curY, 1)));
        }

        return points;
    }
}
