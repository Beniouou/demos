using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;

namespace Demos.Carte;

/// <summary>
/// Algorithme Marching Squares : extrait des courbes de niveau mathématiquement parfaites
/// (isohypses continues, boucles fermées, jamais d'interruption dans le vide).
/// </summary>
public static class ContourExtractor
{
    public readonly record struct Segment(Point2D P1, Point2D P2);

    public static List<ContourLine> ExtractContours(
        Heightmap heightmap,
        IReadOnlyList<double> levels)
    {
        var result = new List<ContourLine>();

        foreach (var level in levels)
        {
            var segments = ExtractSegmentsForLevel(heightmap, level);
            var polylines = ChainSegments(segments);

            var isIndex = ((int)Math.Round(level) % 100) == 0;

            foreach (var poly in polylines)
            {
                if (poly.Count < 2)
                    continue;

                // Vérifier si la polyligne forme une boucle fermée
                var isClosed = Distance(poly[0], poly[^1]) < 8.0;
                result.Add(new ContourLine(level, isIndex, isClosed, poly));
            }
        }

        return result;
    }

    public static List<Segment> ExtractSegmentsForLevel(Heightmap hm, double level)
    {
        var segments = new List<Segment>();

        for (var i = 0; i < hm.Columns - 1; i++)
        {
            for (var j = 0; j < hm.Rows - 1; j++)
            {
                var x0 = hm.GetWorldX(i);
                var x1 = hm.GetWorldX(i + 1);
                var y0 = hm.GetWorldY(j);
                var y1 = hm.GetWorldY(j + 1);

                var zTL = hm[i, j];
                var zTR = hm[i + 1, j];
                var zBR = hm[i + 1, j + 1];
                var zBL = hm[i, j + 1];

                var config = (zTL >= level ? 8 : 0) |
                             (zTR >= level ? 4 : 0) |
                             (zBR >= level ? 2 : 0) |
                             (zBL >= level ? 1 : 0);

                if (config is 0 or 15)
                    continue;

                // Points d'intersection sur les 4 arêtes
                Point2D? ptTop = (config & 12) is 4 or 8 ? Interp(x0, y0, zTL, x1, y0, zTR, level) : null;
                Point2D? ptRight = (config & 6) is 2 or 4 ? Interp(x1, y0, zTR, x1, y1, zBR, level) : null;
                Point2D? ptBottom = (config & 3) is 1 or 2 ? Interp(x0, y1, zBL, x1, y1, zBR, level) : null;
                Point2D? ptLeft = (config & 9) is 1 or 8 ? Interp(x0, y0, zTL, x0, y1, zBL, level) : null;

                switch (config)
                {
                    case 1: // BL
                    case 14:
                        if (ptBottom.HasValue && ptLeft.HasValue) segments.Add(new(ptBottom.Value, ptLeft.Value));
                        break;
                    case 2: // BR
                    case 13:
                        if (ptRight.HasValue && ptBottom.HasValue) segments.Add(new(ptRight.Value, ptBottom.Value));
                        break;
                    case 3: // BL + BR
                    case 12:
                        if (ptLeft.HasValue && ptRight.HasValue) segments.Add(new(ptLeft.Value, ptRight.Value));
                        break;
                    case 4: // TR
                    case 11:
                        if (ptTop.HasValue && ptRight.HasValue) segments.Add(new(ptTop.Value, ptRight.Value));
                        break;
                    case 6: // TR + BR
                    case 9:
                        if (ptTop.HasValue && ptBottom.HasValue) segments.Add(new(ptTop.Value, ptBottom.Value));
                        break;
                    case 7: // TR + BR + BL
                    case 8:
                        if (ptLeft.HasValue && ptTop.HasValue) segments.Add(new(ptLeft.Value, ptTop.Value));
                        break;
                    case 5: // Selle BL + TR
                        var avg5 = (zTL + zTR + zBR + zBL) * 0.25;
                        if (avg5 >= level)
                        {
                            if (ptTop.HasValue && ptLeft.HasValue) segments.Add(new(ptTop.Value, ptLeft.Value));
                            if (ptRight.HasValue && ptBottom.HasValue) segments.Add(new(ptRight.Value, ptBottom.Value));
                        }
                        else
                        {
                            if (ptTop.HasValue && ptRight.HasValue) segments.Add(new(ptTop.Value, ptRight.Value));
                            if (ptLeft.HasValue && ptBottom.HasValue) segments.Add(new(ptLeft.Value, ptBottom.Value));
                        }
                        break;
                    case 10: // Selle TL + BR
                        var avg10 = (zTL + zTR + zBR + zBL) * 0.25;
                        if (avg10 >= level)
                        {
                            if (ptTop.HasValue && ptRight.HasValue) segments.Add(new(ptTop.Value, ptRight.Value));
                            if (ptLeft.HasValue && ptBottom.HasValue) segments.Add(new(ptLeft.Value, ptBottom.Value));
                        }
                        else
                        {
                            if (ptTop.HasValue && ptLeft.HasValue) segments.Add(new(ptTop.Value, ptLeft.Value));
                            if (ptRight.HasValue && ptBottom.HasValue) segments.Add(new(ptRight.Value, ptBottom.Value));
                        }
                        break;
                }
            }
        }

        return segments;
    }

