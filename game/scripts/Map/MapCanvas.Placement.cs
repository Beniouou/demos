using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// Bâtiments d'extraction sur la carte (décidé le 10 novembre 2026) : leurs icônes à leur place, reliées à leur commune,
/// et le mode placement pour en construire un. En mode placement, les zones qui conviennent sont surlignées, un cercle
/// montre la portée de la commune, un curseur vert ou rouge suit la souris, et un clic sur un emplacement permis pose le
/// chantier. Échap (ou <see cref="CancelPlacement"/>) l'annule.
/// </summary>
public partial class MapCanvas
{
    /// <summary>
    /// Une demande de placement : ce qu'on pose (pour le joueur), les zones qui conviennent, la commune et sa portée, la
    /// règle (ce qui empêche de poser à une position, ou null) et ce qu'on fait de la position choisie.
    /// </summary>
    public sealed record PlacementRequest(
        string Label,
        IReadOnlyList<string> Kinds,
        string CommuneId,
        double MaxDistance,
        Func<Point2D, string?> ProblemAt,
        Action<Point2D> Place);

    private PlacementRequest? _placement;

    public bool IsPlacing => _placement is not null;

    /// <summary>Passe en mode placement, centré sur la commune.</summary>
    public void BeginPlacement(PlacementRequest request)
    {
        _placement = request;
        if (GetCommuneWorldPosition(request.CommuneId) is { } commune)
            CenterOnWorld(commune);
        QueueRedraw();
    }

    public void CancelPlacement()
    {
        _placement = null;
        QueueRedraw();
    }

    /// <summary>Un clic en mode placement : pose le bâtiment si l'emplacement est permis. Renvoie vrai si le clic est pris.</summary>
    private bool TryPlaceAt(Vector2 screenPos)
    {
        if (_placement is not { } request)
            return false;

        var world = (screenPos - _panOffset) / _zoom;
        var position = new Point2D(world.X, world.Y);
        if (request.ProblemAt(position) is null)
        {
            _placement = null;
            request.Place(position);
            QueueRedraw();
        }
        return true;
    }

    // ---- Icônes des bâtiments d'extraction -------------------------------------------------------------------------

    private static Color ZoneColor(string? kind) => kind switch
    {
        "foret" => new Color("#3f7a3a"),
        "fertile" => new Color("#c9a227"),
        "pierre" => new Color("#7b8794"),
        "charbon" => new Color("#2b2b2e"),
        "fer" => new Color("#a2502f"),
        _ => new Color("#6b7280"),
    };

