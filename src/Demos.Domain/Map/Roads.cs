using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Domain.Map;

/// <summary>Le niveau d'une liaison : rien, un sentier, un chemin, une route.</summary>
public enum RoadLevel
{
    None = 0,
    Trail = 1,
    Path = 2,
    Road = 3,
}

/// <summary>
/// Une liaison entre deux lieux (deux communes) : son niveau, son tracé sur la carte (en unités de carte), sa longueur (en km) et sa
/// montée (la somme des dénivelés, en mètres), et la structure qui la paie.
/// </summary>
public sealed record RoadLink(string A, string B, RoadLevel Level, IReadOnlyList<Point2D> Route, double Length, double Climb, string PayerId)
{
    /// <summary>La clé d'une paire de lieux, la même dans les deux sens.</summary>
    public static (string, string) Key(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}

/// <summary>Les liaisons de la partie, par paire de lieux.</summary>
public sealed class RoadNetwork
{
    private readonly Dictionary<(string, string), RoadLink> _links = [];

    public IReadOnlyCollection<RoadLink> Links => _links.Values;

    public RoadLink? Between(string a, string b) => _links.GetValueOrDefault(RoadLink.Key(a, b));

    public RoadLevel LevelBetween(string a, string b) => Between(a, b)?.Level ?? RoadLevel.None;

    public void Set(RoadLink link)
    {
        if (link.Level == RoadLevel.None)
            _links.Remove(RoadLink.Key(link.A, link.B));
        else
            _links[RoadLink.Key(link.A, link.B)] = link;
    }

    /// <summary>La part de la perte de transport qui reste entre deux lieux : 1 sans chemin ni route.</summary>
    public double LossFactor(string a, string b) => Roads.LossFactor(LevelBetween(a, b), Balance.Current);
}

/// <summary>Le trafic entre lieux : ce qui a été échangé (dans les deux sens) depuis le début du mois.</summary>
public sealed class RoadTraffic
{
    private Dictionary<(string, string), double> _month = [];

    public void Add(string a, string b, double quantity)
    {
        if (a == b || quantity <= 0)
            return;
        var key = RoadLink.Key(a, b);
        _month[key] = _month.GetValueOrDefault(key) + quantity;
    }

    public IReadOnlyDictionary<(string, string), double> ThisMonth => _month;

    /// <summary>Le trafic du dernier mois fini (celui qui a décidé des routes).</summary>
    public IReadOnlyDictionary<(string, string), double> LastMonth { get; private set; } = new Dictionary<(string, string), double>();

    /// <summary>Clôt le mois : son trafic devient celui du dernier mois, et le compte repart de zéro.</summary>
    public IReadOnlyDictionary<(string, string), double> EndMonth()
    {
        LastMonth = _month;
        _month = [];
        return LastMonth;
    }
}

/// <summary>Ce qu'une liaison a changé au cours d'un mois ou d'une année (pour les messages et le rapport).</summary>
public sealed record RoadChange(RoadLink Link, RoadLevel Before, double Cost, string Reason);

/// <summary>
/// Les routes (règle décidée le 10 novembre 2026) :
/// - le trafic les crée : quand deux lieux échangent assez en un mois, un sentier apparaît, puis il devient un chemin,
///   puis une route (un niveau par mois au plus) ;
/// - le tracé suit le chemin le moins cher sur la carte d'altitude (<see cref="RoadPlanner"/>) ;
/// - un chemin retire une part de la perte de transport, une route une plus grande part ;
/// - la construction coûte selon la longueur et la montée, et l'entretien chaque année ; c'est la plus petite structure
///   qui contient les deux lieux qui paie (la commune, la préfecture, sinon l'État). Sans l'argent, rien n'est construit ;
///   un entretien impayé fait descendre la liaison d'un niveau (validé le 11 novembre 2026).
/// Les chiffres sont des réglages (<see cref="BalanceSettings"/>), validés le 11 novembre 2026.
/// </summary>
public static class Roads
{
    public static double LossFactor(RoadLevel level, BalanceSettings settings) => level switch
    {
        RoadLevel.Path => 1 - settings.RoadPathLossCut,
        RoadLevel.Road => 1 - settings.RoadRoadLossCut,
        _ => 1,
    };

