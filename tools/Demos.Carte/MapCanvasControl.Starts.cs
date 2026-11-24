using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Demos.Domain.Map;

namespace Demos.Carte;

/// <summary>
/// L'outil « Départs » du canevas (décidé le 23 novembre 2026) : les terres d'exil se posent au clic gauche, se
/// déplacent en les glissant et se retirent au clic droit. Leur marque est dessinée dans tous les outils ; dans l'outil
/// Départs, le ressort (8 km) et la portée d'extraction (20 km) sont tracés autour de chaque départ.
/// </summary>
public sealed partial class MapCanvasControl
{
    /// <summary>Distance (en unités de carte, 2,5 km) sous laquelle un clic prend une terre d'exil.</summary>
    private const double ExileHitRadius = 25.0;

    private bool _isDraggingExile;
    private int _draggedExileIndex = -1;
    private PointF _dragExileStartWorld = PointF.Empty;
    private PointF _dragExileCurrentWorld = PointF.Empty;

    public event Action<double, double>? ExileSitePlaced;
    public event Action<double, double>? ExileSiteDeleted;
    public event Action<int, double, double>? ExileSiteMoved;

    /// <summary>Le verdict de chaque terre d'exil, dans l'ordre de la carte (null : pas encore calculé).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<ExileVerdict?> ExileVerdicts { get; set; } = [];

    /// <summary>Vrai si le départ préréglé tient au départ riche, faux sinon, null si ce n'est pas calculé.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool? RichStartHolds { get; set; }

    /// <summary>Rayons tracés autour des départs, en km (ressort et portée d'extraction).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public (double NearKm, double FarKm) StartRadii { get; set; } = (8, 20);