    private static Point2D Interp(double x1, double y1, double z1, double x2, double y2, double z2, double target)
    {
        var denom = z2 - z1;
        var t = Math.Abs(denom) < 0.0001 ? 0.5 : Math.Clamp((target - z1) / denom, 0.0, 1.0);
        return new Point2D(
            Math.Round(x1 + t * (x2 - x1), 1),
            Math.Round(y1 + t * (y2 - y1), 1));
    }

    public static List<List<Point2D>> ChainSegments(List<Segment> segments)
    {
        var polylines = new List<List<Point2D>>();
        var pool = new HashSet<Segment>(segments);

        while (pool.Count > 0)
        {
            var first = pool.First();
            pool.Remove(first);

            var poly = new LinkedList<Point2D>();
            poly.AddFirst(first.P1);
            poly.AddLast(first.P2);

            bool grew;
            do
            {
                grew = false;
                var head = poly.First!.Value;
                var tail = poly.Last!.Value;

                // Tenter d'étendre la tête
                Segment? foundHead = null;
                var flipHead = false;
                foreach (var seg in pool)
                {
                    if (Distance(seg.P2, head) < 1.0)
                    {
                        foundHead = seg;
                        flipHead = false;
                        break;
                    }
                    if (Distance(seg.P1, head) < 1.0)
                    {
                        foundHead = seg;
                        flipHead = true;
                        break;
                    }
                }

                if (foundHead.HasValue)
                {
                    pool.Remove(foundHead.Value);
                    poly.AddFirst(flipHead ? foundHead.Value.P2 : foundHead.Value.P1);
                    grew = true;
                }

                // Tenter d'étendre la queue
                Segment? foundTail = null;
                var flipTail = false;
                foreach (var seg in pool)
                {
                    if (Distance(seg.P1, tail) < 1.0)
                    {
                        foundTail = seg;
                        flipTail = false;
                        break;
                    }
                    if (Distance(seg.P2, tail) < 1.0)
                    {
                        foundTail = seg;
                        flipTail = true;
                        break;
                    }
                }

                if (foundTail.HasValue)
                {
                    pool.Remove(foundTail.Value);
                    poly.AddLast(flipTail ? foundTail.Value.P1 : foundTail.Value.P2);
                    grew = true;
                }

            } while (grew);

            polylines.Add(poly.ToList());
        }

        return polylines;
    }

    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Extrait les étendues maritimes directement à partir de la courbe de niveau 0 m (trait de côte)
    /// calculée par Marching Squares, garantissant un littoral 100% vectoriel, lisse et sans cubes.
    /// </summary>
    public static List<PolygonArea> ExtractWaterBodies(Heightmap hm)
    {
        var result = new List<PolygonArea>();
        if (hm.MinElevation >= 0.0)
            return result;

        if (hm.MaxElevation <= 0.0)
        {
            var allSea = new List<Point2D>
            {
                new(0, 0),
                new(hm.WorldWidth, 0),
                new(hm.WorldWidth, hm.WorldHeight),
                new(0, hm.WorldHeight)
            };
            result.Add(new PolygonArea("Océan", "Sea", allSea));
            return result;
        }

        var segments = ExtractSegmentsForLevel(hm, 0.0);
        var polylines = ChainSegments(segments);

        // Déterminer si le monde extérieur est un océan entourant la terre
        // en vérifiant si les bordures du monde sont sous le niveau de la mer (< 0 m)
        var corners = new Point2D[]
        {
            new(0, 0),
            new(hm.WorldWidth, 0),
            new(hm.WorldWidth, hm.WorldHeight),
            new(0, hm.WorldHeight)
        };
        var isOceanSurrounding = corners.Count(c => hm.SampleElevation(c.X, c.Y) < 0.0) >= 2;

        var openPolylines = new List<List<Point2D>>();

        foreach (var poly in polylines)
        {
            if (poly.Count < 2) continue;

            var touchesBorder = IsOnBorder(poly[0], hm.WorldWidth, hm.WorldHeight) ||
                                IsOnBorder(poly[^1], hm.WorldWidth, hm.WorldHeight);

            if (!touchesBorder && Distance(poly[0], poly[^1]) < 16.0)
            {
                var isIsland = IsIslandLoop(poly, hm);
                if (isIsland)
                {
                    if (isOceanSurrounding)
                    {
                        // Île isolée au milieu de l'eau : l'océan enveloppe la boucle de terre
                        var (northSea, southSea) = BuildOceanAroundIsland(poly, hm.WorldWidth, hm.WorldHeight);
                        if (northSea.Count >= 3) result.Add(new PolygonArea("Océan Boréal", "Sea", northSea));
                        if (southSea.Count >= 3) result.Add(new PolygonArea("Océan Austral", "Sea", southSea));
                    }
                }
                else
                {
                    // Mer intérieure ou lac entouré de terre (altitude intérieure < 0 m)
                    result.Add(new PolygonArea("Mer Intérieure", "Water", poly));
                }
            }
            else
            {
                openPolylines.Add(poly);
            }
        }

        // Fermeture conjointe et topologique de toutes les polylignes de bordure
        if (openPolylines.Count > 0)
        {
            var coastalWaters = CloseOpenCoastlinesAlongBorders(openPolylines, hm);
            result.AddRange(coastalWaters);
        }

        return result;
    }