    /// <summary>Le niveau que mérite un trafic d'un mois (sans compter la montée d'un niveau par mois).</summary>
    public static RoadLevel LevelFor(double monthlyTraffic, BalanceSettings settings) =>
        monthlyTraffic >= settings.RoadRoadTraffic ? RoadLevel.Road
        : monthlyTraffic >= settings.RoadPathTraffic ? RoadLevel.Path
        : monthlyTraffic >= settings.RoadTrailTraffic ? RoadLevel.Trail
        : RoadLevel.None;

    /// <summary>La longueur comptée pour le coût et l'entretien : la longueur plus la montée.</summary>
    public static double CostLength(double length, double climb, BalanceSettings settings) =>
        length + climb * settings.RoadClimbLength;

    /// <summary>Le coût pour monter une liaison à ce niveau (depuis le niveau juste en dessous).</summary>
    public static double BuildCost(RoadLevel level, double length, double climb, BalanceSettings settings) =>
        CostLength(length, climb, settings) * level switch
        {
            RoadLevel.Trail => settings.RoadTrailCost,
            RoadLevel.Path => settings.RoadPathCost,
            RoadLevel.Road => settings.RoadRoadCost,
            _ => 0,
        };

    /// <summary>L'entretien d'une liaison pour une année.</summary>
    public static double Upkeep(RoadLink link, BalanceSettings settings) =>
        CostLength(link.Length, link.Climb, settings) * link.Level switch
        {
            RoadLevel.Trail => settings.RoadTrailUpkeep,
            RoadLevel.Path => settings.RoadPathUpkeep,
            RoadLevel.Road => settings.RoadRoadUpkeep,
            _ => 0,
        };

    /// <summary>
    /// La plus petite structure qui contient les deux lieux : le premier de <paramref name="a"/> et de ses parents qui est
    /// aussi <paramref name="b"/> ou l'un de ses parents (la commune, la préfecture, l'État) ; null s'il n'y en a pas.
    /// </summary>
    public static string? PayerOf(string a, string b, IReadOnlyDictionary<string, Structure> structures)
    {
        var ancestorsOfB = new HashSet<string>(Ancestors(b, structures));
        return Ancestors(a, structures).FirstOrDefault(ancestorsOfB.Contains);
    }

    private static IEnumerable<string> Ancestors(string id, IReadOnlyDictionary<string, Structure> structures)
    {
        var seen = new HashSet<string>();
        for (var current = id; current is not null && seen.Add(current); current = structures.GetValueOrDefault(current)?.ParentId)
            yield return current;
    }

    /// <summary>
    /// Le bilan d'un mois : chaque paire de lieux dont le trafic du mois mérite un niveau de plus monte d'un niveau, si
    /// le tracé est possible et si la structure qui paie a l'argent. Les paires au plus fort trafic passent d'abord (leur
    /// tracé sert ensuite aux autres).
    /// </summary>
    public static IReadOnlyList<RoadChange> RunMonthly(
        RoadNetwork network,
        IReadOnlyDictionary<(string, string), double> monthlyTraffic,
        Func<string, Point2D?> positionOf,
        RoadPlanner planner,
        Func<string, string, string?> payerOf,
        Func<string, Treasury> fundOf,
        BalanceSettings settings)
    {
        var changes = new List<RoadChange>();
        foreach (var ((a, b), traffic) in monthlyTraffic.OrderByDescending(t => t.Value).ThenBy(t => t.Key.Item1).ThenBy(t => t.Key.Item2))
        {
            var current = network.Between(a, b);
            var before = current?.Level ?? RoadLevel.None;
            if (LevelFor(traffic, settings) <= before || before == RoadLevel.Road)
                continue;
            if (payerOf(a, b) is not { } payerId)
                continue;

            var link = current;
            if (link is null)
            {
                if (positionOf(a) is not { } from || positionOf(b) is not { } to
                    || planner.Plan(from, to, network.Links.Select(l => l.Route)) is not { } plan)
                    continue;
                link = new RoadLink(RoadLink.Key(a, b).Item1, RoadLink.Key(a, b).Item2, RoadLevel.None, plan.Route, plan.Length, plan.Climb, payerId);
            }

            var next = before + 1;
            var cost = BuildCost(next, link.Length, link.Climb, settings);
            if (!fundOf(payerId).TrySpend(cost))
                continue;
            var built = link with { Level = next, PayerId = payerId };
            network.Set(built);
            changes.Add(new RoadChange(built, before, cost, "trafic"));
        }
        return changes;
    }

