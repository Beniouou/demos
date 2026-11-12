using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les communes. Vue Monde : des toits (leur nombre suit les habitants, voir <see cref="MapPalette.ResidentsPerHouse"/>)
/// et le nom de chaque commune. Filtre Politique : par-dessus les toits, capitale (sceau or et carmin), chefs-lieux de préfecture (sceau pourpre) et communes ordinaires, ou le
/// plan cadastral de l'ancienne maquette ; écritures : le badge de chaque commune (rang, nom et habitants).
/// </summary>
public sealed class CommunesLayer : MapLayer
{
    public override string Name => "Communes";
    public override string Content => "Les communes, les chefs-lieux et la capitale";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters | MapChange.Game;

    private enum Rank { Capital, PrefectureSeat, Commune }

    // Les toits et leurs ombres, sous le dessin du calque (les sceaux de la vue Politique restent par-dessus).
    private Node2D? _node;
    private readonly List<MultiMeshInstance2D> _roofs = [];
    private readonly List<MultiMeshInstance2D> _roofShadows = [];
    private int _roofsKey;

    public override void CreateNodes(Node2D node, MapDrawContext c) => _node = node;

    public override void UpdateNodes(MapDrawContext c)
    {
        var key = RoofsKey(c);
        if (key != _roofsKey)
        {
            _roofsKey = key;
            BuildRoofs(c);
        }
        foreach (var shadow in _roofShadows)
            MapSprites.PlaceOnMap(shadow, c.Camera, MapPalette.RoofShadowOffset);
        foreach (var roofs in _roofs)
            MapSprites.PlaceOnMap(roofs, c.Camera);
    }

    /// <summary>Ce qui change les toits : les communes, leur place et leurs habitants.</summary>
    private static int RoofsKey(MapDrawContext c)
    {
        var key = new HashCode();
        foreach (var (id, _, pos, population) in c.Data.Communes())
        {
            key.Add(id);
            key.Add(pos);
            key.Add(Houses(population));
        }
        return key.ToHashCode();
    }

    private static int Houses(double population) =>
        Math.Clamp((int)Math.Round(population / MapPalette.ResidentsPerHouse), 1, MapPalette.MaxHouses);

    /// <summary>
    /// Les maisons de chaque commune, toujours à la même place pour une commune donnée : serrées au centre, plus rares
    /// vers le bord, tournées d'un quart de tour une fois sur deux, jamais dans l'eau.
    /// </summary>
    private void BuildRoofs(MapDrawContext c)
    {
        foreach (var node in _roofs.Concat(_roofShadows))
            node.QueueFree();
        _roofs.Clear();
        _roofShadows.Clear();
        if (_node is null || !IsNational(c))
            return;

        var byImage = new List<(Vector2, float)>[3];
        for (var k = 0; k < byImage.Length; k++)
            byImage[k] = [];
        foreach (var (id, _, center, population) in c.Data.Communes())
        {
            var random = new Random(StableHash(id));
            var houses = Houses(population);
            var radius = MapPalette.HouseSpacing * MathF.Sqrt(houses);
            for (var h = 0; h < houses; h++)
            {
                var distance = h == 0 ? 0 : radius * MathF.Pow(random.NextSingle(), 0.8f);
                var angle = random.NextSingle() * MathF.Tau;
                var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                var size = MapPalette.RoofSize * (0.8f + random.NextSingle() * 0.5f);
                var image = random.Next(10) switch { < 6 => 0, < 8 => 1, _ => 2 };
                if (c.Data.Relief is { } relief && relief.AltitudeAt(point) < 0)
                    continue;
                byImage[image].Add((point, size));
            }
        }

        for (var k = 0; k < byImage.Length; k++)
        {
            if (byImage[k].Count == 0 || MapSprites.Texture($"toit-{k + 1}") is not { } texture)
                continue;
            var roofs = MapSprites.Instances(texture, byImage[k]);
            var shadow = MapSprites.Shadow(roofs, MapPalette.RoofShadowStrength);
            roofs.ShowBehindParent = true;
            shadow.ShowBehindParent = true;
            _roofShadows.Add(shadow);
            _roofs.Add(roofs);
        }
        // Toutes les ombres sous tous les toits.
        foreach (var shadow in _roofShadows)
            _node.AddChild(shadow);
        foreach (var roofs in _roofs)
            _node.AddChild(roofs);
    }

