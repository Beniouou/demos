using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;

namespace Demos.Carte;

/// <summary>
/// L'eau sur le relief (24 novembre 2026) : les cuvettes qu'on remplit en lacs, et le tracé des fleuves par le chemin qui
/// creuse le moins (décidé le 24 novembre 2026) vers la mer, le bord de la carte ou un lac.
/// </summary>
public sealed partial class Heightmap
{
    /// <summary>
    /// Une cuvette : son fond, son seuil (le niveau où elle déborde), la case du seuil (le col), la première case au-delà
    /// (par où l'eau sort), et ses cases (celles que l'eau couvre au niveau du seuil).
    /// </summary>
    public sealed record Basin(double Bottom, double Spill, (int I, int J) BottomCell, (int I, int J) Col, (int I, int J) Outflow, bool[,] Cells);

    /// <summary>
    /// La cuvette où l'on a cliqué. Le seuil vient du réseau d'écoulement (<see cref="Drainage"/>) : c'est la hauteur que
    /// l'eau doit franchir pour rejoindre une vraie sortie (la mer, le bord ou un autre lac). Une petite cuvette juste
    /// derrière le col ne compte donc pas comme une sortie : le lac monte jusqu'à ce que l'eau s'en aille vraiment. Le col
    /// est la dernière case à ce niveau sur le chemin de l'eau, la sortie la première plus basse. Null dans la mer.
    /// </summary>
    public Basin? FindBasin(double x, double y, IReadOnlyList<LakeData>? lakes = null)
    {
        var start = CellAt(x, y);
        if (_grid[start.I, start.J] <= 0.0)
            return null;
        // Descendre au fond : de case en case vers la plus basse des voisines (sinon, un clic sur une pente prendrait
        // l'altitude du clic pour seuil).
        while (true)
        {
            var next = start;
            foreach (var n in Neighbours(start.I, start.J))
                if (_grid[n.I, n.J] < _grid[next.I, next.J])
                    next = n;
            if (next == start)
                break;
            start = next;
        }
        if (_grid[start.I, start.J] <= 0.0)
            return null;

        var (parent, _, filled) = Drainage(lakes ?? NoLakes);
        var spill = filled[start.I * Rows + start.J];

        // Les cases sous l'eau au niveau du seuil, reliées au point cliqué, et le fond (la plus basse).
        var cells = new bool[Columns, Rows];
        var stack = new Stack<(int I, int J)>();
        stack.Push(start);
        cells[start.I, start.J] = true;
        var bottom = start;
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            if (_grid[c.I, c.J] < _grid[bottom.I, bottom.J])
                bottom = c;
            foreach (var n in Neighbours(c.I, c.J))
            {
                if (cells[n.I, n.J] || _grid[n.I, n.J] >= spill)
                    continue;
                cells[n.I, n.J] = true;
                stack.Push(n);
            }
        }

