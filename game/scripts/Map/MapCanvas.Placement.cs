using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Demos.Game.Map.Layers;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// Le mode placement d'un bâtiment d'extraction (décidé le 10 novembre 2026) : les zones qui conviennent sont
/// surlignées, un cercle montre la portée de la commune, un curseur vert ou rouge suit la souris, et un clic sur un
/// emplacement permis pose le chantier. Échap (ou <see cref="CancelPlacement"/>) l'annule. Les icônes des bâtiments
/// posés sont dessinées par le calque Production ; ici, seulement le nom de celui sous la souris.
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
        double MaxDistance, // en km
        Func<Point2D, string?> ProblemAt,
        Action<Point2D> Place);

    private PlacementRequest? _placement;

    public bool IsPlacing => _placement is not null;

    /// <summary>Passe en mode placement, centré sur la commune.</summary>
    public void BeginPlacement(PlacementRequest request)
    {
        _placement = request;
        if (_data.CommunePosition(request.CommuneId) is { } commune)
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

        var world = _camera.ToWorld(screenPos);
        var position = new Point2D(world.X, world.Y);
        if (request.ProblemAt(position) is null)
        {
            _placement = null;
            request.Place(position);
            Redraw(MapChange.Game);
        }
        return true;
    }

    /// <summary>Le nom du bâtiment d'extraction sous la souris (à 12 pixels près), quand le calque Production ne les écrit pas tous.</summary>
    private void DrawHoveredExtractionName(Font font)
    {
        if (_data.View is not { } view || ProductionLayer.ShowsAllNames(Filters, _camera.Zoom) || _hoveredWorldPos is not { } mouse)
            return;

        var positions = view.BuildingPositions();
        var hovered = positions.Select(p => (p.Key, Distance: _camera.ToScreen(new Vector2((float)p.Value.X, (float)p.Value.Y)).DistanceTo(_camera.ToScreen(mouse))))
            .Where(p => p.Distance <= 12f).OrderBy(p => p.Distance).Select(p => p.Key).FirstOrDefault();
        if (hovered is null || !MapData.ExtractionBuildings(view).TryGetValue(hovered, out var info))
            return;

        var worldPos = positions[hovered];
        ProductionLayer.DrawName(this, font, _camera.ToScreen(new Vector2((float)worldPos.X, (float)worldPos.Y)),
            ProductionLayer.IconRadius(_camera.Zoom), info.Name, info.Building);
    }

    /// <summary>Les zones qui conviennent surlignées, la portée de la commune, et le curseur vert ou rouge sous la souris.</summary>
    private void DrawPlacement(Font font)
    {
        if (_placement is not { } request || _data.VectorMap is not { } map)
            return;

        var accent = new Color("#d9a441");
        foreach (var zone in map.AllBiomes.Concat(map.AllResourceZones).Where(z => request.Kinds.Contains(z.Kind)))
        {
            if (zone.Boundary.Count < 3)
                continue;
            // Triangles gardés d'un dessin à l'autre, et rien pour une zone hors de l'écran.
            var shape = _overlay.Scaled(zone.Boundary);
            if (!_overlay.IsOnScreen(shape, 8f))
                continue;
            var points = _overlay.ToScreen(shape);
            _overlay.FillTriangles(_overlay.Triangles(shape), new Color(accent, 0.18f));
            DrawPolyline([.. points, points[0]], accent, 2f, antialiased: true);
        }

        if (_data.CommunePosition(request.CommuneId) is { } commune)
            DrawArc(_camera.ToScreen(commune), (float)MapScale.ToUnits(request.MaxDistance) * _camera.Zoom, 0, Mathf.Tau, 96, new Color(accent, 0.9f), 2f, antialiased: true);

        string hint;
        if (_hoveredWorldPos is { } hovered)
        {
            var problem = request.ProblemAt(new Point2D(hovered.X, hovered.Y));
            var cursor = _camera.ToScreen(hovered);
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
