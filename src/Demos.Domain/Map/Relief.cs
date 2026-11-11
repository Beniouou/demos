using System;
using System.Collections.Generic;
using System.Linq;

namespace Demos.Domain.Map;

/// <summary>
/// Les réglages du calcul du relief (outil <c>Demos.Carte</c>, commande <c>--relief</c>). Tous sont des propositions,
/// choisis à l'œil ; ils sont recopiés dans <c>game/data/relief.json</c> avec les images.
/// </summary>
public sealed record ReliefSettings
{
    /// <summary>La taille d'un pixel des images, en lieues.</summary>
    public double CellSize { get; init; } = 2.0;

    /// <summary>Le nombre de passes de l'interpolation, à chaque niveau de détail (plus : plus juste, plus lent).</summary>
    public int SolverIterations { get; init; } = 80;

    /// <summary>Le flou après l'interpolation, en pixels (écart type) : il adoucit les plis le long des courbes.</summary>
    public double SmoothingRadius { get; init; } = 4.0;

    /// <summary>Un dernier flou, en pixels, après avoir creusé la mer : le trait de côte devient une courbe douce.</summary>
    public double CoastSmoothing { get; init; } = 1.0;

    /// <summary>La mer s'enfonce de tant de mètres par lieue de distance à la côte…</summary>
    public double SeaDepthPerLieue { get; init; } = 0.5;

    /// <summary>… jusqu'à cette profondeur, en mètres.</summary>
    public double MaxSeaDepth { get; init; } = 60.0;

    /// <summary>La direction d'où vient la lumière, en degrés depuis le nord, dans le sens des aiguilles d'une montre (315 : nord-ouest).</summary>
    public double LightAzimuth { get; init; } = 315.0;

    /// <summary>La hauteur de la lumière au-dessus de l'horizon, en degrés.</summary>
    public double LightElevation { get; init; } = 45.0;

    /// <summary>
    /// L'exagération des pentes pour les ombres : une lieue fait quelques kilomètres et les courbes sont espacées de
    /// 25 m, donc les vraies pentes ne se verraient presque pas.
    /// </summary>
    public double ShadeExaggeration { get; init; } = 0.10;
}

/// <summary>
/// Une carte d'altitude en grille : un pixel tous les <see cref="CellSize"/> lieues, le pixel (x, y) ayant son centre en
/// ((x + 0,5) × taille, (y + 0,5) × taille). Altitude en mètres ; la mer est sous 0.
/// </summary>
public sealed class ReliefGrid
{
    public ReliefGrid(int width, int height, double cellSize, float[] altitudes)
    {
        if (altitudes.Length != width * height)
            throw new ArgumentException("La grille n'a pas la bonne taille.", nameof(altitudes));
        Width = width;
        Height = height;
        CellSize = cellSize;
        Altitudes = altitudes;
    }

    public int Width { get; }
    public int Height { get; }
    public double CellSize { get; }
    public float[] Altitudes { get; }

    public float this[int x, int y] => Altitudes[y * Width + x];

    public bool IsWater(int x, int y) => this[x, y] < 0;

    /// <summary>L'altitude en un point de la carte (en lieues), interpolée entre les quatre pixels voisins.</summary>
    public double Sample(double worldX, double worldY)
    {
        var fx = Math.Clamp(worldX / CellSize - 0.5, 0, Width - 1);
        var fy = Math.Clamp(worldY / CellSize - 0.5, 0, Height - 1);
        var x0 = (int)fx;
        var y0 = (int)fy;
        var x1 = Math.Min(x0 + 1, Width - 1);
        var y1 = Math.Min(y0 + 1, Height - 1);
        var tx = fx - x0;
        var ty = fy - y0;
        var top = this[x0, y0] * (1 - tx) + this[x1, y0] * tx;
        var bottom = this[x0, y1] * (1 - tx) + this[x1, y1] * tx;
        return top * (1 - ty) + bottom * ty;
    }
}

