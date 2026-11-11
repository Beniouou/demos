using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Le transport.
/// - Dans toutes les vues : les sentiers, chemins et routes, que le trafic crée (règle des routes, lue dans
///   <c>GameView.Roads</c>), sur leur tracé organique (adouci ici pour l'œil).
/// - Filtre Production et transport : les voies navigables, et les échanges entre communes (lus dans
///   <c>GameView.TradeFlows</c>), d'autant plus épais qu'ils sont forts. Écritures : la quantité d'un mois et la perte
///   de transport de chaque échange (la distance et la perte pour deux communes qui n'échangent rien).
/// </summary>
public sealed class TransportLayer : MapLayer
{
    // Les tracés adoucis, gardés tant que la liaison ne change pas.
    private readonly Dictionary<RoadLink, Vector2[]> _smoothed = new(ReferenceEqualityComparer.Instance);

    public override string Name => "Transport";
    public override string Content => "Les routes, les voies navigables et les échanges entre communes";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters | MapChange.Game;

    public override void Draw(MapDrawContext c)
    {
        var s = c.Zoom;
        var production = c.Has(MapFilters.ProductionTransport);

        // 1. Les voies navigables (filtre Production et transport)
        if (production && c.VectorMap is { } map)
        {
            foreach (var river in map.AllRivers)
            {
                if (river.Points.Count < 2) continue;
                var haloWidth = (float)river.Width * 2.0f * s;
                var shape = c.Scaled(river.Points);
                if (!c.IsOnScreen(shape, haloWidth + 4f))
                    continue;
                c.Pen.DrawPolyline(c.ToScreen(shape), MapPalette.TradeRiverHighlight, haloWidth, antialiased: true);
            }
        }

        // 2. Les échanges entre communes (filtre Production et transport), sous les routes
        if (production)
            foreach (var (from, to, flow) in Pairs(c))
            {
                var width = flow is null ? 2.2f * s : Math.Clamp(MathF.Log10(1 + (float)flow.MonthlyQuantity) * 1.6f, 1.5f, 9f);
                var color = flow is null ? new Color(MapPalette.TradeRoad, 0.5f) : MapPalette.TradeFlow;
                c.Pen.DrawDashedLine(c.ToScreen(from), c.ToScreen(to), color, width, dash: 8.0f * s);
            }

        // 3. Les sentiers, puis les chemins, puis les routes par-dessus (dans toutes les vues)
        if (c.GameView is null)
            return;
        var roads = c.GameView.Roads();
        foreach (var level in new[] { RoadLevel.Trail, RoadLevel.Path, RoadLevel.Road })
            foreach (var link in roads.Where(r => r.Level == level))
                DrawRoad(c, link);
        // Les tracés adoucis des liaisons qui ont changé ne servent plus.
        foreach (var stale in _smoothed.Keys.Where(k => !roads.Contains(k)).ToList())
            _smoothed.Remove(stale);
    }

    private void DrawRoad(MapDrawContext c, RoadLink link)
    {
        if (!_smoothed.TryGetValue(link, out var world))
            _smoothed[link] = world = Smooth(link.Route);
        var points = world.Select(c.ToScreen).ToArray();
        var s = c.Zoom;
        switch (link.Level)
        {
            case RoadLevel.Trail:
                for (var i = 1; i < points.Length; i++)
                    c.Pen.DrawDashedLine(points[i - 1], points[i], MapPalette.RoadTrail, Math.Max(1f, 1.2f * s), dash: 4f * s);
                break;
            case RoadLevel.Path:
                c.Pen.DrawPolyline(points, MapPalette.RoadPath, Math.Clamp(2f * s, 1.5f, 5f), antialiased: true);
                break;
            default:
                var width = Math.Clamp(3.5f * s, 2.5f, 9f);
                c.Pen.DrawPolyline(points, MapPalette.RoadEdge, width + 2f, antialiased: true);
                c.Pen.DrawPolyline(points, MapPalette.RoadSurface, width, antialiased: true);
                break;
        }
    }

    /// <summary>
    /// Le tracé adouci : les centres des cases du tracé (des pas en huit directions), arrondis par deux passes qui
    /// coupent chaque angle (Chaikin), les deux bouts gardés.
    /// </summary>
    private static Vector2[] Smooth(IReadOnlyList<Point2D> route)
    {
        var points = route.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList();
        for (var pass = 0; pass < 2 && points.Count > 2; pass++)
        {
            var next = new List<Vector2> { points[0] };
            for (var i = 0; i + 1 < points.Count; i++)
            {
                next.Add(points[i].Lerp(points[i + 1], 0.25f));
                next.Add(points[i].Lerp(points[i + 1], 0.75f));
            }
            next.Add(points[^1]);
            points = next;
        }
        return [.. points];
    }

    public override void DrawLabels(MapDrawContext c)
    {
        if (!c.Has(MapFilters.ProductionTransport))
            return;
        var s = c.Zoom;
        var font = c.Font;

        foreach (var (from, to, flow) in Pairs(c))
        {
            var dist = from.DistanceTo(to);
            var tag = flow is null
                ? $"{dist:F0} lieues · perte {GeographicEconomics.TransportLoss(dist) * 100:F0} %"
                : $"{flow.MonthlyQuantity:N0} par mois · perte {flow.Loss * 100:F0} %";
            var mid = (c.ToScreen(from) + c.ToScreen(to)) * 0.5f;
            var fontSize = (int)Math.Clamp(9 * s, 8, 11);
            var tagSize = font.GetStringSize(tag, HorizontalAlignment.Left, -1, fontSize);
            var tagRect = new Rect2(mid.X - tagSize.X * 0.5f - 4, mid.Y - tagSize.Y * 0.5f - 2, tagSize.X + 8, tagSize.Y + 4);

            c.Pen.DrawRect(tagRect, MapPalette.LabelBackground);
            c.Pen.DrawRect(tagRect, MapPalette.TradeRoad, filled: false, width: 0.9f);
            c.Pen.DrawString(font, new Vector2(tagRect.Position.X + 4, tagRect.Position.Y + tagSize.Y), tag, HorizontalAlignment.Left, -1, fontSize, MapPalette.TradeRoad);
        }
    }

    /// <summary>Chaque paire de communes, avec ce qu'elles échangent (rien si elles n'échangent pas).</summary>
    private static IEnumerable<(Vector2 From, Vector2 To, Demos.Application.TradeFlowView? Flow)> Pairs(MapDrawContext c)
    {
        var communes = c.Data.Communes();
        var flows = c.GameView?.TradeFlows().ToDictionary(f => RoadLink.Key(f.A, f.B)) ?? [];
        for (var i = 0; i < communes.Count; i++)
            for (var j = i + 1; j < communes.Count; j++)
                yield return (communes[i].WorldPos, communes[j].WorldPos, flows.GetValueOrDefault(RoadLink.Key(communes[i].Id, communes[j].Id)));
    }
}
