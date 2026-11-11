using System;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// La production : les bâtiments d'extraction posés sur la carte (décidé le 10 novembre 2026), un disque de la couleur
/// de leur zone (creux pendant le chantier), dans toutes les vues. Filtre Production et transport : un pointillé les
/// relie à leur commune, et de près tous leurs noms sont écrits (sinon la carte montre seulement celui sous la souris).
/// </summary>
public sealed class ProductionLayer : MapLayer
{
    public override string Name => "Production";
    public override string Content => "Les bâtiments d'extraction posés sur la carte";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters | MapChange.Game;

    /// <summary>Tous les noms des bâtiments d'extraction sont écrits de près, avec le filtre Production et transport.</summary>
    public static bool ShowsAllNames(MapFilters filters, float zoom) => (filters & MapFilters.ProductionTransport) != 0 && zoom >= 1.5f;

    public static Color ZoneColor(string? kind) => kind switch
    {
        "foret" => new Color("#3f7a3a"),
        "fertile" => new Color("#c9a227"),
        "pierre" => new Color("#7b8794"),
        "charbon" => new Color("#2b2b2e"),
        "fer" => new Color("#a2502f"),
        _ => new Color("#6b7280"),
    };

    /// <summary>Le rayon de l'icône d'un bâtiment, en pixels.</summary>
    public static float IconRadius(float zoom) => Math.Clamp(6f * zoom, 4f, 10f);

    public override void Draw(MapDrawContext c)
    {
        if (c.GameView is null)
            return;

        var positions = c.GameView.BuildingPositions();
        if (positions.Count == 0)
            return;

        var links = c.Has(MapFilters.ProductionTransport);
        var buildings = MapData.ExtractionBuildings(c.GameView);
        foreach (var (id, worldPos) in positions)
        {
            if (!buildings.TryGetValue(id, out var info))
                continue;

            var center = c.ToScreen(new Vector2((float)worldPos.X, (float)worldPos.Y));
            if (links && info.CommuneId is { } communeId && c.Data.CommunePosition(communeId) is { } commune)
                c.Pen.DrawDashedLine(center, c.ToScreen(commune), MapPalette.ExtractionLink, 1.2f, 6f);

            var color = ZoneColor(info.Kind);
            var radius = IconRadius(c.Zoom);
            if (info.Building)
            {
                c.Pen.DrawArc(center, radius, 0, Mathf.Tau, 24, color, 2f, antialiased: true);
            }
            else
            {
                c.Pen.DrawCircle(center, radius + 1.5f, Colors.White);
                c.Pen.DrawCircle(center, radius, color);
            }
        }
    }

    public override void DrawLabels(MapDrawContext c)
    {
        if (c.GameView is null || !ShowsAllNames(c.Filters, c.Zoom))
            return;

        var positions = c.GameView.BuildingPositions();
        if (positions.Count == 0)
            return;

        var buildings = MapData.ExtractionBuildings(c.GameView);
        foreach (var (id, worldPos) in positions)
            if (buildings.TryGetValue(id, out var info))
                DrawName(c.Pen, c.Font, c.ToScreen(new Vector2((float)worldPos.X, (float)worldPos.Y)), IconRadius(c.Zoom), info.Name, info.Building);
    }

    /// <summary>Le nom d'un bâtiment, à droite de son icône (« (chantier) » pendant le chantier).</summary>
    public static void DrawName(CanvasItem pen, Font font, Vector2 center, float radius, string name, bool building) =>
        pen.DrawString(font, center + new Vector2(radius + 3, 4), building ? $"{name} (chantier)" : name,
            HorizontalAlignment.Left, -1, 11, MapPalette.InkText);
}