/// <summary>
/// Calcule la carte d'altitude à partir de ce que la carte vectorielle garde du relief : les courbes de niveau, les
/// points cotés et la mer (altitude 0 sur la côte).
/// 1. Ces points connus sont posés sur la grille.
/// 2. Les autres pixels prennent la moyenne de leurs voisins jusqu'à l'équilibre (une surface lisse qui passe par les
///    points connus), résolue du plus grossier au plus fin pour aller vite.
/// 3. Un léger flou adoucit les plis le long des courbes.
/// 4. La mer s'enfonce avec la distance à la côte.
/// </summary>
public static class ReliefBuilder
{
    public static ReliefGrid Build(VectorMapDefinition map, ReliefSettings settings) =>
        Build(map.Width, map.Height, map.Contours, map.SpotElevations,
            map.WaterBodies.Where(w => !w.IsErroneousGlobalSea(map.Width, map.Height)).ToList(), settings);

    public static ReliefGrid Build(
        double worldWidth,
        double worldHeight,
        IReadOnlyList<ContourLine> contours,
        IReadOnlyList<SpotElevation> spots,
        IReadOnlyList<PolygonArea> waterBodies,
        ReliefSettings settings)
    {
        var cell = settings.CellSize;
        var width = Math.Max(1, (int)Math.Ceiling(worldWidth / cell));
        var height = Math.Max(1, (int)Math.Ceiling(worldHeight / cell));

        // 1. Ce qu'on sait : la mer à 0, puis les courbes (moyenne si plusieurs tombent sur un pixel), puis les points cotés.
        var water = RasterizeWater(width, height, cell, waterBodies);
        var sum = new double[width * height];
        var count = new int[width * height];
        foreach (var contour in contours)
        {
            var points = contour.Points;
            for (var i = 0; i + 1 < points.Count; i++)
                MarkSegment(points[i], points[i + 1], contour.Altitude, cell, width, height, sum, count);
            if (contour.IsClosed && points.Count > 2)
                MarkSegment(points[^1], points[0], contour.Altitude, cell, width, height, sum, count);
        }

        var known = new bool[width * height];
        var values = new float[width * height];
        for (var i = 0; i < values.Length; i++)
        {
            if (count[i] > 0)
            {
                known[i] = true;
                values[i] = (float)(sum[i] / count[i]);
            }
            else if (water[i])
            {
                known[i] = true;
                values[i] = 0f;
            }
        }
        // Un point coté tient sur un petit disque (le rayon du flou) : sur un seul pixel, l'interpolation en ferait une
        // aiguille que le flou raboterait.
        var spotRadius = Math.Max(1, (int)Math.Ceiling(settings.SmoothingRadius));
        foreach (var spot in spots)
        {
            var (cx, cy) = Pixel(spot.Position, cell);
            for (var y = cy - spotRadius; y <= cy + spotRadius; y++)
                for (var x = cx - spotRadius; x <= cx + spotRadius; x++)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height || (x - cx) * (x - cx) + (y - cy) * (y - cy) > spotRadius * spotRadius)
                        continue;
                    known[y * width + x] = true;
                    values[y * width + x] = (float)spot.Altitude;
                }
        }

        // 2. La surface lisse qui passe par les points connus.
        var altitudes = Solve(width, height, known, values, settings.SolverIterations);

        // 3. Le flou, puis 4. la mer.
        Blur(altitudes, width, height, settings.SmoothingRadius);
        var distance = DistanceToLand(water, width, height);
        for (var i = 0; i < altitudes.Length; i++)
        {
            if (water[i])
                altitudes[i] = -(float)Math.Min(settings.MaxSeaDepth, Math.Max(1.0, distance[i] * cell * settings.SeaDepthPerLieue));
            else if (altitudes[i] < 0)
                altitudes[i] = 0;
        }
        Blur(altitudes, width, height, settings.CoastSmoothing);

        return new ReliefGrid(width, height, cell, altitudes);
    }

    private static (int X, int Y) Pixel(Point2D p, double cell) =>
        ((int)Math.Floor(p.X / cell), (int)Math.Floor(p.Y / cell));

    /// <summary>Pose une altitude sur chaque pixel que traverse le segment (un point tous les demi-pixels).</summary>
    private static void MarkSegment(Point2D a, Point2D b, double altitude, double cell, int width, int height, double[] sum, int[] count)
    {
        var length = a.DistanceTo(b) / cell;
        var steps = Math.Max(1, (int)Math.Ceiling(length * 2));
        var last = -1;
        for (var s = 0; s <= steps; s++)
        {
            var t = (double)s / steps;
            var (x, y) = Pixel(new Point2D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t), cell);
            if (x < 0 || x >= width || y < 0 || y >= height)
                continue;
            var index = y * width + x;
            if (index == last)
                continue;
            last = index;
            sum[index] += altitude;
            count[index]++;
        }
    }

    /// <summary>
    /// Les pixels de mer : dans un plan d'eau (règle pair-impair, trous compris), sauf dans une île ou une terre
    /// (« Island », « Land »).
    /// </summary>
    private static bool[] RasterizeWater(int width, int height, double cell, IReadOnlyList<PolygonArea> waterBodies)
    {
        var water = new bool[width * height];
        foreach (var body in waterBodies)
        {
            var isLand = body.Kind is "Island" or "Land";
            var rings = new List<IReadOnlyList<Point2D>> { body.Boundary };
            if (body.Holes is { } holes)
                rings.AddRange(holes);
            FillRings(rings, width, height, cell, (index, inside) =>
            {
                if (inside)
                    water[index] = !isLand;
            });
        }
        return water;
    }

    private static void FillRings(List<IReadOnlyList<Point2D>> rings, int width, int height, double cell, Action<int, bool> visit)
    {
        var crossings = new List<double>();
        for (var y = 0; y < height; y++)
        {
            var worldY = (y + 0.5) * cell;
            crossings.Clear();
            foreach (var ring in rings)
            {
                for (var i = 0; i < ring.Count; i++)
                {
                    var p = ring[i];
                    var q = ring[(i + 1) % ring.Count];
                    if ((p.Y <= worldY && q.Y > worldY) || (q.Y <= worldY && p.Y > worldY))
                        crossings.Add(p.X + (worldY - p.Y) / (q.Y - p.Y) * (q.X - p.X));
                }
            }
            if (crossings.Count < 2)
                continue;
            crossings.Sort();
            for (var k = 0; k + 1 < crossings.Count; k += 2)
            {
                var x0 = Math.Max(0, (int)Math.Ceiling(crossings[k] / cell - 0.5));
                var x1 = Math.Min(width - 1, (int)Math.Floor(crossings[k + 1] / cell - 0.5));
                for (var x = x0; x <= x1; x++)
                    visit(y * width + x, true);
            }
        }
    }

    // ---- 2. L'interpolation : chaque pixel inconnu prend la moyenne de ses voisins -------------------------------

    private sealed record Level(int Width, int Height, bool[] Known, float[] Values);

    private static float[] Solve(int width, int height, bool[] known, float[] values, int iterations)
    {
        // Les niveaux de détail, du plus fin au plus grossier : un pixel grossier est connu si l'un de ses quatre est
        // connu, à la moyenne de ceux qui le sont.
        var levels = new List<Level> { new(width, height, known, values) };
        while (levels[^1].Width > 16 && levels[^1].Height > 16)
            levels.Add(Coarsen(levels[^1]));

        var coarsest = levels[^1];
        var knownValues = coarsest.Values.Where((_, i) => coarsest.Known[i]).ToList();
        var start = knownValues.Count > 0 ? knownValues.Average() : 0f;
        var solution = new float[coarsest.Width * coarsest.Height];
        for (var i = 0; i < solution.Length; i++)
            solution[i] = coarsest.Known[i] ? coarsest.Values[i] : start;
        Relax(coarsest, solution, iterations * 8);

        for (var l = levels.Count - 2; l >= 0; l--)
        {
            var level = levels[l];
            solution = Refine(levels[l + 1], solution, level);
            Relax(level, solution, iterations);
        }
        return solution;
    }

    private static Level Coarsen(Level fine)
    {
        var width = (fine.Width + 1) / 2;
        var height = (fine.Height + 1) / 2;
        var known = new bool[width * height];
        var values = new float[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var sum = 0.0;
                var n = 0;
                for (var dy = 0; dy < 2; dy++)
                    for (var dx = 0; dx < 2; dx++)
                    {
                        var fx = x * 2 + dx;
                        var fy = y * 2 + dy;
                        if (fx >= fine.Width || fy >= fine.Height || !fine.Known[fy * fine.Width + fx])
                            continue;
                        sum += fine.Values[fy * fine.Width + fx];
                        n++;
                    }
                if (n > 0)
                {
                    known[y * width + x] = true;
                    values[y * width + x] = (float)(sum / n);
                }
            }
        return new Level(width, height, known, values);
    }

    /// <summary>La solution grossière agrandie (interpolation bilinéaire), puis les points connus remis à leur valeur.</summary>
    private static float[] Refine(Level coarse, float[] coarseSolution, Level fine)
    {
        var solution = new float[fine.Width * fine.Height];
        for (var y = 0; y < fine.Height; y++)
        {
            var cy = Math.Clamp((y + 0.5) / 2 - 0.5, 0, coarse.Height - 1);
            var y0 = (int)cy;
            var y1 = Math.Min(y0 + 1, coarse.Height - 1);
            var ty = (float)(cy - y0);
            for (var x = 0; x < fine.Width; x++)
            {
                var index = y * fine.Width + x;
                if (fine.Known[index])
                {
                    solution[index] = fine.Values[index];
                    continue;
                }
                var cx = Math.Clamp((x + 0.5) / 2 - 0.5, 0, coarse.Width - 1);
                var x0 = (int)cx;
                var x1 = Math.Min(x0 + 1, coarse.Width - 1);
                var tx = (float)(cx - x0);
                var top = coarseSolution[y0 * coarse.Width + x0] * (1 - tx) + coarseSolution[y0 * coarse.Width + x1] * tx;
                var bottom = coarseSolution[y1 * coarse.Width + x0] * (1 - tx) + coarseSolution[y1 * coarse.Width + x1] * tx;
                solution[index] = top * (1 - ty) + bottom * ty;
            }
        }
        return solution;
    }

    /// <summary>Passes de sur-relaxation : chaque pixel inconnu va vers la moyenne de ses voisins (le bord compte comme un miroir).</summary>
    private static void Relax(Level level, float[] solution, int iterations)
    {
        const float Overrelaxation = 1.8f;
        var (width, height, known) = (level.Width, level.Height, level.Known);
        for (var it = 0; it < iterations; it++)
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (known[index])
                        continue;
                    var v = solution[index];
                    var left = x > 0 ? solution[index - 1] : v;
                    var right = x < width - 1 ? solution[index + 1] : v;
                    var up = y > 0 ? solution[index - width] : v;
                    var down = y < height - 1 ? solution[index + width] : v;
                    solution[index] = v + Overrelaxation * ((left + right + up + down) * 0.25f - v);
                }
    }

    // ---- 3. Le flou, 4. la distance à la côte -------------------------------------------------------------------

    /// <summary>Un flou gaussien (écart type en pixels), ligne puis colonne.</summary>
    private static void Blur(float[] values, int width, int height, double sigma)
    {
        if (sigma <= 0)
            return;
        var radius = (int)Math.Ceiling(sigma * 3);
        var kernel = new float[radius * 2 + 1];
        var total = 0.0;
        for (var k = -radius; k <= radius; k++)
            total += kernel[k + radius] = (float)Math.Exp(-k * k / (2 * sigma * sigma));
        for (var k = 0; k < kernel.Length; k++)
            kernel[k] /= (float)total;

        var buffer = new float[values.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var acc = 0f;
                for (var k = -radius; k <= radius; k++)
                    acc += kernel[k + radius] * values[y * width + Math.Clamp(x + k, 0, width - 1)];
                buffer[y * width + x] = acc;
            }
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var acc = 0f;
                for (var k = -radius; k <= radius; k++)
                    acc += kernel[k + radius] * buffer[Math.Clamp(y + k, 0, height - 1) * width + x];
                values[y * width + x] = acc;
            }
    }

    /// <summary>Pour chaque pixel de mer, sa distance (en pixels, approchée) à la terre la plus proche ; 0 sur terre.</summary>
    private static float[] DistanceToLand(bool[] water, int width, int height)
    {
        const float Straight = 1f;
        const float Diagonal = 1.4142135f;
        var far = (float)(width + height);
        var distance = new float[water.Length];
        for (var i = 0; i < distance.Length; i++)
            distance[i] = water[i] ? far : 0f;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                if (!water[i]) continue;
                var d = distance[i];
                if (x > 0) d = Math.Min(d, distance[i - 1] + Straight);
                if (y > 0) d = Math.Min(d, distance[i - width] + Straight);
                if (x > 0 && y > 0) d = Math.Min(d, distance[i - width - 1] + Diagonal);
                if (x < width - 1 && y > 0) d = Math.Min(d, distance[i - width + 1] + Diagonal);
                distance[i] = d;
            }
        for (var y = height - 1; y >= 0; y--)
            for (var x = width - 1; x >= 0; x--)
            {
                var i = y * width + x;
                if (!water[i]) continue;
                var d = distance[i];
                if (x < width - 1) d = Math.Min(d, distance[i + 1] + Straight);
                if (y < height - 1) d = Math.Min(d, distance[i + width] + Straight);
                if (x < width - 1 && y < height - 1) d = Math.Min(d, distance[i + width + 1] + Diagonal);
                if (x > 0 && y < height - 1) d = Math.Min(d, distance[i + width - 1] + Diagonal);
                distance[i] = d;
            }
        return distance;
    }
}