    private static bool IsOnBorder(Point2D p, double w, double h) =>
        p.X <= 2.5 || p.X >= w - 2.5 || p.Y <= 2.5 || p.Y >= h - 2.5;

    private static double GetPerimeterPos(Point2D p, double w, double h)
    {
        var dTop = Math.Abs(p.Y);
        var dRight = Math.Abs(w - p.X);
        var dBottom = Math.Abs(h - p.Y);
        var dLeft = Math.Abs(p.X);

        var min = Math.Min(Math.Min(dTop, dRight), Math.Min(dBottom, dLeft));

        if (min == dTop) return Math.Clamp(p.X, 0, w);
        if (min == dRight) return w + Math.Clamp(p.Y, 0, h);
        if (min == dBottom) return w + h + Math.Clamp(w - p.X, 0, w);
        return 2 * w + h + Math.Clamp(h - p.Y, 0, h);
    }

    private static Point2D GetPointOnPerimeter(double s, double w, double h)
    {
        var l = 2 * (w + h);
        s = (s % l + l) % l;
        if (s <= w) return new Point2D(s, 0);
        if (s <= w + h) return new Point2D(w, s - w);
        if (s <= 2 * w + h) return new Point2D(w - (s - (w + h)), h);
        return new Point2D(0, h - (s - (2 * w + h)));
    }

    private sealed record BorderEndpoint(
        Point2D Pt,
        double S,
        int PolyIndex,
        bool IsStart);

    private static Point2D SnapToBorder(Point2D p, double w, double h)
    {
        var x = p.X;
        var y = p.Y;
        var dLeft = x;
        var dRight = w - x;
        var dTop = y;
        var dBottom = h - y;
        var min = Math.Min(Math.Min(dTop, dRight), Math.Min(dBottom, dLeft));
        if (min == dTop) return new Point2D(Math.Clamp(x, 0, w), 0);
        if (min == dRight) return new Point2D(w, Math.Clamp(y, 0, h));
        if (min == dBottom) return new Point2D(Math.Clamp(x, 0, w), h);
        return new Point2D(0, Math.Clamp(y, 0, h));
    }