    /// <summary>L'entretien d'une année : chaque liaison le paie, sinon elle descend d'un niveau.</summary>
    public static IReadOnlyList<RoadChange> RunYearly(RoadNetwork network, Func<string, Treasury> fundOf, BalanceSettings settings)
    {
        var changes = new List<RoadChange>();
        foreach (var link in network.Links.OrderBy(l => l.A).ThenBy(l => l.B).ToList())
        {
            var upkeep = Upkeep(link, settings);
            if (fundOf(link.PayerId).TrySpend(upkeep))
            {
                changes.Add(new RoadChange(link, link.Level, upkeep, "entretien"));
                continue;
            }
            var lowered = link with { Level = link.Level - 1 };
            network.Set(lowered);
            changes.Add(new RoadChange(lowered, link.Level, 0, "entretien impayé"));
        }
        return changes;
    }
}

/// <summary>Un tracé : ses points (en unités de carte), sa longueur (km) et sa montée (somme des dénivelés, en mètres).</summary>
public sealed record RoadPlan(IReadOnlyList<Point2D> Route, double Length, double Climb);

/// <summary>
/// Le tracé d'une liaison : le chemin le moins cher sur une grille posée sur la carte d'altitude. Un pas coûte sa
/// longueur × (1 + poids × pente en %) ; une pente plus forte que la pente permise est interdite, comme la mer ; un pas sur
/// une voie déjà tracée coûte moins cher, donc le réseau se ramifie. Sans carte d'altitude, le tracé est la ligne droite.
/// </summary>
public sealed class RoadPlanner(ReliefGrid? relief, BalanceSettings settings)
{
    private readonly ReliefGrid? _relief = relief;
    // La case de la grille, en unités de carte (le réglage est en km).
    private readonly double _cell = Math.Max(1, MapScale.ToUnits(settings.RoadGridCell));
    private float[]? _altitudes;
    private int _width;
    private int _height;