/// <summary>
/// Les ombres du relief : l'éclairement de chaque pixel par une lumière lointaine (<see cref="ReliefSettings.LightAzimuth"/>,
/// <see cref="ReliefSettings.LightElevation"/>), de 0 (dans l'ombre) à 255 (face à la lumière). Un terrain plat vaut
/// 255 × sinus de la hauteur de la lumière (180 pour 45°) ; la mer est plate.
/// </summary>
public static class Hillshade
{
    public static byte[] Compute(ReliefGrid grid, ReliefSettings settings)
    {
        var azimuth = settings.LightAzimuth * Math.PI / 180;
        var elevation = settings.LightElevation * Math.PI / 180;
        // Vers la lumière : x vers l'est, y vers le sud (comme l'écran), z vers le ciel.
        var lx = Math.Sin(azimuth) * Math.Cos(elevation);
        var ly = -Math.Cos(azimuth) * Math.Cos(elevation);
        var lz = Math.Sin(elevation);
        var flat = ToByte(lz);

        var (width, height) = (grid.Width, grid.Height);
        var shade = new byte[width * height];
        var scale = settings.ShadeExaggeration / (2 * grid.CellSize);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (grid.IsWater(x, y))
                {
                    shade[y * width + x] = flat;
                    continue;
                }
                var dzdx = (Land(grid, Math.Min(x + 1, width - 1), y) - Land(grid, Math.Max(x - 1, 0), y)) * scale;
                var dzdy = (Land(grid, x, Math.Min(y + 1, height - 1)) - Land(grid, x, Math.Max(y - 1, 0))) * scale;
                // La normale (-dz/dx, -dz/dy, 1), normée.
                var norm = Math.Sqrt(dzdx * dzdx + dzdy * dzdy + 1);
                shade[y * width + x] = ToByte((-dzdx * lx - dzdy * ly + lz) / norm);
            }
        return shade;
    }

    // Sur la côte, la mer compte pour 0 (pas pour sa profondeur) : la côte n'a pas d'ombre de falaise.
    private static double Land(ReliefGrid grid, int x, int y) => Math.Max(0, grid[x, y]);

    private static byte ToByte(double light) => (byte)Math.Round(Math.Clamp(light, 0, 1) * 255);
}

/// <summary>
/// Le codage de l'altitude dans l'image <c>relief-altitude.png</c> : en décimètres au-dessus de −1 000 m, sur 16 bits
/// (rouge : octet fort, vert : octet faible) ; le bleu vaut 0 sur la mer et 255 sur terre.
/// </summary>
public static class ReliefEncoding
{
    public const double AltitudeOffset = 1000.0;
    public const double AltitudeScale = 10.0;

    public static (byte High, byte Low) Encode(double altitude)
    {
        var value = (int)Math.Clamp(Math.Round((altitude + AltitudeOffset) * AltitudeScale), 0, 65535);
        return ((byte)(value >> 8), (byte)(value & 0xFF));
    }

    public static double Decode(byte high, byte low) => ((high << 8) | low) / AltitudeScale - AltitudeOffset;
}