    private static List<Point2D> GetPerimeterPath(double sA, double sB, double w, double h, bool clockwise)
    {
        var pts = new List<Point2D>();
        var l = 2 * (w + h);
        var corners = new (double s, Point2D pt)[]
        {
            (0, new Point2D(0, 0)),
            (w, new Point2D(w, 0)),
            (w + h, new Point2D(w, h)),
            (2 * w + h, new Point2D(0, h))
        };

        if (clockwise)
        {
            var dist = (sB - sA + l) % l;
            if (dist < 0.1) dist += l;
            var expanded = corners
                .Concat(corners.Select(c => (s: c.s + l, pt: c.pt)))
                .Concat(corners.Select(c => (s: c.s + 2 * l, pt: c.pt)));
            var inRange = expanded
                .Where(c => c.s > sA + 0.5 && c.s < sA + dist - 0.5)
                .OrderBy(c => c.s);
            foreach (var c in inRange)
            {
                pts.Add(c.pt);
            }
        }
        else
        {
            var dist = (sA - sB + l) % l;
            if (dist < 0.1) dist += l;
            var expanded = corners
                .Concat(corners.Select(c => (s: c.s - l, pt: c.pt)))
                .Concat(corners.Select(c => (s: c.s - 2 * l, pt: c.pt)));
            var inRange = expanded
                .Where(c => c.s < sA - 0.5 && c.s > sA - dist + 0.5)
                .OrderByDescending(c => c.s);
            foreach (var c in inRange)
            {
                pts.Add(c.pt);
            }
        }

        return pts;
    }

    private static double ComputePolygonArea(List<Point2D> poly)
    {
        var area = 0.0;
        for (var i = 0; i < poly.Count; i++)
        {
            var j = (i + 1) % poly.Count;
            area += poly[i].X * poly[j].Y - poly[j].X * poly[i].Y;
        }
        return Math.Abs(area) * 0.5;
    }

    private static bool IsValidWaterBody(List<Point2D> poly, Heightmap hm)
    {
        if (poly.Count < 3) return false;
        var w = hm.WorldWidth;
        var h = hm.WorldHeight;

        // Échantillonner les sommets
        var step = Math.Max(1, poly.Count / 16);
        var sumElev = 0.0;
        var count = 0;
        for (var i = 0; i < poly.Count; i += step)
        {
            sumElev += hm.SampleElevation(poly[i].X, poly[i].Y);
            count++;
        }
        var avgVertex = sumElev / count;
        if (avgVertex > 2.0) return false;

        // Rejet de sécurité : si la carte est continentale (au moins 3 coins émergés)
        // et que le polygone prétend couvrir plus de 75% du monde entier, c'est une inversion
        var corners = new Point2D[] { new(0, 0), new(w, 0), new(w, h), new(0, h) };
        var cornersSubmerged = corners.Count(c => hm.SampleElevation(c.X, c.Y) < 0.0);
        if (cornersSubmerged < 2 && ComputePolygonArea(poly) > 0.75 * w * h)
        {
            return false;
        }

        return true;
    }