    public RoadPlan? Plan(Point2D from, Point2D to, IEnumerable<IReadOnlyList<Point2D>> existingRoutes)
    {
        if (_relief is null)
            return new RoadPlan([from, to], MapScale.KmBetween(from, to), 0);

        EnsureGrid();
        var start = CellOf(from);
        var goal = CellOf(to);
        var onRoad = RasterizeRoutes(existingRoutes);

        var count = _width * _height;
        var best = new double[count];
        Array.Fill(best, double.PositiveInfinity);
        var cameFrom = new int[count];
        Array.Fill(cameFrom, -1);
        var startIndex = start.Y * _width + start.X;
        var goalIndex = goal.Y * _width + goal.X;
        best[startIndex] = 0;
        var open = new PriorityQueue<int, double>();
        open.Enqueue(startIndex, 0);
        var minFactor = Math.Min(1, settings.RoadReuseFactor);

        while (open.TryDequeue(out var index, out var priority))
        {
            if (index == goalIndex)
                break;
            var (x, y) = (index % _width, index / _width);
            if (priority > best[index] + Heuristic(x, y, goal, minFactor) + 1e-9)
                continue;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= _width || ny >= _height)
                        continue;
                    var next = ny * _width + nx;
                    // La mer est interdite, sauf au départ et à l'arrivée.
                    if (_altitudes![next] < 0 && next != goalIndex)
                        continue;
                    var step = _cell * Math.Sqrt(dx * dx + dy * dy);
                    // La pente en % : mètres de dénivelé pour 100 m.
                    var slope = Math.Abs(_altitudes[next] - _altitudes[index]) / (step * MapScale.MetersPerUnit) * 100;
                    if (slope > settings.RoadMaxSlope)
                        continue;
                    var cost = step * (1 + settings.RoadSlopeWeight * slope) * (onRoad[next] ? settings.RoadReuseFactor : 1);
                    var total = best[index] + cost;
                    if (total >= best[next])
                        continue;
                    best[next] = total;
                    cameFrom[next] = index;
                    open.Enqueue(next, total + Heuristic(nx, ny, goal, minFactor));
                }
        }

        if (goalIndex != startIndex && cameFrom[goalIndex] < 0)
            return null;

        // Le tracé, du départ à l'arrivée : les centres des cases, avec les deux lieux exacts aux bouts.
        var cells = new List<int>();
        for (var i = goalIndex; i >= 0; i = cameFrom[i])
            cells.Add(i);
        cells.Reverse();
        var route = new List<Point2D> { from };
        route.AddRange(cells.Skip(1).Take(Math.Max(0, cells.Count - 2)).Select(CenterOf));
        route.Add(to);

        double length = 0, climb = 0;
        for (var i = 1; i < route.Count; i++)
        {
            length += MapScale.KmBetween(route[i - 1], route[i]);
            climb += Math.Abs(_relief.Sample(route[i].X, route[i].Y) - _relief.Sample(route[i - 1].X, route[i - 1].Y));
        }
        return new RoadPlan(route, length, climb);
    }

    private double Heuristic(int x, int y, (int X, int Y) goal, double minFactor)
    {
        var dx = x - goal.X;
        var dy = y - goal.Y;
        return Math.Sqrt(dx * dx + dy * dy) * _cell * minFactor;
    }

    private void EnsureGrid()
    {
        if (_altitudes is not null)
            return;
        var worldWidth = _relief!.Width * _relief.CellSize;
        var worldHeight = _relief.Height * _relief.CellSize;
        _width = Math.Max(1, (int)Math.Ceiling(worldWidth / _cell));
        _height = Math.Max(1, (int)Math.Ceiling(worldHeight / _cell));
        _altitudes = new float[_width * _height];
        for (var y = 0; y < _height; y++)
            for (var x = 0; x < _width; x++)
            {
                var c = CenterOf(y * _width + x);
                _altitudes[y * _width + x] = (float)_relief.Sample(c.X, c.Y);
            }
    }

    private (int X, int Y) CellOf(Point2D p) =>
        (Math.Clamp((int)(p.X / _cell), 0, _width - 1), Math.Clamp((int)(p.Y / _cell), 0, _height - 1));

    private Point2D CenterOf(int index) => new((index % _width + 0.5) * _cell, (index / _width + 0.5) * _cell);

    private bool[] RasterizeRoutes(IEnumerable<IReadOnlyList<Point2D>> routes)
    {
        var onRoad = new bool[_width * _height];
        foreach (var route in routes)
            for (var i = 1; i < route.Count; i++)
            {
                var a = route[i - 1];
                var b = route[i];
                var steps = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) / (_cell * 0.5)));
                for (var s = 0; s <= steps; s++)
                {
                    var t = (double)s / steps;
                    var (x, y) = CellOf(new Point2D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                    onRoad[y * _width + x] = true;
                }
            }
        return onRoad;
    }
}