    private int FindExileAt(double wx, double wy)
    {
        if (_map is null) return -1;
        var best = -1;
        var bestDistance = ExileHitRadius;
        for (var i = 0; i < _map.AllExileSites.Count; i++)
        {
            var p = _map.AllExileSites[i].Position;
            var d = Math.Sqrt((p.X - wx) * (p.X - wx) + (p.Y - wy) * (p.Y - wy));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        return best;
    }

    private bool HandleStartMouseDown(MouseButtons button, double wx, double wy)
    {
        if (button == MouseButtons.Left)
        {
            var index = FindExileAt(wx, wy);
            if (index >= 0)
            {
                _isDraggingExile = true;
                _draggedExileIndex = index;
                _dragExileStartWorld = new PointF((float)wx, (float)wy);
                _dragExileCurrentWorld = _dragExileStartWorld;
                Cursor = Cursors.SizeAll;
            }
            else
            {
                ExileSitePlaced?.Invoke(wx, wy);
            }
            Invalidate();
            return true;
        }

        if (button == MouseButtons.Right)
        {
            ExileSiteDeleted?.Invoke(wx, wy);
            Invalidate();
            return true;
        }

        return false;
    }

    private bool HandleStartMouseMove(double wx, double wy)
    {
        if (_isDraggingExile)
        {
            _dragExileCurrentWorld = new PointF((float)wx, (float)wy);
            Invalidate();
            return true;
        }

        if (_currentTool == EditorTool.PlaceStart)
        {
            Cursor = FindExileAt(wx, wy) >= 0 ? Cursors.Hand : Cursors.Cross;
            Invalidate();
        }
        return false;
    }

    private void HandleStartMouseUp()
    {
        if (!_isDraggingExile)
            return;

        _isDraggingExile = false;
        var index = _draggedExileIndex;
        _draggedExileIndex = -1;
        Cursor = Cursors.Cross;

        var dx = _dragExileCurrentWorld.X - _dragExileStartWorld.X;
        var dy = _dragExileCurrentWorld.Y - _dragExileStartWorld.Y;
        if (Math.Sqrt(dx * dx + dy * dy) >= 2.0 && index >= 0)
            ExileSiteMoved?.Invoke(index, _dragExileCurrentWorld.X, _dragExileCurrentWorld.Y);
        Invalidate();
    }

    /// <summary>Dessine les départs, en coordonnées de carte (la transformation est déjà posée).</summary>
    private void DrawStartOverlays(Graphics g, float finalScale)
    {
        if (_map is null)
            return;

        var inTool = _currentTool == EditorTool.PlaceStart;
        var near = (float)MapScale.ToUnits(StartRadii.NearKm);
        var far = (float)MapScale.ToUnits(StartRadii.FarKm);

        if (inTool && _map.DefaultCommune is { } rich)
        {
            var color = RichStartHolds switch
            {
                true => Color.FromArgb(200, 190, 140, 20),
                false => Color.FromArgb(220, 200, 50, 40),
                null => Color.FromArgb(160, 120, 110, 90),
            };
            DrawRadii(g, rich.Position, near, far, color, finalScale);
            using var font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            using var brush = new SolidBrush(color);
            g.DrawString($"★ Départ riche : {rich.Name}", font, brush, (float)rich.Position.X + 12, (float)rich.Position.Y + 8);
        }

        for (var i = 0; i < _map.AllExileSites.Count; i++)
        {
            var site = _map.AllExileSites[i];
            var position = _isDraggingExile && i == _draggedExileIndex
                ? new Point2D(_dragExileCurrentWorld.X, _dragExileCurrentWorld.Y)
                : site.Position;
            var verdict = i < ExileVerdicts.Count ? ExileVerdicts[i] : null;
            var color = VerdictColor(verdict);

            if (inTool)
                DrawRadii(g, position, near, far, color, finalScale);

            // Marque : une tente (triangle) cerclée.
            var x = (float)position.X;
            var y = (float)position.Y;
            using (var fill = new SolidBrush(color))
            using (var outline = new Pen(Color.White, 1.8f / finalScale))
            {
                var tent = new[] { new PointF(x, y - 9), new PointF(x - 8, y + 6), new PointF(x + 8, y + 6) };
                g.FillPolygon(fill, tent);
                g.DrawPolygon(outline, tent);
            }

            using var font = new Font("Segoe UI", inTool ? 9.0f : 8.0f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(230, Color.FromArgb(color.R / 2, color.G / 2, color.B / 2)));
            g.DrawString($"{i + 1}. {site.Name}", font, textBrush, x + 10, y - 6);
        }

        if (inTool && !_isDraggingExile && _isMouseInside)
        {
            var hover = FindExileAt(_hoverWorldPt.X, _hoverWorldPt.Y);
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            using var brush = new SolidBrush(Color.FromArgb(120, 60, 20));
            var hint = hover >= 0 ? "Glisser pour déplacer · clic droit pour retirer" : "Clic gauche : poser une terre d'exil";
            g.DrawString(hint, font, brush, _hoverWorldPt.X + 12, _hoverWorldPt.Y - 22);
        }
    }

    private static void DrawRadii(Graphics g, Point2D center, float near, float far, Color color, float finalScale)
    {
        var x = (float)center.X;
        var y = (float)center.Y;
        using var nearFill = new SolidBrush(Color.FromArgb(28, color));
        using var nearPen = new Pen(Color.FromArgb(200, color), 2.0f / finalScale);
        using var farPen = new Pen(Color.FromArgb(170, color), 1.5f / finalScale) { DashStyle = DashStyle.Dash };
        g.FillEllipse(nearFill, x - near, y - near, near * 2, near * 2);
        g.DrawEllipse(nearPen, x - near, y - near, near * 2, near * 2);
        g.DrawEllipse(farPen, x - far, y - far, far * 2, far * 2);
    }

    /// <summary>Vert : jouable ; rouge : injouable ; orange : trop facile ; gris : pas encore calculé.</summary>
    public static Color VerdictColor(ExileVerdict? verdict) => verdict switch
    {
        ExileVerdict.Playable => Color.FromArgb(230, 46, 125, 50),
        ExileVerdict.Unplayable => Color.FromArgb(230, 198, 40, 40),
        ExileVerdict.TooEasy => Color.FromArgb(230, 230, 130, 20),
        _ => Color.FromArgb(200, 120, 110, 90),
    };
}