    private static List<PolygonArea> CloseOpenCoastlinesAlongBorders(List<List<Point2D>> openPolylines, Heightmap hm)
    {
        var result = new List<PolygonArea>();
        if (openPolylines.Count == 0) return result;

        var w = hm.WorldWidth;
        var h = hm.WorldHeight;
        var l = 2 * (w + h);

        // 1. Snapper les extrémités de chaque polyligne exactement sur la bordure
        var snappedPolys = new List<List<Point2D>>();
        for (var i = 0; i < openPolylines.Count; i++)
        {
            var poly = openPolylines[i];
            if (poly.Count < 2) continue;
            var list = new List<Point2D>(poly);
            list[0] = SnapToBorder(list[0], w, h);
            list[^1] = SnapToBorder(list[^1], w, h);
            snappedPolys.Add(list);
        }

        if (snappedPolys.Count == 0) return result;

        // 2. Référencer toutes les extrémités sur le périmètre
        var endpoints = new List<BorderEndpoint>();
        for (var i = 0; i < snappedPolys.Count; i++)
        {
            var poly = snappedPolys[i];
            var pStart = poly[0];
            var pEnd = poly[^1];
            endpoints.Add(new BorderEndpoint(pStart, GetPerimeterPos(pStart, w, h), i, true));
            endpoints.Add(new BorderEndpoint(pEnd, GetPerimeterPos(pEnd, w, h), i, false));
        }

        // Trier par position croissante sur le périmètre [0, l)
        var sorted = endpoints.OrderBy(e => e.S).ToList();
        var N = sorted.Count;

        // 3. Classifier chaque intervalle entre deux extrémités consécutives comme Immergé (< 0 m) ou Émergé (>= 0 m)
        var isIntervalSubmerged = new bool[N];
        for (var i = 0; i < N; i++)
        {
            var s1 = sorted[i].S;
            var s2 = sorted[(i + 1) % N].S;
            var dist = (s2 - s1 + l) % l;
            if (dist < 0.05) dist += l;

            var midS = (s1 + dist * 0.5) % l;
            var midPt = GetPointOnPerimeter(midS, w, h);
            var subCount = hm.SampleElevation(midPt.X, midPt.Y) < 0.0 ? 1 : 0;

            if (dist > 15.0)
            {
                var p1 = GetPointOnPerimeter((s1 + dist * 0.25) % l, w, h);
                var p2 = GetPointOnPerimeter((s1 + dist * 0.75) % l, w, h);
                if (hm.SampleElevation(p1.X, p1.Y) < 0.0) subCount++;
                if (hm.SampleElevation(p2.X, p2.Y) < 0.0) subCount++;
                isIntervalSubmerged[i] = subCount >= 2;
            }
            else
            {
                isIntervalSubmerged[i] = subCount >= 1;
            }
        }

        // 4. Parcourir les cycles alternant (Polyligne intérieure) -> (Intervalle périmétrique immergé)
        var visitedPolys = new HashSet<int>();

        for (var startPolyIdx = 0; startPolyIdx < snappedPolys.Count; startPolyIdx++)
        {
            if (visitedPolys.Contains(startPolyIdx)) continue;

            var cycle = new List<Point2D>();
            var curPolyIdx = startPolyIdx;

            var firstPoly = snappedPolys[curPolyIdx];
            cycle.AddRange(firstPoly);
            visitedPolys.Add(curPolyIdx);

            var curEndpointPt = firstPoly[^1];
            var curEndpointS = GetPerimeterPos(curEndpointPt, w, h);
            var targetEndpointPt = firstPoly[0];

            var closed = false;
            for (var stepGuard = 0; stepGuard < snappedPolys.Count * 2 + 8; stepGuard++)
            {
                var epIdx = -1;
                var minDist = double.MaxValue;
                for (var i = 0; i < N; i++)
                {
                    var d = Math.Abs(sorted[i].S - curEndpointS);
                    if (d > l * 0.5) d = l - d;
                    if (d < minDist)
                    {
                        minDist = d;
                        epIdx = i;
                    }
                }

                if (epIdx < 0) break;

                var fwdSub = isIntervalSubmerged[epIdx];
                var bwdSub = isIntervalSubmerged[(epIdx - 1 + N) % N];

                int nextEpIdx;
                bool walkCw;
                if (fwdSub && !bwdSub)
                {
                    nextEpIdx = (epIdx + 1) % N;
                    walkCw = true;
                }
                else if (bwdSub && !fwdSub)
                {
                    nextEpIdx = (epIdx - 1 + N) % N;
                    walkCw = false;
                }
                else
                {
                    var dFwd = (sorted[(epIdx + 1) % N].S - sorted[epIdx].S + l) % l;
                    var dBwd = (sorted[epIdx].S - sorted[(epIdx - 1 + N) % N].S + l) % l;
                    var elevFwd = hm.SampleElevation(GetPointOnPerimeter((sorted[epIdx].S + dFwd * 0.5) % l, w, h).X, GetPointOnPerimeter((sorted[epIdx].S + dFwd * 0.5) % l, w, h).Y);
                    var elevBwd = hm.SampleElevation(GetPointOnPerimeter((sorted[epIdx].S - dBwd * 0.5 + l) % l, w, h).X, GetPointOnPerimeter((sorted[epIdx].S - dBwd * 0.5 + l) % l, w, h).Y);
                    walkCw = elevFwd <= elevBwd;
                    nextEpIdx = walkCw ? (epIdx + 1) % N : (epIdx - 1 + N) % N;
                }

                var nextEp = sorted[nextEpIdx];

                var perimCorners = GetPerimeterPath(sorted[epIdx].S, nextEp.S, w, h, walkCw);
                cycle.AddRange(perimCorners);
                cycle.Add(nextEp.Pt);

                if (Distance(nextEp.Pt, targetEndpointPt) < 2.0 || nextEp.PolyIndex == startPolyIdx)
                {
                    closed = true;
                    break;
                }

                curPolyIdx = nextEp.PolyIndex;
                visitedPolys.Add(curPolyIdx);
                var nextPoly = snappedPolys[curPolyIdx];

                if (nextEp.IsStart)
                {
                    for (var k = 1; k < nextPoly.Count; k++) cycle.Add(nextPoly[k]);
                    curEndpointPt = nextPoly[^1];
                    curEndpointS = GetPerimeterPos(curEndpointPt, w, h);
                }
                else
                {
                    for (var k = nextPoly.Count - 2; k >= 0; k--) cycle.Add(nextPoly[k]);
                    curEndpointPt = nextPoly[0];
                    curEndpointS = GetPerimeterPos(curEndpointPt, w, h);
                }
            }

            if (closed && cycle.Count >= 3 && IsValidWaterBody(cycle, hm))
            {
                result.Add(new PolygonArea("Océan Littoral", "Sea", cycle));
            }
        }

        return result;
    }