    /// <summary>
    /// Chaque bâtiment posé : un disque de la couleur de sa zone (creux pendant le chantier), un trait pointillé vers sa
    /// commune et son nom, au survol (ou pour tous, de près, en vue Production).
    /// </summary>
    private void DrawExtractionBuildings(Font font)
    {
        if (_gameView is null)
            return;

        var positions = _gameView.BuildingPositions();
        if (positions.Count == 0)
            return;

        var buildings = new Dictionary<string, (string Name, string? CommuneId, string? Kind, bool Building)>();
        void Visit(StructureNode node, string? communeId)
        {
            var commune = node.Kind == StructureNodeKind.Commune ? node.Id : communeId;
            if (node.Kind == StructureNodeKind.Building)
                buildings[node.Id] = (node.Name, communeId, ExtractionSites.ZonesFor(node.TypeId, node.ProductId).FirstOrDefault(), false);
            foreach (var child in node.Children)
                Visit(child, commune);
        }
        foreach (var root in _gameView.StructureTree())
            Visit(root, null);
        foreach (var site in _gameView.ConstructionSites())
            buildings[site.BuildingId] = (site.Name, site.CommuneId, ExtractionSites.ZonesFor(site.TypeId, null).FirstOrDefault(), true);

        var s = _zoom;
        // Les noms se chevauchent vite (plusieurs bâtiments près de la commune) : seulement celui sous la souris, ou tous
        // en vue Production de près.
        var showAll = ViewMode == MapViewMode.Production && s >= 1.5f;
        var hovered = _hoveredWorldPos is { } mouse
            ? positions.Select(p => (p.Key, Distance: ToScreen(new Vector2((float)p.Value.X, (float)p.Value.Y)).DistanceTo(ToScreen(mouse))))
                .Where(p => p.Distance <= 12f).OrderBy(p => p.Distance).Select(p => p.Key).FirstOrDefault()
            : null;
        foreach (var (id, worldPos) in positions)
        {
            if (!buildings.TryGetValue(id, out var info))
                continue;

            var showNames = showAll || id == hovered;
            var center = ToScreen(new Vector2((float)worldPos.X, (float)worldPos.Y));
            if (info.CommuneId is { } communeId && GetCommuneWorldPosition(communeId) is { } commune)
                DrawDashedLine(center, ToScreen(commune), new Color(0.25f, 0.22f, 0.18f, 0.45f), 1.2f, 6f);

            var color = ZoneColor(info.Kind);
            var radius = Math.Clamp(6f * s, 4f, 10f);
            if (info.Building)
            {
                DrawArc(center, radius, 0, Mathf.Tau, 24, color, 2f, antialiased: true);
            }
            else
            {
                DrawCircle(center, radius + 1.5f, Colors.White);
                DrawCircle(center, radius, color);
            }

            if (showNames)
                DrawString(font, center + new Vector2(radius + 3, 4), info.Building ? $"{info.Name} (chantier)" : info.Name,
                    HorizontalAlignment.Left, -1, 11, MapPalette.InkText);
        }
    }

    // ---- Mode placement --------------------------------------------------------------------------------------------

    /// <summary>Les zones qui conviennent surlignées, la portée de la commune, et le curseur vert ou rouge sous la souris.</summary>
    private void DrawPlacement(Font font)
    {
        if (_placement is not { } request || _vectorMap is null)
            return;

        var accent = new Color("#d9a441");
        foreach (var zone in _vectorMap.AllBiomes.Concat(_vectorMap.AllResourceZones).Where(z => request.Kinds.Contains(z.Kind)))
        {
            if (zone.Boundary.Count < 3)
                continue;
            var points = zone.Boundary.Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y))).ToArray();
            SafeDrawColoredPolygon(points, new Color(accent, 0.18f));
            DrawPolyline([.. points, points[0]], accent, 2f, antialiased: true);
        }

        if (GetCommuneWorldPosition(request.CommuneId) is { } commune)
            DrawArc(ToScreen(commune), (float)request.MaxDistance * _zoom, 0, Mathf.Tau, 96, new Color(accent, 0.9f), 2f, antialiased: true);

        string hint;
        if (_hoveredWorldPos is { } hovered)
        {
            var problem = request.ProblemAt(new Point2D(hovered.X, hovered.Y));
            var cursor = ToScreen(hovered);
            var color = problem is null ? new Color("#2e9e4f") : new Color("#d33d3d");
            DrawArc(cursor, 12f, 0, Mathf.Tau, 32, color, 3f, antialiased: true);
            DrawLine(cursor + new Vector2(-18, 0), cursor + new Vector2(18, 0), color, 1.5f);
            DrawLine(cursor + new Vector2(0, -18), cursor + new Vector2(0, 18), color, 1.5f);
            hint = problem is null ? $"Cliquez pour poser : {request.Label}" : $"Impossible ici : {problem}";
        }
        else
        {
            hint = $"Choisissez l'emplacement : {request.Label}";
        }

        var banner = new Rect2(12, 12, Size.X - 24, 28);
        DrawRect(banner, MapPalette.LabelBackground);
        DrawRect(banner, accent, filled: false, width: 1.5f);
        DrawString(font, new Vector2(24, 31), $"{hint}   (Échap pour annuler)", HorizontalAlignment.Left, Size.X - 48, 13, MapPalette.InkText);
    }
}
