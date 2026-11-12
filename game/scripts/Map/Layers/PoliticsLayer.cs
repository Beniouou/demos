using System;
using System.Linq;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// La politique (filtre Politique) : les enveloppes des préfectures (disques de juridiction fusionnés), les liaisons administratives entre
/// chef-lieu et membres, et le liseré de souveraineté de l'ancienne maquette. Écritures : le cartouche de chaque
/// préfecture (de près).
/// </summary>
public sealed class PoliticsLayer : MapLayer
{
    public override string Name => "Politique";
    public override string Content => "Les préfectures, leurs frontières et leurs liaisons administratives";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters | MapChange.Game;

    public override void Draw(MapDrawContext c)
    {
        if (!c.Has(MapFilters.Politics))
            return;
        DrawPrefecturesAndJurisdictions(c);
        DrawSovereignty(c);
    }

    public override void DrawLabels(MapDrawContext c)
    {
        if (!c.Has(MapFilters.Politics) || c.Zoom < 0.55f || c.GameView?.Prefectures() is not { Count: > 0 } prefs)
            return;

        foreach (var pref in prefs)
            if (pref.SeatId is not null && c.Data.CommunePosition(pref.SeatId) is { } seatPos)
                DrawPrefectureHeader(c, c.ToScreen(seatPos), pref.Name);
    }

    private static void DrawPrefecturesAndJurisdictions(MapDrawContext c)
    {
        var prefectures = c.GameView?.Prefectures();
        var s = c.SymbolScale;

        if (prefectures is { Count: > 0 } prefs)
        {
            for (var idx = 0; idx < prefs.Count; idx++)
            {
                var pref = prefs[idx];
                var washColor = MapPalette.PrefectureDepartmentWashes[idx % MapPalette.PrefectureDepartmentWashes.Length];
                var borderColor = MapPalette.PrefectureDepartmentBorders[idx % MapPalette.PrefectureDepartmentBorders.Length];

                var regions = c.Data.PrefecturePolygons(pref);
                foreach (var poly in regions)
                {
                    var screenPts = poly.Select(c.ToScreen).ToArray();
                    c.SafeDrawColoredPolygon(screenPts, washColor);
                    c.Pen.DrawPolyline([.. screenPts, screenPts[0]], borderColor, 2.2f * s, antialiased: true);
                }

                // Liaisons administratives pointillées entre le chef-lieu et chaque membre
                if (pref.SeatId is not null && c.Data.CommunePosition(pref.SeatId) is { } seatPos)
                {
                    var screenSeat = c.ToScreen(seatPos);
                    foreach (var memberId in pref.MemberIds)
                    {
                        if (memberId == pref.SeatId) continue;
                        if (c.Data.CommunePosition(memberId) is { } memberPos)
                            c.Pen.DrawDashedLine(screenSeat, c.ToScreen(memberPos), MapPalette.AdministrativeLink, 2.0f * s, dash: 6.0f * s);
                    }
                }
            }
        }
        else
        {
            // Aucune préfecture formelle : tracer les bassins de juridiction directe des communes
            foreach (var (_, _, pos, _) in c.Data.Communes())
            {
                var disk = MapData.CirclePolygon(pos, c.Data.CommuneRadius(), 24);
                var screenPts = disk.Select(c.ToScreen).ToArray();
                var wash = MapPalette.PrefectureWash;
                c.SafeDrawColoredPolygon(screenPts, wash);
                c.Pen.DrawPolyline([.. screenPts, screenPts[0]], MapPalette.InfluenceBorder, 1.8f * s, antialiased: true);
            }
        }
    }

    private static void DrawSovereignty(MapDrawContext c)
    {
        // Masquer le liseré de démo locale pour les cartes de pays/régions
        if (c.VectorMap is not null && (c.VectorMap.Width > 1100.0 || c.VectorMap.Height > 750.0))
            return;

        // Liseré d'arpentage souple délimitant la souveraineté
        var poly = new[]
        {
            c.ToScreen(new Vector2(270, 160)),
            c.ToScreen(new Vector2(480, 120)),
            c.ToScreen(new Vector2(660, 170)),
            c.ToScreen(new Vector2(700, 440)),
            c.ToScreen(new Vector2(480, 510)),
            c.ToScreen(new Vector2(220, 430)),
            c.ToScreen(new Vector2(200, 230)),
        };

        c.Pen.DrawPolyline([.. poly, poly[0]], MapPalette.InfluenceBorder, 1.8f * c.Zoom, antialiased: true);
    }

    private static void DrawPrefectureHeader(MapDrawContext c, Vector2 seatCenter, string prefName)
    {
        var s = c.SymbolScale;
        var fontSize = (int)Math.Clamp(11 * s, 10, 14);
        var label = $"DÉPARTEMENT DE {prefName.ToUpperInvariant()}";
        var textSize = c.Font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
        var padding = new Vector2(8 * s, 4 * s);
        var boxRect = new Rect2(seatCenter.X - textSize.X * 0.5f - padding.X, seatCenter.Y - 26 * s - textSize.Y, textSize.X + padding.X * 2, textSize.Y + padding.Y * 2);

        c.Pen.DrawRect(boxRect, new Color(0.99f, 0.98f, 0.96f, 0.94f));
        c.Pen.DrawRect(boxRect, MapPalette.PrefectureBorder, filled: false, width: 1.2f);
        c.Pen.DrawString(c.Font, new Vector2(boxRect.Position.X + padding.X, boxRect.Position.Y + padding.Y + textSize.Y - 2), label, HorizontalAlignment.Left, -1, fontSize, MapPalette.PrefectureSeat);
    }
}