    private static bool IsIslandLoop(List<Point2D> loop, Heightmap hm)
    {
        if (loop.Count < 3) return false;

        var votesIsland = 0;
        var votesLake = 0;
        var step = Math.Max(1, loop.Count / 16);

        for (var i = 0; i < loop.Count - 1; i += step)
        {
            var p1 = loop[i];
            var p2 = loop[i + 1];
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.5) continue;

            var mx = (p1.X + p2.X) * 0.5;
            var my = (p1.Y + p2.Y) * 0.5;
            var nx = -dy / len;
            var ny = dx / len;

            for (var dist = 2.0; dist <= 6.0; dist += 2.0)
            {
                var s1 = new Point2D(mx + nx * dist, my + ny * dist);
                var s2 = new Point2D(mx - nx * dist, my - ny * dist);

                var in1 = IsPointInPolygon(s1.X, s1.Y, loop);
                var in2 = IsPointInPolygon(s2.X, s2.Y, loop);

                if (in1 != in2)
                {
                    var insidePt = in1 ? s1 : s2;
                    if (hm.SampleElevation(insidePt.X, insidePt.Y) >= 0.0)
                        votesIsland++;
                    else
                        votesLake++;
                }
            }
        }

        if (votesIsland > 0 || votesLake > 0)
            return votesIsland >= votesLake;

        var avgX = loop.Average(p => p.X);
        var avgY = loop.Average(p => p.Y);
        return hm.SampleElevation(avgX, avgY) >= 0.0;
    }

    private static bool IsPointInPolygon(double x, double y, IReadOnlyList<Point2D> poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].Y > y) != (poly[j].Y > y)) &&
                (x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static (List<Point2D> NorthSea, List<Point2D> SouthSea) BuildOceanAroundIsland(
        List<Point2D> loop, double worldWidth, double worldHeight)
    {
        int minIdx = 0, maxIdx = 0;
        double minX = double.MaxValue, maxX = double.MinValue;
        for (var i = 0; i < loop.Count; i++)
        {
            if (loop[i].X < minX) { minX = loop[i].X; minIdx = i; }
            if (loop[i].X > maxX) { maxX = loop[i].X; maxIdx = i; }
        }

        var pathA = new List<Point2D>();
        var pathB = new List<Point2D>();

        var idx = minIdx;
        while (true)
        {
            pathA.Add(loop[idx]);
            if (idx == maxIdx) break;
            idx = (idx + 1) % loop.Count;
        }

        idx = maxIdx;
        while (true)
        {
            pathB.Add(loop[idx]);
            if (idx == minIdx) break;
            idx = (idx + 1) % loop.Count;
        }

        var avgYA = pathA.Average(p => p.Y);
        var avgYB = pathB.Average(p => p.Y);

        List<Point2D> northCoast, southCoast;
        if (avgYA < avgYB)
        {
            northCoast = pathA.AsEnumerable().Reverse().ToList();
            southCoast = pathB;
        }
        else
        {
            northCoast = pathB;
            southCoast = pathA.AsEnumerable().Reverse().ToList();
        }

        var ptLeft = loop[minIdx];
        var ptRight = loop[maxIdx];

        var northSea = new List<Point2D>
        {
            new(0, ptLeft.Y),
            new(0, 0),
            new(worldWidth, 0),
            new(worldWidth, ptRight.Y)
        };
        northSea.AddRange(northCoast);
        northSea.Add(new(0, ptLeft.Y));

        var southSea = new List<Point2D>
        {
            new(0, ptLeft.Y)
        };
        southSea.AddRange(southCoast);
        southSea.Add(new(worldWidth, ptRight.Y));
        southSea.Add(new(worldWidth, worldHeight));
        southSea.Add(new(0, worldHeight));
        southSea.Add(new(0, ptLeft.Y));

        return (northSea, southSea);
    }


}
