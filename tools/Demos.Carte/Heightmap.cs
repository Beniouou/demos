using System;
using System.Collections.Generic;
using Demos.Domain.Map;

namespace Demos.Carte;

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
    /// Crée une carte d'élévation insulaire naturelle avec côtes festonnées et bords découpés (caps, baies, péninsules).
    /// </summary>
    public static Heightmap CreateIsland(int columns, int rows, double worldWidth, double worldHeight)
    {
        var hm = new Heightmap(columns, rows, worldWidth, worldHeight, isFlat: true, baseElevation: -20.0);
        var cx = worldWidth * 0.5;
        var cy = worldHeight * 0.5;
        var rx = worldWidth * 0.38;
        var ry = worldHeight * 0.38;

        for (var i = 0; i < columns; i++)
        {
            var x = hm.GetWorldX(i);
            var dx = (x - cx) / rx;
            for (var j = 0; j < rows; j++)
            {
                var y = hm.GetWorldY(j);
                var dy = (y - cy) / ry;

                var angle = Math.Atan2(dy, dx);
                // Harmoniques de Fourier pour générer des côtes découpées, détroits, caps et fjords naturels
                var radiusMod = 1.0
                    + 0.18 * Math.Sin(3.0 * angle + 0.4)
                    + 0.12 * Math.Cos(5.0 * angle - 0.7)
                    + 0.08 * Math.Sin(8.0 * angle + 1.2)
                    + 0.05 * Math.Cos(13.0 * angle);

                var normalizedDist = Math.Sqrt(dx * dx + dy * dy) / radiusMod;

                double elev;
                if (normalizedDist < 0.25)
                {
                    // Massif central de l'île (crête et collines intérieures)
                    var inner = 1.0 - (normalizedDist / 0.25);
                    elev = 60.0 + 70.0 * inner * inner + 15.0 * Math.Sin(x * 0.03) * Math.Cos(y * 0.03);
                }
                else if (normalizedDist < 1.0)
                {
                    // Versant côtier en pente douce descendant vers la plage (0 m)
                    var t = (1.0 - normalizedDist) / 0.75;
                    elev = 60.0 * Math.Pow(t, 1.3) + 8.0 * Math.Sin(x * 0.04) * Math.Cos(y * 0.04) * t;
                }
                else
                {
                    // Plateforme marine et océan profond entourant l'île
                    var t = Math.Min(1.0, (normalizedDist - 1.0) / 0.35);
                    elev = -4.0 - 22.0 * t;
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
    /// Applique le pinceau de terrain continu :
    /// - deltaElevation > 0 : surélève le terrain (+ altitude, fait émerger de la terre si sous l'eau).
    /// - deltaElevation < 0 : creuse le terrain (- altitude, crée de la mer dès qu'on passe sous 0 m).
    /// </summary>
    public void ApplyBrush(double worldX, double worldY, double radius, double deltaElevation)
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
                    var falloff = Math.Cos((dist / radius) * (Math.PI * 0.5));
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

    private void UpdateMinMax()
    {
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

            // 3. Confluence naturelle : si on approche à moins de 12 lieues d'un cours d'eau existant
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

                // Sonde à 18, 36 puis 72 lieues pour trouver le cheminement vers la mer ou la vallée
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
                    // Aucun dénivelé descendant direct trouvé à 72 lieues.
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
                if (dSq < 64.0) // < 8 lieues
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