        // Le chemin de l'eau depuis le fond : au niveau du seuil jusqu'au col, puis plus bas.
        var k = bottom.I * Rows + bottom.J;
        var col = k;
        while (parent[k] >= 0 && filled[parent[k]] >= spill - 1e-6)
        {
            k = parent[k];
            col = k;
        }
        var outflow = parent[col] >= 0 ? parent[col] : col;
        return new Basin(_grid[bottom.I, bottom.J], spill, bottom, (col / Rows, col % Rows), (outflow / Rows, outflow % Rows), cells);
    }

    /// <summary>
    /// Le contour d'un lac dans cette cuvette, au niveau donné (au plus le seuil) : la rive et les îles, tracées à ce
    /// niveau exact sur le relief.
    /// </summary>
    public List<(List<Point2D> Outer, List<List<Point2D>> Holes)> LakeShape(Basin basin, double level)
    {
        // Les cases sous l'eau, reliées au fond.
        var wet = new bool[Columns, Rows];
        var stack = new Stack<(int I, int J)>();
        stack.Push(basin.BottomCell);
        wet[basin.BottomCell.I, basin.BottomCell.J] = true;
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            foreach (var n in Neighbours(c.I, c.J))
            {
                if (wet[n.I, n.J] || !basin.Cells[n.I, n.J] || _grid[n.I, n.J] >= level)
                    continue;
                wet[n.I, n.J] = true;
                stack.Push(n);
            }
        }

        // La hauteur d'eau, positive sous l'eau ; la rive est la ligne où elle vaut 0.
        var depth = new double[Columns, Rows];
        for (var i = 0; i < Columns; i++)
            for (var j = 0; j < Rows; j++)
                depth[i, j] = wet[i, j] ? level - _grid[i, j] : Math.Min(level - _grid[i, j], -0.01);
        return new ZoneMaskGrid(Columns, Rows, WorldWidth, WorldHeight).ExtractShapes(depth, 0.001);
    }

    /// <summary>
    /// Le niveau sous lequel on ne creuse pas, case par case : celui du lac pour ses cases et les cases qui les touchent
    /// (la rive), NaN ailleurs. Null s'il n'y a pas de lac.
    /// </summary>
    private double[,]? ShoreLevels(IReadOnlyList<LakeData> lakes)
    {
        if (lakes.Count == 0)
            return null;
        var water = LakeLevels(lakes);
        var shore = new double[Columns, Rows];
        for (var i = 0; i < Columns; i++)
            for (var j = 0; j < Rows; j++)
            {
                shore[i, j] = water[i, j];
                foreach (var n in Neighbours(i, j))
                    if (!double.IsNaN(water[n.I, n.J]))
                        shore[i, j] = double.IsNaN(shore[i, j]) ? water[n.I, n.J] : Math.Max(shore[i, j], water[n.I, n.J]);
            }
        return shore;
    }

    /// <summary>Le niveau du lac qui couvre chaque case (NaN hors des lacs).</summary>
    private double[,] LakeLevels(IReadOnlyList<LakeData> lakes)
    {
        var levels = new double[Columns, Rows];
        for (var i = 0; i < Columns; i++)
            for (var j = 0; j < Rows; j++)
                levels[i, j] = double.NaN;
        foreach (var lake in lakes)
        {
            if (lake.Boundary.Count < 3)
                continue;
            var (minX, maxX) = (lake.Boundary.Min(p => p.X), lake.Boundary.Max(p => p.X));
            var (minY, maxY) = (lake.Boundary.Min(p => p.Y), lake.Boundary.Max(p => p.Y));
            var (i0, j0) = CellAt(minX, minY);
            var (i1, j1) = CellAt(maxX, maxY);
            for (var i = Math.Max(0, i0 - 1); i <= Math.Min(Columns - 1, i1 + 1); i++)
                for (var j = Math.Max(0, j0 - 1); j <= Math.Min(Rows - 1, j1 + 1); j++)
                    if (lake.Contains(new Point2D(GetWorldX(i), GetWorldY(j))))
                        levels[i, j] = lake.Level;
        }
        return levels;
    }

    // Le réseau d'écoulement, gardé tant que le relief et les lacs ne changent pas.
    private (long Stamp, IReadOnlyList<LakeData> Lakes, int[] Parent, byte[] Outlet, double[] Filled)? _drainage;

    private static readonly LakeData[] NoLakes = [];

    private const byte NoOutlet = 0, SeaOutlet = 1, LakeOutlet = 2, EdgeOutlet = 3;

    /// <summary>
    /// Le réseau d'écoulement de toute la carte (remplissage par priorité) : l'eau part des sorties (la mer, les lacs, le
    /// bord de la carte) et remonte le relief, la case la plus basse d'abord. Chaque case est rattachée à la voisine par
    /// laquelle l'eau l'a atteinte : en suivant ces liens, on descend vers une sortie par le chemin dont le point le plus
    /// haut est le plus bas (le chemin qui creuse le moins). Là où le terrain descend, c'est la plus basse des voisines ;
    /// une cuvette se vide par son col le plus bas. À hauteur égale, la mer passe avant un lac, un lac avant le bord ;
    /// puis le plus court.
    /// </summary>
    private (int[] Parent, byte[] Outlet, double[] Filled) Drainage(IReadOnlyList<LakeData> lakes)
    {
        var stamp = GridStamp();
        if (_drainage is { } cached && cached.Stamp == stamp && ReferenceEquals(cached.Lakes, lakes))
            return (cached.Parent, cached.Outlet, cached.Filled);

        var count = Columns * Rows;
        var parent = new int[count];
        Array.Fill(parent, -1);
        var outlet = new byte[count];
        var done = new bool[count];
        var open = new PriorityQueue<int, (double Level, byte Kind, long Order)>();
        long order = 0;
        var lakeLevels = LakeLevels(lakes);

        for (var i = 0; i < Columns; i++)
            for (var j = 0; j < Rows; j++)
            {
                var k = i * Rows + j;
                var elevation = _grid[i, j];
                byte kind = elevation <= 0.0 ? SeaOutlet : !double.IsNaN(lakeLevels[i, j]) ? LakeOutlet : IsBorder(i, j) ? EdgeOutlet : NoOutlet;
                if (kind == NoOutlet)
                    continue;
                var level = kind == SeaOutlet ? 0.0 : kind == LakeOutlet ? lakeLevels[i, j] : elevation;
                outlet[k] = kind;
                done[k] = true;
                open.Enqueue(k, (level, kind, order++));
            }

        // 1. Le remplissage : chaque case reçoit sa hauteur d'eau (le point le plus haut à franchir pour sortir) et son
        //    rang d'arrivée.
        var filled = new double[count];
        var rank = new int[count];
        Array.Fill(rank, -1);
        var popped = new List<int>(count);
        while (open.TryDequeue(out var k, out var key))
        {
            filled[k] = key.Level;
            rank[k] = popped.Count;
            popped.Add(k);
            var (i, j) = (k / Rows, k % Rows);
            foreach (var n in Neighbours(i, j))
            {
                var nk = n.I * Rows + n.J;
                if (done[nk])
                    continue;
                done[nk] = true;
                open.Enqueue(nk, (Math.Max(_grid[n.I, n.J], key.Level), key.Kind, order++));
            }
        }

        // 2. Le lien de chaque case : parmi les voisines atteintes avant elle, celle où la pente (sur la hauteur d'eau,
        //    divisée par la distance) est la plus forte ; à pente identique, la mer, puis un lac, puis le bord, puis la
        //    plus tôt atteinte. Les liens vont toujours vers une case atteinte plus tôt : pas de boucle.
        var (dx, dy) = (WorldWidth / (Columns - 1), WorldHeight / (Rows - 1));
        foreach (var k in popped)
        {
            // Les sorties (la mer, les lacs, le bord) n'ont pas de lien.
            if (outlet[k] != NoOutlet)
                continue;
            var (i, j) = (k / Rows, k % Rows);
            var best = -1;
            var bestSlope = double.NegativeInfinity;
            foreach (var n in Neighbours(i, j))
            {
                var nk = n.I * Rows + n.J;
                if (rank[nk] < 0 || rank[nk] >= rank[k])
                    continue;
                var distance = Math.Sqrt(Math.Pow((n.I - i) * dx, 2) + Math.Pow((n.J - j) * dy, 2));
                var slope = (filled[k] - filled[nk]) / distance;
                var better = best < 0 || slope > bestSlope + 1e-9
                    || (Math.Abs(slope - bestSlope) <= 1e-9 && (outlet[nk] < outlet[best] || (outlet[nk] == outlet[best] && rank[nk] < rank[best])));
                if (better)
                {
                    best = nk;
                    bestSlope = slope;
                }
            }
            parent[k] = best;
            outlet[k] = best >= 0 ? outlet[best] : outlet[k];
        }

        _drainage = (stamp, lakes, parent, outlet, filled);
        return (parent, outlet, filled);
    }

    /// <summary>
    /// Trace un fleuve depuis une source (x, y) par le chemin qui creuse le moins (voir <see cref="Drainage"/>), jusqu'à
    /// la mer, un lac, le bord de la carte ou un autre fleuve (confluence). Le tracé de case en case est ensuite adouci.
    /// Sans creusement, le fleuve peut monter pour franchir un col ; creusé, son lit descend toujours.
    /// </summary>
    public List<Point2D> SimulateSpontaneousRiver(double sourceX, double sourceY,
        IReadOnlyList<RiverData>? existingRivers = null, IReadOnlyList<LakeData>? lakes = null)
    {
        var points = new List<Point2D>();
        var x = Math.Clamp(sourceX, 0, WorldWidth);
        var y = Math.Clamp(sourceY, 0, WorldHeight);
        if (SampleElevation(x, y) <= 0.0)
            return points;
        lakes ??= NoLakes;
        if (lakes.Any(l => l.Contains(new Point2D(x, y))))
            return points;

        var (parent, outlet, _) = Drainage(lakes);
        var cell = CellAt(x, y);
        var k = cell.I * Rows + cell.J;

        // De case en case jusqu'à la sortie.
        var cells = new List<int> { k };
        while (parent[k] >= 0 && cells.Count < Columns * Rows)
        {
            k = parent[k];
            cells.Add(k);
        }

        var path = new List<Point2D> { new(x, y) };
        path.AddRange(cells.Skip(1).Select(c => new Point2D(GetWorldX(c / Rows), GetWorldY(c % Rows))));
        if (path.Count < 2)
            return points;
        if (outlet[cells[^1]] == EdgeOutlet)
            path[^1] = OnBorder(path[^1]);
        var smooth = Meander(Chaikin(path, 2), sourceX, sourceY);

        // Confluence : le fleuve s'arrête en rejoignant un autre fleuve (à moins de 12 unités).
        foreach (var p in smooth)
        {
            if (points.Count >= 3 && existingRivers is not null && NearestRiverPoint(p, existingRivers) is { } join)
            {
                points.Add(join);
                return points;
            }
            points.Add(new Point2D(Math.Round(p.X, 1), Math.Round(p.Y, 1)));
        }
        return points;
    }

    /// <summary>Le point du fleuve existant le plus proche, s'il est à moins de 12 unités.</summary>
    private static Point2D? NearestRiverPoint(Point2D p, IReadOnlyList<RiverData> rivers)
    {
        foreach (var river in rivers)
            for (var i = 0; i < river.Points.Count - 1; i++)
            {
                var (a, b) = (river.Points[i], river.Points[i + 1]);
                var (dist, px, py) = DistanceAndProjectionPointToSegment(p.X, p.Y, a.X, a.Y, b.X, b.Y);
                if (dist < 12.0)
                    return new Point2D(Math.Round(px, 1), Math.Round(py, 1));
            }
        return null;
    }

    /// <summary>Écart des ondulations sur le plat, de part et d'autre du tracé (unités ; 10 = 1 km, proposition).</summary>
    private const double MeanderAmplitude = 10.0;

    /// <summary>Longueur d'une ondulation (unités ; 175 = 17,5 km, proposition), un peu irrégulière.</summary>
    private const double MeanderWavelength = 175.0;

    /// <summary>
    /// Les ondulations d'un fleuve (remises le 24 novembre 2026) : chaque point est décalé sur le côté selon une vague,
    /// marquée en plaine et presque nulle en montagne (l'écart est divisé par 1 + pente en % ÷ 3 : moitié moins à 3 %). Les deux bouts ne
    /// bougent pas ; la vague dépend de la source, donc un fleuve retracé au même endroit retrouve ses ondulations.
    /// </summary>
    private List<Point2D> Meander(List<Point2D> path, double sourceX, double sourceY)
    {
        // Des points tous les 5 unités, pour une vague régulière.
        const double spacing = 5.0;
        var points = new List<Point2D> { path[0] };
        for (var i = 1; i < path.Count; i++)
        {
            var (a, b) = (points[^1], path[i]);
            var length = Distance(a, b);
            if (length < spacing && i < path.Count - 1)
                continue;
            var steps = Math.Max(1, (int)Math.Round(length / spacing));
            for (var s = 1; s <= steps; s++)
                points.Add(new Point2D(a.X + (b.X - a.X) * s / steps, a.Y + (b.Y - a.Y) * s / steps));
        }
        if (points.Count < 5)
            return path;

        var along = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
            along[i] = along[i - 1] + Distance(points[i - 1], points[i]);
        var total = along[^1];

        // La pente en % le long du fleuve (sur ±4 points), puis moyennée pour que l'écart change doucement.
        var share = new double[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var (lo, hi) = (Math.Max(0, i - 4), Math.Min(points.Count - 1, i + 4));
            var drop = Math.Abs(SampleElevation(points[lo].X, points[lo].Y) - SampleElevation(points[hi].X, points[hi].Y));
            var slope = drop / Math.Max(1e-6, along[hi] - along[lo]);
            share[i] = 1.0 / (1.0 + slope / 3.0);
        }
        var smoothShare = new double[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var (lo, hi) = (Math.Max(0, i - 10), Math.Min(points.Count - 1, i + 10));
            for (var k = lo; k <= hi; k++)
                smoothShare[i] += share[k];
            smoothShare[i] /= hi - lo + 1;
        }

        var phase0 = (sourceX * 0.013 + sourceY * 0.007) % (2 * Math.PI);
        var result = new List<Point2D>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var (prev, next) = (points[Math.Max(0, i - 1)], points[Math.Min(points.Count - 1, i + 1)]);
            var (tx, ty) = (next.X - prev.X, next.Y - prev.Y);
            var tLen = Math.Sqrt(tx * tx + ty * ty);
            if (tLen < 1e-9)
            {
                result.Add(points[i]);
                continue;
            }
            var s = along[i];
            // Les bouts restent en place : l'écart monte sur les 3 premiers et les 3 derniers km.
            var ends = Math.Clamp(Math.Min(s, total - s) / 30.0, 0.0, 1.0);
            var taper = ends * ends * (3 - 2 * ends);
            var wave = Math.Sin(2 * Math.PI * s / MeanderWavelength + 0.7 * Math.Sin(2 * Math.PI * s / (MeanderWavelength * 3.1)) + phase0);
            var offset = MeanderAmplitude * smoothShare[i] * taper * wave;
            result.Add(new Point2D(points[i].X - ty / tLen * offset, points[i].Y + tx / tLen * offset));
        }
        return result;
    }

    /// <summary>Adoucit un tracé de case en case (coins coupés au quart), en gardant ses deux bouts.</summary>
    private static List<Point2D> Chaikin(List<Point2D> path, int passes)
    {
        var current = path;
        for (var pass = 0; pass < passes && current.Count > 2; pass++)
        {
            var next = new List<Point2D> { current[0] };
            for (var i = 0; i < current.Count - 1; i++)
            {
                var (a, b) = (current[i], current[i + 1]);
                next.Add(new Point2D(a.X * 0.75 + b.X * 0.25, a.Y * 0.75 + b.Y * 0.25));
                next.Add(new Point2D(a.X * 0.25 + b.X * 0.75, a.Y * 0.25 + b.Y * 0.75));
            }
            next.Add(current[^1]);
            current = next;
        }
        return current;
    }

    /// <summary>Le point du bord de la carte le plus proche.</summary>
    private Point2D OnBorder(Point2D p)
    {
        var options = new[]
        {
            (Dist: p.X, Point: new Point2D(0, p.Y)),
            (Dist: WorldWidth - p.X, Point: new Point2D(WorldWidth, p.Y)),
            (Dist: p.Y, Point: new Point2D(p.X, 0)),
            (Dist: WorldHeight - p.Y, Point: new Point2D(p.X, WorldHeight)),
        };
        return options.MinBy(o => o.Dist).Point;
    }

    /// <summary>La case de la grille la plus proche d'un point de la carte.</summary>
    public (int I, int J) CellAt(double x, double y) =>
        (Math.Clamp((int)Math.Round(x / WorldWidth * (Columns - 1)), 0, Columns - 1),
         Math.Clamp((int)Math.Round(y / WorldHeight * (Rows - 1)), 0, Rows - 1));

    private bool IsBorder(int i, int j) => i == 0 || j == 0 || i == Columns - 1 || j == Rows - 1;

    private IEnumerable<(int I, int J)> Neighbours(int i, int j)
    {
        for (var di = -1; di <= 1; di++)
            for (var dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0)
                    continue;
                var (ni, nj) = (i + di, j + dj);
                if (ni >= 0 && nj >= 0 && ni < Columns && nj < Rows)
                    yield return (ni, nj);
            }
    }

    /// <summary>Une empreinte du relief, pour savoir si le réseau d'écoulement gardé est encore bon.</summary>
    private long GridStamp()
    {
        unchecked
        {
            long hash = 17;
            foreach (var value in _grid)
                hash = hash * 31 + BitConverter.DoubleToInt64Bits(value);
            return hash;
        }
    }
}