    /// <summary>Un nombre tiré d'un texte, le même d'une partie à l'autre (string.GetHashCode change à chaque lancement).</summary>
    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var ch in text)
            hash = unchecked(hash * 31 + ch);
        return hash;
    }

    public override void Draw(MapDrawContext c)
    {
        if (!IsNational(c))
        {
            DrawLegacyCadastralTown(c);
            return;
        }

        var s = c.SymbolScale;
        foreach (var (_, center, rank, _) in Communes(c))
        {
            switch (rank)
            {
                case Rank.Capital:
                    // 1. Capitale nationale : Sceau or et carmin
                    c.Pen.DrawCircle(center, 9.0f * s, MapPalette.BlasonGold);
                    c.Pen.DrawCircle(center, 7.5f * s, MapPalette.BlasonRed);
                    c.Pen.DrawCircle(center, 4.0f * s, MapPalette.BlasonGold);
                    c.Pen.DrawCircle(center, 2.0f * s, Colors.White);
                    break;
                case Rank.PrefectureSeat:
                    // 2. Chef-lieu de préfecture : Sceau civique pourpre
                    c.Pen.DrawCircle(center, 8.0f * s, MapPalette.PrefectureSeat);
                    c.Pen.DrawCircle(center, 6.0f * s, Colors.White);
                    c.Pen.DrawCircle(center, 3.5f * s, MapPalette.PrefectureSeat);
                    break;
                default:
                    // 3. Commune ordinaire : Disque bicolore sobre (en vue Monde, ses toits suffisent)
                    if (_roofs.Count > 0 && !c.Has(MapFilters.Politics))
                        break;
                    c.Pen.DrawCircle(center, 6.0f * s, MapPalette.CommuneMark);
                    c.Pen.DrawCircle(center, 4.0f * s, MapPalette.CommuneMarkInner);
                    c.Pen.DrawCircle(center, 2.0f * s, MapPalette.CommuneMark);
                    break;
            }
        }
    }

    public override void DrawLabels(MapDrawContext c)
    {
        if (!IsNational(c))
        {
            DrawLegacyBanner(c);
            return;
        }

        if (c.Zoom < 0.5f)
            return;
        foreach (var (commune, center, rank, popText) in Communes(c))
        {
            switch (rank)
            {
                case Rank.Capital:
                    DrawCommuneBadge(c, center, $"CAPITALE · {commune.Name.ToUpperInvariant()}{(popText.Length > 0 ? " · " + popText : "")}", MapPalette.BlasonRed, MapPalette.BlasonGold);
                    break;
                case Rank.PrefectureSeat:
                    DrawCommuneBadge(c, center, $"CHEF-LIEU · {commune.Name}{(popText.Length > 0 ? " · " + popText : "")}", MapPalette.PrefectureSeat, new Color("#EBDCE4"));
                    break;
                default:
                    DrawCommuneBadge(c, center, $"{commune.Name}{(popText.Length > 0 ? $" ({popText})" : "")}", MapPalette.InkText, MapPalette.LabelBackground);
                    break;
            }
        }
    }

    /// <summary>La carte d'un pays (plusieurs communes) plutôt que le plan d'une seule commune de l'ancienne maquette.</summary>
    private static bool IsNational(MapDrawContext c) =>
        c.VectorMap is not null && (c.VectorMap.Width > 1100.0 || c.VectorMap.AllCommunes.Count > 1 || (c.GameView is not null && c.GameView.Communes().Count > 1));

    /// <summary>
    /// Chaque commune : son centre à l'écran, son rang et ses habitants (« 1 234 hab. », ou rien). Sans le filtre
    /// Politique, toutes sont des communes ordinaires, sans habitants (seulement leur nom).
    /// </summary>
    private static System.Collections.Generic.IEnumerable<((string Id, string Name, Vector2 WorldPos, double Population) Commune, Vector2 Center, Rank Rank, string PopText)> Communes(MapDrawContext c)
    {
        var politics = c.Has(MapFilters.Politics);
        var seatIds = c.GameView?.Prefectures().Where(p => p.SeatId is not null).Select(p => p.SeatId!).ToHashSet() ?? [];
        foreach (var commune in c.Data.Communes())
        {
            var rank = !politics ? Rank.Commune
                : commune.Id == c.GameView?.CapitalId ? Rank.Capital
                : seatIds.Contains(commune.Id) ? Rank.PrefectureSeat
                : Rank.Commune;
            var popText = politics && commune.Population > 0 ? $"{commune.Population:N0} hab." : "";
            yield return (commune, c.ToScreen(commune.WorldPos), rank, popText);
        }
    }

    private static void DrawCommuneBadge(MapDrawContext c, Vector2 center, string text, Color textColor, Color bgColor)
    {
        var s = c.SymbolScale;
        var fontSize = (int)Math.Clamp(10 * s, 9, 13);
        var textSize = c.Font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
        var padding = new Vector2(6 * s, 3 * s);
        var boxRect = new Rect2(center.X - textSize.X * 0.5f - padding.X, center.Y + 12 * s, textSize.X + padding.X * 2, textSize.Y + padding.Y * 2);

        c.Pen.DrawRect(boxRect, bgColor);
        c.Pen.DrawRect(boxRect, MapPalette.LabelBorder, filled: false, width: 1.0f);
        c.Pen.DrawString(c.Font, new Vector2(boxRect.Position.X + padding.X, boxRect.Position.Y + padding.Y + textSize.Y - 2), text, HorizontalAlignment.Left, -1, fontSize, textColor);
    }

    // ---- Ancienne maquette : le plan d'une seule commune -------------------------------------------------------------

    private static void DrawLegacyCadastralTown(MapDrawContext c)
    {
        var townCenter = c.ToScreen(new Vector2(470, 310));

        // 1. Ruelle et chemins pavés reliant les quartiers
        var s = c.Zoom;
        c.Pen.DrawLine(townCenter + new Vector2(-50, 20) * s, townCenter + new Vector2(60, -15) * s, MapPalette.Street, 3f * s);
        c.Pen.DrawLine(townCenter + new Vector2(-20, -40) * s, townCenter + new Vector2(30, 45) * s, MapPalette.Street, 3f * s);
        c.Pen.DrawLine(townCenter, townCenter + new Vector2(-65, -20) * s, MapPalette.Street, 2.5f * s);
        c.Pen.DrawLine(townCenter + new Vector2(20, -10) * s, townCenter + new Vector2(80, 25) * s, MapPalette.Street, 2.5f * s);

        // 2. Bâtiments réels dessinés avec leurs toitures cadastrales
        // Mairie
        var mairiePos = townCenter + new Vector2(-15, -30) * s;
        c.DrawBuildingFootprint(mairiePos, new Vector2(28, 20) * s, MapPalette.RoofSlate);
        c.Pen.DrawCircle(mairiePos + new Vector2(0, -14 * s), 3f * s, MapPalette.BellTower);

        // École
        var ecolePos = townCenter + new Vector2(-65, -15) * s;
        c.DrawBuildingFootprint(ecolePos, new Vector2(24, 16) * s, MapPalette.RoofTile);

        // Moulin & Boulangerie
        var moulinPos = townCenter + new Vector2(45, -20) * s;
        c.DrawBuildingFootprint(moulinPos, new Vector2(22, 16) * s, MapPalette.RoofTile);
        c.Pen.DrawCircle(moulinPos + new Vector2(14 * s, 0), 4f * s, MapPalette.BuildingStroke);

        // Logements d'État
        var logPos = townCenter + new Vector2(-45, 30) * s;
        c.DrawBuildingFootprint(logPos, new Vector2(32, 18) * s, MapPalette.RoofTile);

        // Entrepôt d'État
        var entrepotPos = townCenter + new Vector2(60, 25) * s;
        c.DrawBuildingFootprint(entrepotPos, new Vector2(28, 16) * s, MapPalette.RoofSlate);

        // 3. Le Blason central communal
        DrawBlason(c, townCenter + new Vector2(0, 5) * s);
    }

    private static void DrawLegacyBanner(MapDrawContext c)
    {
        var townCenter = c.ToScreen(new Vector2(470, 310));
        var s = c.Zoom;
        var bannerRect = new Rect2(townCenter.X - 90 * s, townCenter.Y + 60 * s, 180 * s, 22 * s);
        c.Pen.DrawRect(bannerRect, MapPalette.LabelBackground);
        c.Pen.DrawRect(bannerRect, MapPalette.LabelBorder, filled: false, width: 1.2f);
        c.Pen.DrawString(c.Font, new Vector2(bannerRect.Position.X, bannerRect.Position.Y + 15 * s), "PREMIÈRE COMMUNE", HorizontalAlignment.Center, (int)bannerRect.Size.X, (int)(11 * s), MapPalette.InkText);
    }

    private static void DrawBlason(MapDrawContext c, Vector2 pos)
    {
        var s = c.Zoom;
        var shield = new Vector2[]
        {
            pos + new Vector2(-12, -14) * s,
            pos + new Vector2(12, -14) * s,
            pos + new Vector2(12, 2) * s,
            pos + new Vector2(0, 16) * s,
            pos + new Vector2(-12, 2) * s,
        };

        c.Pen.DrawColoredPolygon(shield, MapPalette.BlasonRed);
        c.Pen.DrawPolyline([.. shield, shield[0]], MapPalette.BuildingStroke, 1.5f, antialiased: true);

        // Étoile d'or au chef
        c.Pen.DrawCircle(pos + new Vector2(0, -4) * s, 3f * s, MapPalette.BlasonGold);
    }
}
